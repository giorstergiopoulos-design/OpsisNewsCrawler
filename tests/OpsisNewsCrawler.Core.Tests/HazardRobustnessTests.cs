using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>One broken item must never cost the items after it; the number of broken items is reported.</summary>
public class HazardRobustnessTests
{
    private const string UsgsGood1 = "{\"id\":\"a1\",\"properties\":{\"mag\":5.1,\"place\":\"Crete\",\"time\":1760000000000,\"url\":\"https://x/a1\",\"type\":\"earthquake\"},\"geometry\":{\"coordinates\":[25.0,35.0,10.0]}}";
    private const string UsgsNoTime = "{\"id\":\"b2\",\"properties\":{\"mag\":4.9,\"place\":\"No time\",\"type\":\"earthquake\"},\"geometry\":{\"coordinates\":[25.0,35.0,10.0]}}";
    private const string UsgsNoGeometry = "{\"id\":\"c3\",\"properties\":{\"mag\":4.9,\"place\":\"No geometry\",\"time\":1760000000000,\"type\":\"earthquake\"}}";
    private const string UsgsGood2 = "{\"id\":\"d4\",\"properties\":{\"mag\":6.2,\"place\":\"Rhodes\",\"time\":1760000100000,\"url\":\"https://x/d4\",\"type\":\"earthquake\"},\"geometry\":{\"coordinates\":[28.0,36.0,20.0]}}";

    [Fact]
    public void UsgsKeepsTheItemsAfterABrokenOne()
    {
        var json = "{\"features\":[" + UsgsGood1 + "," + UsgsNoTime + "," + UsgsNoGeometry + "," + UsgsGood2 + "]}";
        var list = HazardFeeds.ParseUsgs(json, out var skipped);
        Assert.Equal(new[] { "usgs:a1", "usgs:d4" }, list.Select(e => e.Id));
        Assert.Equal(2, skipped);
    }

    [Fact]
    public void UsgsNonEarthquakesAreLeftOutButNotCountedAsBroken()
    {
        var quarry = "{\"id\":\"q\",\"properties\":{\"mag\":2.0,\"time\":1760000000000,\"type\":\"quarry blast\"},\"geometry\":{\"coordinates\":[1,2,3]}}";
        var list = HazardFeeds.ParseUsgs("{\"features\":[" + quarry + "," + UsgsGood1 + "]}", out var skipped);
        Assert.Single(list);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void EmscKeepsTheItemsAfterABrokenOne()
    {
        const string bad = "{\"id\":\"x\",\"properties\":{\"mag\":4.0,\"lat\":35.0,\"lon\":25.0}}";               // no time
        const string good = "{\"id\":\"y\",\"properties\":{\"unid\":\"20251001_0000001\",\"mag\":4.4,\"lat\":38.0,\"lon\":23.0,\"time\":\"2025-10-01T10:00:00.0Z\",\"flynn_region\":\"GREECE\"}}";
        var list = HazardFeeds.ParseEmsc("{\"features\":[" + bad + "," + good + "]}", out var skipped);
        Assert.Single(list);
        Assert.Equal("emsc:20251001_0000001", list[0].Id);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void GdacsMissingLongitudeLosesOnlyThatItem()
    {
        const string bad = "<item><gdacs:eventtype>FL</gdacs:eventtype><geo:Point><geo:lat>10</geo:lat></geo:Point><pubDate>Wed, 01 Oct 2025 10:00:00 GMT</pubDate></item>";
        const string good = "<item><gdacs:eventtype>EQ</gdacs:eventtype><gdacs:eventid>77</gdacs:eventid><gdacs:alertlevel>Orange</gdacs:alertlevel><gdacs:severity value=\"6.1\"/><geo:Point><geo:lat>38.5</geo:lat><geo:long>22.5</geo:long></geo:Point><pubDate>Wed, 01 Oct 2025 11:00:00 GMT</pubDate><link>http://www.gdacs.org/x</link></item>";
        var xml = "<rss xmlns:gdacs=\"http://www.gdacs.org\" xmlns:geo=\"http://www.w3.org/2003/01/geo/wgs84_pos#\"><channel>" + bad + good + "</channel></rss>";
        var list = HazardFeeds.ParseGdacs(xml, out var skipped);
        Assert.Single(list);
        Assert.Equal(1, skipped);
        Assert.Equal("https://www.gdacs.org/x", list[0].Url);
        Assert.Equal(2, list[0].Level);
    }

    [Fact]
    public void GdacsGarbageNumbersAreOneBadItem()
    {
        const string bad = "<item><gdacs:eventtype>EQ</gdacs:eventtype><geo:Point><geo:lat>abc</geo:lat><geo:long>1</geo:long></geo:Point><pubDate>Wed, 01 Oct 2025 10:00:00 GMT</pubDate></item>";
        const string good = "<item><gdacs:eventtype>WF</gdacs:eventtype><gdacs:eventid>5</gdacs:eventid><geo:Point><geo:lat>40</geo:lat><geo:long>22</geo:long></geo:Point><pubDate>Wed, 01 Oct 2025 10:00:00 GMT</pubDate></item>";
        var xml = "<rss xmlns:gdacs=\"http://www.gdacs.org\" xmlns:geo=\"http://www.w3.org/2003/01/geo/wgs84_pos#\"><channel>" + bad + good + "</channel></rss>";
        var list = HazardFeeds.ParseGdacs(xml, out var skipped);
        Assert.Single(list);
        Assert.Equal(HazardKind.Wildfire, list[0].Kind);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void EonetKeepsTheItemsAfterABrokenOne()
    {
        const string bad = "{\"id\":\"b\",\"title\":\"no geometry\",\"categories\":[{\"id\":\"wildfires\"}]}";
        const string good = "{\"id\":\"g\",\"title\":\"Fire\",\"link\":\"https://eonet/g\",\"categories\":[{\"id\":\"wildfires\"}],\"geometry\":[{\"type\":\"Point\",\"date\":\"2025-10-01T00:00:00Z\",\"coordinates\":[22.0,40.0]}]}";
        var list = HazardFeeds.ParseEonet("{\"events\":[" + bad + "," + good + "]}", out var skipped);
        Assert.Single(list);
        Assert.Equal(1, skipped);
        Assert.Equal("https://eonet/g", list[0].SourceUrls!["NASA EONET"]);
    }

    [Fact]
    public void MergedEventsKeepTheLinkOfEveryOrganisation()
    {
        var t = DateTimeOffset.Parse("2025-10-01T10:00:00Z");
        var a = new HazardEvent("usgs:1", HazardKind.Earthquake, "Crete", 35, 25, t, "https://usgs/1", 5.6, 5.6, 5.6, 2, new[] { "USGS" }, 10, new Dictionary<string, string> { ["USGS"] = "https://usgs/1" });
        var b = new HazardEvent("emsc:1", HazardKind.Earthquake, "Crete", 35.1, 25.1, t.AddMinutes(1), "https://emsc/1", 5.8, 5.8, 5.8, 2, new[] { "EMSC" }, 12, new Dictionary<string, string> { ["EMSC"] = "https://emsc/1" });
        var merged = Assert.Single(HazardMerge.Combine(new[] { a, b }));
        Assert.Equal(2, merged.SourceUrls!.Count);
        Assert.Equal(5.7, merged.Magnitude);
    }

    [Fact]
    public void EmptyAndBrokenWholeFeedsStillGiveNothingWithoutThrowing()
    {
        Assert.Empty(HazardFeeds.ParseUsgs("not json", out var s1));
        Assert.Equal(0, s1);
        Assert.Empty(HazardFeeds.ParseGdacs("<rss", out _));
        Assert.Empty(HazardFeeds.ParseEonet("{\"events\":5}", out _));
    }
}
