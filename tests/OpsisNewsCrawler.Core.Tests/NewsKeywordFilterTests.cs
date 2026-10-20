using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class NewsKeywordFilterTests
{
    private static NewsItem Item(string title, string? summary = null) =>
        new(title, "https://example.test/story", "Feed", DateTimeOffset.UtcNow, summary);

    [Fact]
    public void Filter_MatchesAccentInsensitiveWordsAndPhrases()
    {
        var items = new[] { Item("Η αγορά ανεβαίνει"), Item("Sports report", "Η ομάδα κέρδισε") , Item("Economic outlook") };
        var result = NewsKeywordFilter.Filter(items, new[] { "ΑΓΟΡΑ", "ομάδα" });
        Assert.Equal(new[] { "Economic outlook" }, result.Select(x => x.Title));
    }

    [Fact]
    public void Filter_UsesWordBoundaries()
    {
        var items = new[] { Item("War in Europe"), Item("Reward program") };
        var result = NewsKeywordFilter.Filter(items, new[] { "war" });
        Assert.Equal(new[] { "Reward program" }, result.Select(x => x.Title));
    }

    [Fact]
    public void Filter_IgnoresEmptyTermsAndKeepsAllItems()
    {
        var items = new[] { Item("One"), Item("Two") };
        Assert.Equal(2, NewsKeywordFilter.Filter(items, new[] { "", " " }).Count);
    }
}
