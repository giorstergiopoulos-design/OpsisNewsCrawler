using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class AnalysisTests
{
    private static List<decimal> Series(params double[] v) => v.Select(x => (decimal)x).ToList();

    [Fact]
    public void Backtest_RisingPrices_BuyAndHoldAndStrategyAgreeInSign()
    {
        var closes = Enumerable.Range(0, 120).Select(i => 100m + i).ToList();
        var r = Backtest.Run(closes, BacktestStrategy.SmaCross, 5, 20);
        Assert.Null(r.Problem);
        Assert.True(r.BuyHoldPercent > 100);
        Assert.True(r.StrategyPercent > 0);
        Assert.Equal(closes.Count, r.Equity.Count);
    }

    [Fact]
    public void Backtest_TooFewDays_ReportsHowManyAreNeeded()
    {
        var r = Backtest.Run(Series(1, 2, 3), BacktestStrategy.SmaCross, 20, 50);
        Assert.NotNull(r.Problem);
        Assert.Empty(r.Trades);
    }

    [Fact]
    public void Backtest_SignalIsTradedNextDay_NotOnTheSameClose()
    {
        // flat, then a jump on the day the fast average crosses: the strategy must not catch the jump day itself
        var closes = new List<decimal>();
        for (var i = 0; i < 30; i++) closes.Add(100m);
        for (var i = 0; i < 30; i++) closes.Add(100m + 10 * (i + 1));
        var r = Backtest.Run(closes, BacktestStrategy.SmaCross, 3, 10);
        Assert.NotEmpty(r.Trades);
        var first = r.Trades[0];
        Assert.True(first.EntryPrice > 100m, "entry happens after the jump has started");
        Assert.True(r.StrategyPercent < r.BuyHoldPercent);
    }

    [Fact]
    public void Backtest_RsiReversion_BuysTheDipAndSellsTheRecovery()
    {
        var closes = new List<decimal>();
        decimal p = 100;
        for (var cycle = 0; cycle < 4; cycle++)
        {
            for (var i = 0; i < 20; i++) closes.Add(p -= 2);
            for (var i = 0; i < 20; i++) closes.Add(p += 2);
        }
        var r = Backtest.Run(closes, BacktestStrategy.RsiReversion);
        Assert.Null(r.Problem);
        Assert.NotEmpty(r.Trades);
        Assert.InRange(r.MaxDrawdownPercent, 0, 100);
    }

    [Fact]
    public void FearGreed_AllRising_IsGreedy_AllFalling_IsFearful()
    {
        var up = Enumerable.Range(0, 80).Select(i => 100m + i).ToList();
        var down = Enumerable.Range(0, 80).Select(i => 200m - i).ToList();
        var greedy = FearGreed.Compute(new IReadOnlyList<decimal>[] { up, up, up });
        var fearful = FearGreed.Compute(new IReadOnlyList<decimal>[] { down, down, down });
        Assert.NotNull(greedy.Score);
        Assert.NotNull(fearful.Score);
        Assert.True(greedy.Score > 60, $"greedy {greedy.Score}");
        Assert.True(fearful.Score < 40, $"fearful {fearful.Score}");
    }

    [Fact]
    public void FearGreed_NotEnoughData_HasNoScore()
    {
        var r = FearGreed.Compute(new IReadOnlyList<decimal>[] { Series(1), Series(2) });
        Assert.Null(r.Score);
        Assert.True(r.Missing > 0);
    }

    [Fact]
    public void QuoteStore_Candles_SampledThenExact()
    {
        var path = Path.Combine(Path.GetTempPath(), "ohlc-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new QuoteStore(path);
            store.Record("X", "20261019", 10m);
            store.Record("X", "20261019", 12m);
            store.Record("X", "20261019", 9m);
            store.Record("X", "20261019", 11m);
            var c = store.Candles("X").Single();
            Assert.Equal((10m, 12m, 9m, 11m, false), (c.Open, c.High, c.Low, c.Close, c.Exact));

            store.RecordCandle("X", "20261019", 10m, 12.5m, 8.5m, 11m);
            c = store.Candles("X").Single();
            Assert.True(c.Exact);
            Assert.Equal(12.5m, c.High);

            store.Save();
            var again = new QuoteStore(path);
            Assert.Equal(12.5m, again.Candles("X").Single().High);
            Assert.Contains("X", again.Codes());
        }
        finally
        {
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileNameWithoutExtension(path) + "*")) File.Delete(f);
        }
    }

    [Fact]
    public void AiAssistant_ParseAnswer_JoinsTextBlocks_AndRejectsEmpty()
    {
        Assert.Equal("a\nb", AiAssistant.ParseAnswer("{\"content\":[{\"type\":\"text\",\"text\":\"a\"},{\"type\":\"text\",\"text\":\"b\"}]}"));
        Assert.Throws<AiException>(() => AiAssistant.ParseAnswer("{\"content\":[]}"));
        Assert.Throws<AiException>(() => AiAssistant.ParseAnswer("nope"));
    }
}
