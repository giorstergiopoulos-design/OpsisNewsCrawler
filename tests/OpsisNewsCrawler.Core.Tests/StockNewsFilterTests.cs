using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class StockNewsFilterTests
{
    [Fact]
    public void Filter_MatchesSymbolsAndAccentInsensitiveNames_OnlyForEnabledShares()
    {
        var watchlist = new[]
        {
            new QuoteInstrument(QuoteSource.Athens, "ETE-ISIN", "ETE", "Εθνική Τράπεζα", "National Bank", "€"),
            new QuoteInstrument(QuoteSource.Athens, "ALPHA-ISIN", "ALPHA", "Alpha Bank", "Alpha Bank", "€", enabled: false),
            new QuoteInstrument(QuoteSource.Metals, "XAU", "XAU", "Χρυσός", "Gold", "$"),
        };
        var items = new[]
        {
            new NewsItem("Η Εθνικη Τραπεζα ανακοίνωσε αποτελέσματα", "https://example.com/1", "A", DateTimeOffset.UtcNow),
            new NewsItem("ETE shares gain", "https://example.com/2", "B", DateTimeOffset.UtcNow),
            new NewsItem("Alpha Bank update", "https://example.com/3", "C", DateTimeOffset.UtcNow),
            new NewsItem("Gold rises", "https://example.com/4", "D", DateTimeOffset.UtcNow),
        };

        var result = StockNewsFilter.Filter(items, watchlist);

        Assert.Equal(new[] { "Η Εθνικη Τραπεζα ανακοίνωσε αποτελέσματα", "ETE shares gain" }, result.Select(item => item.Title));
    }

    [Fact]
    public void Filter_ReturnsEmptyWhenNoShareIsEnabled()
    {
        var instrument = new QuoteInstrument(QuoteSource.Athens, "X", "ETE", "ETE", "ETE", "€", enabled: false);
        Assert.Empty(StockNewsFilter.Filter(new[] { new NewsItem("ETE update", "", "", DateTimeOffset.UtcNow) }, new[] { instrument }));
    }
}
