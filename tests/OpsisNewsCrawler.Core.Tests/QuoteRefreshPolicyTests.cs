using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class QuoteRefreshPolicyTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static QuoteInstrument Instrument(QuoteSource source) => new(source, "TEST", "T", "Test", "Test", "");

    [Fact]
    public void LiveSources_RespectConfiguredInterval()
    {
        var items = new[] { Instrument(QuoteSource.Metals) };
        Assert.Equal(120, QuoteRefreshPolicy.GetIntervalSeconds(items, 120,
            new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), Utc));
    }

    [Fact]
    public void AthensDuringWeekdaySession_UsesLiveCadenceUnlessConfiguredSlower()
    {
        var items = new[] { Instrument(QuoteSource.Athens) };
        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(60, QuoteRefreshPolicy.GetIntervalSeconds(items, 30, now, Utc));
        Assert.Equal(180, QuoteRefreshPolicy.GetIntervalSeconds(items, 180, now, Utc));
    }

    [Theory]
    [InlineData(2026, 10, 17, 12, 900)]
    [InlineData(2026, 10, 15, 20, 900)]
    public void AthensOutsideSession_UsesSlowerCadence(int year, int month, int day, int hour, int expected)
    {
        var items = new[] { Instrument(QuoteSource.Athens) };
        var now = new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, QuoteRefreshPolicy.GetIntervalSeconds(items, 60, now, Utc));
    }

    [Fact]
    public void EndOfDayAndDailySources_UseSlowerIntervals()
    {
        var now = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(3600, QuoteRefreshPolicy.GetIntervalSeconds(new[] { Instrument(QuoteSource.Euronext) }, 60, now, Utc));
        Assert.Equal(21600, QuoteRefreshPolicy.GetIntervalSeconds(new[] { Instrument(QuoteSource.Currency) }, 60, now, Utc));
        Assert.Equal(60, QuoteRefreshPolicy.GetIntervalSeconds(new[] { Instrument(QuoteSource.Energy) }, 60, now, Utc)); // near-live Pyth Hermes
        Assert.Equal(60, QuoteRefreshPolicy.GetIntervalSeconds(new[] { Instrument(QuoteSource.Athens), Instrument(QuoteSource.Metals) }, 60, now, Utc));
    }

    [Fact]
    public void DisabledInstrumentsDoNotAffectTheInterval()
    {
        var item = Instrument(QuoteSource.Metals);
        item.Enabled = false;
        Assert.Equal(21600, QuoteRefreshPolicy.GetIntervalSeconds(new[] { item, Instrument(QuoteSource.Currency) }, 60,
            new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), Utc));
    }
}
