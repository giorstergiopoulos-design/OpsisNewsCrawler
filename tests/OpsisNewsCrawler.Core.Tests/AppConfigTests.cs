using System.Text.Json;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class AppConfigTests
{

    [Fact]
    public void Load_ClampsNewsAgeAndKeepsNewestFirstPreference()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opsis-config-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new AppConfig
            {
                NewsMaxAgeHours = 9999,
                NewsSortNewestFirst = true
            }));
            var config = AppConfig.Load(path);
            Assert.Equal(720, config.NewsMaxAgeHours);
            Assert.True(config.NewsSortNewestFirst);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_NormalizesMutedKeywordsAndLimitsCount()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opsis-config-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new AppConfig { MutedNewsKeywords = new() { " sport ", "sport", "", "politics" } }));
            var config = AppConfig.Load(path);
            Assert.Equal(new[] { "sport", "politics" }, config.MutedNewsKeywords);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_NormalizesAthensMarketAndCollapsesDuplicateWatchlistEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opsis-config-{Guid.NewGuid():N}.json");
        try
        {
            var duplicateA = new QuoteInstrument(QuoteSource.Athens, "GRS003003035", "ETE", "ETE", "ETE", "€", false);
            var duplicateB = new QuoteInstrument(QuoteSource.Athens, "grs003003035", "ETE", "ETE", "ETE", "€", true);
            File.WriteAllText(path, JsonSerializer.Serialize(new AppConfig { Watchlist = new() { duplicateA, duplicateB } }));
            var config = AppConfig.Load(path);
            var ete = config.Watchlist.Where(x => x.Source == QuoteSource.Athens && x.Code.Equals("GRS003003035", StringComparison.OrdinalIgnoreCase)).ToList();
            var instrument = Assert.Single(ete);
            Assert.Equal("XATH", instrument.Market);
            Assert.True(instrument.Enabled);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
