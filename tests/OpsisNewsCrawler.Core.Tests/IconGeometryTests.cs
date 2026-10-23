using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class IconGeometryTests
{
    [Theory]
    [InlineData(42, 168)]
    [InlineData(63, 252)]
    [InlineData(84, 336)]
    public void WideIconIsFourTimesAsWideAsHigh(int height, int width) =>
        Assert.Equal(width, IconGeometry.WideWidthFor(height));

    [Fact]
    public void WideIconIsNeverNarrowerThanHigh() =>
        Assert.True(IconGeometry.WideWidthFor(1) >= 1);

    [Theory]
    [InlineData(168, 38)]
    [InlineData(40, 40)]
    [InlineData(10, 10)]
    public void CornerRadiusIsSmallAndNeverACircle(int width, int height)
    {
        var r = IconGeometry.CornerRadius(width, height);
        Assert.True(r >= 2f);
        Assert.True(r <= Math.Min(width, height) * 0.2f + 2f);
        Assert.True(r * 2 < Math.Min(width, height) || Math.Min(width, height) <= 4, "radius must stay below half of the short side");
    }

    [Theory]
    [InlineData(1000, 42, 38, 152)]
    [InlineData(1000, 84, 80, 320)]
    public void IntroUsesTheBarHeight(int barWidth, int barHeight, int expectedHeight, int expectedWidth)
    {
        var (w, h) = IconGeometry.IntroSize(barWidth, barHeight);
        Assert.Equal(expectedHeight, h);
        Assert.Equal(expectedWidth, w);
    }

    [Fact]
    public void IntroNeverExceedsANarrowBar()
    {
        var (w, h) = IconGeometry.IntroSize(100, 42);
        Assert.True(w <= 100 - 8);
        Assert.True(w >= h);
    }

    [Fact]
    public void IntroSurvivesTinyBars()
    {
        var (w, h) = IconGeometry.IntroSize(0, 0);
        Assert.True(w >= 8 && h >= 8);
    }

    [Theory]
    [InlineData(32, 16, 64, 64, 32)]   // wide picture: full width, half height
    [InlineData(16, 32, 64, 32, 64)]   // tall picture: full height
    [InlineData(48, 48, 64, 64, 64)]   // square: fills the box (scaled up from 48)
    public void FaviconIsFittedNotCropped(float sw, float sh, float box, float expectedW, float expectedH)
    {
        var (w, h) = IconGeometry.FitInside(sw, sh, box);
        Assert.Equal(expectedW, w, 3);
        Assert.Equal(expectedH, h, 3);
    }

    [Fact]
    public void FitInsideHandlesAnEmptySource()
    {
        var (w, h) = IconGeometry.FitInside(0, 0, 64);
        Assert.Equal(64f, w);
        Assert.Equal(64f, h);
    }

    [Theory]
    [InlineData(1f, 20)]
    [InlineData(1.5f, 30)]
    [InlineData(2f, 40)]
    [InlineData(0.7f, 16)]
    public void ItemIconFollowsTheBarButHasAFloor(float scale, int size) =>
        Assert.Equal(size, IconGeometry.ItemIconSize(scale));
}
