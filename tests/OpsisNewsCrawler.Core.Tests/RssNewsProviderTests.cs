using System.Text;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class RssNewsProviderTests
{
    [Fact]
    public void Parse_HandlesRssDatesAndHtmlEntities()
    {
        const string rss = """
            <rss version="2.0"><channel><item>
              <title>Markets &amp; economy</title>
              <link>https://example.com/story</link>
              <pubDate>Tue, 13 Oct 2026 10:00:00 +0000</pubDate>
              <description><![CDATA[<p>Summary &amp; details</p>]]></description>
            </item></channel></rss>
            """;
        var items = RssNewsProvider.Parse(Encoding.UTF8.GetBytes(rss), "Example", 10);
        var item = Assert.Single(items);
        Assert.Equal("Markets & economy", item.Title);
        Assert.Equal("https://example.com/story", item.Url);
        Assert.Equal("Example", item.Source);
        Assert.Contains("Summary", item.Summary);
    }


    [Fact]
    public void Parse_KeepsUndatedEntriesAndSortsDatedEntriesNewestFirst()
    {
        const string rss = """
            <rss><channel>
              <item><title>Old story</title><link>https://example.com/old</link><pubDate>Mon, 12 Oct 2026 10:00:00 +0000</pubDate></item>
              <item><title>New story</title><link>https://example.com/new</link><pubDate>Thu, 15 Oct 2026 10:00:00 +0000</pubDate></item>
              <item><title>Undated story</title><link>https://example.com/undated</link></item>
            </channel></rss>
            """;
        var items = RssNewsProvider.Parse(Encoding.UTF8.GetBytes(rss), "Example", 10);
        Assert.Equal(3, items.Count);
        Assert.Equal("New story", items[0].Title);
        Assert.Contains(items, item => item.Title == "Undated story" && item.PublishedAt == DateTimeOffset.MinValue);
    }


    [Fact]
    public void FilterAndOrder_DropsOldDatedStoriesButKeepsUndatedAndNewestFirst()
    {
        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var items = new[]
        {
            new NewsItem("Old", "https://example.com/old", "A", now.AddDays(-10)),
            new NewsItem("Recent", "https://example.com/recent", "B", now.AddHours(-2)),
            new NewsItem("Undated", "https://example.com/undated", "C", DateTimeOffset.MinValue),
            new NewsItem("Newest", "https://example.com/newest", "D", now.AddMinutes(-5)),
        };

        var filtered = RssNewsProvider.FilterAndOrder(items, 24, newestFirst: true, now: now);

        Assert.Equal(new[] { "Newest", "Recent", "Undated" }, filtered.Select(item => item.Title));
    }

    [Fact]
    public void Parse_IgnoresEntriesWithoutHttpLinks()
    {
        const string rss = "<rss><channel><item><title>Invalid</title><link>javascript:alert(1)</link></item></channel></rss>";
        Assert.Empty(RssNewsProvider.Parse(Encoding.UTF8.GetBytes(rss), "Example", 10));
    }
}
