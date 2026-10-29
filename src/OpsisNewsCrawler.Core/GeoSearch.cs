using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>A place found by name, postcode or coordinates.</summary>
public sealed record GeoPlace(string Name, string? Admin1, string? Admin2, string? Country, string CountryCode, double Lat, double Lon,
    long? OpenMeteoId = null, int? Population = null, string? FeatureCode = null, IReadOnlyList<string>? Postcodes = null)
{
    /// <summary>The id the place gets in the weather list: its coordinates rounded to about 100 m, so the same place is never added twice.</summary>
    public string CityId => GeoSearch.IdFor(Lat, Lon);

    /// <summary>"Zakynthos — Ionian Islands, Greece (37.7802, 20.8955)".</summary>
    public string Label
    {
        get
        {
            var where = string.Join(", ", new[] { Admin2, Admin1, Country }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
            var tk = Postcodes is { Count: > 0 } ? "  ·  " + string.Join(" ", Postcodes.Take(2)) : "";
            return $"{Name}{(where.Length > 0 ? " — " + where : "")}{tk}  ({Lat.ToString("0.####", CultureInfo.InvariantCulture)}, {Lon.ToString("0.####", CultureInfo.InvariantCulture)})";
        }
    }
}

/// <summary>
/// Finds any place on Earth (towns, villages, islands) for the weather list: by name (Open-Meteo geocoding), by Greek postcode (OpenStreetMap
/// Nominatim, because Open-Meteo does not know Greek postcodes) or by coordinates (the place is recognised with Nominatim).
/// Pure parsers are separate from the requests so they can be tested. Nominatim is only asked when the user presses Search, at most once a second.
/// </summary>
public static class GeoSearch
{
    private static readonly SemaphoreSlim NominatimGate = new(1, 1);
    private static DateTimeOffset lastNominatim = DateTimeOffset.MinValue;

    public static string IdFor(double lat, double lon) =>
        "geo:" + Math.Round(lat, 3).ToString("0.###", CultureInfo.InvariantCulture) + ":" + Math.Round(lon, 3).ToString("0.###", CultureInfo.InvariantCulture);

    private static readonly Regex IdPattern = new(@"^geo:-?\d{1,2}(\.\d{1,3})?:-?\d{1,3}(\.\d{1,3})?$", RegexOptions.Compiled);

    /// <summary>True for an id made by <see cref="IdFor"/> with coordinates on Earth.</summary>
    public static bool IsValidId(string? id)
    {
        if (id is null || !IdPattern.IsMatch(id)) return false;
        var parts = id.Split(':');
        return double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) && Math.Abs(lat) <= 90
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) && Math.Abs(lon) <= 180;
    }

    /// <summary>Greek postcode: five digits, optionally written "542 48".</summary>
    public static bool LooksLikeGreekPostcode(string text) => Regex.IsMatch(text.Trim(), @"^\d{3}\s?\d{2}$");

    /// <summary>
    /// "37.78, 20.89", "37.78 20.89", "37,78 ; 20,89" (decimal comma), "37.78N 20.89E", "37.78° N, 20.89° E": latitude first. S and W make a number negative.
    /// </summary>
    public static bool TryParseCoordinates(string? text, out double lat, out double lon)
    {
        lat = lon = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().Replace('°', ' ').Replace('−', '-');
        // "37,78 20,89" / "37,78; 20,89": decimal commas, the two numbers split by a semicolon or a space
        if (Regex.IsMatch(s, @"^-?\d{1,3},\d+\s*[;\s]\s*-?\d{1,3},\d+\s*$")) s = s.Replace(';', ' ').Replace(',', '.');
        else s = s.Replace(',', ' ').Replace(';', ' ');    // "37.78, 20.89": the comma only separates
        var m = Regex.Matches(s, @"(-?\d{1,3}(?:\.\d+)?)\s*([NSEWnsew])?");
        if (m.Count != 2) return false;
        if (Regex.Replace(s, @"(-?\d{1,3}(?:\.\d+)?)\s*([NSEWnsew])?|\s", "").Length != 0) return false;   // nothing else may be in the text
        if (!double.TryParse(m[0].Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)) return false;
        if (!double.TryParse(m[1].Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lon)) return false;
        var h0 = m[0].Groups[2].Value.ToUpperInvariant();
        var h1 = m[1].Groups[2].Value.ToUpperInvariant();
        if (h0 is "E" or "W" || h1 is "N" or "S") return false;       // a latitude is north/south, a longitude east/west
        if (h0 == "S") lat = -Math.Abs(lat);
        if (h1 == "W") lon = -Math.Abs(lon);
        return Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180;
    }

    // ---- parsers --------------------------------------------------------------------------------

    /// <summary>The answer of geocoding-api.open-meteo.com/v1/search (or /v1/get, which has one object).</summary>
    public static IReadOnlyList<GeoPlace> ParseOpenMeteo(string json)
    {
        var list = new List<GeoPlace>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return list;
            IEnumerable<JsonElement> items = root.TryGetProperty("results", out var r) && r.ValueKind == JsonValueKind.Array ? r.EnumerateArray()
                : root.TryGetProperty("latitude", out _) ? new[] { root } : Array.Empty<JsonElement>();
            foreach (var e in items)
            {
                if (!e.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String) continue;
                if (!TryNumber(e, "latitude", out var lat) || !TryNumber(e, "longitude", out var lon)) continue;
                if (Math.Abs(lat) > 90 || Math.Abs(lon) > 180) continue;
                var codes = new List<string>();
                if (e.TryGetProperty("postcodes", out var pc) && pc.ValueKind == JsonValueKind.Array)
                    foreach (var p in pc.EnumerateArray()) if (p.ValueKind == JsonValueKind.String && p.GetString() is { Length: > 0 } code) codes.Add(code);
                list.Add(new GeoPlace(Clip(n.GetString()!), Str(e, "admin1"), Str(e, "admin2"), Str(e, "country"), Str(e, "country_code") ?? "", lat, lon,
                    e.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : null,
                    e.TryGetProperty("population", out var pop) && pop.ValueKind == JsonValueKind.Number ? pop.GetInt32() : null,
                    Str(e, "feature_code"), codes));
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { }
        return list;
    }

    /// <summary>The answer of Nominatim /search (an array) or /reverse (one object).</summary>
    public static IReadOnlyList<GeoPlace> ParseNominatim(string json)
    {
        var list = new List<GeoPlace>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var items = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : root.ValueKind == JsonValueKind.Object ? new List<JsonElement> { root } : new List<JsonElement>();
            foreach (var e in items)
            {
                if (e.TryGetProperty("error", out _)) continue;
                if (!TryNumber(e, "lat", out var lat) || !TryNumber(e, "lon", out var lon) || Math.Abs(lat) > 90 || Math.Abs(lon) > 180) continue;
                string? village = null, town = null, city = null, island = null, county = null, state = null, country = null, cc = null, postcode = null;
                if (e.TryGetProperty("address", out var a) && a.ValueKind == JsonValueKind.Object)
                {
                    village = Str(a, "village") ?? Str(a, "hamlet") ?? Str(a, "suburb") ?? Str(a, "neighbourhood");
                    town = Str(a, "town"); city = Str(a, "city") ?? Str(a, "municipality"); island = Str(a, "island");
                    county = Str(a, "county"); state = Str(a, "state") ?? Str(a, "region"); country = Str(a, "country"); cc = Str(a, "country_code"); postcode = Str(a, "postcode");
                }
                var name = city ?? town ?? village ?? island ?? Str(e, "name") ?? county ?? Str(e, "display_name")?.Split(',')[0];
                if (string.IsNullOrWhiteSpace(name)) continue;
                list.Add(new GeoPlace(Clip(name), state, county, country, (cc ?? "").ToUpperInvariant(), lat, lon, null, null, null,
                    postcode is null ? null : new[] { postcode }));
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { }
        return list;
    }

    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool TryNumber(JsonElement e, string name, out double value)
    {
        value = 0;
        if (!e.TryGetProperty(name, out var v)) return false;
        if (v.ValueKind == JsonValueKind.Number) return v.TryGetDouble(out value);
        return v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string Clip(string s) => s.Length > 80 ? s[..80] : s;

    // ---- requests ---------------------------------------------------------------------------------

    private static HttpClient NewClient(HttpMessageHandler? handler)
    {
        var http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(10);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler/1.0 (+https://github.com/giorstergiopoulos-design/OpsisNewsCrawler)");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("el,en;q=0.8");
        return http;
    }

    /// <summary>Name, postcode or coordinates. Returns the places best first (Greek places first when <paramref name="greekFirst"/>, then the bigger ones).</summary>
    public static async Task<IReadOnlyList<GeoPlace>> SearchAsync(string query, string language, bool greekFirst, CancellationToken ct, HttpMessageHandler? handler = null)
    {
        query = query.Trim();
        if (query.Length < 2) return Array.Empty<GeoPlace>();
        if (TryParseCoordinates(query, out var lat, out var lon))
        {
            var found = await ReverseAsync(lat, lon, language, ct, handler).ConfigureAwait(false);
            // The coordinates are what the user typed, whatever place Nominatim recognised there.
            return found is null ? new[] { new GeoPlace($"{lat.ToString("0.####", CultureInfo.InvariantCulture)}, {lon.ToString("0.####", CultureInfo.InvariantCulture)}", null, null, null, "", lat, lon) }
                : new[] { found with { Lat = lat, Lon = lon } };
        }
        using var http = NewClient(handler);
        if (LooksLikeGreekPostcode(query))
        {
            var url = "https://nominatim.openstreetmap.org/search?format=jsonv2&addressdetails=1&limit=8&country=gr&postalcode=" + Uri.EscapeDataString(query.Replace(" ", "")) + "&accept-language=" + language;
            var json = await NominatimGetAsync(http, url, ct).ConfigureAwait(false);
            return json is null ? Array.Empty<GeoPlace>() : ParseNominatim(json);
        }
        var geo = "https://geocoding-api.open-meteo.com/v1/search?count=20&format=json&name=" + Uri.EscapeDataString(query) + "&language=" + language;
        var answer = await http.GetStringAsync(geo, ct).ConfigureAwait(false);
        var places = ParseOpenMeteo(answer).ToList();
        return places.OrderByDescending(p => greekFirst && p.CountryCode == "GR" ? 1 : 0).ThenByDescending(p => p.Population ?? 0).ToList();
    }

    /// <summary>The place at the coordinates (town, village or island), or null when Nominatim does not know one.</summary>
    public static async Task<GeoPlace?> ReverseAsync(double lat, double lon, string language, CancellationToken ct, HttpMessageHandler? handler = null)
    {
        using var http = NewClient(handler);
        var url = "https://nominatim.openstreetmap.org/reverse?format=jsonv2&addressdetails=1&zoom=12&lat=" + lat.ToString("0.#####", CultureInfo.InvariantCulture)
                  + "&lon=" + lon.ToString("0.#####", CultureInfo.InvariantCulture) + "&accept-language=" + language;
        var json = await NominatimGetAsync(http, url, ct).ConfigureAwait(false);
        return json is null ? null : ParseNominatim(json).FirstOrDefault();
    }

    /// <summary>The name of an Open-Meteo place in another language (best effort: null when it cannot be had).</summary>
    public static async Task<string?> NameInAsync(long openMeteoId, string language, CancellationToken ct, HttpMessageHandler? handler = null)
    {
        try
        {
            using var http = NewClient(handler);
            var json = await http.GetStringAsync($"https://geocoding-api.open-meteo.com/v1/get?id={openMeteoId}&language={language}", ct).ConfigureAwait(false);
            return ParseOpenMeteo(json).FirstOrDefault()?.Name;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return null; }
    }

    // Nominatim's usage policy: an identifying User-Agent, no more than one request a second, no bulk use.
    private static async Task<string?> NominatimGetAsync(HttpClient http, string url, CancellationToken ct)
    {
        await NominatimGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var wait = lastNominatim + TimeSpan.FromMilliseconds(1100) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct).ConfigureAwait(false);
            try
            {
                using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
                lastNominatim = DateTimeOffset.UtcNow;
                return response.StatusCode == HttpStatusCode.OK ? await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false) : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastNominatim = DateTimeOffset.UtcNow;
                if (ct.IsCancellationRequested) throw;
                return null;
            }
        }
        finally
        {
            NominatimGate.Release();
        }
    }

    /// <summary>The weather-list entry for a place; the two names are what the places are called in Greek and in English.</summary>
    public static GreekCity ToCity(GeoPlace place, string? nameEl = null, string? nameEn = null) =>
        new(place.CityId, nameEl ?? place.Name, nameEn ?? place.Name, Math.Round(place.Lat, 4), Math.Round(place.Lon, 4));
}
