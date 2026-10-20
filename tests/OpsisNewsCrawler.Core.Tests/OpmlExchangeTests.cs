using System.Text;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class OpmlExchangeTests
{
    [Fact]
    public void Import_ReadsFeedsAndIgnoresUnsafeOrDuplicateUrls()
    {
        const string opml = """
            <?xml version="1.0"?>
            <opml version="2.0"><body>
              <outline text="BBC" type="rss" xmlUrl="https://feeds.bbci.co.uk/news/rss.xml" />
              <outline title="BBC duplicate" type="rss" xmlUrl="https://feeds.bbci.co.uk/news/rss.xml" />
              <outline text="Bad" type="rss" xmlUrl="file:///etc/passwd" />
              <outline text="Folder"><outline text="Nested" type="rss" xmlUrl="https://example.com/feed" /></outline>
            </body></opml>
            """;
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(opml));
        var feeds = OpmlExchange.Import(input);
        Assert.Equal(2, feeds.Count);
        Assert.Equal("BBC", feeds[0].Name);
        Assert.Equal("https://example.com/feed", feeds[1].Url);
    }

    [Fact]
    public void Import_RejectsDtdAndNonOpmlDocuments()
    {
        const string xml = "<!DOCTYPE opml [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><opml><body>&x;</body></opml>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        Assert.ThrowsAny<Exception>(() => OpmlExchange.Import(input));
    }

    [Fact]
    public void ExportThenImport_RoundTripsEnabledFeedsOnly()
    {
        var feeds = new[]
        {
            new FeedSource("Enabled", "https://example.com/rss"),
            new FeedSource("Disabled", "https://disabled.example/rss") { Enabled = false }
        };
        using var output = new MemoryStream();
        OpmlExchange.Export(feeds, output);
        output.Position = 0;
        var imported = OpmlExchange.Import(output);
        var feed = Assert.Single(imported);
        Assert.Equal("Enabled", feed.Name);
        Assert.Equal("https://example.com/rss", feed.Url);
    }
}
