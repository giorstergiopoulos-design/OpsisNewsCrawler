using System.Globalization;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>One holding of the portfolio: an instrument, how many, and the average price paid.</summary>
public sealed class PortfolioPosition
{
    public QuoteSource Source { get; set; }
    /// <summary>Euronext shares: the MIC of the market; empty otherwise.</summary>
    public string Market { get; set; } = "";
    public string Code { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string NameEl { get; set; } = "";
    public string NameEn { get; set; } = "";
    public decimal Quantity { get; set; }
    /// <summary>Average price paid per unit, in <see cref="Currency"/>.</summary>
    public decimal AvgCost { get; set; }
    /// <summary>Currency of the price (EUR, USD, GBP, CHF, JPY...).</summary>
    public string Currency { get; set; } = "EUR";

    public string DisplayName(bool greek) => greek ? NameEl : NameEn;

    /// <summary>The instrument whose price is needed (never shown on the bar by itself).</summary>
    public QuoteInstrument ToInstrument() =>
        new(Source, Code, Symbol, NameEl, NameEn, Currency == "USD" ? "$" : "€", enabled: false) { Market = Market };

    public static PortfolioPosition From(QuoteInstrument i, decimal quantity, decimal avgCost, string currency) => new()
    {
        Source = i.Source, Market = i.Market ?? "", Code = i.Code, Symbol = i.Symbol, NameEl = i.NameEl, NameEn = i.NameEn,
        Quantity = quantity, AvgCost = avgCost, Currency = currency
    };

    public string Key => $"{Source}|{(Market ?? "").ToUpperInvariant()}|{Code.ToUpperInvariant()}";
}

/// <summary>One line of the portfolio table, all money in the base currency (euro) except the native price columns.</summary>
public sealed record PortfolioRow(
    PortfolioPosition Position, decimal? Price, decimal? ChangePercent, decimal? Value, decimal Cost, decimal? Pnl, decimal? PnlPercent,
    decimal? DayChange, bool Delayed, DateTimeOffset? AsOf, double? Rsi, string Signal);

public sealed record PortfolioSummary(IReadOnlyList<PortfolioRow> Rows, decimal Value, decimal Cost, decimal Pnl, decimal PnlPercent, decimal DayChange, bool Complete);

public static class PortfolioCalc
{
    /// <summary>
    /// Value, cost and profit/loss of every position and in total, in euro. <paramref name="eurPerUnit"/> says how many units of a currency
    /// are one euro (EUR = 1, USD = 1.12...): a price in USD is divided by that. A position without a price or without a rate is left out of the
    /// totals and the summary says it is not complete.
    /// </summary>
    public static PortfolioSummary Compute(IEnumerable<PortfolioPosition> positions,
        IReadOnlyDictionary<string, MarketQuote> quotesByKey, IReadOnlyDictionary<string, decimal> eurPerUnit,
        Func<PortfolioPosition, IReadOnlyList<decimal>>? history = null)
    {
        var rows = new List<PortfolioRow>();
        decimal value = 0, cost = 0, day = 0;
        var complete = true;
        foreach (var p in positions)
        {
            var rate = Rate(eurPerUnit, p.Currency);
            var costEur = rate is { } r0 ? p.Quantity * p.AvgCost / r0 : 0m;
            double? rsi = null;
            var signal = "";
            if (history?.Invoke(p) is { Count: > 0 } closes)
            {
                rsi = Technical.Rsi(closes);
                signal = Technical.Signal(closes);
            }
            if (rate is null || !quotesByKey.TryGetValue(p.Key, out var q))
            {
                complete = false;
                rows.Add(new PortfolioRow(p, null, null, null, costEur, null, null, null, false, null, rsi, signal));
                continue;
            }
            var r = rate.Value;
            var valueEur = p.Quantity * q.Price / r;
            var pnl = valueEur - costEur;
            decimal? pnlPercent = costEur > 0 ? Math.Round(pnl / costEur * 100, 2) : null;
            decimal? dayChange = q.ChangePercent is { } c && c > -100 ? valueEur - valueEur / (1 + c / 100m) : null;
            rows.Add(new PortfolioRow(p, q.Price, q.ChangePercent, valueEur, costEur, pnl, pnlPercent, dayChange, q.Delayed, q.AsOf, rsi, signal));
            value += valueEur;
            cost += costEur;
            day += dayChange ?? 0;
        }
        var total = value - cost;
        return new PortfolioSummary(rows, value, cost, total, cost > 0 ? Math.Round(total / cost * 100, 2) : 0, day, complete);
    }

    private static decimal? Rate(IReadOnlyDictionary<string, decimal> eurPerUnit, string currency)
    {
        if (string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase)) return 1m;
        return eurPerUnit.TryGetValue(currency.ToUpperInvariant(), out var v) && v > 0 ? v : null;
    }
}

/// <summary>The currency rates of the European Central Bank through Frankfurter (units of a currency per one euro), cached for an hour.</summary>
public sealed class EcbRates
{
    private readonly HttpClient http = QuoteHttp.Create();
    private IReadOnlyDictionary<string, decimal> last = new Dictionary<string, decimal> { ["EUR"] = 1m };
    private DateTimeOffset fetched = DateTimeOffset.MinValue;

    public IReadOnlyDictionary<string, decimal> Latest => last;

    public async Task<IReadOnlyDictionary<string, decimal>> GetAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow - fetched < TimeSpan.FromHours(1) && last.Count > 1) return last;
        try
        {
            using var limit = QuoteHttp.Limit(ct, 15);
            var json = await http.GetStringAsync("https://api.frankfurter.dev/v1/latest?base=EUR", limit.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var rates = doc.RootElement.GetProperty("rates").EnumerateObject().ToDictionary(r => r.Name.ToUpperInvariant(), r => r.Value.GetDecimal());
            rates["EUR"] = 1m;
            last = rates;
            fetched = DateTimeOffset.UtcNow;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            if (!ct.IsCancellationRequested) Log.Write($"ECB rates: {ex.GetType().Name}: {ex.Message}");
        }
        return last;
    }
}

/// <summary>Simple technical indicators from the closing prices the program has stored (an idea taken from greek-stocks-ai, written from scratch).</summary>
public static class Technical
{
    /// <summary>Relative Strength Index (Wilder, 14 days). Null when there are fewer than period + 1 closes.</summary>
    public static double? Rsi(IReadOnlyList<decimal> closes, int period = 14)
    {
        if (closes.Count < period + 1) return null;
        double gain = 0, loss = 0;
        for (var i = 1; i <= period; i++)
        {
            var d = (double)(closes[i] - closes[i - 1]);
            if (d >= 0) gain += d; else loss -= d;
        }
        gain /= period;
        loss /= period;
        for (var i = period + 1; i < closes.Count; i++)
        {
            var d = (double)(closes[i] - closes[i - 1]);
            gain = (gain * (period - 1) + Math.Max(d, 0)) / period;
            loss = (loss * (period - 1) + Math.Max(-d, 0)) / period;
        }
        if (loss == 0) return gain == 0 ? 50 : 100;
        return Math.Round(100 - 100 / (1 + gain / loss), 1);
    }

    public static double? Sma(IReadOnlyList<decimal> closes, int period, int endOffset = 0)
    {
        var end = closes.Count - endOffset;
        if (period <= 0 || end < period) return null;
        double sum = 0;
        for (var i = end - period; i < end; i++) sum += (double)closes[i];
        return sum / period;
    }

    /// <summary>
    /// A short text signal: "▲ SMA20>SMA50" / "▼ SMA20&lt;SMA50" right after a crossing (within 3 days), otherwise RSI above 70 ("overbought")
    /// or below 30 ("oversold"). Empty when there is nothing to say or not enough data. Not investment advice.
    /// </summary>
    public static string Signal(IReadOnlyList<decimal> closes)
    {
        for (var back = 0; back <= 3; back++)
        {
            var fastNow = Sma(closes, 20, back); var slowNow = Sma(closes, 50, back);
            var fastPrev = Sma(closes, 20, back + 1); var slowPrev = Sma(closes, 50, back + 1);
            if (fastNow is null || slowNow is null || fastPrev is null || slowPrev is null) break;
            if (fastPrev <= slowPrev && fastNow > slowNow) return "▲ SMA20/50";
            if (fastPrev >= slowPrev && fastNow < slowNow) return "▼ SMA20/50";
        }
        return Rsi(closes) switch { > 70 => "RSI ▲", < 30 => "RSI ▼", _ => "" };
    }
}
