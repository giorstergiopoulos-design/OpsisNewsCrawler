using System.Globalization;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// OPTIONAL, UNOFFICIAL daily history from Yahoo Finance's public chart address (the data source of greek-stocks-ai and several terminal dashboards).
/// Yahoo's terms do not allow automated use, so this is OFF until the user switches it on, says so in the settings, asks for very little
/// (one request per instrument and day, one second apart, stopped at the first "too many requests") and does nothing to get around limits.
/// It only fills the stored history so that charts, the back-test and the gauge work from the first day.
/// </summary>
public static class YahooSymbols
{
    /// <summary>The Yahoo ticker of an instrument, or null when there is no sensible one.</summary>
    public static string? For(QuoteInstrument i)
    {
        var symbol = i.Symbol.Trim();
        if (symbol.Length == 0) return null;
        switch (i.Source)
        {
            case QuoteSource.Yahoo: return i.Code;
            case QuoteSource.Athens: return symbol + ".AT";
            case QuoteSource.Euronext:
                var suffix = (i.Market ?? "").ToUpperInvariant() switch
                {
                    "XPAR" => ".PA", "XAMS" => ".AS", "XBRU" => ".BR", "XLIS" => ".LS", "XMSM" => ".IR", "MTAA" => ".MI", "XOSL" => ".OL", "XATH" => ".AT", _ => null
                };
                return suffix is null ? null : symbol + suffix;
            case QuoteSource.Metals:
                return i.Code.ToUpperInvariant() switch { "XAU" => "GC=F", "XAG" => "SI=F", "XPT" => "PL=F", "XPD" => "PA=F", "HG" => "HG=F", _ => null };
            case QuoteSource.Energy:
                return i.Code.ToUpperInvariant() switch { "DCOILBRENTEU" => "BZ=F", "DCOILWTICO" => "CL=F", "DHHNGSP" => "NG=F", _ => null };
            case QuoteSource.Crypto:
                return i.Code.ToUpperInvariant() is "BTC" or "ETH" ? i.Code.ToUpperInvariant() + "-USD" : null;
            case QuoteSource.Currency:
                return i.Code.Length == 6 ? i.Code.ToUpperInvariant() + "=X" : null;
            default: return null;
        }
    }
}

internal sealed record YahooSnapshot(decimal Price, decimal? PreviousClose, DateTimeOffset At, string Currency, string Type, string? Name, List<Candle> Candles);

public static class YahooHistory
{
    private const string Url = "https://query1.finance.yahoo.com/v8/finance/chart/{0}?range=6mo&interval=1d";
    internal const string ChartUrl = Url;

    internal static readonly HttpClient Http = Create();

    private static HttpClient Create()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler/0.13 (+https://github.com/giorstergiopoulos-design/OpsisNewsCrawler)");
        return c;
    }

    /// <summary>The latest price, previous close, time, currency and type of a chart answer, with its daily candles; null when there is no price.</summary>
    internal static YahooSnapshot? ParseSnapshot(string json)
    {
        try
        {
            var candles = Parse(json);
            using var doc = JsonDocument.Parse(json);
            var meta = doc.RootElement.GetProperty("chart").GetProperty("result")[0].GetProperty("meta");
            decimal? Num(string name) => meta.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var d) && d > 0 ? Math.Round(d, 4) : null;
            var price = Num("regularMarketPrice") ?? (candles.Count > 0 ? candles[^1].Close : null);
            if (price is null) return null;
            var at = meta.TryGetProperty("regularMarketTime", out var t) && t.TryGetInt64(out var secs) ? DateTimeOffset.FromUnixTimeSeconds(secs) : DateTimeOffset.UtcNow;
            var offset = meta.TryGetProperty("gmtoffset", out var g) && g.TryGetInt32(out var o) ? o : 0;
            var quoteDay = at.AddSeconds(offset).UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var earlier = candles.LastOrDefault(c => string.CompareOrdinal(c.Day, quoteDay) < 0);
            decimal? previous = earlier.Day is not null ? earlier.Close : Num("chartPreviousClose") ?? Num("previousClose");
            string Text(string name) => meta.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";
            var name = Text("longName").Length > 0 ? Text("longName") : Text("shortName");
            return new YahooSnapshot(price.Value, previous, at, Text("currency"), Text("instrumentType"), name.Length > 0 ? name : null, candles);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Parses the chart JSON into daily candles (days without a full set of prices are skipped).</summary>
    public static List<Candle> Parse(string json)
    {
        var result = new List<Candle>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
            var offset = r.GetProperty("meta").TryGetProperty("gmtoffset", out var g) && g.TryGetInt32(out var o) ? o : 0;
            var times = r.GetProperty("timestamp").EnumerateArray().Select(t => t.GetInt64()).ToList();
            var q = r.GetProperty("indicators").GetProperty("quote")[0];
            decimal? At(string name, int i)
            {
                var arr = q.GetProperty(name);
                if (i >= arr.GetArrayLength()) return null;
                var el = arr[i];
                return el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d) && d > 0 ? Math.Round(d, 4) : null;
            }
            for (var i = 0; i < times.Count; i++)
            {
                if (At("open", i) is not { } open || At("high", i) is not { } high || At("low", i) is not { } low || At("close", i) is not { } close) continue;
                var day = DateTimeOffset.FromUnixTimeSeconds(times[i] + offset).UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                result.Add(new Candle(day, open, high, low, close, true));
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
        }
        return result;
    }

    /// <summary>
    /// Fills the history of the given instruments that were not asked for today. At most <paramref name="maxInstruments"/> per run.
    /// Returns how many instruments got data. Stops quietly when Yahoo refuses.
    /// </summary>
    public static async Task<int> BackfillAsync(QuoteStore store, IEnumerable<QuoteInstrument> instruments, int maxInstruments, CancellationToken ct, Action? progress = null)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var done = 0;
        foreach (var inst in instruments.GroupBy(i => i.StoreKey).Select(g => g.First()).Take(400))
        {
            if (ct.IsCancellationRequested || done >= maxInstruments) break;
            var key = inst.StoreKey;
            if (store.BackfillDay(key) == today) continue;
            if (YahooSymbols.For(inst) is not { } symbol) continue;
            try
            {
                using var response = await Http.GetAsync(string.Format(Url, Uri.EscapeDataString(symbol)), ct).ConfigureAwait(false);
                if ((int)response.StatusCode is 429 or 401 or 403)
                {
                    Log.Write($"Yahoo history: {(int)response.StatusCode} - stopped for now (not retried today).");
                    break;
                }
                store.MarkBackfill(key, today); // one try per instrument and day, whatever the answer
                if (!response.IsSuccessStatusCode) continue;
                var candles = Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                if (candles.Count > 0) { store.MergeCandles(key, candles); store.Save(); done++; progress?.Invoke(); }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                if (ct.IsCancellationRequested) break;
                Log.Write($"Yahoo history {symbol}: {ex.GetType().Name}");
                break;
            }
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }
        if (done > 0) store.Save();
        return done;
    }
}
