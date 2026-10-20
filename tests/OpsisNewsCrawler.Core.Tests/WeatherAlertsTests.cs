using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class WeatherAlertsTests
{
    private static DateTimeOffset[] Hours(int n) => Enumerable.Range(0, n).Select(i => new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero).AddHours(i)).ToArray();

    private static List<WeatherAlert> Run(int n, Func<int, double> temp, Func<int, double> rain, Func<int, double> gust, Func<int, int> code) =>
        WeatherAlerts.Evaluate(Hours(n), Enumerable.Range(0, n).Select(temp).ToArray(), Enumerable.Range(0, n).Select(rain).ToArray(),
            Enumerable.Range(0, n).Select(gust).ToArray(), Enumerable.Range(0, n).Select(code).ToArray());

    [Fact]
    public void CalmWeather_GivesNoAlerts() =>
        Assert.Empty(Run(12, _ => 20, _ => 0, _ => 20, _ => 1));

    [Fact]
    public void Thunderstorm_Wind_Rain_AreReported()
    {
        var alerts = Run(12, _ => 20, i => i == 3 ? 12 : 0, i => i == 5 ? 90 : 20, i => i == 2 ? 95 : 1);
        Assert.Contains(alerts, a => a.Kind == "storm");
        Assert.Contains(alerts, a => a.Kind == "wind" && a.TextEn.Contains("90"));
        Assert.Contains(alerts, a => a.Kind == "rain");
    }

    [Fact]
    public void HeatAndCold_UseThresholds()
    {
        Assert.Contains(Run(12, _ => 41, _ => 0, _ => 10, _ => 0), a => a.Kind == "heat");
        Assert.Contains(Run(12, _ => -4, _ => 0, _ => 10, _ => 0), a => a.Kind == "cold");
        Assert.Empty(Run(12, _ => 39, _ => 0, _ => 10, _ => 0));
    }

    [Fact]
    public void MissingValues_AreIgnored_AndShortArraysDoNotThrow()
    {
        Assert.Empty(Run(12, _ => double.NaN, _ => double.NaN, _ => double.NaN, _ => 0));
        Assert.Empty(WeatherAlerts.Evaluate(Hours(3), new double[] { 20 }, new double[0], new double[0], new int[0]));
    }

    [Fact]
    public void Parse_ReadsOpenMeteoAnswer()
    {
        const string json = "{\"utc_offset_seconds\":10800,\"hourly\":{\"time\":[\"2026-10-19T09:00\",\"2026-10-19T10:00\"],\"temperature_2m\":[20,21],\"precipitation\":[0,15],\"wind_gusts_10m\":[10,20],\"weather_code\":[1,3]}}";
        var alerts = WeatherAlerts.ParseAndEvaluate(json);
        Assert.NotNull(alerts);
        Assert.Single(alerts!, a => a.Kind == "rain");
        Assert.Null(WeatherAlerts.ParseAndEvaluate("{}"));
    }
}
