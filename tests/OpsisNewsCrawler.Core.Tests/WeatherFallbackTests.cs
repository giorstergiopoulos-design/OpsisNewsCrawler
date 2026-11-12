using System.Net;
using System.Text;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class WeatherFallbackTests
{
    private static readonly GreekCity Athens = new("athens", "Αθήνα", "Athens", 37.98, 23.73);

    private const string Sample = """
        {"properties":{"timeseries":[
          {"time":"2026-11-11T10:00:00Z","data":{"instant":{"details":{"air_temperature":9.5,"wind_speed":5.0,"wind_from_direction":310,"relative_humidity":80,"cloud_area_fraction":100,"air_pressure_at_sea_level":1012.5}},"next_1_hours":{"summary":{"symbol_code":"heavyrainshowers_day"},"details":{"precipitation_amount":3.2}}}},
          {"time":"2026-11-11T11:00:00Z","data":{"instant":{"details":{"air_temperature":1.0,"wind_speed":1.0}},"next_1_hours":{"summary":{"symbol_code":"clearsky_night"},"details":{"precipitation_amount":0}}}}]}}
        """;

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        public int Calls; public List<HttpRequestMessage> Requests = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Calls++; Requests.Add(r); return Task.FromResult(f(r)); }
    }

    [Fact]
    public void ParsesTheCurrentStepAndConvertsWindToKmh()
    {
        var r = MetNoWeather.Parse(Athens, Sample, new DateTimeOffset(2026, 11, 11, 10, 30, 0, TimeSpan.Zero))!;
        Assert.Equal(9.5, r.TemperatureC); Assert.Equal(18.0, r.WindKmh, 3); Assert.Equal(65, r.Code); Assert.Equal(80, r.HumidityPercent);
        Assert.Equal(100, r.CloudPercent); Assert.Equal("MET Norway", r.Source); Assert.Equal(3.2, r.PrecipitationMm);
        var later = MetNoWeather.Parse(Athens, Sample, new DateTimeOffset(2026, 11, 11, 11, 30, 0, TimeSpan.Zero))!;
        Assert.Equal(1.0, later.TemperatureC); Assert.Equal(0, later.Code);
    }

    [Theory]
    [InlineData("not json")] [InlineData("{}")] [InlineData("{\"properties\":{\"timeseries\":[]}}")]
    public void BadPayloadsGiveNull(string json) => Assert.Null(MetNoWeather.Parse(Athens, json, DateTimeOffset.UtcNow));

    [Theory]
    [InlineData("clearsky_day", 0)] [InlineData("fair_night", 1)] [InlineData("partlycloudy_polartwilight", 2)] [InlineData("cloudy", 3)] [InlineData("fog", 45)]
    [InlineData("lightrain", 61)] [InlineData("rainshowers_day", 63)] [InlineData("heavyrain", 65)] [InlineData("lightsnow", 71)] [InlineData("snow", 73)]
    [InlineData("heavysnow", 75)] [InlineData("sleet", 66)] [InlineData("heavysleet", 67)] [InlineData("rainandthunder", 95)] [InlineData("heavyrainandthunder", 99)]
    public void SymbolsMapToWmoCodesWithTheSameIntensity(string symbol, int wmo) => Assert.Equal(wmo, MetNoWeather.WmoFromSymbol(symbol));

    [Fact]
    public void TheCodesFeedTheExistingIcons()
    {
        Assert.Equal(WeatherKind.Rain, WeatherInfo.KindOf(MetNoWeather.WmoFromSymbol("heavyrain")));
        Assert.Equal(WeatherKind.Thunder, WeatherInfo.KindOf(MetNoWeather.WmoFromSymbol("rainandthunder")));
        Assert.Equal(WeatherKind.Snow, WeatherInfo.KindOf(MetNoWeather.WmoFromSymbol("lightsnow")));
    }

    [Fact]
    public void UrlHasRoundedInvariantCoordinatesAndNoKey()
    {
        var old = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("el-GR");
            Assert.Equal("https://api.met.no/weatherapi/locationforecast/2.0/compact?lat=37.98&lon=23.73", MetNoWeather.Url(37.98381234, 23.72754321));
        }
        finally { Thread.CurrentThread.CurrentCulture = old; }
    }

    [Fact]
    public async Task SendsAUserAgentRespectsExpiresAndUsesIfModifiedSince()
    {
        var clock = new DateTimeOffset(2026, 11, 11, 10, 30, 0, TimeSpan.Zero); int n = 0;
        var handler = new Handler(_ =>
        {
            if (++n == 1)
            {
                var r = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Sample, Encoding.UTF8, "application/json") };
                r.Content.Headers.Expires = clock.AddMinutes(20); r.Content.Headers.LastModified = clock.AddMinutes(-5);
                return r;
            }
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        });
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(MetNoWeather.UserAgent);
        var svc = new MetNoWeather(http, () => clock);

        Assert.NotNull(await svc.GetAsync(Athens, default));
        Assert.Contains("OpsisNewsCrawler/", handler.Requests[0].Headers.UserAgent.ToString());
        Assert.NotNull(await svc.GetAsync(Athens, default));
        Assert.Equal(1, handler.Calls);                                   // inside Expires: no request
        clock = clock.AddMinutes(30);
        Assert.NotNull(await svc.GetAsync(Athens, default));
        Assert.Equal(2, handler.Calls);
        Assert.NotNull(handler.Requests[1].Headers.IfModifiedSince);      // conditional repeat; 304 served from the stored body
    }

    [Fact]
    public async Task OfflineGivesNullWithoutThrowing()
    {
        using var http = new HttpClient(new Handler(_ => throw new HttpRequestException("offline")));
        Assert.Null(await new MetNoWeather(http).GetAsync(Athens, default));
    }

    [Fact]
    public void SunPositionKnowsDayNightAndTheMidnightSun()
    {
        Assert.InRange(SunPosition.Elevation(new DateTime(2026, 6, 21, 10, 25, 0, DateTimeKind.Utc), 37.98, 23.73), 73.5, 77.5);
        Assert.True(SunPosition.IsDay(new DateTime(2026, 6, 21, 22, 0, 0, DateTimeKind.Utc), 69.65, 18.96));
        Assert.False(SunPosition.IsDay(new DateTime(2026, 12, 21, 12, 0, 0, DateTimeKind.Utc), 69.65, 18.96));
        Assert.False(SunPosition.IsDay(new DateTime(2026, 11, 11, 23, 0, 0, DateTimeKind.Utc), 37.98, 23.73));
    }
}
