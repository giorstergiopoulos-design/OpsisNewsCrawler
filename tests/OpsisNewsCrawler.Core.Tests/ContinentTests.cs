using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class ContinentTests
{
    [Theory]
    [InlineData(37.98, 23.73, Continent.Europe)]      // Athens
    [InlineData(36.43, 28.22, Continent.Europe)]      // Rhodes
    [InlineData(51.5, -0.12, Continent.Europe)]       // London
    [InlineData(64.1, -21.9, Continent.Europe)]       // Reykjavik
    [InlineData(39.9, 32.8, Continent.Asia)]          // Ankara
    [InlineData(35.7, 139.7, Continent.Asia)]         // Tokyo
    [InlineData(28.6, 77.2, Continent.Asia)]          // Delhi
    [InlineData(-6.2, 106.8, Continent.Asia)]         // Jakarta
    [InlineData(30.0, 31.2, Continent.Africa)]        // Cairo
    [InlineData(-33.9, 18.4, Continent.Africa)]       // Cape Town
    [InlineData(6.5, 3.4, Continent.Africa)]          // Lagos
    [InlineData(40.7, -74.0, Continent.NorthAmerica)] // New York
    [InlineData(19.4, -99.1, Continent.NorthAmerica)] // Mexico City
    [InlineData(-33.4, -70.7, Continent.SouthAmerica)] // Santiago
    [InlineData(-12.0, -77.0, Continent.SouthAmerica)] // Lima
    [InlineData(-33.9, 151.2, Continent.Oceania)]     // Sydney
    [InlineData(-41.3, 174.8, Continent.Oceania)]     // Wellington
    [InlineData(21.3, -157.9, Continent.Oceania)]     // Honolulu
    [InlineData(-9.4, 147.2, Continent.Oceania)]      // Port Moresby
    [InlineData(-77.8, 166.7, Continent.Antarctica)]  // McMurdo
    public void KnownPlacesBelongToTheirContinent(double lat, double lon, Continent expected) => Assert.Equal(expected, Continents.Of(lat, lon));

    [Fact]
    public void EveryContinentHasAnOutlineWithSaneBounds()
    {
        foreach (var c in Continents.Order)
        {
            var rings = Continents.Outline(c);
            Assert.NotEmpty(rings);
            Assert.All(rings, r => Assert.True(r.Length >= 4));
            var (minLon, minLat, maxLon, maxLat) = Continents.Bounds(c);
            Assert.True(maxLon > minLon && maxLat > minLat);
            Assert.InRange(minLon, -180, 180); Assert.InRange(maxLon, -180, 180);
            Assert.InRange(minLat, -90, 90); Assert.InRange(maxLat, -90, 90);
        }
    }

    [Fact]
    public void WorldGroupingKeepsTheFixedOrderAndDropsEmptyContinents()
    {
        var t = DateTimeOffset.Parse("2025-10-01T10:00:00Z");
        HazardEvent E(string id, double lat, double lon) => new(id, HazardKind.Earthquake, id, lat, lon, t, "", 5, 5, 5, 1, new[] { "USGS" });
        var groups = HazardWorld.Group(new[] { E("tokyo", 35.7, 139.7), E("athens", 37.9, 23.7), E("lima", -12, -77), E("rome", 41.9, 12.5) });
        Assert.Equal(new[] { Continent.Europe, Continent.Asia, Continent.SouthAmerica }, groups.Select(g => g.Continent));
        Assert.Equal(2, groups[0].Events.Count);
    }

    [Fact]
    public void IconCodeRoundTripsKindAndLevel()
    {
        foreach (var kind in Enum.GetValues<HazardKind>())
            foreach (var level in new[] { 1, 2, 3 })
                Assert.Equal((kind, level), HazardText.DecodeIcon(HazardText.IconCode(kind, level)));
        Assert.Equal((HazardKind.Other, 1), HazardText.DecodeIcon(HazardText.IconCode((HazardKind)99, 1)));
    }

    [Fact]
    public void ContinentItemCarriesItsPictureAndName()
    {
        var item = HazardText.ContinentItem(Continent.Africa, k => "<" + k + ">");
        Assert.Equal(NewsItemKind.Continent, item.Kind);
        Assert.Equal((int)Continent.Africa, item.Icon);
        Assert.Equal("<continent.africa>", item.Title);
        Assert.Equal("continent:africa", item.Key);
    }

    [Fact]
    public void BarItemsOfEventsCarryTheKindInTheIcon()
    {
        var t = DateTimeOffset.Parse("2025-10-01T10:00:00Z");
        var fire = new HazardEvent("eonet:1", HazardKind.Wildfire, "Evia", 38.8, 23.3, t, "https://x", null, null, null, 2, new[] { "NASA EONET" });
        var item = HazardText.ToItem(fire, t, k => k, (k, a) => k);
        Assert.Equal((HazardKind.Wildfire, 2), HazardText.DecodeIcon(item.Icon));
        Assert.Equal("eonet:1", item.Key);
    }

    [Fact]
    public void ContinentNamesExistInEveryLanguage()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Locales");
        var codes = new[] { "el", "en", "de", "fr", "es", "it", "pt", "ru", "tr", "ja", "ko", "zh", "ar", "hi" };
        foreach (var code in codes)
        {
            var path = Directory.Exists(dir) ? Path.Combine(dir, code + ".json") : "";
            if (!File.Exists(path)) continue;   // the locale files are embedded; LocaleFilesTests checks every key of every language
        }
        Assert.All(Continents.Order, c => Assert.False(string.IsNullOrEmpty(Continents.Key(c))));
    }
}
