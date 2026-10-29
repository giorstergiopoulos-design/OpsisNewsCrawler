using System.Net;
using System.Text.Json;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

[Collection("custom places")]
public sealed class GeoSearchTests
{
    private const string OpenMeteo = """
    { "results": [
      { "id": 251280, "name": "Ζάκυνθος", "latitude": 37.78022, "longitude": 20.89555, "feature_code": "PPLA2", "country_code": "GR", "population": 11541,
        "country": "Ελλάδα", "admin1": "Ιόνια Νησιά", "admin2": "Ζάκυνθος", "postcodes": ["290 91", "290 92"] },
      { "id": 5, "name": "Zakynthos", "latitude": 51.5, "longitude": 0.1, "country_code": "GB", "population": 900000, "country": "United Kingdom" },
      { "name": "Broken", "latitude": 999, "longitude": 0 },
      { "latitude": 1, "longitude": 2 }
    ] }
    """;

    private const string NominatimReverse = """
    { "lat": "37.7252125", "lon": "20.7710325", "name": "Ζάκυνθος", "address": { "county": "Ζακύνθου", "state": "Ιόνια Νησιά", "country": "Ελλάδα", "country_code": "gr", "postcode": "290 91", "village": "Λιθακιά" } }
    """;

    private const string NominatimSearch = """
    [ { "lat": "40.6010699", "lon": "22.9618392", "name": "542 48", "display_name": "542 48, Θεσσαλονίκη", "address": { "city": "Θεσσαλονίκη", "postcode": "542 48", "country": "Ελλάδα", "country_code": "gr" } } ]
    """;

    [Fact]
    public void ParsesOpenMeteoAndDropsBrokenEntries()
    {
        var places = GeoSearch.ParseOpenMeteo(OpenMeteo);
        Assert.Equal(2, places.Count);
        Assert.Equal("Ζάκυνθος", places[0].Name);
        Assert.Equal("GR", places[0].CountryCode);
        Assert.Equal(251280, places[0].OpenMeteoId);
        Assert.Equal(new[] { "290 91", "290 92" }, places[0].Postcodes);
        Assert.Contains("37.7802, 20.89", places[0].Label);
    }

    [Fact]
    public void ParsesNominatimReverseWithTheSmallestPlaceName()
    {
        var p = Assert.Single(GeoSearch.ParseNominatim(NominatimReverse));
        Assert.Equal("Λιθακιά", p.Name);          // village before county
        Assert.Equal("GR", p.CountryCode);
        Assert.Equal("290 91", p.Postcodes![0]);
    }

    [Fact]
    public void ParsesNominatimPostcodeSearch()
    {
        var p = Assert.Single(GeoSearch.ParseNominatim(NominatimSearch));
        Assert.Equal("Θεσσαλονίκη", p.Name);
        Assert.Equal(40.601, p.Lat, 3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[{\"error\":\"Unable to geocode\"}]")]
    public void GarbageGivesNoPlaces(string json)
    {
        Assert.Empty(GeoSearch.ParseOpenMeteo(json));
        Assert.Empty(GeoSearch.ParseNominatim(json));
    }

    [Theory]
    [InlineData("37.78, 20.89", 37.78, 20.89)]
    [InlineData("37.78 20.89", 37.78, 20.89)]
    [InlineData("37.78,20.89", 37.78, 20.89)]
    [InlineData("37,78 20,89", 37.78, 20.89)]
    [InlineData("37,78; 20,89", 37.78, 20.89)]
    [InlineData("37.78N 20.89E", 37.78, 20.89)]
    [InlineData("37.78° N, 20.89° E", 37.78, 20.89)]
    [InlineData("33.9 S 18.4 E", -33.9, 18.4)]
    [InlineData("40.7 N 74.0 W", 40.7, -74.0)]
    [InlineData("-33.9, 151.2", -33.9, 151.2)]
    public void ParsesCoordinates(string text, double lat, double lon)
    {
        Assert.True(GeoSearch.TryParseCoordinates(text, out var a, out var b));
        Assert.Equal(lat, a, 4);
        Assert.Equal(lon, b, 4);
    }

    [Theory]
    [InlineData("Zakynthos")]
    [InlineData("542 48")]                   // a postcode, not a pair
    [InlineData("91.0, 20.0")]               // latitude out of range
    [InlineData("37.78, 200.0")]             // longitude out of range
    [InlineData("37.78E 20.89N")]            // hemispheres the wrong way round
    [InlineData("37.78")]
    [InlineData("37.78, 20.89, 5")]
    [InlineData("near 37.78, 20.89")]
    [InlineData("")]
    public void RejectsWhatAreNotCoordinates(string text) => Assert.False(GeoSearch.TryParseCoordinates(text, out _, out _));

    [Theory]
    [InlineData("542 48", true)]
    [InlineData("54248", true)]
    [InlineData(" 106 82 ", true)]
    [InlineData("5424", false)]
    [InlineData("Athens", false)]
    public void RecognisesGreekPostcodes(string text, bool expected) => Assert.Equal(expected, GeoSearch.LooksLikeGreekPostcode(text));

    [Fact]
    public void IdsAreStableAndValidated()
    {
        Assert.Equal("geo:37.78:20.896", GeoSearch.IdFor(37.78022, 20.89555));
        Assert.Equal(GeoSearch.IdFor(37.78002, 20.89601), GeoSearch.IdFor(37.78, 20.896));      // the same place within ~100 m
        Assert.True(GeoSearch.IsValidId("geo:-33.9:151.2"));
        Assert.False(GeoSearch.IsValidId("geo:95:10"));
        Assert.False(GeoSearch.IsValidId("athens"));
        Assert.False(GeoSearch.IsValidId("geo:1:2; DROP"));
        Assert.False(GeoSearch.IsValidId(null));
    }

    [Fact]
    public void CustomPlacesJoinTheCityListAndAreValidated()
    {
        try
        {
            var good = new GreekCity("geo:37.78:20.896", "Ζάκυνθος", "Zakynthos", 37.78, 20.896);
            GreekCities.SetCustom(new[]
            {
                good,
                good,                                                                   // duplicate
                new GreekCity("geo:1:2", "", "No name", 1, 2),                         // no name
                new GreekCity("athens-copy", "Α", "A", 1, 2),                          // not a geo id
                new GreekCity("geo:10:20", "Μ", "M", 95, 20),                          // latitude off the Earth
            });
            Assert.Single(GreekCities.Custom);
            Assert.Equal(GreekCities.BuiltIn.Count + 1, GreekCities.All.Count);
            Assert.NotNull(GreekCities.Find("GEO:37.78:20.896"));
            Assert.NotNull(GreekCities.Find("athens"));
        }
        finally { GreekCities.SetCustom(null); }
        Assert.Empty(GreekCities.Custom);
        Assert.Null(GreekCities.Find("geo:37.78:20.896"));
    }

    [Fact]
    public void CustomPlacesSurviveTheSettingsFileAndKeepTheirCitySelection()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opsis-places-{Guid.NewGuid():N}.json");
        try
        {
            var cfg = new AppConfig { CustomPlaces = { new GreekCity("geo:37.78:20.896", "Ζάκυνθος", "Zakynthos", 37.78, 20.896) } };
            cfg.WeatherCities = new List<string> { "athens", "geo:37.78:20.896", "geo:1:1" };   // the last one is not a known place
            File.WriteAllText(path, JsonSerializer.Serialize(cfg));
            var loaded = AppConfig.Load(path);
            Assert.Equal(new[] { "athens", "geo:37.78:20.896" }, loaded.WeatherCities);
            Assert.Single(loaded.CustomPlaces);
            Assert.Equal("Zakynthos", GreekCities.Find("geo:37.78:20.896")!.NameEn);
        }
        finally
        {
            GreekCities.SetCustom(null);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class Fake : HttpMessageHandler
    {
        public readonly List<string> Urls = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.RequestUri.Host.Contains("nominatim") ? NominatimReverse : OpenMeteo) });
        }
    }

    [Fact]
    public async Task NameSearchUsesOpenMeteoAndPutsGreekPlacesFirstWhenAsked()
    {
        var fake = new Fake();
        var places = await GeoSearch.SearchAsync("Zakynthos", "el", greekFirst: true, default, fake);
        Assert.Equal("GR", places[0].CountryCode);
        Assert.Contains("geocoding-api.open-meteo.com", fake.Urls[0]);
        Assert.Contains("name=Zakynthos", fake.Urls[0]);
        Assert.Contains("language=el", fake.Urls[0]);
        var world = await GeoSearch.SearchAsync("Zakynthos", "en", greekFirst: false, default, fake);
        Assert.Equal("GB", world[0].CountryCode);      // the biggest first when Greece is not preferred
    }

    [Fact]
    public async Task CoordinatesAreRecognisedWithNominatimButKeepTheTypedPosition()
    {
        var fake = new Fake();
        var places = await GeoSearch.SearchAsync("37.78, 20.89", "el", true, default, fake);
        var p = Assert.Single(places);
        Assert.Equal(37.78, p.Lat, 3);
        Assert.Equal(20.89, p.Lon, 3);
        Assert.Contains("nominatim.openstreetmap.org/reverse", fake.Urls[0]);
    }

    [Fact]
    public async Task PostcodeSearchGoesToNominatimNotOpenMeteo()
    {
        var fake = new Fake();
        await GeoSearch.SearchAsync("542 48", "el", true, default, fake);
        Assert.Contains("nominatim.openstreetmap.org/search", fake.Urls[0]);
        Assert.Contains("postalcode=54248", fake.Urls[0]);
        Assert.Contains("country=gr", fake.Urls[0]);
    }

    [Fact]
    public async Task TooShortQueriesAskNothing()
    {
        var fake = new Fake();
        Assert.Empty(await GeoSearch.SearchAsync("a", "el", true, default, fake));
        Assert.Empty(fake.Urls);
    }
}

[CollectionDefinition("custom places", DisableParallelization = true)]
public sealed class CustomPlacesCollection { }

public sealed class GeoSearchOddInputTests
{
    [Theory]
    [InlineData("[1,2,3]")]
    [InlineData("\"text\"")]
    [InlineData("[{\"lat\":\"x\",\"lon\":\"y\"}]")]
    public void OddJsonNeverThrows(string json)
    {
        Assert.Empty(GeoSearch.ParseNominatim(json));
        Assert.Empty(GeoSearch.ParseOpenMeteo(json));
    }
}
