using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>Filters headlines and summaries against enabled share instruments in the user's watchlist.</summary>
public static class StockNewsFilter
{
    public static IReadOnlyList<NewsItem> Filter(IEnumerable<NewsItem> items, IEnumerable<QuoteInstrument> watchlist)
    {
        var terms = (watchlist ?? Enumerable.Empty<QuoteInstrument>())
            .Where(i => i is not null && i.Enabled && i.Source is QuoteSource.Athens or QuoteSource.Euronext)
            .SelectMany(i => new[] { i.Symbol, i.NameEl, i.NameEn })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(Normalize)
            .Where(s => s.Length >= 3)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (terms.Length == 0) return Array.Empty<NewsItem>();

        return items.Where(item =>
        {
            var text = " " + Normalize(item.Title + " " + item.Summary) + " ";
            return terms.Any(term => text.Contains(" " + term + " ", StringComparison.Ordinal));
        }).ToList();
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
    }
}
