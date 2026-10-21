namespace OpsisNewsCrawler.Core;

public enum BacktestStrategy { SmaCross, RsiReversion, MacdCross }

/// <summary>One completed (or still open) round trip of a back-test. Indexes point into the closing-price list.</summary>
public sealed record BacktestTrade(int EntryIndex, int? ExitIndex, decimal EntryPrice, decimal ExitPrice, double ReturnPercent, bool Open);

public sealed record BacktestResult(
    IReadOnlyList<BacktestTrade> Trades, double StrategyPercent, double BuyHoldPercent, double WinRatePercent,
    double MaxDrawdownPercent, int DaysInMarket, int Days, IReadOnlyList<double> Equity, string? Problem);

/// <summary>
/// A simple long-only back-test on stored closing prices (the idea of strategy tests from stonks-cli and greek-stocks-ai, written from scratch).
/// A signal seen at the close of day t is traded at the close of day t+1, so the test never uses a price it could not have known.
/// The whole capital is in or out; there are no fees, taxes or dividends. It is an illustration, not a promise: it says nothing about the future.
/// </summary>
public static class Backtest
{
    public static BacktestResult Run(IReadOnlyList<decimal> closes, BacktestStrategy strategy, int fast = 20, int slow = 50, int rsiPeriod = 14, double buyBelow = 30, double sellAbove = 70, double feePercent = 0)
    {
        var warmup = strategy switch { BacktestStrategy.SmaCross => Math.Max(fast, slow) + 2, BacktestStrategy.MacdCross => 26 + 9 + 2, _ => rsiPeriod + 2 };
        var macd = strategy == BacktestStrategy.MacdCross ? Technical.Macd(closes) : null;
        if (closes.Count < warmup + 2)
            return new BacktestResult(Array.Empty<BacktestTrade>(), 0, 0, 0, 0, 0, closes.Count, new[] { 100.0 }, $"{warmup + 2}");

        var inMarket = false;
        var entry = 0m; var entryIndex = 0;
        var trades = new List<BacktestTrade>();
        var equity = new List<double>(closes.Count) { 100 };
        double capital = 100, peak = 100, maxDd = 0;
        var daysIn = 0;
        var fee = feePercent / 100.0;
        var wantLong = false;
        for (var i = 1; i < closes.Count; i++)
        {
            // today's close carries out what yesterday's close decided
            var held = inMarket && closes[i - 1] > 0 ? (double)(closes[i] / closes[i - 1]) : 1.0;
            if (wantLong && !inMarket)
            {
                inMarket = true; entry = closes[i]; entryIndex = i;
                capital *= 1 - fee;                       // bought at the close: nothing earned today
            }
            else if (!wantLong && inMarket)
            {
                capital *= held * (1 - fee);              // held through today, sold at the close
                daysIn++;
                inMarket = false;
                trades.Add(new BacktestTrade(entryIndex, i, entry, closes[i], (double)((closes[i] - entry) / entry * 100m), false));
            }
            else if (inMarket) { capital *= held; daysIn++; }
            equity.Add(capital);
            peak = Math.Max(peak, capital);
            maxDd = Math.Max(maxDd, (peak - capital) / peak * 100);

            // today's signal, valid for tomorrow
            var upto = Slice(closes, i + 1);
            switch (strategy)
            {
                case BacktestStrategy.SmaCross:
                    var f = Technical.Sma(upto, fast); var s = Technical.Sma(upto, slow);
                    if (f is not null && s is not null) wantLong = f > s;
                    break;
                case BacktestStrategy.MacdCross:
                    if (macd is not null && !double.IsNaN(macd.Signal[i])) wantLong = macd.Macd[i] > macd.Signal[i];
                    break;
                default:
                    var rsi = Technical.Rsi(upto, rsiPeriod);
                    if (rsi is not null)
                    {
                        if (rsi < buyBelow) wantLong = true;
                        else if (rsi > sellAbove) wantLong = false;
                    }
                    break;
            }
        }
        if (inMarket)
        {
            var last = closes[^1];
            trades.Add(new BacktestTrade(entryIndex, null, entry, last, (double)((last - entry) / entry * 100m), true));
        }
        var closed = trades.Where(t => !t.Open).ToList();
        var wins = closed.Count == 0 ? 0 : closed.Count(t => t.ReturnPercent > 0) * 100.0 / closed.Count;
        var buyHold = closes[0] > 0 ? (double)((closes[^1] - closes[0]) / closes[0] * 100m) : 0;
        return new BacktestResult(trades, Math.Round(capital - 100, 2), Math.Round(buyHold, 2), Math.Round(wins, 1), Math.Round(maxDd, 2), daysIn, closes.Count, equity, null);
    }

    private static IReadOnlyList<decimal> Slice(IReadOnlyList<decimal> list, int count) =>
        count >= list.Count ? list : new SliceView(list, count);

    private sealed class SliceView : IReadOnlyList<decimal>
    {
        private readonly IReadOnlyList<decimal> inner;
        public SliceView(IReadOnlyList<decimal> inner, int count) { this.inner = inner; Count = count; }
        public int Count { get; }
        public decimal this[int index] => inner[index];
        public IEnumerator<decimal> GetEnumerator() { for (var i = 0; i < Count; i++) yield return inner[i]; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

public enum FearGreedLevel { ExtremeFear, Fear, Neutral, Greed, ExtremeGreed }

/// <param name="Id">Stable key of the component (for the language files): breadth, today, rsi, trend, volatility.</param>
/// <param name="Score">0 = fear, 100 = greed.</param>
/// <param name="Detail">The raw figure behind the score, e.g. "62 % above SMA20".</param>
public sealed record FearGreedComponent(string Id, double Score, string Detail);

public sealed record FearGreedResult(double? Score, FearGreedLevel? Level, IReadOnlyList<FearGreedComponent> Components, int Instruments, int Missing);

/// <summary>
/// A "fear &amp; greed" gauge computed from the program's own stored prices of the shares and instruments it watches - NOT the CNN index
/// (that one is a trademark with its own data). Five parts, each 0 (fear) to 100 (greed); the score is their average.
/// A part that needs more days of history than are stored is left out and listed as missing.
/// </summary>
public static class FearGreed
{
    public static FearGreedResult Compute(IReadOnlyList<IReadOnlyList<decimal>> closesPerInstrument)
    {
        var comps = new List<FearGreedComponent>();
        var missing = 0;
        var n = closesPerInstrument.Count;

        // 1. breadth: how many are above their 20-day average
        var above = 0; var withSma = 0;
        foreach (var c in closesPerInstrument)
            if (Technical.Sma(c, 20) is { } sma) { withSma++; if ((double)c[^1] > sma) above++; }
        if (withSma >= 2) comps.Add(new("breadth", above * 100.0 / withSma, $"{above}/{withSma}")); else missing++;

        // 2. today: how many rose since the previous close
        var up = 0; var withPrev = 0;
        foreach (var c in closesPerInstrument)
            if (c.Count >= 2) { withPrev++; if (c[^1] > c[^2]) up++; }
        if (withPrev >= 2) comps.Add(new("today", up * 100.0 / withPrev, $"{up}/{withPrev}")); else missing++;

        // 3. strength: the average RSI(14)
        var rsis = closesPerInstrument.Select(c => Technical.Rsi(c)).Where(r => r is not null).Select(r => r!.Value).ToList();
        if (rsis.Count >= 2) comps.Add(new("rsi", rsis.Average(), $"RSI {rsis.Average():0}")); else missing++;

        // 4. trend: the average distance from the 50-day average, -10 % = fear ... +10 % = greed
        var dist = new List<double>();
        foreach (var c in closesPerInstrument)
            if (Technical.Sma(c, 50) is { } sma50 && sma50 > 0) dist.Add(((double)c[^1] - sma50) / sma50 * 100);
        if (dist.Count >= 2) comps.Add(new("trend", Math.Clamp(50 + dist.Average() * 5, 0, 100), $"{dist.Average():+0.0;-0.0} %")); else missing++;

        // 5. volatility: calm recent days (greed) against the longer run; a jump in movement is fear
        var ratios = new List<double>();
        foreach (var c in closesPerInstrument)
        {
            var r = Returns(c);
            if (r.Count < 30) continue;
            var recent = StdDev(r.Skip(r.Count - 10)); var longer = StdDev(r.Skip(Math.Max(0, r.Count - 60)));
            if (longer > 0) ratios.Add(recent / longer);
        }
        if (ratios.Count >= 2)
        {
            var ratio = ratios.Average();
            comps.Add(new("volatility", Math.Clamp((1.6 - ratio) / (1.6 - 0.7) * 100, 0, 100), $"x{ratio:0.00}"));
        }
        else missing++;

        if (comps.Count < 2) return new FearGreedResult(null, null, comps, n, missing);
        var score = comps.Average(c => c.Score);
        var level = score < 25 ? FearGreedLevel.ExtremeFear : score < 45 ? FearGreedLevel.Fear : score < 55 ? FearGreedLevel.Neutral : score < 75 ? FearGreedLevel.Greed : FearGreedLevel.ExtremeGreed;
        return new FearGreedResult(Math.Round(score, 0), level, comps, n, missing);
    }

    private static List<double> Returns(IReadOnlyList<decimal> c)
    {
        var r = new List<double>();
        for (var i = 1; i < c.Count; i++) if (c[i - 1] > 0) r.Add((double)(c[i] / c[i - 1]) - 1);
        return r;
    }

    private static double StdDev(IEnumerable<double> values)
    {
        var v = values.ToList();
        if (v.Count < 2) return 0;
        var mean = v.Average();
        return Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / (v.Count - 1));
    }
}
