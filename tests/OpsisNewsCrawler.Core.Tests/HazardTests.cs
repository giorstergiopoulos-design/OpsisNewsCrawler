using System.Net;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class HazardTests
{
    private const string Usgs = """
    { "type": "FeatureCollection", "features": [
      { "id": "us7000q1", "properties": { "mag": 5.6, "place": "88 km NE of Luganville, Vanuatu", "time": 1793091323000, "url": "https://earthquake.usgs.gov/earthquakes/eventpage/us7000q1", "type": "earthquake" },
        "geometry": { "type": "Point", "coordinates": [165.60, -13.90, 10.0] } },
      { "id": "us7000q2", "properties": { "mag": 4.2, "place": "10 km S of Chania, Greece", "time": 1793000000000, "type": "earthquake" },
        "geometry": { "type": "Point", "coordinates": [24.02, 35.40, 12.5] } },
      { "id": "ex1", "properties": { "mag": 2.9, "place": "quarry blast", "time": 1793000000000, "type": "explosion" }, "geometry": { "type": "Point", "coordinates": [0, 0, 0] } },
      { "id": "bad", "properties": { "place": "no magnitude", "time": 1 }, "geometry": { "type": "Point", "coordinates": [0, 0] } }
    ] }
    """;

    private const string Emsc = """
    { "type": "FeatureCollection", "metadata": { "count": 1 }, "features": [
      { "type": "Feature", "id": "20261027_0000200", "geometry": { "type": "Point", "coordinates": [165.62, -13.89, -10.0] },
        "properties": { "unid": "20261027_0000200", "time": "2026-10-27T08:55:40.0Z", "flynn_region": "VANUATU REGION", "lat": -13.89, "lon": 165.62, "depth": 10.0, "mag": 5.7 } }
    ] }
    """;

    private const string Gdacs = """
    <?xml version="1.0" encoding="utf-8"?>
    <rss version="2.0" xmlns:geo="http://www.w3.org/2003/01/geo/wgs84_pos#" xmlns:gdacs="http://www.gdacs.org">
     <channel>
      <item>
       <title>Green earthquake (Magnitude 5.8M, Depth:10km) in Vanuatu</title>
       <link>http://www.gdacs.org/report.aspx?eventtype=EQ&amp;eventid=1574115</link>
       <gdacs:fromdate>Tue, 27 Oct 2026 08:55:23 GMT</gdacs:fromdate>
       <geo:Point><geo:lat>-13.8962</geo:lat><geo:long>165.6262</geo:long></geo:Point>
       <gdacs:eventtype>EQ</gdacs:eventtype><gdacs:alertlevel>Green</gdacs:alertlevel><gdacs:eventid>1574115</gdacs:eventid>
       <gdacs:severity unit="M" value="5.8">Magnitude 5.8M</gdacs:severity><gdacs:country>Vanuatu</gdacs:country>
      </item>
      <item>
       <title>Orange flood in Pakistan</title><link>http://www.gdacs.org/report.aspx?eventtype=FL&amp;eventid=1</link>
       <gdacs:fromdate>Mon, 26 Oct 2026 10:00:00 GMT</gdacs:fromdate>
       <geo:Point><geo:lat>30.0</geo:lat><geo:long>70.0</geo:long></geo:Point>
       <gdacs:eventtype>FL</gdacs:eventtype><gdacs:alertlevel>Orange</gdacs:alertlevel><gdacs:eventid>1</gdacs:eventid><gdacs:country>Pakistan</gdacs:country>
      </item>
      <item><title>no point</title><gdacs:eventtype>EQ</gdacs:eventtype></item>
     </channel>
    </rss>
    """;

    private const string Eonet = """
    { "events": [
      { "id": "EONET_1", "title": "Wildfire near Athens", "link": "https://eonet.gsfc.nasa.gov/api/v3/events/EONET_1", "closed": null,
        "categories": [ { "id": "wildfires", "title": "Wildfires" } ],
        "geometry": [ { "date": "2026-10-26T10:00:00Z", "type": "Point", "coordinates": [23.7, 38.0] }, { "date": "2026-10-27T01:00:00Z", "type": "Point", "coordinates": [23.8, 38.1] } ] },
      { "id": "EONET_2", "title": "Closed one", "closed": "2026-10-20T00:00:00Z", "categories": [ { "id": "wildfires" } ], "geometry": [ { "date": "2026-10-19T00:00:00Z", "type": "Point", "coordinates": [1, 1] } ] },
      { "id": "EONET_3", "title": "Sea ice", "link": "x", "closed": null, "categories": [ { "id": "seaLakeIce" } ], "geometry": [ { "date": "2026-10-19T00:00:00Z", "type": "Point", "coordinates": [1, 1] } ] }
    ] }
    """;

    [Fact]
    public void ParsesUsgsAndSkipsNonQuakesAndMissingMagnitudes()
    {
        var list = HazardFeeds.ParseUsgs(Usgs);
        Assert.Equal(2, list.Count);
        Assert.Equal(5.6, list[0].Magnitude);
        Assert.Equal(2, list[0].Level);
        Assert.Equal(-13.90, list[0].Lat);
        Assert.Equal(165.60, list[0].Lon);
        Assert.Equal(10.0, list[0].DepthKm);
    }

    [Fact]
    public void ParsesEmscAndGdacsAndEonet()
    {
        var emsc = Assert.Single(HazardFeeds.ParseEmsc(Emsc));
        Assert.Equal("Vanuatu Region", emsc.Place);
        Assert.Equal(5.7, emsc.Magnitude);

        var gdacs = HazardFeeds.ParseGdacs(Gdacs);
        Assert.Equal(2, gdacs.Count);
        Assert.Equal(5.8, gdacs[0].Magnitude);
        Assert.Equal(HazardKind.Flood, gdacs[1].Kind);
        Assert.Equal(2, gdacs[1].Level);                    // Orange
        Assert.StartsWith("https://", gdacs[0].Url);

        var eonet = Assert.Single(HazardFeeds.ParseEonet(Eonet));      // closed and sea-ice events are skipped
        Assert.Equal(HazardKind.Wildfire, eonet.Kind);
        Assert.Equal(38.1, eonet.Lat);                                  // the newest position
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("<html>")]
    public void GarbageGivesNothing(string text)
    {
        Assert.Empty(HazardFeeds.ParseUsgs(text));
        Assert.Empty(HazardFeeds.ParseEmsc(text));
        Assert.Empty(HazardFeeds.ParseGdacs(text));
        Assert.Empty(HazardFeeds.ParseEonet(text));
    }

    [Fact]
    public void TheSameQuakeFromThreeOrganisationsIsOneEventWithTheAverageMagnitude()
    {
        var all = HazardFeeds.ParseUsgs(Usgs).Concat(HazardFeeds.ParseEmsc(Emsc)).Concat(HazardFeeds.ParseGdacs(Gdacs)).Concat(HazardFeeds.ParseEonet(Eonet));
        var merged = HazardMerge.Combine(all);
        var vanuatu = Assert.Single(merged, e => e.Kind == HazardKind.Earthquake && e.Lat < -10);
        Assert.Equal(5.7, vanuatu.Magnitude);                           // (5.6 + 5.7 + 5.8) / 3
        Assert.Equal(5.6, vanuatu.MagnitudeMin);
        Assert.Equal(5.8, vanuatu.MagnitudeMax);
        Assert.Equal(3, vanuatu.SourceCount);
        Assert.Equal(new[] { "EMSC", "GDACS", "USGS" }, vanuatu.Sources.OrderBy(s => s).ToArray());
        Assert.Equal(2, vanuatu.Level);
        Assert.Contains(merged, e => e.Place.Contains("Chania"));       // the Greek quake stays separate
    }

    [Fact]
    public void QuakesFarApartOrFarApartInTimeAreNotMerged()
    {
        var t = new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);
        HazardEvent Q(string id, double lat, DateTimeOffset time, double mag) => new(id, HazardKind.Earthquake, "x", lat, 20, time, "", mag, mag, mag, 1, new[] { id });
        Assert.Equal(2, HazardMerge.Combine(new[] { Q("a", 35, t, 4), Q("b", 38, t, 4) }).Count);               // 300 km apart
        Assert.Equal(2, HazardMerge.Combine(new[] { Q("a", 35, t, 4), Q("b", 35.1, t.AddMinutes(10), 4) }).Count);   // 10 minutes apart
        Assert.Single(HazardMerge.Combine(new[] { Q("a", 35, t, 4), Q("b", 35.1, t.AddSeconds(40), 4.2) }));
    }

    [Fact]
    public void ResultIsSortedByLevelThenNewest()
    {
        var t = new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);
        HazardEvent E(string id, int level, DateTimeOffset time, double lat) => new(id, HazardKind.Other, "p", lat, 0, time, "", null, null, null, level, new[] { id });
        var list = HazardMerge.Combine(new[] { E("old3", 3, t.AddDays(-5), 0), E("new1", 1, t, 50), E("new3", 3, t, -50) });
        Assert.Equal(new[] { "new3", "old3", "new1" }, list.Select(e => e.Id).ToArray());
    }

    [Theory]
    [InlineData(4.4, 1)]
    [InlineData(5.5, 2)]
    [InlineData(6.9, 2)]
    [InlineData(7.0, 3)]
    public void EarthquakeLevelFollowsTheMagnitude(double magnitude, int level) => Assert.Equal(level, HazardFeeds.EarthquakeLevel(magnitude));

    [Fact]
    public void FilterAppliesRegionMagnitudeKindAndAge()
    {
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        HazardEvent Q(double lat, double lon, double mag, double hoursAgo, HazardKind kind = HazardKind.Earthquake) =>
            new("q", kind, "p", lat, lon, now.AddHours(-hoursAgo), "", mag, mag, mag, 1, new[] { "s" });
        var filter = new HazardFilter("greece", 4.0, 24, new[] { HazardKind.Earthquake });
        Assert.True(filter.Matches(Q(35.4, 24.0, 4.5, 2), now));                  // Crete
        Assert.False(filter.Matches(Q(35.4, 24.0, 3.5, 2), now));                 // too weak
        Assert.False(filter.Matches(Q(-13.9, 165.6, 6.0, 2), now));               // Vanuatu
        Assert.False(filter.Matches(Q(35.4, 24.0, 4.5, 30), now));                // too old
        Assert.False(filter.Matches(Q(35.4, 24.0, 4.5, 2, HazardKind.Flood), now));   // kind not wanted
        Assert.True(new HazardFilter("world", 4.0, 24, new[] { HazardKind.Earthquake }).Matches(Q(-13.9, 165.6, 6.0, 2), now));
        Assert.False(new HazardFilter("europe", 4.0, 24, new[] { HazardKind.Earthquake }).Matches(Q(-13.9, 165.6, 6.0, 2), now));
        Assert.True(HazardFilter.IsKnownRegion("world"));
        Assert.False(HazardFilter.IsKnownRegion("mars"));
    }

    [Fact]
    public void DistanceIsRoughlyRight()
    {
        Assert.InRange(HazardMerge.DistanceKm(37.98, 23.73, 40.64, 22.94), 295, 305);     // Athens - Thessaloniki
        Assert.Equal(0, HazardMerge.DistanceKm(10, 10, 10, 10), 6);
    }

    private sealed class Fake : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var host = request.RequestUri!.Host;
            if (host.Contains("seismicportal")) throw new HttpRequestException("down");
            var body = host.Contains("usgs") ? Usgs : host.Contains("gdacs") ? Gdacs : Eonet;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    [Fact]
    public async Task OneBrokenSourceDoesNotHideTheOthers()
    {
        var service = new HazardService(new Fake());
        var events = await service.FetchAsync(HazardSources.All, default);
        Assert.NotEmpty(events);
        Assert.Equal(new[] { "EMSC" }, service.LastFailed);
        Assert.Contains(events, e => e.Sources.Contains("USGS") && e.Sources.Contains("GDACS"));    // the Vanuatu quake from two of the three that answered
    }

    [Fact]
    public async Task OnlyTheChosenSourcesAreAsked()
    {
        var service = new HazardService(new Fake());
        var events = await service.FetchAsync(HazardSources.Usgs, default);
        Assert.All(events, e => Assert.Equal(new[] { "USGS" }, e.Sources.ToArray()));
        Assert.Empty(service.LastFailed);
    }
}

public sealed class HazardTextTests
{
    private static string T(string key) => key.Replace("hazard.kind.", "");
    private static string F(string key, object[] args) => key + "(" + string.Join(",", args) + ")";
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static HazardEvent Quake(double mag, double lo, double hi, int sources, DateTimeOffset time, string place = "Crete, Greece") =>
        new("q", HazardKind.Earthquake, place, 35, 24, time, "https://x", mag, lo, hi, 1, Enumerable.Range(0, sources).Select(i => "S" + i).ToList(), 12.4);

    [Fact]
    public void ShowsTheAverageAndTheRangeWhenOrganisationsDisagree()
    {
        var title = HazardText.Title(Quake(5.7, 5.6, 5.8, 3, Now.AddHours(-1)), Now, F, TimeZoneInfo.Utc);
        Assert.Equal("M5.7 (hazard.range(5.6,5.8,3)) · Crete, Greece · 11:00", title);
    }

    [Fact]
    public void NoRangeWhenTheyAgreeOrThereIsOneSource()
    {
        Assert.Equal("M4.2 · Crete, Greece · 11:00", HazardText.Title(Quake(4.2, 4.2, 4.2, 1, Now.AddHours(-1)), Now, F, TimeZoneInfo.Utc));
        Assert.Equal("M4.2 · Crete, Greece · 11:00", HazardText.Title(Quake(4.2, 4.2, 4.25, 2, Now.AddHours(-1)), Now, F, TimeZoneInfo.Utc));
    }

    [Fact]
    public void OlderEventsShowTheDate()
    {
        Assert.EndsWith("· 25/10 09:30", HazardText.Title(Quake(4.2, 4.2, 4.2, 1, new DateTimeOffset(2026, 10, 25, 9, 30, 0, TimeSpan.Zero)), Now, F, TimeZoneInfo.Utc));
    }

    [Fact]
    public void ItemCarriesKindLevelLinkAndSources()
    {
        var item = HazardText.ToItem(Quake(7.1, 7.0, 7.2, 2, Now.AddHours(-1)) with { Level = 3 }, Now, T, F, TimeZoneInfo.Utc);
        Assert.Equal(NewsItemKind.Hazard, item.Kind);
        Assert.Equal("earthquake", item.Source);
        Assert.Equal(3, item.Icon);
        Assert.Equal(-1, item.Trend);
        Assert.Equal("https://x", item.Url);
        Assert.Equal("hazard.sourcesList(S0, S1) · hazard.depth(12)", item.Summary);
    }

    [Fact]
    public void EventWithoutMagnitudeIsPlaceAndTime()
    {
        var fire = new HazardEvent("f", HazardKind.Wildfire, "Wildfire near Athens", 38, 23, Now.AddHours(-2), "", null, null, null, 1, new[] { "NASA EONET" });
        Assert.Equal("Wildfire near Athens · 10:00", HazardText.Title(fire, Now, F, TimeZoneInfo.Utc));
        Assert.Equal("wildfire Wildfire near Athens", HazardText.Tip(fire, T));
    }
}

public sealed class EnergyYahooMappingTests
{
    [Fact]
    public void EveryEnergySeriesHasAYahooFuture() =>
        Assert.All(PythHermes.FeedIds.Keys, code => Assert.True(FredEnergyProvider.YahooSymbols.ContainsKey(code), code));
}
