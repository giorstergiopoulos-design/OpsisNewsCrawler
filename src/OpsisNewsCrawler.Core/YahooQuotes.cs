using System.Globalization;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// The switch and the safety limits of everything that talks to Yahoo Finance (unofficial: its terms do not allow automated use, so the
/// user has to switch it on). The limits keep the traffic as small and as ordinary as possible: at most <see cref="MaxInstruments"/>
/// instruments, one request per instrument at most every <see cref="MinInterval"/>, requests one after the other with a pause between them,
/// and a stop of <see cref="BlockFor"/> at the first 401/403/429 answer. Nothing is done to get around a refusal.
/// </summary>
public static class YahooAccess
{
    public const int MaxInstruments = 30;
    public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan BlockFor = TimeSpan.FromHours(6);

    /// <summary>Set from the settings (<see cref="AppConfig.HistoryFromYahoo"/>). Off: no request to Yahoo is made.</summary>
    public static volatile bool Enabled;

    private static long blockedUntilTicks;

    public static bool IsBlocked => DateTimeOffset.UtcNow.UtcTicks < Interlocked.Read(ref blockedUntilTicks);

    public static void Block(string why)
    {
        Interlocked.Exchange(ref blockedUntilTicks, (DateTimeOffset.UtcNow + BlockFor).UtcTicks);
        Log.Write($"Yahoo Finance answered {why}: not asked again for {BlockFor.TotalHours:0} hours.");
    }
}

public sealed record YahooHit(string Symbol, string Name, string Type, string Exchange);

/// <summary>The search box of the catalogue: one request, only when the user presses the button.</summary>
public static class YahooSearch
{
    public static async Task<IReadOnlyList<YahooHit>> SearchAsync(string query, CancellationToken ct)
    {
        query = query.Trim();
        if (query.Length < 2 || !YahooAccess.Enabled || YahooAccess.IsBlocked) return Array.Empty<YahooHit>();
        var url = "https://query1.finance.yahoo.com/v1/finance/search?quotesCount=20&newsCount=0&listsCount=0&q=" + Uri.EscapeDataString(query);
        using var response = await YahooHistory.Http.GetAsync(url, ct).ConfigureAwait(false);
        if ((int)response.StatusCode is 401 or 403 or 429)
        {
            YahooAccess.Block(((int)response.StatusCode).ToString());
            throw new HttpRequestException(((int)response.StatusCode).ToString());
        }
        response.EnsureSuccessStatusCode();
        return ParseHits(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
    }

    /// <summary>The usable results (shares, indices, currencies, futures, ETFs, funds, crypto) of a search answer.</summary>
    public static List<YahooHit> ParseHits(string json)
    {
        var hits = new List<YahooHit>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var q in doc.RootElement.GetProperty("quotes").EnumerateArray())
            {
                var symbol = q.TryGetProperty("symbol", out var s) ? s.GetString() : null;
                var type = q.TryGetProperty("quoteType", out var t) ? t.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(symbol) || type is not ("EQUITY" or "INDEX" or "CURRENCY" or "FUTURE" or "ETF" or "MUTUALFUND" or "CRYPTOCURRENCY")) continue;
                string Pick(string a, string b) => q.TryGetProperty(a, out var x) && x.ValueKind == JsonValueKind.String && x.GetString()!.Trim().Length > 0 ? x.GetString()!.Trim()
                    : q.TryGetProperty(b, out var y) && y.ValueKind == JsonValueKind.String ? y.GetString()!.Trim() : "";
                var name = Pick("longname", "shortname");
                var exchange = Pick("exchDisp", "exchange");
                hits.Add(new YahooHit(symbol!, name.Length > 0 ? name : symbol!, type, exchange));
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
        }
        return hits;
    }
}

/// <summary>Ready-made groups of Yahoo instruments (symbols checked on 2026-10-21).</summary>
public static class YahooPresets
{
    public sealed record Item(string Symbol, string NameEl, string NameEn);
    public sealed record Group(string Key, IReadOnlyList<Item> Items);

    public static readonly IReadOnlyList<Group> All = new[]
    {
        new Group("indices", new Item[]
        {
            new("GD.AT", "Γενικός Δείκτης Χρηματιστηρίου Αθηνών", "Athens General Composite Index"), new("^GSPC", "S&P 500", "S&P 500"), new("^DJI", "Dow Jones", "Dow Jones"),
            new("^IXIC", "Nasdaq Composite", "Nasdaq Composite"), new("^STOXX50E", "Euro Stoxx 50", "Euro Stoxx 50"), new("^GDAXI", "DAX (Γερμανία)", "DAX (Germany)"),
            new("^FCHI", "CAC 40 (Γαλλία)", "CAC 40 (France)"), new("^FTSE", "FTSE 100 (Ην. Βασίλειο)", "FTSE 100 (UK)"), new("^N225", "Nikkei 225 (Ιαπωνία)", "Nikkei 225 (Japan)"),
        }),
        new Group("commodities", new Item[]
        {
            new("GC=F", "Χρυσός (συμβόλαια)", "Gold (futures)"), new("SI=F", "Άργυρος (συμβόλαια)", "Silver (futures)"), new("CL=F", "Πετρέλαιο WTI (συμβόλαια)", "WTI crude (futures)"),
            new("BZ=F", "Πετρέλαιο Brent (συμβόλαια)", "Brent crude (futures)"), new("NG=F", "Φυσικό αέριο (συμβόλαια)", "Natural gas (futures)"), new("HG=F", "Χαλκός (συμβόλαια)", "Copper (futures)"),
        }),
        new Group("currencies", new Item[]
        {
            new("EURUSD=X", "Ευρώ / Δολάριο ΗΠΑ", "Euro / US dollar"), new("EURGBP=X", "Ευρώ / Στερλίνα", "Euro / British pound"), new("EURJPY=X", "Ευρώ / Γιεν", "Euro / Japanese yen"),
            new("EURCHF=X", "Ευρώ / Φράγκο Ελβετίας", "Euro / Swiss franc"), new("USDJPY=X", "Δολάριο / Γιεν", "US dollar / Japanese yen"), new("GBPUSD=X", "Στερλίνα / Δολάριο", "British pound / US dollar"),
        }),
        new Group("crypto", new Item[]
        {
            new("BTC-USD", "Bitcoin", "Bitcoin"), new("ETH-USD", "Ethereum", "Ethereum"),
        }),
    };
}

/// <summary>The prices of the instruments the user added from Yahoo Finance (source <see cref="QuoteSource.Yahoo"/>, code = Yahoo symbol).</summary>
public sealed class YahooQuoteProvider : IQuoteProvider
{
    private static readonly Random Jitter = new();
    private readonly QuoteStore store;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (MarketQuote Quote, DateTimeOffset At)> cache = new(StringComparer.Ordinal);

    public YahooQuoteProvider(QuoteStore store) => this.store = store;

    public QuoteSource Source => QuoteSource.Yahoo;

    public async Task<IReadOnlyList<MarketQuote>> FetchAsync(IReadOnlyList<QuoteInstrument> instruments, CancellationToken ct)
    {
        if (!YahooAccess.Enabled) return Array.Empty<MarketQuote>();
        var wanted = instruments.Where(i => i.Code.Length > 0).GroupBy(i => i.Code, StringComparer.Ordinal).Select(g => g.First()).Take(YahooAccess.MaxInstruments).ToList();
        var now = DateTimeOffset.UtcNow;
        var todo = wanted.Where(i => !cache.TryGetValue(i.Code, out var c) || now - c.At >= YahooAccess.MinInterval).ToList();
        if (todo.Count > 0 && !YahooAccess.IsBlocked)
        {
            var changed = false;
            for (var k = 0; k < todo.Count && !ct.IsCancellationRequested; k++)
            {
                if (k > 0) await Task.Delay(900 + Jitter.Next(0, 700), ct).ConfigureAwait(false);   // one after the other, never in a burst
                var inst = todo[k];
                try
                {
                    using var response = await YahooHistory.Http.GetAsync(string.Format(YahooHistory.ChartUrl, Uri.EscapeDataString(inst.Code)), ct).ConfigureAwait(false);
                    if ((int)response.StatusCode is 401 or 403 or 429) { YahooAccess.Block(((int)response.StatusCode).ToString()); break; }
                    if (!response.IsSuccessStatusCode) continue;
                    var snap = YahooHistory.ParseSnapshot(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                    if (snap is null) continue;
                    ApplyMeta(inst, snap);
                    store.MergeCandles(inst.StoreKey, snap.Candles);
                    store.MarkBackfill(inst.StoreKey, DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                    var day = snap.At.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                    store.Record(inst.StoreKey, day, snap.Price);
                    var prev = snap.PreviousClose;
                    decimal? change = prev is > 0 ? Math.Round((snap.Price - prev.Value) / prev.Value * 100, 2) : null;
                    cache[inst.Code] = (new MarketQuote(inst.Symbol, inst.NameEn, snap.Price, change, snap.At, Delayed: true), DateTimeOffset.UtcNow);
                    changed = true;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                {
                    if (ct.IsCancellationRequested) break;
                    Log.Write($"Yahoo Finance {inst.Code}: {ex.GetType().Name}");
                    break;
                }
            }
            if (changed) store.Save();
        }
        return wanted.Where(i => cache.ContainsKey(i.Code)).Select(i => cache[i.Code].Quote).ToList();
    }

    /// <summary>Gives a new instrument its currency sign and, when it has none, its name (indices keep an empty unit: they are in points).</summary>
    private static void ApplyMeta(QuoteInstrument inst, YahooSnapshot snap)
    {
        if (string.IsNullOrEmpty(inst.Unit) && !string.Equals(snap.Type, "INDEX", StringComparison.OrdinalIgnoreCase) && snap.Currency.Length > 0)
            inst.Unit = snap.Currency switch { "EUR" => "€", "USD" => "$", "GBP" => "£", "JPY" => "¥", var other => other };
        if (string.IsNullOrWhiteSpace(inst.NameEn) && !string.IsNullOrWhiteSpace(snap.Name)) inst.NameEn = snap.Name!;
        if (string.IsNullOrWhiteSpace(inst.NameEl) && !string.IsNullOrWhiteSpace(snap.Name)) inst.NameEl = snap.Name!;
    }
}
