using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class ContrastTextTests
{
    private static double L(byte v) => ContrastChooser.RelativeLuminance(v, v, v);

    [Fact]
    public void LuminanceAndRatioFollowWcag()
    {
        Assert.Equal(0, L(0), 6);
        Assert.Equal(1, L(255), 6);
        Assert.Equal(21, ContrastChooser.ContrastRatio(1, 0), 6);
        Assert.Equal(1, ContrastChooser.ContrastRatio(0.4, 0.4), 6);
        Assert.Equal(0.2126, ContrastChooser.RelativeLuminance(255, 0, 0), 4);
    }

    [Theory]
    [InlineData(0, false)]      // black background: light text
    [InlineData(40, false)]     // dark
    [InlineData(255, true)]     // white: dark text
    [InlineData(220, true)]     // light
    public void LightAndDarkBackgroundsGetTheOppositeText(byte gray, bool expectDark)
    {
        var c = new ContrastChooser();
        c.Update(L(gray));
        Assert.Equal(expectDark, c.UseDarkText);
        Assert.True(ContrastChooser.Contrast(L(gray), expectDark) >= 4.5, "contrast under WCAG 4.5:1");
    }

    [Fact]
    public void EveryBackgroundGetsAtLeastWcagContrast()
    {
        // the worst case is the middle: both colours reach about 4.6:1 there, never lower
        for (var gray = 0; gray <= 255; gray++)
        {
            var c = new ContrastChooser();
            c.Update(L((byte)gray));
            var best = ContrastChooser.Contrast(L((byte)gray), c.UseDarkText!.Value);
            Assert.True(best >= 4.5, $"gray {gray}: {best:0.00}");
        }
    }

    [Fact]
    public void ABackgroundThatHoversAroundTheLimitDoesNotMakeTheTextBlink()
    {
        var c = new ContrastChooser();
        c.Update(0.05);                                   // dark: light text
        Assert.False(c.UseDarkText);
        var changes = 0;
        foreach (var l in new[] { 0.16, 0.20, 0.17, 0.21, 0.19, 0.15, 0.22 }) if (c.Update(l)) changes++;
        Assert.Equal(0, changes);
        Assert.False(c.UseDarkText);
    }

    [Fact]
    public void ARealChangeNeedsTwoAgreeingSamples()
    {
        var c = new ContrastChooser();
        c.Update(0.05);
        Assert.False(c.Update(0.6));                      // one bright sample (a window passed behind the bar): nothing yet
        Assert.True(c.IsPending);                         // ... so the bar looks again after a second, not at the next slow look
        Assert.False(c.Update(0.05));                     // and a dark one cancels it
        Assert.False(c.Update(0.6));
        Assert.True(c.Update(0.7));                       // two bright samples in a row: the text turns dark
        Assert.True(c.UseDarkText);
        Assert.False(c.Update(0.7));
    }

    [Fact]
    public void TheMedianIgnoresTheFewPixelsOfTextAndIcons()
    {
        var samples = Enumerable.Repeat(0.02, 90).Concat(Enumerable.Repeat(0.95, 10)).ToList();   // dark strip with 10 % bright text
        Assert.Equal(0.02, ContrastChooser.Median(samples), 6);
        Assert.Equal(0.5, ContrastChooser.Median(Array.Empty<double>()));
    }

    [Fact]
    public void TheSettingIsCleaned()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "config.json");
        try
        {
            File.WriteAllText(path, "{\"TextColorMode\":\"FIXED\"}");
            Assert.Equal("fixed", AppConfig.Load(path).TextColorMode);
            File.WriteAllText(path, "{\"TextColorMode\":\"banana\"}");
            Assert.Equal("auto", AppConfig.Load(path).TextColorMode);
            Assert.Equal("auto", new AppConfig().TextColorMode);
            Assert.False(new AppConfig().TextShadow);
        }
        finally { Directory.Delete(dir, true); }
    }
}
