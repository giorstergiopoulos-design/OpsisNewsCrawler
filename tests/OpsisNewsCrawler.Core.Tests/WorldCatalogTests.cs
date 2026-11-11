using Xunit;
using Xunit.Abstractions;

namespace OpsisNewsCrawler.Core.Tests;

public class WorldCatalogTests
{
    private readonly ITestOutputHelper output;
    public WorldCatalogTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void EuropeIsFirstAndEveryContinentHasCountriesWithCities()
    {
        var continents = WorldPlaces.Continents;
        Assert.Equal("europe", continents[0].Key);
        Assert.Equal(new[] { "europe", "asia", "africa", "north-america", "south-america", "oceania" }, continents.Select(c => c.Key).ToArray());
        Assert.True(continents[0].Countries.Count >= 45, "Europe countries: " + continents[0].Countries.Count);
        foreach (var country in continents.SelectMany(c => c.Countries)) Assert.NotEmpty(country.Cities);
        output.WriteLine($"countries {continents.Sum(c => c.Countries.Count)}, cities {continents.Sum(c => c.Countries.Sum(k => k.Cities.Count))}");
    }

    [Fact]
    public void EveryCityCanBeAddedToTheWeatherListAndHasBothNames()
    {
        var ids = new HashSet<string>();
        foreach (var city in WorldPlaces.Continents.SelectMany(c => c.Countries).SelectMany(c => c.Cities))
        {
            Assert.True(GeoSearch.IsValidId(city.Id), "id " + city.Id + " of " + city.NameEn);                     // the settings accept it as a place
            Assert.True(ids.Add(city.Id), "two cities share the coordinates " + city.Id + " (" + city.NameEn + ")");
            Assert.False(string.IsNullOrWhiteSpace(city.NameEl)); Assert.False(string.IsNullOrWhiteSpace(city.NameEn));
            Assert.InRange(city.Lat, -90, 90); Assert.InRange(city.Lon, -180, 180);
        }
        Assert.True(ids.Count >= 200, "cities: " + ids.Count);
        // the list is added to the custom places (at most 60): the code path of the settings keeps them
        GreekCities.SetCustom(WorldPlaces.Continents[0].Countries.SelectMany(c => c.Cities).Take(GreekCities.MaxCustom));
        Assert.Equal(GreekCities.MaxCustom, GreekCities.Custom.Count);
        GreekCities.SetCustom(null);
    }

    [Theory]
    [InlineData("France", 48.8566, 2.3522)]
    [InlineData("Italy", 41.9028, 12.4964)]
    [InlineData("Japan", 35.6762, 139.6503)]
    public void CapitalsAreWhereTheyShouldBe(string country, double lat, double lon)
    {
        var c = WorldPlaces.Continents.SelectMany(k => k.Countries).First(k => k.NameEn == country);
        Assert.Contains(c.Cities, x => Math.Abs(x.Lat - lat) < 0.05 && Math.Abs(x.Lon - lon) < 0.05);
    }

    [Fact]
    public void TheNewsFeedsAreOpenHttpsAddressesWithoutDuplicates()
    {
        var feeds = WorldFeeds.All;
        Assert.True(feeds.Count >= 40, "feeds: " + feeds.Count);
        Assert.Equal(feeds.Count, feeds.Select(f => f.Url.ToLowerInvariant()).Distinct().Count());
        foreach (var f in feeds)
        {
            Assert.True(Uri.TryCreate(f.Url, UriKind.Absolute, out var u) && u.Scheme is "https" or "http", f.Url);
            Assert.False(f.Url.Contains("utm_") || f.Url.Contains("token=") || f.Url.Contains("apikey", StringComparison.OrdinalIgnoreCase), "a feed address must not carry a key: " + f.Url);
            Assert.Contains(f.ContinentKey, WorldPlaces.Continents.Select(c => c.Key));
            Assert.False(string.IsNullOrWhiteSpace(f.Name) || string.IsNullOrWhiteSpace(f.CountryEl) || string.IsNullOrWhiteSpace(f.CountryEn));
        }
        Assert.NotEmpty(WorldFeeds.Countries("europe"));
        Assert.Contains(WorldFeeds.Of("europe", "GB"), f => f.Name == "BBC News");
        output.WriteLine($"feeds {feeds.Count} in {feeds.Select(f => f.CountryCode).Distinct().Count()} places");
    }

    [Fact]
    public void TheMarketCatalogueHasNoDuplicateSymbolAndTheNewIndicesAreThere()
    {
        var symbols = YahooPresets.All.SelectMany(g => g.Items).Select(i => i.Symbol).ToList();
        Assert.Equal(symbols.Count, symbols.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var world = YahooPresets.All.First(g => g.Key == "worldindices");
        Assert.True(world.Items.Count >= 20);
        Assert.Contains(world.Items, i => i.Symbol == "^NSEI");
        Assert.Contains(YahooPresets.Filter(null, "Norway"), x => x.Item.Symbol == "OBX.OL");     // found by country
        Assert.Contains(YahooPresets.Filter(null, "Νορβηγία"), x => x.Item.Symbol == "OBX.OL");
    }

    [Fact]
    public void EveryLanguageHasTheTextsOfTheWorldCards()
    {
        var keys = new[] { "yahoo.group.worldindices", "world.card", "world.note", "world.continent", "world.country", "worldfeeds.title", "worldfeeds.note", "worldfeeds.add", "worldfeeds.addAll", "worldfeeds.added" };
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OpsisNewsCrawler.Core", "Locales");
        foreach (var f in Directory.GetFiles(dir, "*.json"))
        {
            var d = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(f))!;
            foreach (var k in keys) Assert.True(d.TryGetValue(k, out var v) && v.Length > 0, $"{Path.GetFileName(f)}: {k}");
            Assert.Contains("{0}", d["worldfeeds.added"]);
        }
    }
}
