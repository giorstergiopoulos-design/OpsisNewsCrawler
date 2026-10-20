using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>Filters muted words and phrases from news without being sensitive to case or accents.</summary>
public static class NewsKeywordFilter
{
    public static IReadOnlyList<NewsItem> Filter(IEnumerable<NewsItem> items, IEnumerable<string>? mutedKeywords)
    {
        var terms = (mutedKeywords ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize).Where(x => x.Length >= 2)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (terms.Length == 0) return items.ToList();

        return items.Where(item =>
        {
            var text = " " + Normalize((item.Title ?? string.Empty) + " " + (item.Summary ?? string.Empty)) + " ";
            return !terms.Any(term => text.Contains(" " + term + " ", StringComparison.Ordinal));
        }).ToList();
    }

    public static bool IsMuted(NewsItem item, IEnumerable<string>? mutedKeywords) =>
        Filter(new[] { item }, mutedKeywords).Count == 0;

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant(),
            @"[^\p{L}\p{N}]+", " ").Trim();
    }
}
