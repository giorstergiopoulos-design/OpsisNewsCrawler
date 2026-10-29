using System.Net;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Finds the RSS/Atom feed of a website from its address: either the address already is a feed, or the page declares it with
/// <c>&lt;link rel="alternate" type="application/rss+xml"&gt;</c>, or one of the usual paths (/feed, /rss.xml, ...) answers with a feed.
/// Never throws for network problems; an empty result means "nothing found".
/// </summary>
public static class FeedDiscovery
{
    private static readonly string[] CommonPaths = { "/feed", "/feed/", "/rss", "/rss.xml", "/feed.xml", "/atom.xml", "/index.xml", "/rss/all" };

    private static readonly Regex LinkTag = new(@"<link\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TypeAttr = new(@"type\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HrefAttr = new(@"href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TitleAttr = new(@"title\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PageTitle = new(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static async Task<IReadOnlyList<FeedSource>> DiscoverAsync(string input, CancellationToken ct = default)
    {
        var result = new List<FeedSource>();
        if (!TryNormalize(input, out var site)) return result;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; OpsisNewsCrawler)");

        try
        {
            var (body, _) = await GetTextAsync(http, site, ct).ConfigureAwait(false);
            if (body is null) return result;

            // The address itself is a feed.
            if (LooksLikeFeed(body))
            {
                result.Add(new FeedSource(FeedTitle(body) ?? site.Host, site.AbsoluteUri));
                return result;
            }

            var siteName = PageTitle.Match(body) is { Success: true } t ? Clean(t.Groups[1].Value) : site.Host;

            foreach (Match tag in LinkTag.Matches(body))
            {
                var type = TypeAttr.Match(tag.Value);
                var href = HrefAttr.Match(tag.Value);
                if (!type.Success || !href.Success) continue;
                var mime = type.Groups[1].Value.ToLowerInvariant();
                if (!mime.Contains("rss") && !mime.Contains("atom")) continue;
                if (!Uri.TryCreate(site, WebUtility.HtmlDecode(href.Groups[1].Value), out var abs)) continue;
                var title = TitleAttr.Match(tag.Value) is { Success: true } ta ? Clean(ta.Groups[1].Value) : siteName;
                if (result.All(r => r.Url != abs.AbsoluteUri)) result.Add(new FeedSource(title, abs.AbsoluteUri));
            }
            if (result.Count > 0) return result;

            // Nothing declared: try the usual places.
            foreach (var path in CommonPaths)
            {
                if (!Uri.TryCreate(new Uri(site.GetLeftPart(UriPartial.Authority)), path, out var probe)) continue;
                var (text, _) = await GetTextAsync(http, probe, ct).ConfigureAwait(false);
                if (text is not null && LooksLikeFeed(text))
                {
                    result.Add(new FeedSource(FeedTitle(text) ?? siteName, probe.AbsoluteUri));
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UriFormatException)
        {
        }
        return result;
    }

    private static bool TryNormalize(string input, out Uri uri)
    {
        uri = null!;
        var text = (input ?? "").Trim();
        if (text.Length == 0) return false;
        if (!text.Contains("://")) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https")) return false;
        uri = parsed;
        return true;
    }

    private static async Task<(string? Body, string? ContentType)> GetTextAsync(HttpClient http, Uri uri, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return (null, null);
            var bytes = new byte[400_000];
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var read = 0;
            int n;
            while (read < bytes.Length && (n = await stream.ReadAsync(bytes.AsMemory(read), ct).ConfigureAwait(false)) > 0) read += n;
            return (System.Text.Encoding.UTF8.GetString(bytes, 0, read), response.Content.Headers.ContentType?.MediaType);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return (null, null);
        }
    }

    private static bool LooksLikeFeed(string text)
    {
        var head = text.Length > 2000 ? text[..2000] : text;
        return head.Contains("<rss", StringComparison.OrdinalIgnoreCase) ||
               head.Contains("<feed", StringComparison.OrdinalIgnoreCase) ||
               head.Contains("<rdf:RDF", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FeedTitle(string text)
    {
        var m = PageTitle.Match(text);
        return m.Success ? Clean(m.Groups[1].Value) : null;
    }

    private static string Clean(string value)
    {
        value = Regex.Replace(WebUtility.HtmlDecode(value), @"<!\[CDATA\[|\]\]>", "");
        return Regex.Replace(value, @"\s+", " ").Trim();
    }
}
