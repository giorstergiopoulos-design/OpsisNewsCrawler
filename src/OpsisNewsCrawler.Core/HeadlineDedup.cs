using System.Net;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Finds the same story told by different publishers: titles that are not equal but share almost all their words
/// ("Eurogroup: no change in the fiscal path" / "No change in the fiscal path, says Eurogroup").
/// </summary>
public static class HeadlineDedup
{
    private const int MinWordLength = 3;
    private const int MinWords = 4;
    private const double Threshold = 0.7;

    /// <summary>The set of significant lower-case words of a title (accents removed, punctuation ignored).</summary>
    public static HashSet<string> Words(string? title)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(title)) return set;
        var text = StripAccents(WebUtility.HtmlDecode(title)).ToLowerInvariant().Replace('ς', 'σ');
        foreach (Match m in Regex.Matches(text, @"[\p{L}\p{N}]+"))
            if (m.Length >= MinWordLength) set.Add(m.Value);
        return set;
    }

    private static string StripAccents(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text.Normalize(System.Text.NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString();
    }

    /// <summary>True when the overlap of the two word sets is at least 70 % of the smaller one (and both have at least four words).</summary>
    public static bool AreSameStory(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count < MinWords || b.Count < MinWords) return false;
        var common = a.Count <= b.Count ? a.Count(b.Contains) : b.Count(a.Contains);
        return common >= Math.Min(a.Count, b.Count) * Threshold;
    }
}
