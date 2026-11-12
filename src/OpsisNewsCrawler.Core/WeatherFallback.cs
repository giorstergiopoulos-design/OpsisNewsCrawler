using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// MET Norway locationforecast 2.0 as the second, independent weather source (free, no key, worldwide, CC BY 4.0). Used only for places the first source
/// (Open-Meteo) did not answer for. Terms (api.met.no/doc/TermsOfService, read 2026-11-11): an identifying User-Agent, at most 4 decimals in the coordinates,
/// no new request before the "Expires" time of the previous answer, and "If-Modified-Since" for repeats.
/// </summary>
public sealed class MetNoWeather
{
    private sealed class Entry { public string Body = ""; public string? LastModified; public DateTimeOffset Expires; }

    private readonly HttpClient http;
    private readonly Func<DateTimeOffset> now;
    private readonly Dictionary<string, Entry> cache = new();
    private readonly object gate = new();

    public MetNoWeather(HttpClient? client = null, Func<DateTimeOffset>? clock = null)
    {
        http = client ?? Create();
        now = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public static string UserAgent
    {
        get
        {
            var v = typeof(MetNoWeather).Assembly.GetName().Version;
            return $"OpsisNewsCrawler/{(v == null ? "0" : v.ToString(3))} (+https://github.com/giorstergiopoulos-design/OpsisNewsCrawler)";
        }
    }

    private static HttpClient Create()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return c;
    }

    /// <summary>Coordinates are sent rounded to 2 decimals (about 1 km): enough for weather and never more than the provider allows.</summary>
    public static string Url(double lat, double lon) =>
        $"https://api.met.no/weatherapi/locationforecast/2.0/compact?lat={Math.Round(lat, 2).ToString("0.##", CultureInfo.InvariantCulture)}&lon={Math.Round(lon, 2).ToString("0.##", CultureInfo.InvariantCulture)}";

    public async Task<WeatherReading?> GetAsync(GreekCity city, CancellationToken ct)
    {
        var url = Url(city.Lat, city.Lon);
        try
        {
            Entry? cached;
            lock (gate) cache.TryGetValue(url, out cached);
            if (cached != null && now() < cached.Expires) return Parse(city, cached.Body, now());

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (cached?.LastModified != null && DateTimeOffset.TryParse(cached.LastModified, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lm)) req.Headers.IfModifiedSince = lm;
            using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
            string body;
            if (resp.StatusCode == HttpStatusCode.NotModified && cached != null) body = cached.Body;
            else if (!resp.IsSuccessStatusCode) return cached == null ? null : Parse(city, cached.Body, now());
            else body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var entry = new Entry
            {
                Body = body,
                LastModified = resp.Content.Headers.LastModified?.ToString("R", CultureInfo.InvariantCulture) ?? cached?.LastModified,
                Expires = resp.Content.Headers.Expires ?? now().AddMinutes(10),
            };
            lock (gate) { cache[url] = entry; if (cache.Count > 40) cache.Remove(cache.Keys.First(k => k != url)); }
            return Parse(city, body, now());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            if (!ct.IsCancellationRequested) Log.Write($"Weather (MET Norway): {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>The time step that is current (the last one not after <paramref name="at"/>): a model forecast for this hour, not a station reading.</summary>
    public static WeatherReading? Parse(GreekCity city, string json, DateTimeOffset at)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var series = doc.RootElement.GetProperty("properties").GetProperty("timeseries");
            JsonElement? pick = null; DateTimeOffset pickTime = default;
            foreach (var step in series.EnumerateArray())
            {
                if (!DateTimeOffset.TryParse(step.GetProperty("time").GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t)) continue;
                if (t > at && pick != null) break;
                pick = step; pickTime = t;
            }
            if (pick == null) return null;
            var data = pick.Value.GetProperty("data");
            var d = data.GetProperty("instant").GetProperty("details");
            double? N(string n) => d.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
            if (N("air_temperature") is not { } temp) return null;
            var symbol = ""; double precip = 0;
            if (data.TryGetProperty("next_1_hours", out var n1))
            {
                if (n1.TryGetProperty("summary", out var s) && s.TryGetProperty("symbol_code", out var sc)) symbol = sc.GetString() ?? "";
                if (n1.TryGetProperty("details", out var nd) && nd.TryGetProperty("precipitation_amount", out var pa) && pa.ValueKind == JsonValueKind.Number) precip = pa.GetDouble();
            }
            var cloud = N("cloud_area_fraction");
            return new WeatherReading(city.Id, temp, (int)Math.Round(N("relative_humidity") ?? 0), (N("wind_speed") ?? 0) * 3.6, (int)Math.Round(N("wind_from_direction") ?? 0),
                WmoFromSymbol(symbol, cloud), SunPosition.IsDay(pickTime.UtcDateTime, city.Lat, city.Lon), pickTime, null, precip, cloud is { } c ? (int)Math.Round(c) : null,
                N("air_pressure_at_sea_level"), N("wind_speed_of_gust") is { } g ? g * 3.6 : null, "MET Norway");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return null; }
    }

    /// <summary>MET symbol codes ("lightrainshowers_day", "heavysnowandthunder") to WMO codes of the same intensity.</summary>
    public static int WmoFromSymbol(string symbol, double? cloudPercent = null)
    {
        var s = symbol.ToLowerInvariant();
        var us = s.IndexOf('_'); if (us >= 0) s = s[..us];
        var thunder = s.Contains("thunder"); bool heavy = s.StartsWith("heavy"), light = s.StartsWith("light");
        if (s.Contains("snow") && !s.Contains("sleet")) return thunder ? 95 : heavy ? 75 : light ? 71 : 73;
        if (s.Contains("sleet")) return thunder ? 95 : heavy ? 67 : 66;
        if (s.Contains("rain")) return thunder ? (heavy ? 99 : 95) : heavy ? 65 : light ? 61 : 63;
        return s switch
        {
            "clearsky" => 0, "fair" => 1, "partlycloudy" => 2, "cloudy" => 3, "fog" => 45,
            _ => cloudPercent is { } c ? (c < 15 ? 0 : c < 40 ? 1 : c < 75 ? 2 : 3) : 2,
        };
    }
}

/// <summary>Sun elevation from latitude, longitude and UTC time (no network): day when the upper limb is above the horizon.</summary>
public static class SunPosition
{
    private const double Rad = Math.PI / 180;
    private static double Mod(double x, double m) { var r = x % m; return r < 0 ? r + m : r; }

    public static double Elevation(DateTime utc, double lat, double lon)
    {
        var n = utc.ToUniversalTime().ToOADate() + 2415018.5 - 2451545.0;
        var l = Mod(280.460 + 0.9856474 * n, 360);
        var g = Mod(357.528 + 0.9856003 * n, 360) * Rad;
        var lambda = (l + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2 * g)) * Rad;
        var eps = (23.439 - 0.0000004 * n) * Rad;
        var dec = Math.Asin(Math.Sin(eps) * Math.Sin(lambda));
        var ra = Math.Atan2(Math.Cos(eps) * Math.Sin(lambda), Math.Cos(lambda));
        var gmst = Mod(18.697374558 + 24.06570982441908 * n, 24) * 15;
        var h = (gmst + lon) * Rad - ra;
        var sinEl = Math.Sin(lat * Rad) * Math.Sin(dec) + Math.Cos(lat * Rad) * Math.Cos(dec) * Math.Cos(h);
        return Math.Asin(Math.Clamp(sinEl, -1, 1)) / Rad;
    }

    public static bool IsDay(DateTime utc, double lat, double lon) => Elevation(utc, lat, lon) > -0.833;
}
