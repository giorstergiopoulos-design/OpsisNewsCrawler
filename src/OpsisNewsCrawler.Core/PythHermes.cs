using System.Globalization;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Pyth Network "Hermes" public price service (no API key): near-live Brent, WTI and natural gas index prices.
/// Used before the daily FRED/EIA series, which is published about a week late. The prices are Pyth index / front-month
/// reference prices, not the NYMEX/ICE tick, so they can differ slightly from a broker screen.
/// </summary>
public static class PythHermes
{
    /// <summary>FRED series id (the instrument code in the watch list) → Pyth feed id (hex, no 0x). Ids from the Hermes price_feeds list.</summary>
    public static readonly IReadOnlyDictionary<string, string> FeedIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["DCOILBRENTEU"] = "f33ce961935076ef4dc98be75cf2126046eac1bffdcd7a0fa05ccf18b746fda6", // Commodities.Index.BRENT/USD (24/7)
        ["DCOILWTICO"] = "f2464139c45f26097e0173b03207c4a490dbd8bf1b72dfbf064eeb832c7c2933",   // Commodities.Index.WTI1M/USD (23/5)
        ["DHHNGSP"] = "45f95717fbe158e896415d1cea33814d73e54169022a8f7bcbb815ebef92f71c"       // Commodities.Index.NATGAS/USD (24/7)
    };

    /// <summary>One parsed price.</summary>
    public sealed record Price(string Id, decimal Value, DateTimeOffset PublishedAt);

    public static string BuildUrl(IEnumerable<string> ids) =>
        "https://hermes.pyth.network/v2/updates/price/latest?parsed=true&encoding=hex&ignore_invalid_price_ids=true"
        + string.Concat(ids.Select(id => "&ids[]=" + id));

    /// <summary>
    /// Reads the "parsed" array of a Hermes answer. Entries that are malformed, not positive, or whose confidence interval
    /// is wider than 2 % of the price are skipped. Never throws for unexpected content.
    /// </summary>
    public static List<Price> Parse(string json)
    {
        var list = new List<Price>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("parsed", out var parsed) || parsed.ValueKind != JsonValueKind.Array) return list;

        foreach (var entry in parsed.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String
                || !entry.TryGetProperty("price", out var price) || price.ValueKind != JsonValueKind.Object) continue;

            var id = (idElement.GetString() ?? "").Trim();
            if (id.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) id = id[2..];
            id = id.ToLowerInvariant();

            if (!Number(price, "price", out var raw) || !Number(price, "expo", out var expo) || !Number(price, "publish_time", out var time)) continue;
            if (expo < -18 || expo > 0 || expo != decimal.Truncate(expo) || time <= 0 || time > 4_000_000_000m) continue;

            var scale = 1m;
            for (var i = 0; i < (int)Math.Abs(expo); i++) scale *= 10m;
            var value = expo < 0 ? raw / scale : raw * scale;
            if (value <= 0) continue;

            if (Number(price, "conf", out var conf) && conf >= 0)
            {
                var confidence = expo < 0 ? conf / scale : conf * scale;
                if (confidence / value > 0.02m) continue;
            }
            list.Add(new Price(id, value, DateTimeOffset.FromUnixTimeSeconds((long)time)));
        }
        return list;
    }

    /// <summary>The numbers of a Hermes answer are strings ("7512345678") or JSON numbers, depending on the field.</summary>
    private static bool Number(JsonElement parent, string name, out decimal value)
    {
        value = 0;
        if (!parent.TryGetProperty(name, out var element)) return false;
        return element.ValueKind switch
        {
            JsonValueKind.String => decimal.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value),
            JsonValueKind.Number => element.TryGetDecimal(out value),
            _ => false
        };
    }
}
