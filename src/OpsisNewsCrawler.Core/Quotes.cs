using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>Where the price of an instrument comes from.</summary>
public enum QuoteSource
{
    /// <summary>Euronext Athens official 15-minute delayed trade files (key = ISIN).</summary>
    Athens,
    /// <summary>gold-api.com spot prices: XAU, XAG, XPT, XPD, HG (key = symbol). Free, no API key.</summary>
    Metals,
    /// <summary>Brent, WTI, Henry Hub gas (key = FRED series id): near-live Pyth Hermes price first, FRED/EIA daily CSV as the fallback.</summary>
    Energy,
    /// <summary>Shares of the other Euronext markets (Market = MIC, key = ISIN): the Euronext "Download" CSV, end-of-day prices.</summary>
    Euronext,
    /// <summary>gold-api.com crypto spot prices: BTC, ETH (key = symbol). Free, no API key, real time.</summary>
    Crypto,
    /// <summary>Frankfurter (ECB reference rates): currency pairs, key = "EURUSD" (base+quote). Once a working day.</summary>
    Currency,
    /// <summary>Yahoo Finance (unofficial, opt-in): any share, index, currency, future or crypto it offers; key = Yahoo symbol. At most one request per instrument every 15 minutes.</summary>
    Yahoo
}

/// <summary>One line of the watch list.</summary>
public sealed class QuoteInstrument
{
    public QuoteSource Source { get; set; }
    /// <summary>ISIN (Athens), spot symbol (Metals) or FRED series id (Energy).</summary>
    public string Code { get; set; } = "";

    /// <summary>Euronext shares: the market's MIC (XATH, XPAR, XAMS, XBRU, XLIS, XMSM, MTAA, XOSL); empty for metals and energy.</summary>
    public string Market { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string NameEl { get; set; } = "";
    public string NameEn { get; set; } = "";
    /// <summary>Currency symbol shown after the price.</summary>
    public string Unit { get; set; } = "€";
    public bool Enabled { get; set; } = true;

    public QuoteInstrument() { }

    public QuoteInstrument(QuoteSource source, string code, string symbol, string nameEl, string nameEn, string unit, bool enabled = true)
    {
        Source = source; Code = code; Symbol = symbol; NameEl = nameEl; NameEn = nameEn; Unit = unit; Enabled = enabled;
    }

    public string DisplayName(bool greek) => greek ? NameEl : NameEn;

    /// <summary>The key under which the prices of this instrument are stored (Euronext shares of other markets: "MIC:ISIN").</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string StoreKey => Source == QuoteSource.Euronext ? (Market ?? "") + ":" + Code : Code;

    /// <summary>Copies all instrument fields, including the market identifier used by Euronext.</summary>
    public QuoteInstrument Clone() => new(Source, Code, Symbol, NameEl, NameEn, Unit, Enabled) { Market = Market };

    public static List<QuoteInstrument> Defaults() => new()
    {
        // Athens — ISINs checked against the 2026-10-13 trade file and OpenFIGI (see docs/DATA-SOURCES.md).
        new(QuoteSource.Athens, "GRS003003035", "ETE", "Εθνική Τράπεζα", "National Bank", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS829003003", "EUROB", "Eurobank", "Eurobank", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS830003000", "ALPHA", "Alpha Bank", "Alpha Bank", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS831003009", "TPEIR", "Πειραιώς", "Piraeus Bank", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS533003000", "OPTIMA", "Optima Bank", "Optima Bank", "€", enabled: false) { Market = "XATH" },
        new(QuoteSource.Athens, "GRS260333000", "HTO", "ΟΤΕ", "OTE", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS434003000", "PPC", "ΔΕΗ", "PPC", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS298343005", "ELPE", "Helleniq Energy", "Helleniq Energy", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS426003000", "MOH", "Motor Oil", "Motor Oil", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS419003009", "ALWN", "Allwyn (ΟΠΑΠ)", "Allwyn (OPAP)", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS282183003", "BELA", "Jumbo", "Jumbo", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS495003006", "AEGN", "Aegean", "Aegean", "€") { Market = "XATH" },
        new(QuoteSource.Athens, "GRS395363005", "EXAE", "Euronext Athens", "Euronext Athens", "€", enabled: false) { Market = "XATH" },
        // Metals (USD per troy ounce; copper per pound)
        new(QuoteSource.Metals, "XAU", "XAU", "Χρυσός", "Gold", "$/oz"),
        new(QuoteSource.Metals, "XAG", "XAG", "Άργυρος", "Silver", "$/oz"),
        new(QuoteSource.Metals, "XPT", "XPT", "Πλατίνα", "Platinum", "$/oz", enabled: false),
        new(QuoteSource.Metals, "XPD", "XPD", "Παλλάδιο", "Palladium", "$/oz", enabled: false),
        new(QuoteSource.Metals, "HG", "HG", "Χαλκός", "Copper", "$/lb"),
        // Crypto (real time, gold-api.com)
        new(QuoteSource.Crypto, "BTC", "BTC", "Bitcoin", "Bitcoin", "$", enabled: false),
        new(QuoteSource.Crypto, "ETH", "ETH", "Ethereum", "Ethereum", "$", enabled: false),
        // Currencies (ECB reference rates, once a working day)
        new(QuoteSource.Currency, "EURUSD", "EUR/USD", "Ευρώ / Δολάριο ΗΠΑ", "Euro / US dollar", "", enabled: false),
        new(QuoteSource.Currency, "EURGBP", "EUR/GBP", "Ευρώ / Στερλίνα", "Euro / British pound", "", enabled: false),
        new(QuoteSource.Currency, "EURCHF", "EUR/CHF", "Ευρώ / Φράγκο Ελβετίας", "Euro / Swiss franc", "", enabled: false),
        new(QuoteSource.Currency, "USDJPY", "USD/JPY", "Δολάριο / Γιεν", "US dollar / Japanese yen", "", enabled: false),
        new(QuoteSource.Currency, "GBPUSD", "GBP/USD", "Στερλίνα / Δολάριο", "British pound / US dollar", "", enabled: false),
        // Energy (daily EIA data)
        new(QuoteSource.Energy, "DCOILBRENTEU", "BRENT", "Πετρέλαιο Brent", "Brent crude", "$/bbl"),
        new(QuoteSource.Energy, "DCOILWTICO", "WTI", "Πετρέλαιο WTI", "WTI crude", "$/bbl"),
        new(QuoteSource.Energy, "DHHNGSP", "NG", "Φυσικό αέριο (Henry Hub)", "Natural gas (Henry Hub)", "$/MMBtu"),
    };
}

/// <summary>One day of trading: open, high, low and close. <see cref="Exact"/> is false when it was only sampled from the program's own refreshes (or has just a close).</summary>
public readonly record struct Candle(string Day, decimal Open, decimal High, decimal Low, decimal Close, bool Exact);

/// <summary>Remembers the last price of each instrument per day, to know the previous close (the sources give no history).</summary>
public sealed class QuoteStore
{
    private readonly string path;
    private readonly Dictionary<string, SortedDictionary<string, decimal>> days = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();
    private readonly Dictionary<string, SortedDictionary<string, Candle>> candles = new(StringComparer.OrdinalIgnoreCase);
    private readonly string candlePath;
    private readonly string backfillPath;
    private readonly Dictionary<string, string> backfill = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The price cache; the folder can be redirected with the environment variable OPSISNEWSCRAWLER_DATA (a test aid, e.g. for screenshots with made-up data).</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetEnvironmentVariable("OPSISNEWSCRAWLER_DATA") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpsisNewsCrawler"),
        "quotes-cache.json");

    public QuoteStore(string? path = null)
    {
        this.path = path ?? DefaultPath;
        candlePath = Path.Combine(Path.GetDirectoryName(this.path) ?? "", Path.GetFileNameWithoutExtension(this.path) + "-ohlc.json");
        backfillPath = Path.Combine(Path.GetDirectoryName(this.path) ?? "", Path.GetFileNameWithoutExtension(this.path) + "-backfill.json");
        LoadCandles();
        try
        {
            if (!File.Exists(this.path)) return;
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(File.ReadAllText(this.path));
            if (data is null) return;
            foreach (var (code, perDay) in data) days[code] = new SortedDictionary<string, decimal>(perDay, StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    /// <summary>Last price seen on a given day (yyyyMMdd).</summary>
    public void Record(string code, string day, decimal price)
    {
        lock (gate)
        {
            if (!days.TryGetValue(code, out var perDay)) days[code] = perDay = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
            perDay[day] = price;
            while (perDay.Count > 130) perDay.Remove(perDay.Keys.First());
            // The same price also feeds the day's candle: the first price seen is the open, the extremes are the high and low.
            if (!candles.TryGetValue(code, out var cs)) candles[code] = cs = new SortedDictionary<string, Candle>(StringComparer.Ordinal);
            cs[day] = cs.TryGetValue(day, out var c)
                ? c.Exact ? c with { Close = price, High = Math.Max(c.High, price), Low = Math.Min(c.Low, price) }
                          : new Candle(day, c.Open, Math.Max(c.High, price), Math.Min(c.Low, price), price, false)
                : new Candle(day, price, price, price, price, false);
            while (cs.Count > 400) cs.Remove(cs.Keys.First());
        }
    }

    /// <summary>The exact open, high, low and close of a day (from a source that publishes every trade); replaces what was sampled.</summary>
    public void RecordCandle(string code, string day, decimal open, decimal high, decimal low, decimal close)
    {
        lock (gate)
        {
            if (!candles.TryGetValue(code, out var cs)) candles[code] = cs = new SortedDictionary<string, Candle>(StringComparer.Ordinal);
            cs[day] = new Candle(day, open, high, low, close, true);
            while (cs.Count > 400) cs.Remove(cs.Keys.First());
        }
    }

    /// <summary>Daily candles, oldest first. Days that only have a closing price (stored before candles existed) are drawn from the previous close.</summary>
    public IReadOnlyList<Candle> Candles(string code)
    {
        lock (gate)
        {
            var result = new SortedDictionary<string, Candle>(StringComparer.Ordinal);
            if (days.TryGetValue(code, out var closes))
            {
                decimal? prev = null;
                foreach (var (day, close) in closes)
                {
                    var open = prev ?? close;
                    result[day] = new Candle(day, open, Math.Max(open, close), Math.Min(open, close), close, false);
                    prev = close;
                }
            }
            if (candles.TryGetValue(code, out var cs)) foreach (var (day, c) in cs) result[day] = c;
            return result.Values.ToList();
        }
    }

    /// <summary>The day (yyyyMMdd) history was last asked for from an optional history source, or null.</summary>
    public string? BackfillDay(string code) { lock (gate) return backfill.GetValueOrDefault(code); }

    public void MarkBackfill(string code, string day) { lock (gate) backfill[code] = day; }

    /// <summary>Adds days from a history source: exact candles from the trade file are kept, other days are added or replaced; closes are added where missing.</summary>
    public void MergeCandles(string code, IEnumerable<Candle> incoming)
    {
        lock (gate)
        {
            if (!candles.TryGetValue(code, out var cs)) candles[code] = cs = new SortedDictionary<string, Candle>(StringComparer.Ordinal);
            if (!days.TryGetValue(code, out var closes)) days[code] = closes = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var c in incoming)
            {
                if (!(cs.TryGetValue(c.Day, out var have) && have.Exact)) cs[c.Day] = c;
                closes.TryAdd(c.Day, c.Close);
            }
            while (cs.Count > 400) cs.Remove(cs.Keys.First());
            while (closes.Count > 130) closes.Remove(closes.Keys.First());
        }
    }

    private void LoadCandles()
    {
        try
        {
            if (File.Exists(backfillPath))
                foreach (var (k, v) in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(backfillPath)) ?? new()) backfill[k] = v;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        try
        {
            if (!File.Exists(candlePath)) return;
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal[]>>>(File.ReadAllText(candlePath));
            if (data is null) return;
            foreach (var (code, perDay) in data)
            {
                var cs = new SortedDictionary<string, Candle>(StringComparer.Ordinal);
                foreach (var (day, v) in perDay)
                    if (v.Length >= 5) cs[day] = new Candle(day, v[0], v[1], v[2], v[3], v[4] != 0);
                candles[code] = cs;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    /// <summary>The stored closing prices of an instrument, oldest first (up to about 130 days, kept as the program runs).</summary>
    public IReadOnlyList<decimal> History(string code)
    {
        lock (gate) return days.TryGetValue(code, out var perDay) ? perDay.Values.ToList() : new List<decimal>();
    }

    /// <summary>The codes of every instrument that has stored prices.</summary>
    public IReadOnlyList<string> Codes()
    {
        lock (gate) return days.Keys.Concat(candles.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Last price of the latest day before <paramref name="day"/>, or null.</summary>
    public decimal? PreviousClose(string code, string day)
    {
        lock (gate)
        {
            if (!days.TryGetValue(code, out var perDay)) return null;
            var before = perDay.Keys.Where(k => string.CompareOrdinal(k, day) < 0).LastOrDefault();
            return before is null ? null : perDay[before];
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string json;
            string candleJson;
            lock (gate)
            {
                json = JsonSerializer.Serialize(days);
                candleJson = JsonSerializer.Serialize(candles.ToDictionary(kv => kv.Key,
                    kv => kv.Value.ToDictionary(d => d.Key, d => new[] { d.Value.Open, d.Value.High, d.Value.Low, d.Value.Close, d.Value.Exact ? 1m : 0m })));
            }
            SafeFile.WriteAllText(path, json);
            SafeFile.WriteAllText(candlePath, candleJson);
            string backfillJson;
            lock (gate) backfillJson = JsonSerializer.Serialize(backfill);
            SafeFile.WriteAllText(backfillPath, backfillJson);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public interface IQuoteProvider
{
    QuoteSource Source { get; }
    /// <summary>Never throws for network/format problems: returns what it could get (possibly nothing).</summary>
    Task<IReadOnlyList<MarketQuote>> FetchAsync(IReadOnlyList<QuoteInstrument> instruments, CancellationToken ct);
}

/// <summary>Shared HTTP client settings for all quote providers.</summary>
internal static class QuoteHttp
{
    /// <summary>A token that is cancelled by the caller or after <paramref name="seconds"/>: one slow server must not hold everything up.</summary>
    public static CancellationTokenSource Limit(CancellationToken ct, int seconds)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(seconds));
        return cts;
    }

    public static HttpClient Create()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(40), MaxResponseContentBufferSize = 32 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; OpsisNewsCrawler)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return client;
    }
}

/// <summary>
/// Euronext Athens official delayed trade files (15 min, free): the last price of each ISIN is the row with the latest trade time.
/// The files are cumulative for the day and ~7 MB, so a new one is only downloaded when the listing shows a newer file.
/// </summary>
public sealed class AthensDelayedFileProvider : IQuoteProvider
{
    /// <summary>
    /// Every Greek share (ISIN "GRS...", venues XATH and ENAX) with at least two trades in the newest delayed file:
    /// the list of Athens shares the user can pick from. Never throws for network problems (returns what it has).
    /// </summary>
    internal static async Task<HashSet<string>> ReadIsinsAsync(HttpClient http, CancellationToken ct)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var html = await http.GetStringAsync(ListUrl, ct).ConfigureAwait(false);
            var newest = FileRx.Matches(html).Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).LastOrDefault();
            if (newest is null) return result;
            using var response = await http.GetAsync(Base + newest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            await reader.ReadLineAsync(ct).ConfigureAwait(false);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            string? line;
            while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                var f = line.Split(';');
                if (f.Length < 16 || !f[1].StartsWith("GRS", StringComparison.Ordinal) || f[15] is not ("XATH" or "ENAX")) continue;
                counts[f[1]] = counts.GetValueOrDefault(f[1]) + 1;
            }
            foreach (var (isin, n) in counts) if (n >= 2) result.Add(isin);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
        }
        return result;
    }

    private const string Base = "https://athens.euronext.com";
    private const string ListUrl = Base + "/en/market-data/data-services/delayed-feed";
    private static readonly Regex FileRx = new(@"/en/market-data/data-services/trade-document/PO_DAILY/Daily_PostTrade_(\d{8})_(\d{2}\.\d{2}\.\d{2})\.csv", RegexOptions.Compiled);

    private readonly HttpClient http = QuoteHttp.Create();
    private readonly QuoteStore store;
    private string? lastFile;
    private IReadOnlyList<MarketQuote> lastResult = Array.Empty<MarketQuote>();
    private string lastWatchKey = "";
    private DateTimeOffset lastCheck = DateTimeOffset.MinValue;

    public AthensDelayedFileProvider(QuoteStore store)
    {
        this.store = store;
        http.Timeout = TimeSpan.FromSeconds(120); // the day file is ~7 MB; slow links need time
    }

    public QuoteSource Source => QuoteSource.Athens;

    public async Task<IReadOnlyList<MarketQuote>> FetchAsync(IReadOnlyList<QuoteInstrument> instruments, CancellationToken ct)
    {
        if (instruments.Count == 0) return Array.Empty<MarketQuote>();
        // The listing page is ~250 KB and a new file appears every 15 minutes: look at most every 5 minutes.
        var watch = string.Join(",", instruments.Select(i => i.Code));
        if (lastResult.Count > 0 && watch == lastWatchKey && DateTimeOffset.UtcNow - lastCheck < TimeSpan.FromMinutes(5)) return lastResult;
        lastCheck = DateTimeOffset.UtcNow;
        try
        {
            var html = await http.GetStringAsync(ListUrl, ct).ConfigureAwait(false);
            var newest = FileRx.Matches(html).Select(m => (Url: m.Value, Day: m.Groups[1].Value, Time: m.Groups[2].Value))
                .OrderBy(f => f.Day + f.Time, StringComparer.Ordinal).LastOrDefault();
            if (newest.Url is null)
            {
                Log.Write("Athens prices: no trade file link found on the page");
                return Array.Empty<MarketQuote>();
            }

            var watchKey = string.Join(",", instruments.Select(i => i.Code));
            if (newest.Url == lastFile && watchKey == lastWatchKey) return lastResult;

            // Settings from older versions may contain duplicate ISINs. Never let one duplicate
            // abort the entire Athens feed; keep the first matching instrument.
            var wanted = new Dictionary<string, QuoteInstrument>(StringComparer.Ordinal);
            foreach (var instrument in instruments) wanted.TryAdd(instrument.Code, instrument);
            var last = new Dictionary<string, (string At, decimal Price)>(StringComparer.Ordinal);
            var first = new Dictionary<string, (string At, decimal Price)>(StringComparer.Ordinal);
            var high = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var low = new Dictionary<string, decimal>(StringComparer.Ordinal);

            using var response = await http.GetAsync(Base + newest.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            await reader.ReadLineAsync(ct).ConfigureAwait(false); // header: trading_date_time;isin_code;price;...;canc;amnd;...
            string? line;
            while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                var f = line.Split(';');
                if (f.Length < 15 || !wanted.ContainsKey(f[1])) continue;
                if (f[13].Length > 0) continue; // cancelled trade
                if (!decimal.TryParse(f[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0) continue;
                var at = f[0];
                if (!last.TryGetValue(f[1], out var l) || string.CompareOrdinal(at, l.At) > 0) last[f[1]] = (at, price);
                if (!first.TryGetValue(f[1], out var fi) || string.CompareOrdinal(at, fi.At) < 0) first[f[1]] = (at, price);
                if (!high.TryGetValue(f[1], out var hi) || price > hi) high[f[1]] = price;
                if (!low.TryGetValue(f[1], out var lo) || price < lo) low[f[1]] = price;
            }

            var results = new List<MarketQuote>();
            foreach (var inst in instruments)
            {
                if (!last.TryGetValue(inst.Code, out var l)) continue;
                store.Record(inst.Code, newest.Day, l.Price);
                if (first.TryGetValue(inst.Code, out var firstTrade)) // the file holds every trade of the day: an exact candle
                    store.RecordCandle(inst.Code, newest.Day, firstTrade.Price, high[inst.Code], low[inst.Code], l.Price);
                var prev = store.PreviousClose(inst.Code, newest.Day);
                var fromOpen = false;
                if (prev is null && first.TryGetValue(inst.Code, out var fi) && fi.Price > 0)
                {
                    prev = fi.Price;
                    fromOpen = true;
                }
                decimal? change = prev is > 0 ? Math.Round((l.Price - prev.Value) / prev.Value * 100, 2) : null;
                var asOf = DateTimeOffset.TryParse(l.At, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : DateTimeOffset.UtcNow;
                results.Add(new MarketQuote(inst.Symbol, inst.NameEn, l.Price, change, asOf, Delayed: true, ChangeFromOpen: fromOpen));
            }
            store.Save();
            lastFile = newest.Url;
            lastWatchKey = watchKey;
            lastResult = results;
            return results;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            Log.Write($"Athens prices: {ex.GetType().Name}: {ex.Message}");
            return Array.Empty<MarketQuote>(); // the service shows its stored copy, labelled
        }
    }
}

/// <summary>gold-api.com: free spot prices for metals, no API key. Prices are real time (not delayed).</summary>
public sealed class GoldApiMetalsProvider : IQuoteProvider
{
    private readonly HttpClient http = QuoteHttp.Create();
    private readonly QuoteStore store;
    private IReadOnlyList<MarketQuote> lastResult = Array.Empty<MarketQuote>();

    private readonly QuoteSource source;

    public GoldApiMetalsProvider(QuoteStore store, QuoteSource source = QuoteSource.Metals)
    {
        this.store = store;
        this.source = source;
    }

    public QuoteSource Source => source;

    public async Task<IReadOnlyList<MarketQuote>> FetchAsync(IReadOnlyList<QuoteInstrument> instruments, CancellationToken ct)
    {
        if (instruments.Count == 0) return Array.Empty<MarketQuote>();
        var tasks = instruments.Select(i => FetchOne(i, ct)).ToList();
        var done = await Task.WhenAll(tasks).ConfigureAwait(false);
        var results = done.Where(q => q is not null).Select(q => q!).ToList();
        if (results.Count == 0) return Array.Empty<MarketQuote>(); // the service shows its stored copy, labelled
        store.Save();
        lastResult = results;
        return results;
    }

    private async Task<MarketQuote?> FetchOne(QuoteInstrument inst, CancellationToken ct)
    {
        try
        {
            using var limit = QuoteHttp.Limit(ct, 15);
            using var doc = JsonDocument.Parse(await http.GetStringAsync($"https://api.gold-api.com/price/{Uri.EscapeDataString(inst.Code)}", limit.Token).ConfigureAwait(false));
            if (!doc.RootElement.TryGetProperty("price", out var p) || !p.TryGetDecimal(out var price) || price <= 0) return null;
            var asOf = doc.RootElement.TryGetProperty("updatedAt", out var u) && DateTimeOffset.TryParse(u.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t)
                ? t : DateTimeOffset.UtcNow;
            var day = asOf.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            store.Record(inst.Code, day, price);
            var prev = store.PreviousClose(inst.Code, day);
            decimal? change = prev is > 0 ? Math.Round((price - prev.Value) / prev.Value * 100, 2) : null;
            return new MarketQuote(inst.Symbol, inst.NameEn, price, change, asOf, Delayed: false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
        {
            if (!ct.IsCancellationRequested) Log.Write($"gold-api {inst.Code}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Oil and gas: first the near-live Pyth Hermes price (Brent, WTI, natural gas), and the FRED public CSV (U.S. EIA daily data, a few
/// days behind, no API key) for the instruments Pyth cannot give a recent price for. When both exist the newer one is shown.
/// </summary>
public sealed class FredEnergyProvider : IQuoteProvider
{
    /// <summary>A Pyth price older than this is shown with its date instead of as a live price.</summary>
    private static readonly TimeSpan LiveAge = TimeSpan.FromMinutes(30);
    /// <summary>A Pyth price older than this (closed market, stopped feed) is not used at all.</summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(4);

    private readonly HttpClient http = QuoteHttp.Create();
    private readonly QuoteStore? store;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (MarketQuote Quote, DateTimeOffset At)> fredCache = new(StringComparer.Ordinal);

    public FredEnergyProvider(QuoteStore? store = null) => this.store = store;

    public QuoteSource Source => QuoteSource.Energy;

    public async Task<IReadOnlyList<MarketQuote>> FetchAsync(IReadOnlyList<QuoteInstrument> instruments, CancellationToken ct)
    {
        if (instruments.Count == 0) return Array.Empty<MarketQuote>();

        var live = await FetchPythAsync(instruments, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        // Only when the user switched Yahoo Finance on: the futures prices (about 15 minutes delayed) replace the days-old daily values.
        foreach (var (code, quote) in await FetchYahooAsync(instruments, ct).ConfigureAwait(false))
            if (!live.TryGetValue(code, out var existing) || quote.AsOf > existing.AsOf) live[code] = quote;
        ct.ThrowIfCancellationRequested();
        // FRED is also asked when a live price has no day change yet: the last official daily value is then the reference.
        var needFred = instruments.Where(i => !live.TryGetValue(i.Code, out var q) || q.Delayed || q.ChangePercent is null).ToList();
        var daily = needFred.Count == 0 ? new Dictionary<string, MarketQuote>(StringComparer.Ordinal) : await FetchFredAsync(needFred, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var results = new List<MarketQuote>();
        foreach (var inst in instruments)
        {
            live.TryGetValue(inst.Code, out var p);
            daily.TryGetValue(inst.Code, out var f);
            var best = p is null ? f : f is null ? p : p.AsOf >= f.AsOf ? p : f;
            if (ReferenceEquals(best, p) && p is not null && p.ChangePercent is null && f is not null && f.Price > 0 && f.AsOf.UtcDateTime.Date < p.AsOf.UtcDateTime.Date)
                best = p with { ChangePercent = Math.Round((p.Price - f.Price) / f.Price * 100, 2) };
            if (best is not null && results.All(r => r.Symbol != best.Symbol)) results.Add(best);
        }
        return results;
    }

    /// <summary>The Yahoo Finance futures that stand for the three FRED series (Brent, WTI, Henry Hub gas).</summary>
    internal static readonly IReadOnlyDictionary<string, string> YahooSymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["DCOILBRENTEU"] = "BZ=F", ["DCOILWTICO"] = "CL=F", ["DHHNGSP"] = "NG=F"
    };

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (MarketQuote Quote, DateTimeOffset At)> yahooCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Opt-in and unofficial (see docs/DATA-SOURCES.md): one request per instrument, at most every 15 minutes, never in a burst. Empty when Yahoo is off or blocked.</summary>
    private async Task<Dictionary<string, MarketQuote>> FetchYahooAsync(IReadOnlyList<QuoteInstrument> instruments, CancellationToken ct)
    {
        var result = new Dictionary<string, MarketQuote>(StringComparer.Ordinal);
        if (!YahooAccess.Enabled || YahooAccess.IsBlocked) return result;
        var first = true;
        foreach (var inst in instruments.Where(i => YahooSymbols.ContainsKey(i.Code)))
        {
            if (yahooCache.TryGetValue(inst.Code, out var cached) && DateTimeOffset.UtcNow - cached.At < YahooAccess.MinInterval) { result[inst.Code] = cached.Quote; continue; }
            if (YahooAccess.IsBlocked || ct.IsCancellationRequested) break;
            if (!first) await Task.Delay(1100, ct).ConfigureAwait(false);
            first = false;
            try
            {
                using var response = await YahooHistory.Http.GetAsync(string.Format(YahooHistory.ChartUrl, Uri.EscapeDataString(YahooSymbols[inst.Code])), ct).ConfigureAwait(false);
                if ((int)response.StatusCode is 401 or 403 or 429) { YahooAccess.Block(((int)response.StatusCode).ToString()); break; }
                if (!response.IsSuccessStatusCode) continue;
                var snap = YahooHistory.ParseSnapshot(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                if (snap is null || snap.Price <= 0) continue;
                decimal? change = snap.PreviousClose is > 0 ? Math.Round((snap.Price - snap.PreviousClose.Value) / snap.PreviousClose.Value * 100, 2) : null;
                var quote = new MarketQuote(inst.Symbol, inst.NameEn, snap.Price, change, snap.At, Delayed: true);
                yahooCache[inst.Code] = (quote, DateTimeOffset.UtcNow);
                result[inst.Code] = quote;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                if (ct.IsCancellationRequested) throw;
                Log.Write($"Yahoo energy {inst.Code}: {ex.GetType().Name}: {ex.Message}");
            }
        }
        return result;
    }

    /// <summary>Pyth Hermes: one request for all instruments that have a feed. Keyed by instrument code; empty when it fails.</summary>
    // Since October 2026 the public Hermes price endpoints answer 401 (an API key is needed). Then it is not asked again for 12 hours
    // (one log line instead of one per minute) and the daily FRED/EIA series is used.
    private DateTimeOffset pythBlockedUntil = DateTimeOffset.MinValue;

    private async Task<Dictionary<string, MarketQuote>> FetchPythAsync(IReadOnlyList<QuoteInstrument> instruments, CancellationToken ct)
    {
        var result = new Dictionary<string, MarketQuote>(StringComparer.Ordinal);
        var mapped = instruments.Where(i => PythHermes.FeedIds.ContainsKey(i.Code)).ToList();
        if (mapped.Count == 0 || DateTimeOffset.UtcNow < pythBlockedUntil) return result;
        try
        {
            using var limit = QuoteHttp.Limit(ct, 15);
            var url = PythHermes.BuildUrl(mapped.Select(i => PythHermes.FeedIds[i.Code]).Distinct());
            var prices = PythHermes.Parse(await http.GetStringAsync(url, limit.Token).ConfigureAwait(false));
            foreach (var inst in mapped)
            {
                var id = PythHermes.FeedIds[inst.Code];
                var price = prices.FirstOrDefault(p => p.Id == id);
                if (price is null) continue;
                var age = DateTimeOffset.UtcNow - price.PublishedAt;
                if (age > MaxAge) continue;
                var day = price.PublishedAt.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                decimal? change = null;
                if (store is not null)
                {
                    store.Record(inst.Code, day, price.Value);
                    var prev = store.PreviousClose(inst.Code, day);
                    if (prev is > 0) change = Math.Round((price.Value - prev.Value) / prev.Value * 100, 2);
                }
                result[inst.Code] = new MarketQuote(inst.Symbol, inst.NameEn, price.Value, change, price.PublishedAt, Delayed: age > LiveAge);
            }
            store?.Save();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            pythBlockedUntil = DateTimeOffset.UtcNow.AddHours(12);
            Log.Write("Pyth Hermes now needs an API key (HTTP " + (int)ex.StatusCode! + "): oil and gas use the daily FRED/EIA prices; not asked again for 12 hours.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            if (!ct.IsCancellationRequested) Log.Write($"Pyth Hermes: {ex.GetType().Name}: {ex.Message}");
        }
        return result;
    }

    /// <summary>FRED daily series. The series change once a day, so each one is asked for at most hourly.</summary>
    private async Task<Dictionary<string, MarketQuote>> FetchFredAsync(IReadOnlyList<QuoteInstrument> wanted, CancellationToken ct)
    {
        var result = new Dictionary<string, MarketQuote>(StringComparer.Ordinal);
        var todo = new List<QuoteInstrument>();
        foreach (var inst in wanted)
        {
            if (fredCache.TryGetValue(inst.Code, out var cached) && DateTimeOffset.UtcNow - cached.At < TimeSpan.FromHours(1)) result[inst.Code] = cached.Quote;
            else todo.Add(inst);
        }
        if (todo.Count == 0) return result;

        var since = DateTime.UtcNow.AddDays(-20).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var fetched = await Task.WhenAll(todo.Select(inst => FetchOne(inst, since, ct))).ConfigureAwait(false);
        for (var k = 0; k < todo.Count; k++)
        {
            var code = todo[k].Code;
            if (fetched[k] is { } quote)
            {
                fredCache[code] = (quote, DateTimeOffset.UtcNow);
                result[code] = quote;
            }
            else if (fredCache.TryGetValue(code, out var old)) result[code] = old.Quote; // keep the last known value when the request failed
        }
        return result;
    }

    private async Task<MarketQuote?> FetchOne(QuoteInstrument inst, string since, CancellationToken ct)
    {
        try
        {
            using var limit = QuoteHttp.Limit(ct, 15);
            var csv = await http.GetStringAsync($"https://fred.stlouisfed.org/graph/fredgraph.csv?id={Uri.EscapeDataString(inst.Code)}&cosd={since}", limit.Token).ConfigureAwait(false);
            var points = new List<(DateTime Day, decimal Value)>();
            foreach (var line in csv.Split('\n').Skip(1))
            {
                var f = line.Trim().Split(',');
                if (f.Length < 2 || !DateTime.TryParse(f[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) continue;
                if (decimal.TryParse(f[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) points.Add((day, v)); // holidays are "."
            }
            if (points.Count == 0)
            {
                // FRED answered with something that is not the CSV (a bot-check page): use the EIA history pages instead.
                if (!fredWarned) { fredWarned = true; Log.Write("FRED did not return a CSV (bot check?): using the EIA daily prices for oil and gas."); }
                return await FetchEia(inst, ct).ConfigureAwait(false);
            }
            var last = points[^1];
            decimal? change = points.Count > 1 && points[^2].Value > 0 ? Math.Round((last.Value - points[^2].Value) / points[^2].Value * 100, 2) : null;
            return new MarketQuote(inst.Symbol, inst.NameEn, last.Value, change, new DateTimeOffset(last.Day, TimeSpan.Zero), Delayed: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (!ct.IsCancellationRequested) Log.Write($"FRED {inst.Code}: {ex.GetType().Name}: {ex.Message}");
            return ct.IsCancellationRequested ? null : await FetchEia(inst, ct).ConfigureAwait(false);
        }
    }

    private bool fredWarned;

    private async Task<MarketQuote?> FetchEia(QuoteInstrument inst, CancellationToken ct)
    {
        if (!EiaDaily.Pages.TryGetValue(inst.Code, out var url)) return null;
        try
        {
            using var limit = QuoteHttp.Limit(ct, 20);
            return EiaDaily.Latest(inst, await http.GetStringAsync(url, limit.Token).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (!ct.IsCancellationRequested) Log.Write($"EIA {inst.Code}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// Frankfurter (api.frankfurter.dev, open source, free, no key): the ECB euro reference rates, published once per working day
/// (about 16:00 Central European Time). Any pair is computed from the euro rates; the change is against the previous day.
/// </summary>
public sealed class FrankfurterFxProvider : IQuoteProvider
{
    private readonly HttpClient http = QuoteHttp.Create();
    private IReadOnlyList<MarketQuote> lastResult = Array.Empty<MarketQuote>();
    private string lastKey = "";
    private DateTimeOffset lastFetch = DateTimeOffset.MinValue;

    public QuoteSource Source => QuoteSource.Currency;

    public async Task<IReadOnlyList<MarketQuote>> FetchAsync(IReadOnlyList<QuoteInstrument> instruments, CancellationToken ct)
    {
        var pairs = instruments.Where(i => i.Code.Length == 6).ToList();
        if (pairs.Count == 0) return Array.Empty<MarketQuote>();
        var key = string.Join(",", pairs.Select(p => p.Code));
        if (key == lastKey && lastResult.Count > 0 && DateTimeOffset.UtcNow - lastFetch < TimeSpan.FromMinutes(30)) return lastResult;

        var codes = pairs.SelectMany(p => new[] { p.Code[..3], p.Code[3..] }).Where(c => c != "EUR").Distinct().ToList();
        try
        {
            using var limit = QuoteHttp.Limit(ct, 15);
            var from = DateTime.UtcNow.AddDays(-8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var json = await http.GetStringAsync($"https://api.frankfurter.dev/v1/{from}..?base=EUR&symbols={string.Join(",", codes)}", limit.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var days = doc.RootElement.GetProperty("rates").EnumerateObject()
                .Select(d => (Day: DateTime.Parse(d.Name, CultureInfo.InvariantCulture), Rates: d.Value.EnumerateObject().ToDictionary(r => r.Name, r => r.Value.GetDecimal())))
                .OrderBy(d => d.Day).ToList();
            if (days.Count == 0) return lastResult;

            static decimal? Rate(Dictionary<string, decimal> r, string code) => code == "EUR" ? 1m : r.TryGetValue(code, out var v) ? v : null;
            var results = new List<MarketQuote>();
            foreach (var p in pairs)
            {
                var (b, q) = (p.Code[..3], p.Code[3..]);
                decimal? Pair((DateTime Day, Dictionary<string, decimal> Rates) d) =>
                    Rate(d.Rates, b) is > 0 and var rb && Rate(d.Rates, q) is { } rq ? rq / rb : null;
                var now = Pair(days[^1]);
                if (now is null) continue;
                var before = days.Count > 1 ? Pair(days[^2]) : null;
                decimal? change = before is > 0 ? Math.Round((now.Value - before.Value) / before.Value * 100, 2) : null;
                results.Add(new MarketQuote(p.Symbol, p.NameEn, now.Value, change, new DateTimeOffset(days[^1].Day, TimeSpan.Zero), Delayed: true));
            }
            lastResult = results;
            lastKey = key;
            lastFetch = DateTimeOffset.UtcNow;
            return results;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            if (!ct.IsCancellationRequested) Log.Write($"Currency rates: {ex.GetType().Name}: {ex.Message}");
            return Array.Empty<MarketQuote>();
        }
    }
}

/// <summary>What the last run of one price source gave (shown in the settings window).</summary>
public sealed record SourceStatus(QuoteSource Source, DateTimeOffset CheckedAt, int Count, bool Failed, DateTimeOffset? Newest);

/// <summary>Runs all providers for the enabled instruments and turns the prices into strip items.</summary>
public sealed class QuoteService
{
    private readonly Dictionary<QuoteSource, Cached<List<MarketQuote>>> lastGood = new();

    /// <summary>Sources that gave nothing in the last run and are shown from the stored copy: source → time of the stored data.</summary>
    public IReadOnlyDictionary<QuoteSource, DateTimeOffset> StaleSources { get; private set; } = new Dictionary<QuoteSource, DateTimeOffset>();

    private void LoadLastGood()
    {
        foreach (var source in Enum.GetValues<QuoteSource>())
            if (DataCache.Load<List<MarketQuote>>("quotes-" + source) is { Data.Count: > 0 } saved) lastGood[source] = saved;
    }

    private static readonly object StatusLock = new();
    private static readonly Dictionary<QuoteSource, SourceStatus> StatusBySource = new();

    /// <summary>The result of the last run of every source that was wanted at least once, in source order.</summary>
    public static IReadOnlyList<SourceStatus> Statuses()
    {
        lock (StatusLock) return StatusBySource.Values.OrderBy(s => s.Source).ToList();
    }

    private static void RecordStatus(QuoteSource source, IReadOnlyList<MarketQuote> quotes, bool failed)
    {
        var status = new SourceStatus(source, DateTimeOffset.Now, quotes.Count, failed, quotes.Count > 0 ? quotes.Max(q => q.AsOf) : null);
        lock (StatusLock) StatusBySource[source] = status;
    }

    private readonly List<IQuoteProvider> providers;

    /// <summary>Closing prices stored by the providers (for indicators).</summary>
    public QuoteStore Store { get; }

    public QuoteService(QuoteStore? store = null)
    {
        store ??= new QuoteStore();
        Store = store;
        LoadLastGood();
        providers = new()
        {
            new AthensDelayedFileProvider(store), new EuronextListProvider(store), new GoldApiMetalsProvider(store),
            new GoldApiMetalsProvider(store, QuoteSource.Crypto), new FrankfurterFxProvider(), new FredEnergyProvider(store),
            new YahooQuoteProvider(store)
        };
    }

    /// <summary>Every price of the last run, also of instruments that are only in the portfolio (key = <see cref="PortfolioPosition.Key"/> layout).</summary>
    public IReadOnlyDictionary<string, MarketQuote> LatestByKey { get; private set; } = new Dictionary<string, MarketQuote>();

    private static string KeyOf(QuoteInstrument i) => $"{i.Source}|{(i.Market ?? "").ToUpperInvariant()}|{i.Code.ToUpperInvariant()}";

    /// <summary>The prices of the last run with their instruments (for price alerts).</summary>
    public IReadOnlyList<(QuoteInstrument Instrument, MarketQuote Quote)> LastQuotes { get; private set; } = Array.Empty<(QuoteInstrument, MarketQuote)>();

    /// <summary>Sources that gave no price in the last run although prices were wanted (for the message on the bar and the log).</summary>
    public IReadOnlyList<QuoteSource> LastFailed { get; private set; } = Array.Empty<QuoteSource>();

    /// <summary>
    /// Fetches all wanted prices. Every source runs on its own: a slow or broken one never holds the others back, and
    /// <paramref name="progress"/> is called with the prices known so far each time a source has answered.
    /// </summary>
    public async Task<IReadOnlyList<NewsItem>> FetchItemsAsync(IEnumerable<QuoteInstrument> watchlist, bool greek, CancellationToken ct,
        Action<IReadOnlyList<NewsItem>>? progress = null, IEnumerable<QuoteInstrument>? alsoNeeded = null)
    {
        var enabled = watchlist.Where(i => i.Enabled).ToList();
        // What is fetched: what the bars show plus what only the portfolio needs (one download serves both, the providers cache by this list).
        var fetchList = enabled.Concat(alsoNeeded ?? Enumerable.Empty<QuoteInstrument>()).GroupBy(KeyOf).Select(g => g.First())
            .Where(i => i.Source != QuoteSource.Yahoo || YahooAccess.Enabled).ToList();   // Yahoo only when the user switched it on
        var answers = new Dictionary<QuoteSource, IReadOnlyList<MarketQuote>>();
        var failed = new List<QuoteSource>();
        var stale = new Dictionary<QuoteSource, DateTimeOffset>();

        async Task<(QuoteSource Source, IReadOnlyList<MarketQuote> Quotes, bool Failed)> Run(IQuoteProvider p)
        {
            var wanted = fetchList.Where(i => i.Source == p.Source).ToList();
            if (wanted.Count == 0) return (p.Source, Array.Empty<MarketQuote>(), false);
            try
            {
                var quotes = await p.FetchAsync(wanted, ct).ConfigureAwait(false);
                if (quotes.Count == 0) Log.Write($"{p.Source}: no prices received ({wanted.Count} wanted)");
                return (p.Source, quotes, quotes.Count == 0);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Whatever went wrong inside one source (parsing, network, certificates...) stays inside that source.
                Log.Write($"{p.Source}: {ex.GetType().Name}: {ex.Message}");
                return (p.Source, Array.Empty<MarketQuote>(), true);
            }
        }

        List<NewsItem> Build()
        {
            var items = new List<NewsItem>();
            foreach (var inst in enabled) // keep the user's order
            {
                if (!answers.TryGetValue(inst.Source, out var quotes)) continue;
                var q = quotes.FirstOrDefault(x => string.Equals(x.Symbol, inst.Symbol, StringComparison.OrdinalIgnoreCase));
                if (q is not null) items.Add(ToItem(inst, q, greek));
            }
            return items;
        }

        var pending = providers.Select(Run).ToList();
        while (pending.Count > 0)
        {
            var finished = await Task.WhenAny(pending).ConfigureAwait(false);
            pending.Remove(finished);
            var (source, quotes, didFail) = await finished.ConfigureAwait(false);
            answers[source] = quotes;
            if (enabled.Any(i => i.Source == source))
            {
                RecordStatus(source, quotes, didFail);
                if (quotes.Count > 0)
                {
                    var copy = new Cached<List<MarketQuote>>(DateTimeOffset.Now, quotes.ToList());
                    lastGood[source] = copy;
                    DataCache.Save("quotes-" + source, copy.Data);
                }
                else if (didFail && lastGood.TryGetValue(source, out var old))
                {
                    // No answer: show the last prices we had, marked as not current, and say so on the bar.
                    answers[source] = old.Data.Select(x => x with { Delayed = true }).ToList();
                    stale[source] = old.At;
                    progress?.Invoke(Build());
                }
            }
            if (didFail) failed.Add(source);
            if (quotes.Count > 0) progress?.Invoke(Build());
        }
        LastFailed = failed;
        StaleSources = stale;
        var raw = new List<(QuoteInstrument, MarketQuote)>();
        foreach (var inst in enabled)
        {
            if (stale.ContainsKey(inst.Source) || !answers.TryGetValue(inst.Source, out var list)) continue; // stored prices never raise price alerts
            var found = list.FirstOrDefault(x => string.Equals(x.Symbol, inst.Symbol, StringComparison.OrdinalIgnoreCase));
            if (found is not null) raw.Add((inst, found));
        }
        LastQuotes = raw;
        var all = new Dictionary<string, MarketQuote>();
        foreach (var inst in fetchList)
        {
            if (stale.ContainsKey(inst.Source) || !answers.TryGetValue(inst.Source, out var list2)) continue;
            var found2 = list2.FirstOrDefault(x => string.Equals(x.Symbol, inst.Symbol, StringComparison.OrdinalIgnoreCase));
            if (found2 is not null) all[KeyOf(inst)] = found2;
        }
        LatestByKey = all;
        return Build();
    }

    /// <summary>Short provider name for messages ("The connection to ... is not working").</summary>
    public static string ProviderName(QuoteSource source) => source switch
    {
        QuoteSource.Athens => "Euronext Athens",
        QuoteSource.Euronext => "Euronext",
        QuoteSource.Metals or QuoteSource.Crypto => "gold-api.com",
        QuoteSource.Currency => "ECB / Frankfurter",
        QuoteSource.Energy => "EIA / FRED",
        QuoteSource.Yahoo => "Yahoo Finance",
        _ => source.ToString()
    };

    private static readonly CultureInfo Gr = CultureInfo.GetCultureInfo("el-GR");

    public static NewsItem ToItem(QuoteInstrument inst, MarketQuote q, bool greek)
    {
        var price = q.Price.ToString(inst.Source == QuoteSource.Currency && q.Price < 100 ? "0.0000" : q.Price >= 100 ? "#,##0.00" : q.Price >= 1 ? "0.00" : "0.000", Gr);
        var text = $"{price} {inst.Unit}";
        var trend = 0;
        if (q.ChangePercent is { } c)
        {
            trend = c > 0 ? 1 : c < 0 ? -1 : 0;
            var arrow = trend > 0 ? "▲" : trend < 0 ? "▼" : "■";
            text += $"  {arrow} {(c > 0 ? "+" : "")}{c.ToString("0.00", Gr)} %";
            if (q.ChangeFromOpen) text += " " + Loc.T("q.sinceOpen");
        }
        // The 15-minute delay of the free Athens data is explained in the settings (Information), not on every price of the bar.
        if (inst.Source == QuoteSource.Yahoo)
            text += "  · " + q.AsOf.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        else if (q.Delayed && inst.Source != QuoteSource.Athens)
        {
            text += "  · " + (inst.Source == QuoteSource.Euronext
                ? Loc.T("q.close", q.AsOf.ToString("dd/MM"))
                : Loc.T("q.daily", q.AsOf.ToString("dd/MM")));
        }
        var title = inst.Symbol.Length > 0 && inst.Source is QuoteSource.Athens or QuoteSource.Euronext or QuoteSource.Currency or QuoteSource.Yahoo ? inst.Symbol : inst.DisplayName(greek);
        return new NewsItem(text, "", title, q.AsOf, Summary: inst.DisplayName(greek), Kind: NewsItemKind.Quote, Trend: trend);
    }
}
