using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class BarSlotsTests
{
    [Fact]
    public void Spec_RoundTrips()
    {
        foreach (var spec in new[] { "news", "news:user-1", "stocks", "weather" })
            Assert.Equal(spec, BarSlot.Parse(spec)!.Value.ToSpec());
        Assert.Null(BarSlot.Parse(""));
        Assert.Null(BarSlot.Parse("bogus"));
    }

    [Fact]
    public void LegacyBoth_KeepsOrder()
    {
        var left = BarSpec.FromLegacy(BarContent.Both, stocksLeft: true);
        Assert.Equal(SlotKind.Stocks, left.A.Kind);
        Assert.Equal(SlotKind.News, left.B!.Value.Kind);
        var right = BarSpec.FromLegacy(BarContent.Both, stocksLeft: false);
        Assert.Equal(SlotKind.News, right.A.Kind);
    }

    [Fact]
    public void Resolve_PrefersSavedSpecs_AndDropsDuplicateSecondPart()
    {
        var slots = BarSpec.Resolve("weather", "news:x", BarContent.News, false);
        Assert.Equal(SlotKind.Weather, slots.A.Kind);
        Assert.Equal("x", slots.B!.Value.Category);
        Assert.Null(BarSpec.Resolve("stocks", "stocks", BarContent.News, false).B);
        Assert.Equal(SlotKind.Weather, BarSpec.Resolve("", "", BarContent.Weather, false).A.Kind);
    }

    [Fact]
    public void Clean_ReplacesUnknownCategory()
    {
        Assert.Equal("news", BarSpec.Clean("news:gone", _ => false));
        Assert.Equal("news:here", BarSpec.Clean("news:here", _ => true));
        Assert.Equal("", BarSpec.Clean("junk", _ => true));
    }
}
