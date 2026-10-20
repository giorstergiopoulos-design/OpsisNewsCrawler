using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class PortfolioTests
{
    private static PortfolioPosition Pos(string code, decimal qty, decimal cost, string currency = "EUR") => new()
    {
        Source = QuoteSource.Athens, Market = "XATH", Code = code, Symbol = code, NameEl = code, NameEn = code, Quantity = qty, AvgCost = cost, Currency = currency
    };

    private static MarketQuote Quote(decimal price, decimal? change = null) => new("X", "X", price, change, DateTimeOffset.Now);

    private static readonly Dictionary<string, decimal> Eur = new() { ["EUR"] = 1m, ["USD"] = 1.25m };

    [Fact]
    public void Profit_and_loss_of_a_euro_position()
    {
        var p = Pos("AAA", 10, 4m);
        var s = PortfolioCalc.Compute(new[] { p }, new Dictionary<string, MarketQuote> { [p.Key] = Quote(5m, 2m) }, Eur);
        Assert.Equal(50m, s.Value);
        Assert.Equal(40m, s.Cost);
        Assert.Equal(10m, s.Pnl);
        Assert.Equal(25m, s.PnlPercent);
        Assert.True(s.Complete);
        // +2 % today: the value yesterday was 50 / 1.02
        Assert.InRange(s.DayChange, 0.97m, 0.99m);
    }

    [Fact]
    public void A_dollar_position_is_converted_to_euro()
    {
        var p = Pos("BBB", 2, 100m, "USD");
        var s = PortfolioCalc.Compute(new[] { p }, new Dictionary<string, MarketQuote> { [p.Key] = Quote(125m) }, Eur);
        Assert.Equal(200m, s.Value); // 2 x 125 USD / 1.25
        Assert.Equal(160m, s.Cost);  // 2 x 100 USD / 1.25
        Assert.Equal(40m, s.Pnl);
    }

    [Fact]
    public void A_position_without_a_price_is_left_out_and_the_summary_says_so()
    {
        var a = Pos("AAA", 1, 10m); var b = Pos("NOPRICE", 5, 3m);
        var s = PortfolioCalc.Compute(new[] { a, b }, new Dictionary<string, MarketQuote> { [a.Key] = Quote(12m) }, Eur);
        Assert.False(s.Complete);
        Assert.Equal(12m, s.Value);
        Assert.Equal(2, s.Rows.Count);
        Assert.Null(s.Rows[1].Value);
    }

    [Fact]
    public void An_unknown_currency_is_not_guessed()
    {
        var p = Pos("AAA", 1, 10m, "XYZ");
        var s = PortfolioCalc.Compute(new[] { p }, new Dictionary<string, MarketQuote> { [p.Key] = Quote(12m) }, Eur);
        Assert.False(s.Complete);
        Assert.Equal(0m, s.Value);
    }

    [Fact]
    public void Rsi_of_a_steady_rise_is_100_and_of_a_steady_fall_is_0()
    {
        var up = Enumerable.Range(1, 30).Select(i => (decimal)i).ToList();
        var down = Enumerable.Range(1, 30).Select(i => (decimal)(100 - i)).ToList();
        Assert.Equal(100.0, Technical.Rsi(up));
        Assert.Equal(0.0, Technical.Rsi(down));
        Assert.Null(Technical.Rsi(up.Take(10).ToList()));
    }

    [Fact]
    public void Sma_cross_is_reported_right_after_it_happens()
    {
        // falling for 60 days, then rising: stop at the first day on which SMA20 is above SMA50
        var closes = Enumerable.Range(0, 60).Select(i => 100m - i * 0.5m).ToList();
        while (!(Technical.Sma(closes, 20) > Technical.Sma(closes, 50))) closes.Add(closes[^1] + 3m);
        Assert.Equal("▲ SMA20/50", Technical.Signal(closes));
        // three days after the crossing it is still reported, a week later it is not
        for (var i = 0; i < 3; i++) closes.Add(closes[^1] + 1m);
        Assert.Equal("▲ SMA20/50", Technical.Signal(closes));
        for (var i = 0; i < 8; i++) closes.Add(closes[^1] + 1m);
        Assert.NotEqual("▲ SMA20/50", Technical.Signal(closes));
    }

    [Fact]
    public void Signal_is_empty_without_enough_history()
    {
        Assert.Equal("", Technical.Signal(new List<decimal> { 1, 2, 3 }));
    }

    [Fact]
    public void Config_drops_invalid_positions()
    {
        var config = new AppConfig { Portfolio = new() { Pos("OK", 1, 1m), Pos("ZERO", 0, 1m), new PortfolioPosition { Code = "", Quantity = 1 } } };
        config.Normalize();
        Assert.Single(config.Portfolio);
    }
}
