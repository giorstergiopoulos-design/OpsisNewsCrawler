using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>A Euronext market the user can pick shares from.</summary>
public sealed record EuronextMarket(string Mic, string NameEl, string NameEn)
{
    public string DisplayName => Loc.Pick(NameEl, NameEn);
}

/// <summary>
/// The complete list of shares of a Euronext market, so the user can tick the ones to show.
/// Athens: the ISINs found in the official delayed trade file, named through OpenFIGI.
/// Every other market: the public "Download" CSV of live.euronext.com (all shares of the market, end-of-day prices).
/// Catalogs are cached in %AppData%\OpsisNewsCrawler\catalog-&lt;MIC&gt;.json.
/// </summary>
public static class EuronextCatalog
{
    public static readonly IReadOnlyList<EuronextMarket> Markets = new[]
    {
        new EuronextMarket("XATH", "Euronext Athens (Αθήνα)", "Euronext Athens"),
        new EuronextMarket("XPAR", "Euronext Paris (Παρίσι)", "Euronext Paris"),
        new EuronextMarket("XAMS", "Euronext Amsterdam (Άμστερνταμ)", "Euronext Amsterdam"),
        new EuronextMarket("XBRU", "Euronext Brussels (Βρυξέλλες)", "Euronext Brussels"),
        new EuronextMarket("XLIS", "Euronext Lisbon (Λισαβόνα)", "Euronext Lisbon"),
        new EuronextMarket("XMSM", "Euronext Dublin (Δουβλίνο)", "Euronext Dublin"),
        new EuronextMarket("MTAA", "Euronext Milan (Μιλάνο)", "Euronext Milan"),
        new EuronextMarket("XOSL", "Oslo Børs (Όσλο)", "Oslo Børs"),
    };

    internal const string CsvUrl = "https://live.euronext.com/en/pd_es/data/stocks/download?mics={0}&display_datapoints=dp_stocks&display_filters=df_stocks&format=csv";

    private static string CachePath(string mic) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpsisNewsCrawler", $"catalog-{mic}.json");

    /// <summary>What was downloaded last time (empty when never).</summary>
    public static IReadOnlyList<QuoteInstrument> LoadCached(string mic)
    {
        try
        {
            var path = CachePath(mic);
            if (!File.Exists(path)) return Array.Empty<QuoteInstrument>();
            return JsonSerializer.Deserialize<List<QuoteInstrument>>(File.ReadAllText(path)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Array.Empty<QuoteInstrument>();
        }
    }

    public static DateTime? CacheTime(string mic)
    {
        var path = CachePath(mic);
        return File.Exists(path) ? File.GetLastWriteTime(path) : null;
    }

    private static void SaveCache(string mic, IReadOnlyList<QuoteInstrument> list)
    {
        try
        {
            var path = CachePath(mic);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            SafeFile.WriteAllText(path, JsonSerializer.Serialize(list));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Downloads the full list of a market (and caches it). Returns the cached list when the network fails.</summary>
    public static async Task<IReadOnlyList<QuoteInstrument>> FetchAsync(string mic, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var list = mic == "XATH" ? await FetchAthensAsync(progress, ct).ConfigureAwait(false) : await FetchCsvAsync(mic, ct).ConfigureAwait(false);
            if (list.Count > 0)
            {
                SaveCache(mic, list);
                return list;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or FormatException)
        {
        }
        return LoadCached(mic);
    }

    // ---- every market except Athens: the Euronext CSV ---------------------------------------

    private static async Task<IReadOnlyList<QuoteInstrument>> FetchCsvAsync(string mic, CancellationToken ct)
    {
        using var http = QuoteHttp.Create();
        var csv = await http.GetStringAsync(string.Format(CsvUrl, mic), ct).ConfigureAwait(false);
        var parsed = EuronextCsv.Parse(csv);
        return parsed.Rows.Select(r => new QuoteInstrument(QuoteSource.Euronext, r.Isin, r.Symbol, r.Name, r.Name, UnitOf(r.Currency), enabled: false) { Market = mic })
            .GroupBy(i => i.Code).Select(g => g.First()).OrderBy(i => i.NameEn, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static string UnitOf(string currency) => currency switch { "EUR" => "€", "USD" => "$", "GBP" => "£", "" => "€", _ => currency };

    // ---- Athens: ISINs from the trade file + names from OpenFIGI -----------------------------

    private static async Task<IReadOnlyList<QuoteInstrument>> FetchAthensAsync(IProgress<string>? progress, CancellationToken ct)
    {
        using var http = QuoteHttp.Create();
        var isins = await AthensDelayedFileProvider.ReadIsinsAsync(http, ct).ConfigureAwait(false);
        if (isins.Count == 0) return Array.Empty<QuoteInstrument>();

        var known = LoadCached("XATH").ToDictionary(i => i.Code, StringComparer.Ordinal);
        var defaults = QuoteInstrument.Defaults().Where(d => d.Source == QuoteSource.Athens).ToDictionary(d => d.Code, StringComparer.Ordinal);
        var result = new List<QuoteInstrument>();
        var unknown = new List<string>();
        foreach (var isin in isins.OrderBy(i => i, StringComparer.Ordinal))
        {
            if (defaults.TryGetValue(isin, out var d)) result.Add(new QuoteInstrument(QuoteSource.Athens, d.Code, d.Symbol, d.NameEl, d.NameEn, "€", enabled: false) { Market = "XATH" });
            else if (known.TryGetValue(isin, out var k)) result.Add(k);
            else unknown.Add(isin);
        }

        // OpenFIGI without a key: 10 identifiers per request, about 25 requests a minute.
        var done = 0;
        for (var i = 0; i < unknown.Count; i += 10)
        {
            ct.ThrowIfCancellationRequested();
            var batch = unknown.Skip(i).Take(10).ToList();
            progress?.Report($"{done}/{unknown.Count}");
            var named = await NameBatchAsync(http, batch, ct).ConfigureAwait(false);
            foreach (var isin in batch)
            {
                named.TryGetValue(isin, out var n);
                var name = n.Name ?? isin;
                var symbol = n.Ticker ?? isin[^6..];
                result.Add(new QuoteInstrument(QuoteSource.Athens, isin, symbol, name, name, "€", enabled: false) { Market = "XATH" });
            }
            done += batch.Count;
            if (i + 10 < unknown.Count) await Task.Delay(2600, ct).ConfigureAwait(false);
        }
        return result.OrderBy(i => i.NameEn, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<Dictionary<string, (string? Name, string? Ticker)>> NameBatchAsync(HttpClient http, List<string> isins, CancellationToken ct)
    {
        var map = new Dictionary<string, (string?, string?)>(StringComparer.Ordinal);
        var body = JsonSerializer.Serialize(isins.Select(i => new { idType = "ID_ISIN", idValue = i }));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openfigi.com/v3/mapping") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if ((int)response.StatusCode == 429)
            {
                await Task.Delay(30_000, ct).ConfigureAwait(false);
                continue;
            }
            if (!response.IsSuccessStatusCode) return map;
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var index = 0;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (index >= isins.Count) break;
                string? name = null, ticker = null;
                if (item.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    JsonElement? best = null;
                    foreach (var d in data.EnumerateArray())
                    {
                        best ??= d;
                        if (d.TryGetProperty("exchCode", out var ex) && ex.GetString() == "GA") { best = d; break; } // the Athens listing
                    }
                    if (best is { } b)
                    {
                        name = b.TryGetProperty("name", out var n) ? n.GetString() : null;
                        ticker = b.TryGetProperty("ticker", out var t) ? t.GetString() : null;
                    }
                }
                map[isins[index]] = (name, ticker);
                index++;
            }
            return map;
        }
        return map;
    }
}

/// <summary>The Euronext "Download" CSV: ";"-separated, quoted names, a few title lines before the data.</summary>
internal static class EuronextCsv
{
    internal sealed record Row(string Name, string Isin, string Symbol, string Currency, decimal? Last, decimal? Open, decimal? Close);

    internal sealed record Result(string Day, List<Row> Rows);

    public static Result Parse(string csv)
    {
        var rows = new List<Row>();
        var day = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        foreach (var raw in csv.Split('\n'))
        {
            var line = raw.Trim('\r', '﻿');
            if (line.Length == 0) continue;
            var f = Split(line);
            if (f.Count < 14)
            {
                if (f.Count == 1 && DateTime.TryParse(f[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) day = d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                continue;
            }
            if (f[1] == "ISIN" || f[1].Length != 12) continue; // header
            rows.Add(new Row(f[0], f[1], f[2], f[4], Num(f[8]), Num(f[5]), Num(f[13])));
        }
        return new Result(day, rows);
    }

    private static decimal? Num(string s) =>
        decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static List<string> Split(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == ';' && !quoted)
            {
                fields.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else sb.Append(c);
        }
        fields.Add(sb.ToString().Trim());
        return fields;
    }
}

/// <summary>Prices of the shares picked from the other Euronext markets (end of the last trading day).</summary>
public sealed class EuronextListProvider : IQuoteProvider
{
    private readonly HttpClient http = QuoteHttp.Create();
    private readonly QuoteStore store;
    private readonly Dictionary<string, (DateTimeOffset At, EuronextCsv.Result Data)> cache = new();

    public EuronextListProvider(QuoteStore store) => this.store = store;

    public QuoteSource Source => QuoteSource.Euronext;

    public async Task<IReadOnlyList<MarketQuote>> FetchAsync(IReadOnlyList<QuoteInstrument> instruments, CancellationToken ct)
    {
        var results = new List<MarketQuote>();
        foreach (var group in instruments.GroupBy(i => i.Market))
        {
            var data = await GetMarketAsync(group.Key, ct).ConfigureAwait(false);
            if (data is null) continue;
            var byIsin = data.Rows.GroupBy(r => r.Isin).ToDictionary(g => g.Key, g => g.First());
            foreach (var inst in group)
            {
                if (!byIsin.TryGetValue(inst.Code, out var row)) continue;
                var price = row.Last ?? row.Close;
                if (price is null or <= 0) continue;
                var key = inst.Market + ":" + inst.Code;
                store.Record(key, data.Day, price.Value);
                var prev = store.PreviousClose(key, data.Day);
                var fromOpen = false;
                if (prev is null && row.Open is > 0) { prev = row.Open; fromOpen = true; }
                decimal? change = prev is > 0 ? Math.Round((price.Value - prev.Value) / prev.Value * 100, 2) : null;
                var asOf = DateTime.TryParseExact(data.Day, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? new DateTimeOffset(d, TimeSpan.Zero) : DateTimeOffset.UtcNow;
                results.Add(new MarketQuote(inst.Symbol, inst.NameEn, price.Value, change, asOf, Delayed: true, ChangeFromOpen: fromOpen));
            }
        }
        if (results.Count > 0) store.Save();
        return results;
    }

    private async Task<EuronextCsv.Result?> GetMarketAsync(string mic, CancellationToken ct)
    {
        // The list only changes at the end of the trading day: ask at most every 30 minutes.
        if (cache.TryGetValue(mic, out var hit) && DateTimeOffset.UtcNow - hit.At < TimeSpan.FromMinutes(30)) return hit.Data;
        try
        {
            var csv = await http.GetStringAsync(string.Format(EuronextCatalog.CsvUrl, mic), ct).ConfigureAwait(false);
            var parsed = EuronextCsv.Parse(csv);
            if (parsed.Rows.Count == 0) return cache.TryGetValue(mic, out var old) ? old.Data : null;
            cache[mic] = (DateTimeOffset.UtcNow, parsed);
            return parsed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return cache.TryGetValue(mic, out var old) ? old.Data : null;
        }
    }
}
