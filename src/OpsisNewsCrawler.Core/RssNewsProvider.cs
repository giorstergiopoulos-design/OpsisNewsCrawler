using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Downloads and parses RSS 2.0 / Atom feeds. A failing feed never breaks the others.
/// </summary>
public sealed class RssNewsProvider : IDisposable
{
    private const long MaxFeedBytes = 5 * 1024 * 1024;
    private const int MaxTitleLength = 200;
    private const int MaxSummaryLength = 400;

    private readonly HttpClient http;

    public RssNewsProvider()
    {
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = MaxFeedBytes };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; OpsisNewsCrawler/0.6)");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml, */*");
    }

    public async Task<IReadOnlyList<NewsItem>> FetchAsync(
        IEnumerable<FeedSource> sources, int maxPerFeed, CancellationToken cancellationToken = default)
    {
        var tasks = sources
            .Where(s => !string.IsNullOrWhiteSpace(s.Url))
            .Select(s => FetchOneAsync(s, maxPerFeed, cancellationToken))
            .ToList();

        var perFeed = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Round-robin, so every source is visible in the ticker instead of the busiest one winning.
        var result = new List<NewsItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < maxPerFeed; i++)
        {
            foreach (var feed in perFeed)
            {
                if (i >= feed.Count) continue;
                var item = feed[i];
                if (seen.Add(item.Url)) result.Add(item);
            }
        }
        return result;
    }

    private async Task<List<NewsItem>> FetchOneAsync(FeedSource source, int maxItems, CancellationToken ct)
    {
        try
        {
            using var response = await http
                .GetAsync(source.Url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is > MaxFeedBytes) return new List<NewsItem>();
            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (bytes.LongLength > MaxFeedBytes) return new List<NewsItem>();

            return Parse(bytes, source.Name, maxItems);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException or IOException)
        {
            System.Diagnostics.Debug.WriteLine($"OpsisNewsCrawler: feed '{source.Name}' failed: {ex.Message}");
            return new List<NewsItem>();
        }
    }

    public static List<NewsItem> Parse(byte[] xml, string sourceName, int maxItems)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            CheckCharacters = false,
            MaxCharactersFromEntities = 1024
        };

        XDocument doc;
        using (var stream = new MemoryStream(xml))
        using (var reader = XmlReader.Create(stream, settings))
        {
            doc = XDocument.Load(reader);
        }

        var items = new List<NewsItem>();
        var entries = doc.Descendants().Where(e => e.Name.LocalName is "item" or "entry");
        foreach (var entry in entries)
        {
            var title = Clean(ChildValue(entry, "title"));
            var link = FindLink(entry);
            if (string.IsNullOrEmpty(title) || link is null) continue;

            if (title.Length > MaxTitleLength) title = title[..MaxTitleLength].TrimEnd() + "…";
            var date = ParseDate(ChildValue(entry, "pubDate") ?? ChildValue(entry, "published")
                                 ?? ChildValue(entry, "updated") ?? ChildValue(entry, "date"));
            var summary = ExtractSummary(entry, title);
            var image = ExtractImage(entry);
            items.Add(new NewsItem(title, link, sourceName, date ?? DateTimeOffset.MinValue, summary, image));
        }

        // Stable sort: items without a date keep the order of the feed.
        return items.OrderByDescending(i => i.PublishedAt).Take(maxItems).ToList();
    }

    private static string? ExtractSummary(XElement entry, string title)
    {
        var html = ChildValue(entry, "description") ?? ChildValue(entry, "summary")
                   ?? ChildValue(entry, "encoded") ?? ChildValue(entry, "content");
        if (string.IsNullOrWhiteSpace(html)) return null;

        var text = Clean(Regex.Replace(html, "<[^>]+>", " "));
        // WordPress feeds append "The post X appeared first on Y."
        text = Regex.Replace(text, @"\s*The post .* appeared first on .*$", "", RegexOptions.IgnoreCase).Trim();
        if (text.Length == 0 || string.Equals(text, title, StringComparison.OrdinalIgnoreCase)) return null;
        return text.Length > MaxSummaryLength ? text[..MaxSummaryLength].TrimEnd() + "…" : text;
    }

    private static string? ExtractImage(XElement entry)
    {
        foreach (var e in entry.Descendants())
        {
            var name = e.Name.LocalName;
            if (name is not ("enclosure" or "thumbnail" or "content")) continue;
            var url = (string?)e.Attribute("url");
            if (string.IsNullOrWhiteSpace(url)) continue;

            var type = (string?)e.Attribute("type");
            var medium = (string?)e.Attribute("medium");
            var isImage = name == "thumbnail"
                          || medium == "image"
                          || (type is not null && type.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                          || (type is null && LooksLikeImage(url));
            if (isImage && ValidUrl(url) is { } valid) return valid;
        }

        var html = ChildValue(entry, "encoded") ?? ChildValue(entry, "description")
                   ?? ChildValue(entry, "summary") ?? ChildValue(entry, "content");
        if (string.IsNullOrEmpty(html)) return null;
        var m = Regex.Match(html, "<img[^>]+src=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
        return m.Success ? ValidUrl(WebUtility.HtmlDecode(m.Groups[1].Value)) : null;
    }

    private static bool LooksLikeImage(string url)
    {
        var path = url.Split('?', '#')[0];
        return path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ChildValue(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static string? FindLink(XElement entry)
    {
        foreach (var l in entry.Elements().Where(e => e.Name.LocalName == "link"))
        {
            var rel = (string?)l.Attribute("rel");
            if (rel is not null && rel != "alternate") continue;
            var candidate = (string?)l.Attribute("href");
            if (string.IsNullOrWhiteSpace(candidate)) candidate = l.Value;
            var url = ValidUrl(candidate);
            if (url is not null) return url;
        }
        return ValidUrl(ChildValue(entry, "guid"));
    }

    private static string? ValidUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri
            : null;
    }

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
    }

    private static DateTimeOffset? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        const DateTimeStyles styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal;
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, styles, out var value)) return value;

        // Accept "+0300" style offsets that some Greek feeds use after an ISO-like date.
        var fixedText = Regex.Replace(text, @"\s*([+-]\d{2})(\d{2})$", " $1:$2");
        return DateTimeOffset.TryParse(fixedText, CultureInfo.InvariantCulture, styles, out value) ? value : null;
    }

    public void Dispose() => http.Dispose();
}
