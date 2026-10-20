using System.Globalization;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>One severe-weather warning for the user's area. Kind is a stable id ("storm", "wind", "rain", "snow", "heat", "cold").</summary>
public sealed record WeatherAlert(string Kind, string TextEl, string TextEn, DateTimeOffset From);

/// <summary>
/// Severe-weather warnings worked out from the Open-Meteo hourly forecast (next 12 hours) of one place. They are NOT the official
/// bulletin of the national weather service: they are a computed warning from forecast values, and the app says so.
/// </summary>
public static class WeatherAlerts
{
    public const double GustKmh = 75;          // about Beaufort 9
    public const double HeavyRainMmPerHour = 10;
    public const double HeavyRainMmTotal = 40;
    public const double HeatC = 40;
    public const double ColdC = -3;

    /// <summary>Pure rule set. The arrays have one entry per hour; NaN = no value. Never throws for unequal lengths.</summary>
    public static List<WeatherAlert> Evaluate(IReadOnlyList<DateTimeOffset> times, IReadOnlyList<double> temperature,
        IReadOnlyList<double> precipitation, IReadOnlyList<double> gusts, IReadOnlyList<int> codes)
    {
        var result = new List<WeatherAlert>();
        var n = new[] { times.Count, temperature.Count, precipitation.Count, gusts.Count, codes.Count }.Min();
        if (n == 0) return result;
        var culture = CultureInfo.GetCultureInfo("el-GR");

        int First(Func<int, bool> test)
        {
            for (var i = 0; i < n; i++) if (test(i)) return i;
            return -1;
        }

        var storm = First(i => codes[i] >= 95);
        if (storm >= 0)
            result.Add(new WeatherAlert("storm", "Καταιγίδες με κεραυνούς", "Thunderstorms", times[storm]));

        var windIndex = First(i => !double.IsNaN(gusts[i]) && gusts[i] >= GustKmh);
        if (windIndex >= 0)
        {
            var max = Enumerable.Range(0, n).Where(i => !double.IsNaN(gusts[i])).Max(i => gusts[i]);
            result.Add(new WeatherAlert("wind", $"Ισχυροί άνεμοι, ριπές έως {max.ToString("0", culture)} km/h", $"Strong winds, gusts up to {max.ToString("0", culture)} km/h", times[windIndex]));
        }

        var rainValues = Enumerable.Range(0, n).Where(i => !double.IsNaN(precipitation[i])).ToList();
        var rainHour = First(i => !double.IsNaN(precipitation[i]) && precipitation[i] >= HeavyRainMmPerHour);
        var rainTotal = rainValues.Sum(i => precipitation[i]);
        if (rainHour >= 0 || rainTotal >= HeavyRainMmTotal)
        {
            var at = rainHour >= 0 ? times[rainHour] : times[rainValues.First(i => precipitation[i] > 0)];
            result.Add(new WeatherAlert("rain", $"Έντονες βροχοπτώσεις, περίπου {rainTotal.ToString("0", culture)} mm τις επόμενες ώρες", $"Heavy rain, about {rainTotal.ToString("0", culture)} mm in the next hours", at));
        }

        var snow = First(i => codes[i] is >= 71 and <= 77 or 85 or 86);
        if (snow >= 0)
            result.Add(new WeatherAlert("snow", "Χιονόπτωση", "Snowfall", times[snow]));

        var heat = First(i => !double.IsNaN(temperature[i]) && temperature[i] >= HeatC);
        if (heat >= 0)
            result.Add(new WeatherAlert("heat", $"Καύσωνας, θερμοκρασία έως {temperature.Take(n).Where(t => !double.IsNaN(t)).Max().ToString("0", culture)} °C", $"Heat wave, up to {temperature.Take(n).Where(t => !double.IsNaN(t)).Max().ToString("0", CultureInfo.InvariantCulture)} °C", times[heat]));

        var cold = First(i => !double.IsNaN(temperature[i]) && temperature[i] <= ColdC);
        if (cold >= 0)
            result.Add(new WeatherAlert("cold", $"Παγετός, θερμοκρασία έως {temperature.Take(n).Where(t => !double.IsNaN(t)).Min().ToString("0", culture)} °C", $"Frost, down to {temperature.Take(n).Where(t => !double.IsNaN(t)).Min().ToString("0", CultureInfo.InvariantCulture)} °C", times[cold]));

        return result;
    }

    /// <summary>Reads the "hourly" object of an Open-Meteo answer and applies <see cref="Evaluate"/>. Null when the answer cannot be read.</summary>
    public static List<WeatherAlert>? ParseAndEvaluate(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("hourly", out var hourly) || hourly.ValueKind != JsonValueKind.Object) return null;
        var offset = root.TryGetProperty("utc_offset_seconds", out var off) && off.ValueKind == JsonValueKind.Number ? TimeSpan.FromSeconds(off.GetInt32()) : TimeSpan.Zero;

        List<double> Numbers(string name) => hourly.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number ? e.GetDouble() : double.NaN).ToList()
            : new List<double>();

        if (!hourly.TryGetProperty("time", out var timeArray) || timeArray.ValueKind != JsonValueKind.Array) return null;
        var times = timeArray.EnumerateArray()
            .Select(e => DateTime.TryParse(e.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? new DateTimeOffset(dt, offset) : DateTimeOffset.MinValue)
            .ToList();
        var codes = Numbers("weather_code").Select(d => double.IsNaN(d) ? 0 : (int)d).ToList();
        return Evaluate(times, Numbers("temperature_2m"), Numbers("precipitation"), Numbers("wind_gusts_10m"), codes);
    }
}

/// <summary>Asks Open-Meteo (free for non-commercial use, CC BY 4.0, no key) for the next hours of one place.</summary>
public sealed class WeatherAlertService
{
    private readonly HttpClient http = QuoteHttp.Create();

    /// <summary>The warnings for the place, an empty list when there are none, or null when the request failed.</summary>
    public async Task<List<WeatherAlert>?> FetchAsync(GreekCity city, CancellationToken ct)
    {
        var url = "https://api.open-meteo.com/v1/forecast?latitude=" + city.Lat.ToString("0.####", CultureInfo.InvariantCulture) +
                  "&longitude=" + city.Lon.ToString("0.####", CultureInfo.InvariantCulture) +
                  "&hourly=temperature_2m,precipitation,wind_gusts_10m,weather_code&forecast_hours=12&wind_speed_unit=kmh&timezone=auto";
        try
        {
            using var limit = QuoteHttp.Limit(ct, 20);
            var json = await http.GetStringAsync(url, limit.Token).ConfigureAwait(false);
            return WeatherAlerts.ParseAndEvaluate(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or FormatException)
        {
            if (!ct.IsCancellationRequested) Log.Write($"Weather alerts: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
