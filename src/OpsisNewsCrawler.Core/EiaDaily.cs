using System.Globalization;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Daily Brent, WTI and Henry Hub prices from the U.S. Energy Information Administration's public history pages (eia.gov, US government data,
/// robots.txt allows the dnav pages). It is the fallback when FRED answers with a bot-check page instead of its CSV (seen 2026-10-21) and Pyth
/// needs a key. The pages list one row per week, so the newest prices are a few days old.
/// </summary>
public static class EiaDaily
{
    public static readonly IReadOnlyDictionary<string, string> Pages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DCOILBRENTEU"] = "https://www.eia.gov/dnav/pet/hist/RBRTED.htm",
        ["DCOILWTICO"] = "https://www.eia.gov/dnav/pet/hist/RWTCD.htm",
        ["DHHNGSP"] = "https://www.eia.gov/dnav/ng/hist/rngwhhdD.htm",
    };

    private static readonly Regex Row = new(
        @"<td class='B6'>[^<]*?(?<y>\d{4}) (?<m1>[A-Za-z]{3})-\s*(?<d1>\d{1,2}) to [A-Za-z]{3}-\s*\d{1,2}</td>(?<cells>(?:\s*<td class='B3'>[^<]*</td>){1,5})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Cell = new(@"<td class='B3'>([^<]*)</td>", RegexOptions.Compiled);

    /// <summary>Every daily value of the page, oldest first (weeks are Monday to Friday; empty cells and "NA" are skipped).</summary>
    public static List<(DateTime Day, decimal Value)> Parse(string html)
    {
        var result = new List<(DateTime, decimal)>();
        foreach (Match m in Row.Matches(html))
        {
            if (!DateTime.TryParseExact($"{m.Groups["y"].Value} {m.Groups["m1"].Value} {m.Groups["d1"].Value}", "yyyy MMM d", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monday)) continue;
            var i = 0;
            foreach (Match c in Cell.Matches(m.Groups["cells"].Value))
            {
                var text = c.Groups[1].Value.Trim();
                if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0) result.Add((monday.AddDays(i), v));
                i++;
            }
        }
        return result;
    }

    /// <summary>The newest price of a series with its change against the previous day, or null.</summary>
    public static MarketQuote? Latest(QuoteInstrument inst, string html)
    {
        var points = Parse(html);
        if (points.Count == 0) return null;
        var last = points[^1];
        decimal? change = points.Count > 1 && points[^2].Value > 0 ? Math.Round((last.Value - points[^2].Value) / points[^2].Value * 100, 2) : null;
        return new MarketQuote(inst.Symbol, inst.NameEn, last.Value, change, new DateTimeOffset(last.Day, TimeSpan.Zero), Delayed: true);
    }
}
