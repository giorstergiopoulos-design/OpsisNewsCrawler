using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>A live video source (YouTube or a web TV page) shown in the preview window while breaking news is active.</summary>
public sealed class LiveFeed
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Enabled { get; set; } = true;

    public LiveFeed() { }
    public LiveFeed(string name, string url) { Name = name; Url = url; }
}

/// <summary>Decides which headlines are urgent, and turns a live-stream address into something a web view can play.</summary>
public static class Breaking
{
    public static readonly string[] DefaultKeywords = { "ΕΚΤΑΚΤΟ", "ΕΚΤΑΚΤΗ", "ΕΚΤΑΚΤΑ", "BREAKING", "URGENT", "FLASH" };

    /// <summary>Upper case, without accents (Greek and Latin), so "Έκτακτο" matches "ΕΚΤΑΚΤΟ".</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.ToUpper(CultureInfo.InvariantCulture).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>True when the title contains one of the keywords and the item is recent enough.</summary>
    public static bool IsBreaking(NewsItem item, IEnumerable<string> keywords, TimeSpan maxAge)
    {
        if (item.Kind != NewsItemKind.News || string.IsNullOrWhiteSpace(item.Title)) return false;
        if (DateTimeOffset.Now - item.PublishedAt > maxAge) return false;
        var title = Normalize(item.Title);
        foreach (var k in keywords)
        {
            var key = Normalize(k).Trim();
            if (key.Length >= 3 && title.Contains(key, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static readonly Regex YoutubeId = new(@"(?:v=|youtu\.be/|/live/|/shorts/|/embed/)([A-Za-z0-9_-]{11})", RegexOptions.Compiled);
    private static readonly Regex YoutubeChannel = new(@"youtube\.com/channel/(UC[A-Za-z0-9_-]{22})", RegexOptions.Compiled);

    /// <summary>
    /// The address to open in the preview: a YouTube video / live link becomes its embed player (autoplay, muted),
    /// a YouTube channel link becomes the channel's live stream, anything else (a web TV page) is used as it is.
    /// Returns null for an address that is not a web address.
    /// </summary>
    public static string? ToPlayableUrl(string? url)
    {
        var text = (url ?? "").Trim();
        if (text.Length == 0) return null;
        if (!text.Contains("://")) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return null;

        if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var id = YoutubeId.Match(uri.AbsoluteUri);
            if (id.Success) return $"https://www.youtube.com/embed/{id.Groups[1].Value}?autoplay=1&mute=1&rel=0";
            var channel = YoutubeChannel.Match(uri.AbsoluteUri);
            if (channel.Success) return $"https://www.youtube.com/embed/live_stream?channel={channel.Groups[1].Value}&autoplay=1&mute=1";
        }
        return uri.AbsoluteUri;
    }
}
