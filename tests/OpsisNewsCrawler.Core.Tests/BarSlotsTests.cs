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

public class FeedSlotTests
{
    [Fact]
    public void FeedSpecRoundTrips()
    {
        var slot = BarSlot.Parse("feed:san-simera");
        Assert.NotNull(slot);
        Assert.Equal(SlotKind.Feed, slot!.Value.Kind);
        Assert.Equal("san-simera", slot.Value.Category);
        Assert.Equal("feed:san-simera", slot.Value.ToSpec());
        Assert.Null(BarSlot.Parse("feed:"));
    }

    [Fact]
    public void FeedCountsAsNewsForLegacy()
    {
        Assert.Equal(BarContent.Both, new BarSlots(new BarSlot(SlotKind.Feed, "x"), new BarSlot(SlotKind.Stocks)).ToLegacy());
        Assert.Equal(BarContent.News, new BarSlots(new BarSlot(SlotKind.Feed, "x"), null).ToLegacy());
        Assert.Equal("feed:x", BarSpec.Clean("feed:x", _ => false));
    }
}
