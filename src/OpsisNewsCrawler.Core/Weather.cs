using System.Globalization;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>A Greek city (capital of a regional unit / former prefecture, plus a few well-known islands) with its position.</summary>
public sealed record GreekCity(string Id, string NameEl, string NameEn, double Lat, double Lon);

public static class GreekCities
{
    // id|Greek name|English name|latitude|longitude  (city centres; one line per regional unit capital)
    private const string Data = """
athens|Αθήνα|Athens|37.9838|23.7275
piraeus|Πειραιάς|Piraeus|37.9475|23.6469
eleusis|Ελευσίνα|Eleusis|38.0415|23.5389
kropia|Κρωπία|Kropia|37.9060|23.8970
hydra|Ύδρα|Hydra|37.3500|23.4667
thessaloniki|Θεσσαλονίκη|Thessaloniki|40.6401|22.9444
veria|Βέροια|Veria|40.5236|22.2026
kilkis|Κιλκίς|Kilkis|40.9937|22.8753
edessa|Έδεσσα|Edessa|40.8021|22.0467
katerini|Κατερίνη|Katerini|40.2719|22.5028
serres|Σέρρες|Serres|41.0850|23.5475
polygyros|Πολύγυρος|Polygyros|40.3770|23.4430
alexandroupoli|Αλεξανδρούπολη|Alexandroupoli|40.8497|25.8744
drama|Δράμα|Drama|41.1500|24.1478
kavala|Καβάλα|Kavala|40.9396|24.4069
xanthi|Ξάνθη|Xanthi|41.1349|24.8880
komotini|Κομοτηνή|Komotini|41.1224|25.4066
thasos|Θάσος|Thasos|40.7800|24.7100
grevena|Γρεβενά|Grevena|40.0850|21.4280
kastoria|Καστοριά|Kastoria|40.5192|21.2687
kozani|Κοζάνη|Kozani|40.3000|21.7889
florina|Φλώρινα|Florina|40.7821|21.4093
arta|Άρτα|Arta|39.1600|20.9850
igoumenitsa|Ηγουμενίτσα|Igoumenitsa|39.5000|20.2667
ioannina|Ιωάννινα|Ioannina|39.6650|20.8537
preveza|Πρέβεζα|Preveza|38.9560|20.7517
karditsa|Καρδίτσα|Karditsa|39.3650|21.9210
larissa|Λάρισα|Larissa|39.6390|22.4191
volos|Βόλος|Volos|39.3666|22.9507
trikala|Τρίκαλα|Trikala|39.5550|21.7680
skiathos|Σκιάθος|Skiathos|39.1630|23.4900
corfu|Κέρκυρα|Corfu|39.6243|19.9217
zakynthos|Ζάκυνθος|Zakynthos|37.7870|20.8990
argostoli|Αργοστόλι|Argostoli|38.1750|20.4890
lefkada|Λευκάδα|Lefkada|38.8333|20.7000
ithaca|Ιθάκη|Ithaca|38.3667|20.7167
messolonghi|Μεσολόγγι|Messolonghi|38.3708|21.4340
agrinio|Αγρίνιο|Agrinio|38.6214|21.4078
patras|Πάτρα|Patras|38.2466|21.7346
pyrgos|Πύργος|Pyrgos|37.6753|21.4414
livadeia|Λιβαδειά|Livadeia|38.4360|22.8750
chalkida|Χαλκίδα|Chalkida|38.4636|23.5993
karpenisi|Καρπενήσι|Karpenisi|38.9167|21.7833
lamia|Λαμία|Lamia|38.9000|22.4333
amfissa|Άμφισσα|Amfissa|38.5300|22.3700
nafplio|Ναύπλιο|Nafplio|37.5673|22.8062
tripoli|Τρίπολη|Tripoli|37.5089|22.3722
corinth|Κόρινθος|Corinth|37.9400|22.9300
sparti|Σπάρτη|Sparta|37.0743|22.4297
kalamata|Καλαμάτα|Kalamata|37.0389|22.1142
mytilene|Μυτιλήνη|Mytilene|39.1100|26.5550
chios|Χίος|Chios|38.3700|26.1360
samos|Σάμος|Samos|37.7550|26.9770
ikaria|Ικαρία|Ikaria|37.6100|26.2300
limnos|Λήμνος|Limnos|39.8700|25.0600
syros|Σύρος|Syros|37.4450|24.9430
naxos|Νάξος|Naxos|37.1050|25.3760
paros|Πάρος|Paros|37.0850|25.1500
santorini|Σαντορίνη|Santorini|36.4167|25.4333
mykonos|Μύκονος|Mykonos|37.4467|25.3289
milos|Μήλος|Milos|36.7250|24.4300
tinos|Τήνος|Tinos|37.5400|25.1600
kea|Κέα|Kea|37.6200|24.3300
kalymnos|Κάλυμνος|Kalymnos|36.9500|26.9833
rhodes|Ρόδος|Rhodes|36.4349|28.2176
kos|Κως|Kos|36.8930|27.2880
karpathos|Κάρπαθος|Karpathos|35.5071|27.2133
heraklion|Ηράκλειο|Heraklion|35.3387|25.1442
chania|Χανιά|Chania|35.5138|24.0180
rethymno|Ρέθυμνο|Rethymno|35.3650|24.4820
agios-nikolaos|Άγιος Νικόλαος|Agios Nikolaos|35.1900|25.7160
""";

    public static readonly IReadOnlyList<GreekCity> BuiltIn = Data.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(l => l.Split('|'))
        .Where(p => p.Length == 5)
        .Select(p => new GreekCity(p[0], p[1], p[2], double.Parse(p[3], CultureInfo.InvariantCulture), double.Parse(p[4], CultureInfo.InvariantCulture)))
        .ToList();

    private static IReadOnlyList<GreekCity> custom = Array.Empty<GreekCity>();
    private static IReadOnlyList<GreekCity> everything = BuiltIn;

    /// <summary>The built-in cities followed by the places the user added by search or coordinates (see <see cref="GeoSearch"/>).</summary>
    public static IReadOnlyList<GreekCity> All => everything;

    /// <summary>The places the user added.</summary>
    public static IReadOnlyList<GreekCity> Custom => custom;

    public const int MaxCustom = 60;

    /// <summary>Replaces the user's places. Entries with a bad id, bad coordinates or no name are dropped, duplicates too; at most <see cref="MaxCustom"/> are kept.</summary>
    public static void SetCustom(IEnumerable<GreekCity>? places)
    {
        var list = new List<GreekCity>();
        foreach (var c in places ?? Enumerable.Empty<GreekCity>())
        {
            if (list.Count >= MaxCustom) break;
            if (!GeoSearch.IsValidId(c.Id) || Math.Abs(c.Lat) > 90 || Math.Abs(c.Lon) > 180 || string.IsNullOrWhiteSpace(c.NameEl) || string.IsNullOrWhiteSpace(c.NameEn)) continue;
            if (list.Any(x => string.Equals(x.Id, c.Id, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add(c with { NameEl = c.NameEl.Trim(), NameEn = c.NameEn.Trim() });
        }
        custom = list;
        everything = list.Count == 0 ? BuiltIn : BuiltIn.Concat(list).ToList();
    }

    public static GreekCity? Find(string? id) => everything.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string[] DefaultSelection => new[] { "athens", "thessaloniki", "patras", "heraklion" };
}

/// <summary>What the weather is like right now in one city.</summary>
public sealed record WeatherReading(string CityId, double TemperatureC, int HumidityPercent, double WindKmh, int WindFromDegrees, int Code, bool IsDay, DateTimeOffset At,
    double? FeelsLikeC = null, double? PrecipitationMm = null, int? CloudPercent = null, double? PressureHpa = null, double? GustsKmh = null);

/// <summary>Simplified weather kind drawn as an icon (from the WMO weather code).</summary>
public enum WeatherKind { Clear, PartlyCloudy, Cloudy, Fog, Drizzle, Rain, Snow, Thunder }

public static class WeatherInfo
{
    public static WeatherKind KindOf(int wmoCode) => wmoCode switch
    {
        0 => WeatherKind.Clear,
        1 or 2 => WeatherKind.PartlyCloudy,
        3 => WeatherKind.Cloudy,
        45 or 48 => WeatherKind.Fog,
        >= 51 and <= 57 => WeatherKind.Drizzle,
        >= 61 and <= 67 or >= 80 and <= 82 => WeatherKind.Rain,
        >= 71 and <= 77 or 85 or 86 => WeatherKind.Snow,
        >= 95 => WeatherKind.Thunder,
        _ => WeatherKind.Cloudy
    };

    /// <summary>Plain-language name of a weather kind ("Clear", "Rain", ...), in Greek or English.</summary>
    public static string KindName(WeatherKind kind, bool greek) => kind switch
    {
        WeatherKind.Clear => Loc.T("weather.kind.clear"),
        WeatherKind.PartlyCloudy => Loc.T("weather.kind.partly"),
        WeatherKind.Cloudy => Loc.T("weather.kind.cloudy"),
        WeatherKind.Fog => Loc.T("weather.kind.fog"),
        WeatherKind.Drizzle => Loc.T("weather.kind.drizzle"),
        WeatherKind.Rain => Loc.T("weather.kind.rain"),
        WeatherKind.Snow => Loc.T("weather.kind.snow"),
        _ => Loc.T("weather.kind.thunder")
    };

    /// <summary>Compass point the wind comes FROM: 0 = N, 1 = NE, ... 7 = NW.</summary>
    public static int CompassIndex(int degrees) => (int)Math.Round(((degrees % 360 + 360) % 360) / 45.0) % 8;

    private static readonly string[] CompassKeys = { "wind.n", "wind.ne", "wind.e", "wind.se", "wind.s", "wind.sw", "wind.w", "wind.nw" };

    /// <summary>The details that can follow the temperature, in the order they are shown.</summary>
    public static readonly IReadOnlyList<string> FieldIds = new[] { "wind", "gusts", "humidity", "feels", "rain", "cloud", "pressure" };

    /// <summary>The details shown when the user has not chosen any.</summary>
    public static readonly IReadOnlyList<string> DefaultFields = new[] { "wind", "humidity" };

    /// <summary>One bar item: city, temperature with one decimal, then the chosen details (default: wind speed and direction, humidity). The icon code is kind + 10 when it is night.</summary>
    public static NewsItem ToItem(GreekCity city, WeatherReading w, bool greek, IReadOnlyCollection<string>? fields = null)
    {
        var culture = CultureInfo.GetCultureInfo("el-GR");
        var temp = w.TemperatureC.ToString("0.0", culture);
        fields ??= DefaultFields;
        var parts = new List<string> { $"{temp} °C" };
        foreach (var id in FieldIds)
        {
            if (!fields.Contains(id)) continue;
            switch (id)
            {
                case "wind":
                    parts.Add(Loc.T("weather.wind") + " " + w.WindKmh.ToString("0", culture) + " km/h " + Loc.T(CompassKeys[CompassIndex(w.WindFromDegrees)]));
                    break;
                case "gusts" when w.GustsKmh is { } gusts:
                    parts.Add(Loc.T("weather.gusts") + " " + gusts.ToString("0", culture) + " km/h");
                    break;
                case "humidity":
                    parts.Add(Loc.T("weather.humidity") + " " + w.HumidityPercent + "%");
                    break;
                case "feels" when w.FeelsLikeC is { } feels:
                    parts.Add(Loc.T("weather.feels") + " " + feels.ToString("0.0", culture) + " °C");
                    break;
                case "rain" when w.PrecipitationMm is { } rain:
                    parts.Add(Loc.T("weather.rain") + " " + rain.ToString("0.0", culture) + " mm");
                    break;
                case "cloud" when w.CloudPercent is { } cloud:
                    parts.Add(Loc.T("weather.cloud") + " " + cloud + "%");
                    break;
                case "pressure" when w.PressureHpa is { } pressure:
                    parts.Add(Loc.T("weather.pressure") + " " + pressure.ToString("0", culture) + " hPa");
                    break;
            }
        }
        var text = string.Join("  ·  ", parts);
        var icon = (int)KindOf(w.Code) + (w.IsDay ? 0 : 10);
        var name = greek ? city.NameEl : city.NameEn;
        return new NewsItem(text, "", name, w.At, Summary: name, Kind: NewsItemKind.Weather, Icon: icon, Key: city.Id);
    }
}

/// <summary>
/// Open-Meteo (open-meteo.com, free for non-commercial use, no API key, data licensed CC BY 4.0): current conditions of many places in one
/// request. The answer is cached for ten minutes; a failed request keeps the previous readings.
/// </summary>
public sealed class WeatherService
{
    private readonly HttpClient http = QuoteHttp.Create();
    private IReadOnlyList<WeatherReading> last = Array.Empty<WeatherReading>();
    private string lastKey = "";
    private DateTimeOffset lastFetch = DateTimeOffset.MinValue;

    /// <summary>True when the last request failed (the readings returned are then the previous ones).</summary>
    public bool LastFailed { get; private set; }

    public async Task<IReadOnlyList<WeatherReading>> FetchAsync(IEnumerable<string> cityIds, CancellationToken ct)
    {
        var cities = cityIds.Select(GreekCities.Find).Where(c => c is not null).Select(c => c!).Distinct().Take(100).ToList();
        if (cities.Count == 0) return Array.Empty<WeatherReading>();
        var key = string.Join(",", cities.Select(c => c.Id));
        if (key == lastKey && last.Count > 0 && DateTimeOffset.UtcNow - lastFetch < TimeSpan.FromMinutes(10)) { LastFailed = false; return last; }

        var lat = string.Join(",", cities.Select(c => c.Lat.ToString("0.####", CultureInfo.InvariantCulture)));
        var lon = string.Join(",", cities.Select(c => c.Lon.ToString("0.####", CultureInfo.InvariantCulture)));
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                  "&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m,wind_direction_10m,is_day,apparent_temperature,precipitation,cloud_cover,pressure_msl,wind_gusts_10m&wind_speed_unit=kmh&timezone=auto";
        try
        {
            using var limit = QuoteHttp.Limit(ct, 20);
            var json = await http.GetStringAsync(url, limit.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var results = new List<WeatherReading>();
            var root = doc.RootElement;
            // One place: an object; several places: an array of objects in the order asked.
            var nodes = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : new List<JsonElement> { root };
            for (var i = 0; i < nodes.Count && i < cities.Count; i++)
            {
                if (!nodes[i].TryGetProperty("current", out var cur)) continue;
                double D(string n) => cur.GetProperty(n).GetDouble();
                double? O(string n) => cur.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetDouble() : null;
                var at = cur.TryGetProperty("time", out var t) && DateTime.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
                    ? new DateTimeOffset(dt, TimeSpan.Zero) : DateTimeOffset.UtcNow;
                results.Add(new WeatherReading(cities[i].Id, D("temperature_2m"), (int)Math.Round(D("relative_humidity_2m")), D("wind_speed_10m"),
                    (int)Math.Round(D("wind_direction_10m")), (int)D("weather_code"), cur.GetProperty("is_day").GetInt32() == 1, at,
                    O("apparent_temperature"), O("precipitation"), O("cloud_cover") is { } cloud ? (int)Math.Round(cloud) : null, O("pressure_msl"), O("wind_gusts_10m")));
            }
            if (results.Count == 0)
            {
                LastFailed = true;
                return last;
            }
            LastFailed = false;
            last = results;
            lastKey = key;
            lastFetch = DateTimeOffset.UtcNow;
            return results;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            if (!ct.IsCancellationRequested) Log.Write($"Weather: {ex.GetType().Name}: {ex.Message}");
            LastFailed = true;
            return last;
        }
    }

    /// <summary>Bar items for the readings, in the order of the user's selection.</summary>
    public static IReadOnlyList<NewsItem> ToItems(IEnumerable<string> order, IReadOnlyList<WeatherReading> readings, bool greek, IReadOnlyCollection<string>? fields = null)
    {
        var items = new List<NewsItem>();
        foreach (var id in order)
        {
            var city = GreekCities.Find(id);
            var reading = readings.FirstOrDefault(r => r.CityId == id);
            if (city is not null && reading is not null) items.Add(WeatherInfo.ToItem(city, reading, greek, fields));
        }
        return items;
    }
}
