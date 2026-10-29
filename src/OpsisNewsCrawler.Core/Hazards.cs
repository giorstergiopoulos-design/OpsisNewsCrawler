using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace OpsisNewsCrawler.Core;

public enum HazardKind { Earthquake, Wildfire, Flood, Storm, Volcano, Drought, Tsunami, Other }

/// <summary>Which organisations are asked.</summary>
[Flags]
public enum HazardSources { None = 0, Usgs = 1, Emsc = 2, Gdacs = 4, Eonet = 8, All = Usgs | Emsc | Gdacs | Eonet }

/// <summary>
/// One natural event, possibly reported by several organisations. For an earthquake <see cref="Magnitude"/> is the average of the reported
/// magnitudes (<see cref="MagnitudeMin"/>..<see cref="MagnitudeMax"/> show how much they differ). <see cref="Level"/>: 1 = information, 2 = orange, 3 = red.
/// </summary>
public sealed record HazardEvent(string Id, HazardKind Kind, string Place, double Lat, double Lon, DateTimeOffset Time, string Url,
    double? Magnitude, double? MagnitudeMin, double? MagnitudeMax, int Level, IReadOnlyList<string> Sources, double? DepthKm = null)
{
    public int SourceCount => Sources.Count;
}

/// <summary>Parsers for the four public feeds. Each one never throws: broken input gives an empty list.</summary>
public static class HazardFeeds
{
    public const string UsgsUrl = "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_day.geojson";
    public const string EmscUrl = "https://www.seismicportal.eu/fdsnws/event/1/query?format=json&limit=100&minmag=2.5&orderby=time";
    public const string GdacsUrl = "https://www.gdacs.org/xml/rss.xml";
    public const string EonetUrl = "https://eonet.gsfc.nasa.gov/api/v3/events?status=open&days=10&limit=200";

    public static int EarthquakeLevel(double magnitude) => magnitude >= 7.0 ? 3 : magnitude >= 5.5 ? 2 : 1;

    public static IReadOnlyList<HazardEvent> ParseUsgs(string json)
    {
        var list = new List<HazardEvent>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array) return list;
            foreach (var f in features.EnumerateArray())
            {
                var props = f.GetProperty("properties");
                if (props.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() != "earthquake") continue;
                if (!Num(props, "mag", out var mag)) continue;
                var c = f.GetProperty("geometry").GetProperty("coordinates");
                var lon = c[0].GetDouble();
                var lat = c[1].GetDouble();
                var depth = c.GetArrayLength() > 2 ? c[2].GetDouble() : (double?)null;
                var ms = props.GetProperty("time").GetInt64();
                list.Add(new HazardEvent("usgs:" + f.GetProperty("id").GetString(), HazardKind.Earthquake, Str(props, "place") ?? "", lat, lon,
                    DateTimeOffset.FromUnixTimeMilliseconds(ms), Str(props, "url") ?? "", mag, mag, mag, EarthquakeLevel(mag), new[] { "USGS" }, depth));
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or FormatException) { }
        return list;
    }

    public static IReadOnlyList<HazardEvent> ParseEmsc(string json)
    {
        var list = new List<HazardEvent>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array) return list;
            foreach (var f in features.EnumerateArray())
            {
                var p = f.GetProperty("properties");
                if (!Num(p, "mag", out var mag) || !Num(p, "lat", out var lat) || !Num(p, "lon", out var lon)) continue;
                if (!DateTimeOffset.TryParse(Str(p, "time"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)) continue;
                var unid = Str(p, "unid") ?? f.GetProperty("id").GetString() ?? "";
                list.Add(new HazardEvent("emsc:" + unid, HazardKind.Earthquake, Title(Str(p, "flynn_region") ?? ""), lat, lon, time,
                    "https://www.emsc-csem.org/Earthquake_information/earthquake.php?id=" + Uri.EscapeDataString(unid), mag, mag, mag, EarthquakeLevel(mag), new[] { "EMSC" },
                    Num(p, "depth", out var depth) ? depth : null));
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { }
        return list;
    }

    public static IReadOnlyList<HazardEvent> ParseGdacs(string xml)
    {
        var list = new List<HazardEvent>();
        try
        {
            var doc = XDocument.Parse(xml.TrimStart('﻿'));
            foreach (var item in doc.Descendants().Where(e => e.Name.LocalName == "item"))
            {
                string? V(string local) => item.Elements().FirstOrDefault(e => e.Name.LocalName == local)?.Value;
                var type = V("eventtype");
                if (type is null) continue;
                var kind = type switch { "EQ" => HazardKind.Earthquake, "TC" => HazardKind.Storm, "FL" => HazardKind.Flood, "VO" => HazardKind.Volcano, "WF" => HazardKind.Wildfire, "DR" => HazardKind.Drought, "TS" => HazardKind.Tsunami, _ => HazardKind.Other };
                var point = item.Elements().FirstOrDefault(e => e.Name.LocalName == "Point");
                if (point is null) continue;
                var lat = double.Parse(point.Elements().First(e => e.Name.LocalName == "lat").Value, CultureInfo.InvariantCulture);
                var lon = double.Parse(point.Elements().First(e => e.Name.LocalName == "long").Value, CultureInfo.InvariantCulture);
                var level = V("alertlevel") switch { "Red" => 3, "Orange" => 2, _ => 1 };
                var sev = item.Elements().FirstOrDefault(e => e.Name.LocalName == "severity");
                double? magnitude = kind == HazardKind.Earthquake && double.TryParse((string?)sev?.Attribute("value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : null;
                if (!DateTimeOffset.TryParse(V("fromdate") ?? V("pubDate"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)) continue;
                var country = V("country") ?? "";
                var name = V("eventname");
                var place = !string.IsNullOrWhiteSpace(name) ? (country.Length > 0 ? name + ", " + country : name) : country;
                if (place.Length == 0) place = (V("title") ?? "").Split(" in ").LastOrDefault() ?? "";
                list.Add(new HazardEvent("gdacs:" + type + (V("eventid") ?? ""), kind, place, lat, lon, time, V("link")?.Replace("http://", "https://") ?? "",
                    magnitude, magnitude, magnitude, level, new[] { "GDACS" }));
            }
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or FormatException or InvalidOperationException) { }
        return list;
    }

    public static IReadOnlyList<HazardEvent> ParseEonet(string json)
    {
        var list = new List<HazardEvent>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array) return list;
            foreach (var e in events.EnumerateArray())
            {
                if (e.TryGetProperty("closed", out var closed) && closed.ValueKind == JsonValueKind.String) continue;
                var category = e.TryGetProperty("categories", out var cats) && cats.GetArrayLength() > 0 ? Str(cats[0], "id") : null;
                var kind = category switch { "wildfires" => HazardKind.Wildfire, "floods" => HazardKind.Flood, "severeStorms" => HazardKind.Storm, "volcanoes" => HazardKind.Volcano, "drought" => HazardKind.Drought, _ => HazardKind.Other };
                if (category is "seaLakeIce" or "snow" or "manmade" or "dustHaze" or "waterColor" or "tempExtremes") continue;   // not an emergency
                if (!e.TryGetProperty("geometry", out var geo) || geo.ValueKind != JsonValueKind.Array || geo.GetArrayLength() == 0) continue;
                var last = geo[geo.GetArrayLength() - 1];
                if (last.GetProperty("coordinates").ValueKind != JsonValueKind.Array || last.GetProperty("type").GetString() != "Point") continue;
                var c = last.GetProperty("coordinates");
                if (!DateTimeOffset.TryParse(Str(last, "date"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)) continue;
                list.Add(new HazardEvent("eonet:" + Str(e, "id"), kind, Str(e, "title") ?? "", c[1].GetDouble(), c[0].GetDouble(), time, Str(e, "link") ?? "",
                    null, null, null, 1, new[] { "NASA EONET" }));
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or FormatException) { }
        return list;
    }

    private static string Title(string upper) =>
        string.Join(' ', upper.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    private static string? Str(JsonElement e, string name) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Num(JsonElement e, string name, out double value)
    {
        value = 0;
        return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out value);
    }
}

/// <summary>Combines the reports of the different organisations about the same event.</summary>
public static class HazardMerge
{
    public const double SameQuakeKm = 60;
    public static readonly TimeSpan SameQuakeTime = TimeSpan.FromMinutes(3);
    public const double SameEventKm = 250;
    public static readonly TimeSpan SameEventTime = TimeSpan.FromDays(4);

    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>
    /// Earthquakes reported by several organisations within a few minutes and 60 km are one quake: the magnitude is the average of the reports
    /// (rounded to 0.1) and the range is kept. Other events of the same kind within 250 km and 4 days are merged the same way (the higher level wins).
    /// The result is sorted: highest level first, then the newest.
    /// </summary>
    public static IReadOnlyList<HazardEvent> Combine(IEnumerable<HazardEvent> events)
    {
        var groups = new List<List<HazardEvent>>();
        foreach (var e in events.OrderBy(e => e.Time))
        {
            var group = groups.FirstOrDefault(g => Same(g[0], e));
            if (group is null) groups.Add(new List<HazardEvent> { e });
            else group.Add(e);
        }
        return groups.Select(Merge).OrderByDescending(e => e.Level).ThenByDescending(e => e.Time).ToList();
    }

    private static bool Same(HazardEvent a, HazardEvent b)
    {
        if (a.Kind != b.Kind) return false;
        var km = DistanceKm(a.Lat, a.Lon, b.Lat, b.Lon);
        var dt = (a.Time - b.Time).Duration();
        return a.Kind == HazardKind.Earthquake ? km <= SameQuakeKm && dt <= SameQuakeTime : km <= SameEventKm && dt <= SameEventTime;
    }

    private static HazardEvent Merge(List<HazardEvent> g)
    {
        if (g.Count == 1) return g[0];
        // the report with the most detail first: a magnitude and a place name from an earthquake agency
        var best = g.OrderByDescending(e => e.Magnitude.HasValue).ThenByDescending(e => e.Url.Length > 0).ThenBy(e => e.Sources[0] == "USGS" ? 0 : 1).First();
        var mags = g.Where(e => e.Magnitude.HasValue).Select(e => e.Magnitude!.Value).ToList();
        var sources = g.SelectMany(e => e.Sources).Distinct().ToList();
        double? mean = mags.Count == 0 ? null : Math.Round(mags.Average(), 1, MidpointRounding.AwayFromZero);
        var level = Math.Max(g.Max(e => e.Level), mean is { } m ? HazardFeeds.EarthquakeLevel(m) : 0);
        var place = g.Select(e => e.Place).FirstOrDefault(p => p.Length > 0) ?? best.Place;
        var depth = g.Where(e => e.DepthKm.HasValue).Select(e => e.DepthKm!.Value).DefaultIfEmpty().Average();
        return best with
        {
            Place = place, Magnitude = mean, MagnitudeMin = mags.Count == 0 ? null : mags.Min(), MagnitudeMax = mags.Count == 0 ? null : mags.Max(),
            Level = level, Sources = sources, Time = g.Min(e => e.Time), DepthKm = g.Any(e => e.DepthKm.HasValue) ? depth : null,
            Id = best.Id
        };
    }
}

/// <summary>What the user wants to see.</summary>
public sealed record HazardFilter(string Region, double MinMagnitude, int MaxAgeHours, IReadOnlyCollection<HazardKind> Kinds)
{
    public static readonly HazardFilter Default = new("greece", 4.0, 48, Enum.GetValues<HazardKind>());

    // lat min, lat max, lon min, lon max
    private static readonly Dictionary<string, (double, double, double, double)> Boxes = new()
    {
        ["greece"] = (32.0, 43.0, 18.0, 30.5),     // Greece, the seas around it, the Balkan and Turkish coasts
        ["europe"] = (30.0, 72.0, -25.0, 45.0),
    };

    public static bool IsKnownRegion(string? region) => region is "greece" or "europe" or "world";

    public bool Matches(HazardEvent e, DateTimeOffset now)
    {
        if (!Kinds.Contains(e.Kind)) return false;
        if (now - e.Time > TimeSpan.FromHours(Math.Clamp(MaxAgeHours, 1, 24 * 30))) return false;
        if (e.Kind == HazardKind.Earthquake && e.Magnitude is { } m && m < MinMagnitude) return false;
        if (Boxes.TryGetValue(Region, out var b) && (e.Lat < b.Item1 || e.Lat > b.Item2 || e.Lon < b.Item3 || e.Lon > b.Item4)) return false;
        return true;
    }

    public IReadOnlyList<HazardEvent> Apply(IEnumerable<HazardEvent> events, DateTimeOffset now) => events.Where(e => Matches(e, now)).ToList();
}

/// <summary>Downloads the feeds (each one on its own: one that fails never hides the others) and combines them.</summary>
public sealed class HazardService
{
    private readonly HttpClient http;
    public HazardService(HttpMessageHandler? handler = null)
    {
        http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(15);
        http.MaxResponseContentBufferSize = 8 * 1024 * 1024;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler/1.0 (+https://github.com/giorstergiopoulos-design/OpsisNewsCrawler)");
    }

    /// <summary>Names of the sources that did not answer in the last run.</summary>
    public IReadOnlyList<string> LastFailed { get; private set; } = Array.Empty<string>();

    public async Task<IReadOnlyList<HazardEvent>> FetchAsync(HazardSources sources, CancellationToken ct)
    {
        var failed = new List<string>();
        async Task<IReadOnlyList<HazardEvent>> One(string name, string url, Func<string, IReadOnlyList<HazardEvent>> parse)
        {
            try
            {
                var text = await http.GetStringAsync(url, ct).ConfigureAwait(false);
                var parsed = parse(text);
                if (parsed.Count == 0 && text.Length < 200) { lock (failed) failed.Add(name); }
                return parsed;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                if (ct.IsCancellationRequested) throw;
                lock (failed) failed.Add(name);
                return Array.Empty<HazardEvent>();
            }
        }
        var tasks = new List<Task<IReadOnlyList<HazardEvent>>>();
        if (sources.HasFlag(HazardSources.Usgs)) tasks.Add(One("USGS", HazardFeeds.UsgsUrl, HazardFeeds.ParseUsgs));
        if (sources.HasFlag(HazardSources.Emsc)) tasks.Add(One("EMSC", HazardFeeds.EmscUrl, HazardFeeds.ParseEmsc));
        if (sources.HasFlag(HazardSources.Gdacs)) tasks.Add(One("GDACS", HazardFeeds.GdacsUrl, HazardFeeds.ParseGdacs));
        if (sources.HasFlag(HazardSources.Eonet)) tasks.Add(One("NASA EONET", HazardFeeds.EonetUrl, HazardFeeds.ParseEonet));
        var all = (await Task.WhenAll(tasks).ConfigureAwait(false)).SelectMany(x => x);
        LastFailed = failed.ToList();
        return HazardMerge.Combine(all);
    }
}

/// <summary>Text and bar items for the events (the translator can be replaced for tests).</summary>
public static class HazardText
{
    public static string KindName(HazardKind kind, Func<string, string>? t = null) =>
        (t ?? Loc.T)("hazard.kind." + kind.ToString().ToLowerInvariant());

    /// <summary>"M5.7 (5.6–5.8, 3 sources) · Crete, Greece · 10:55". Without a magnitude: the place and the time.</summary>
    public static string Title(HazardEvent e, DateTimeOffset now, Func<string, object[], string>? f = null, TimeZoneInfo? zone = null)
    {
        f ??= (k, a) => Loc.T(k, a);
        var local = TimeZoneInfo.ConvertTime(e.Time, zone ?? TimeZoneInfo.Local);
        var time = local.Date == TimeZoneInfo.ConvertTime(now, zone ?? TimeZoneInfo.Local).Date ? local.ToString("HH:mm", CultureInfo.InvariantCulture) : local.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        var parts = new List<string>();
        if (e.Magnitude is { } m)
        {
            var text = "M" + m.ToString("0.0", CultureInfo.InvariantCulture);
            if (e.SourceCount > 1 && e.MagnitudeMin is { } lo && e.MagnitudeMax is { } hi && hi - lo >= 0.15)
                text += " (" + f("hazard.range", new object[] { lo.ToString("0.0", CultureInfo.InvariantCulture), hi.ToString("0.0", CultureInfo.InvariantCulture), e.SourceCount }) + ")";
            parts.Add(text);
        }
        if (!string.IsNullOrWhiteSpace(e.Place)) parts.Add(e.Place);
        parts.Add(time);
        return string.Join(" · ", parts);
    }

    public static NewsItem ToItem(HazardEvent e, DateTimeOffset now, Func<string, string>? t = null, Func<string, object[], string>? f = null, TimeZoneInfo? zone = null)
    {
        t ??= Loc.T;
        f ??= (k, a) => Loc.T(k, a);
        var summary = f("hazard.sourcesList", new object[] { string.Join(", ", e.Sources) });
        if (e.DepthKm is { } d && d > 0) summary += " · " + f("hazard.depth", new object[] { Math.Round(d).ToString("0", CultureInfo.InvariantCulture) });
        return new NewsItem(Title(e, now, f, zone), e.Url, KindName(e.Kind, t), e.Time, summary, null, NewsItemKind.Hazard, Trend: e.Level >= 3 ? -1 : 0, Icon: e.Level, Key: e.Id);
    }

    /// <summary>The short line for the tooltip of the tray icon: "Earthquake M5.7 Crete".</summary>
    public static string Tip(HazardEvent e, Func<string, string>? t = null)
    {
        var m = e.Magnitude is { } mag ? " M" + mag.ToString("0.0", CultureInfo.InvariantCulture) : "";
        return KindName(e.Kind, t) + m + (string.IsNullOrWhiteSpace(e.Place) ? "" : " " + e.Place);
    }
}
