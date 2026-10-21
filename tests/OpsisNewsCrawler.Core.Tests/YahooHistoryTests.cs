using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class YahooHistoryTests
{
    private const string Sample = """
    {"chart":{"result":[{"meta":{"gmtoffset":10800},"timestamp":[1792400000,1792486400,1792572800],
    "indicators":{"quote":[{"open":[10.0,10.5,null],"high":[10.9,11.0,null],"low":[9.8,10.2,null],"close":[10.5,10.8,null],"volume":[1,2,3]}]}}]}}
    """;

    [Fact]
    public void Parse_ReadsDailyCandles_AndSkipsDaysWithMissingPrices()
    {
        var candles = YahooHistory.Parse(Sample);
        Assert.Equal(2, candles.Count);
        Assert.Equal(10.0m, candles[0].Open);
        Assert.Equal(11.0m, candles[1].High);
        Assert.All(candles, c => Assert.True(c.Exact));
        Assert.True(string.CompareOrdinal(candles[0].Day, candles[1].Day) < 0);
    }

    [Fact]
    public void Parse_Garbage_GivesNothing()
    {
        Assert.Empty(YahooHistory.Parse("<html>nope</html>"));
        Assert.Empty(YahooHistory.Parse("{\"chart\":{\"result\":null}}"));
    }

    [Fact]
    public void Symbols_MapTheKnownSources()
    {
        Assert.Equal("ETE.AT", YahooSymbols.For(new QuoteInstrument(QuoteSource.Athens, "GRS003003035", "ETE", "", "", "€")));
        Assert.Equal("GC=F", YahooSymbols.For(new QuoteInstrument(QuoteSource.Metals, "XAU", "XAU", "", "", "$")));
        Assert.Equal("BZ=F", YahooSymbols.For(new QuoteInstrument(QuoteSource.Energy, "DCOILBRENTEU", "BRENT", "", "", "$")));
        Assert.Equal("BTC-USD", YahooSymbols.For(new QuoteInstrument(QuoteSource.Crypto, "BTC", "BTC", "", "", "$")));
        Assert.Equal("EURUSD=X", YahooSymbols.For(new QuoteInstrument(QuoteSource.Currency, "EURUSD", "EUR/USD", "", "", "")));
        Assert.Equal("AIR.PA", YahooSymbols.For(new QuoteInstrument(QuoteSource.Euronext, "NL0000235190", "AIR", "", "", "€") { Market = "XPAR" }));
        Assert.Null(YahooSymbols.For(new QuoteInstrument(QuoteSource.Metals, "ZZZ", "ZZZ", "", "", "$")));
    }

    [Fact]
    public void StoreKey_EuronextShares_UseMarketAndIsin()
    {
        var i = new QuoteInstrument(QuoteSource.Euronext, "NL0000235190", "AIR", "", "", "€") { Market = "XPAR" };
        Assert.Equal("XPAR:NL0000235190", i.StoreKey);
        Assert.Equal("GRS1", new QuoteInstrument(QuoteSource.Athens, "GRS1", "X", "", "", "€").StoreKey);
    }

    [Fact]
    public void MergeCandles_KeepsExactTradeFileDays_AndFillsTheRest()
    {
        var path = Path.Combine(Path.GetTempPath(), "merge-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new QuoteStore(path);
            store.RecordCandle("X", "20261020", 10m, 12m, 9m, 11m);
            store.MergeCandles("X", new[]
            {
                new Candle("20261019", 9m, 10m, 8m, 9.5m, true),
                new Candle("20261020", 1m, 1m, 1m, 1m, true)   // must not replace the exact day from the trade file
            });
            var c = store.Candles("X");
            Assert.Equal(2, c.Count);
            Assert.Equal(12m, c[1].High);
            Assert.Equal(9.5m, c[0].Close);
            Assert.Contains(9.5m, store.History("X"));

            store.MarkBackfill("X", "20261020");
            store.Save();
            Assert.Equal("20261020", new QuoteStore(path).BackfillDay("X"));
        }
        finally
        {
            foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileNameWithoutExtension(path) + "*")) File.Delete(f);
        }
    }
}

public class YahooCatalogTests
{
    private const string Chart = """
    {"chart":{"result":[{"meta":{"currency":"USD","instrumentType":"INDEX","regularMarketPrice":7761.15,"regularMarketTime":1792587600,"chartPreviousClose":7718.94,"gmtoffset":-14400,"longName":"S&P 500"},
    "timestamp":[1792400000,1792486400,1792572800],
    "indicators":{"quote":[{"open":[7700,7710,7740],"high":[7730,7750,7770],"low":[7690,7700,7730],"close":[7710,7718.94,7761.15],"volume":[1,2,3]}]}}]}}
    """;

    [Fact]
    public void ParseSnapshot_ReadsPriceNameTypeAndPreviousClose()
    {
        var s = YahooHistory.ParseSnapshot(Chart)!;
        Assert.Equal(7761.15m, s.Price);
        Assert.Equal("INDEX", s.Type);
        Assert.Equal("USD", s.Currency);
        Assert.Equal("S&P 500", s.Name);
        Assert.Equal(3, s.Candles.Count);
        Assert.Equal(7718.94m, s.PreviousClose);
    }

    [Fact]
    public void ParseSnapshot_Garbage_IsNull() => Assert.Null(YahooHistory.ParseSnapshot("<html/>"));

    [Fact]
    public void ParseHits_KeepsOnlyUsableInstrumentTypes()
    {
        var json = """{"quotes":[{"symbol":"AAPL","longname":"Apple Inc.","quoteType":"EQUITY","exchDisp":"NASDAQ"},{"symbol":"^GSPC","shortname":"S&P 500","quoteType":"INDEX","exchange":"SNP"},{"symbol":"X","quoteType":"OPTION"}]}""";
        var hits = YahooSearch.ParseHits(json);
        Assert.Equal(2, hits.Count);
        Assert.Equal("Apple Inc.", hits[0].Name);
        Assert.Equal("S&P 500", hits[1].Name);
    }

    [Fact]
    public async Task Provider_DoesNothingWhileTheSwitchIsOff()
    {
        YahooAccess.Enabled = false;
        var path = Path.Combine(Path.GetTempPath(), "yq-" + Guid.NewGuid().ToString("N") + ".json");
        var provider = new YahooQuoteProvider(new QuoteStore(path));
        var inst = new QuoteInstrument(QuoteSource.Yahoo, "^GSPC", "^GSPC", "", "", "");
        Assert.Empty(await provider.FetchAsync(new[] { inst }, CancellationToken.None));
    }

    [Fact]
    public void Presets_HaveFourGroupsWithinTheLimit()
    {
        Assert.Equal(4, YahooPresets.All.Count);
        Assert.True(YahooPresets.All.Sum(g => g.Items.Count) <= YahooAccess.MaxInstruments);
    }
}
