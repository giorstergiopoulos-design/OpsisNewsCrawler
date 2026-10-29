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

public sealed class BarSpeedConfigTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(5, 10)]
    [InlineData(60, 60)]
    [InlineData(9999, 400)]
    public void SpeedIsZeroOrWithinTheLimits(int input, int expected) => Assert.Equal(expected, AppConfig.CleanSpeed(input));

    [Fact]
    public void SavedSpeedsAreCleanedOnLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opsis-speed-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new AppConfig { ScrollSpeed = 80, MainSpeedB = 3, SecondSpeed = 120, SecondSpeedB = -1, MiniSpeed = 9999, MonitorSpeed = 40 }));
            var c = AppConfig.Load(path);
            Assert.Equal(80, c.ScrollSpeed);
            Assert.Equal(10, c.MainSpeedB);
            Assert.Equal(120, c.SecondSpeed);
            Assert.Equal(0, c.SecondSpeedB);
            Assert.Equal(400, c.MiniSpeed);
            Assert.Equal(0, c.MiniSpeedB);
            Assert.Equal(40, c.MonitorSpeed);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
