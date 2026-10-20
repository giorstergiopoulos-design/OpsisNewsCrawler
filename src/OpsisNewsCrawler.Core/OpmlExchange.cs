using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace OpsisNewsCrawler.Core;

/// <summary>Safe OPML 2.0 import/export helpers for moving RSS subscriptions between news readers.</summary>
public static class OpmlExchange
{
    private const int MaxDocumentBytes = 5 * 1024 * 1024;
    private const int MaxFeeds = 2000;

    public static IReadOnlyList<FeedSource> Import(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxDocumentBytes)
                throw new InvalidDataException("OPML document exceeds the 5 MB safety limit.");
            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxDocumentBytes };
        using var reader = XmlReader.Create(buffer, settings);
        var doc = XDocument.Load(reader);
        if (!string.Equals(doc.Root?.Name.LocalName, "opml", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The document is not an OPML file.");

        var result = new List<FeedSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var outline in doc.Descendants().Where(e => e.Name.LocalName == "outline"))
        {
            var type = (string?)outline.Attribute("type");
            var url = ((string?)outline.Attribute("xmlUrl") ?? (string?)outline.Attribute("xmlurl") ?? "").Trim();
            if (!string.Equals(type, "rss", StringComparison.OrdinalIgnoreCase) && url.Length == 0) continue;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) continue;
            var canonical = uri.AbsoluteUri;
            if (!seen.Add(canonical)) continue;
            var name = ((string?)outline.Attribute("title") ?? (string?)outline.Attribute("text") ?? uri.Host).Trim();
            if (name.Length == 0) name = uri.Host;
            result.Add(new FeedSource(name, canonical));
            if (result.Count >= MaxFeeds) break;
        }
        return result;
    }

    public static void Export(IEnumerable<FeedSource> feeds, Stream output, string title = "Opsis News Crawler")
    {
        ArgumentNullException.ThrowIfNull(feeds);
        ArgumentNullException.ThrowIfNull(output);
        var outlines = new List<XElement>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var feed in feeds.Where(f => f is not null && f.Enabled))
        {
            if (!Uri.TryCreate(feed.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) continue;
            if (!seen.Add(uri.AbsoluteUri)) continue;
            outlines.Add(new XElement("outline", new XAttribute("text", feed.Name ?? uri.Host),
                new XAttribute("title", feed.Name ?? uri.Host), new XAttribute("type", "rss"), new XAttribute("xmlUrl", uri.AbsoluteUri)));
            if (outlines.Count >= MaxFeeds) break;
        }
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("opml", new XAttribute("version", "2.0"),
                new XElement("head", new XElement("title", string.IsNullOrWhiteSpace(title) ? "Opsis News Crawler" : title)),
                new XElement("body", outlines)));
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false };
        using var writer = XmlWriter.Create(output, settings);
        doc.Save(writer);
    }
}
