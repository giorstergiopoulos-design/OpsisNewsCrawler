using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class PriceAlertTests
{
    private static readonly QuoteInstrument Brent = new(QuoteSource.Energy, "DCOILBRENTEU", "BRENT", "Brent", "Brent", "$/bbl");

    private static IReadOnlyList<(QuoteInstrument Instrument, MarketQuote Quote)> Prices(decimal price) =>
        new[] { (Brent, new MarketQuote("BRENT", "Brent", price, null, DateTimeOffset.UtcNow, Delayed: false)) };

    private static PriceAlert Alert(bool above, decimal limit) =>
        new() { Source = QuoteSource.Energy, Code = "dcoilbrenteu", Label = "BRENT", Above = above, Threshold = limit };

    [Fact]
    public void Fires_Once_When_Limit_Is_Passed_And_Rearms_After_Return()
    {
        var alert = Alert(above: true, limit: 90m);
        Assert.Empty(PriceAlertCheck.Evaluate(new[] { alert }, Prices(89m)));
        Assert.Single(PriceAlertCheck.Evaluate(new[] { alert }, Prices(91m)));
        Assert.Empty(PriceAlertCheck.Evaluate(new[] { alert }, Prices(92m)));   // already shown
        Assert.Empty(PriceAlertCheck.Evaluate(new[] { alert }, Prices(88m)));   // back below: re-armed
        Assert.Single(PriceAlertCheck.Evaluate(new[] { alert }, Prices(90m)));  // limit itself counts
    }

    [Fact]
    public void Below_Alert_And_Disabled_Alert()
    {
        Assert.Single(PriceAlertCheck.Evaluate(new[] { Alert(above: false, limit: 70m) }, Prices(69.5m)));
        var off = Alert(above: false, limit: 70m);
        off.Enabled = false;
        Assert.Empty(PriceAlertCheck.Evaluate(new[] { off }, Prices(60m)));
    }

    [Fact]
    public void Alert_For_Another_Instrument_Is_Ignored()
    {
        var other = Alert(above: true, limit: 1m);
        other.Code = "DCOILWTICO";
        Assert.Empty(PriceAlertCheck.Evaluate(new[] { other }, Prices(100m)));
    }

    [Fact]
    public void Normalize_Drops_Invalid_Alerts()
    {
        var config = new AppConfig { PriceAlerts = new List<PriceAlert> { Alert(true, 90m), Alert(true, 0m), new PriceAlert { Threshold = 5m } } };
        config.Normalize();
        Assert.Single(config.PriceAlerts);
    }
}
