using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>The performance tier with simulated specs: the free GitHub runner (2 vCPU, ~7 GB, no GPU), a laptop, a strong desktop, a failing detection.</summary>
public class PerformanceTests
{
    private static HardwareSpecs Runner => new(2, 7, false, false, false);          // GitHub windows runner
    private static HardwareSpecs Strong => new(16, 32, true, false, false);

    [Fact]
    public void TheFreeRunnerIsALowPc()
    {
        var (tier, reason) = PerformanceClassifier.Classify(Runner);
        Assert.Equal(PerformanceTier.Low, tier);
        Assert.Equal(PerformanceClassifier.ReasonGpu, reason);
    }

    [Theory]
    [InlineData(2, 16, true, false, false, PerformanceTier.Low, "cores")]
    [InlineData(8, 4, true, false, false, PerformanceTier.Low, "ram")]
    [InlineData(16, 32, true, false, true, PerformanceTier.Low, "saver")]       // battery saver beats everything
    [InlineData(16, 32, false, false, false, PerformanceTier.Low, "gpu")]
    [InlineData(4, 8, true, false, false, PerformanceTier.Balanced, "cores")]
    [InlineData(8, 8, true, false, false, PerformanceTier.Balanced, "ram")]
    [InlineData(16, 32, true, true, false, PerformanceTier.Balanced, "battery")]  // strong but unplugged
    [InlineData(8, 12, true, false, false, PerformanceTier.High, "enough")]
    [InlineData(16, 32, true, false, false, PerformanceTier.High, "enough")]
    public void ClassifierTable(int cores, double ram, bool gpu, bool battery, bool saver, PerformanceTier tier, string reason)
    {
        var result = PerformanceClassifier.Classify(new HardwareSpecs(cores, ram, gpu, battery, saver));
        Assert.Equal((tier, reason), result);
    }

    [Fact]
    public void EveryTierHasItsOwnProfileAndTheLowOneIsTheGentlest()
    {
        var low = PerformanceProfile.For(PerformanceTier.Low);
        var balanced = PerformanceProfile.For(PerformanceTier.Balanced);
        var high = PerformanceProfile.For(PerformanceTier.High);
        Assert.True(low.ScrollIntervalMs > balanced.ScrollIntervalMs && balanced.ScrollIntervalMs > high.ScrollIntervalMs);
        Assert.True(low.TaskbarPollMs > balanced.TaskbarPollMs && balanced.TaskbarPollMs > high.TaskbarPollMs);
        Assert.True(low.SimpleAnimations && !balanced.SimpleAnimations && !high.SimpleAnimations);
        Assert.Equal((TrayAnimation.Off, TrayAnimation.Slow, TrayAnimation.Full), (low.Tray, balanced.Tray, high.Tray));
        Assert.True(low.RefreshFactor > high.RefreshFactor);
        Assert.Equal(1.0, high.RefreshFactor);
    }

    [Fact]
    public void ScaledIntervalsNeverGetShorterThanTheUsers()
    {
        foreach (var tier in Enum.GetValues<PerformanceTier>())
            foreach (var seconds in new[] { 1, 60, 300, 3600 })
                Assert.True(PerformanceProfile.For(tier).Scaled(seconds) >= seconds);
        Assert.Equal(600, PerformanceProfile.For(PerformanceTier.Low).Scaled(300));
        Assert.Equal(300, PerformanceProfile.For(PerformanceTier.High).Scaled(300));
    }

    [Fact]
    public void ControllerStartsWithTheDetectedTier()
    {
        var c = new PerformanceController(() => Runner);
        Assert.True(c.Evaluate(PerformanceMode.Auto, null, null));
        Assert.Equal(PerformanceTier.Low, c.Tier);
        Assert.Equal(Runner, c.Specs);
    }

    [Fact]
    public void GoingDownIsImmediateAndGoingUpNeedsTwoReadings()
    {
        var current = Strong;
        var c = new PerformanceController(() => current);
        c.Evaluate(PerformanceMode.Auto, null, null);
        Assert.Equal(PerformanceTier.High, c.Tier);

        current = Strong with { BatterySaver = true };                       // battery saver: down at once
        Assert.True(c.Evaluate(PerformanceMode.Auto, null, null));
        Assert.Equal(PerformanceTier.Low, c.Tier);

        current = Strong;                                                    // plugged in again: not yet ...
        Assert.False(c.Evaluate(PerformanceMode.Auto, null, null));
        Assert.Equal(PerformanceTier.Low, c.Tier);
        Assert.True(c.Evaluate(PerformanceMode.Auto, null, null));           // ... the second equal reading brings it back up by itself
        Assert.Equal(PerformanceTier.High, c.Tier);
    }

    [Fact]
    public void AFlickeringReadingDoesNotCauseAChange()
    {
        var plugged = true;
        var c = new PerformanceController(() => Strong with { OnBattery = !plugged });
        c.Evaluate(PerformanceMode.Auto, null, null);                        // High
        plugged = false; Assert.True(c.Evaluate(PerformanceMode.Auto, null, null)); Assert.Equal(PerformanceTier.Balanced, c.Tier);
        plugged = true; Assert.False(c.Evaluate(PerformanceMode.Auto, null, null));   // one reading up ...
        plugged = false; Assert.False(c.Evaluate(PerformanceMode.Auto, null, null));  // ... and down again: nothing changed, no flicker
        Assert.Equal(PerformanceTier.Balanced, c.Tier);
    }

    [Fact]
    public void TheSettingBeatsTheDetectionAndAppliesAtOnceBothWays()
    {
        var c = new PerformanceController(() => Strong);
        c.Evaluate(PerformanceMode.Low, null, null);
        Assert.Equal((PerformanceTier.Low, PerformanceClassifier.ReasonSetting), (c.Tier, c.Reason));
        Assert.True(c.Evaluate(PerformanceMode.High, null, null));           // forcing UP is not delayed
        Assert.Equal(PerformanceTier.High, c.Tier);
    }

    [Fact]
    public void TheTestVariableBeatsEverything()
    {
        var c = new PerformanceController(() => Strong);
        c.Evaluate(PerformanceMode.High, PerformanceTier.Balanced, PerformanceTier.Low);
        Assert.Equal((PerformanceTier.Low, PerformanceClassifier.ReasonForced), (c.Tier, c.Reason));
    }

    [Fact]
    public void InsideOpsisTheTierOfOpsisDecidesWhileTheSettingIsAutomatic()
    {
        var c = new PerformanceController(() => Strong);
        c.Evaluate(PerformanceMode.Auto, PerformanceTier.Low, null);
        Assert.Equal((PerformanceTier.Low, PerformanceClassifier.ReasonHost), (c.Tier, c.Reason));
        c.Evaluate(PerformanceMode.Balanced, PerformanceTier.Low, null);     // a choice of the user still wins
        Assert.Equal(PerformanceTier.Balanced, c.Tier);
    }

    [Fact]
    public void AFailingDetectionNeverThrowsAndKeepsAMiddleTier()
    {
        var c = new PerformanceController(() => throw new InvalidOperationException("no WMI"));
        Assert.True(c.Evaluate(PerformanceMode.Auto, null, null));
        Assert.Equal((PerformanceTier.Balanced, PerformanceClassifier.ReasonUnknown), (c.Tier, c.Reason));
        Assert.False(c.Evaluate(PerformanceMode.Auto, null, null));          // and keeps it
        var d = new PerformanceController(() => throw new DllNotFoundException("user32"));
        d.Evaluate(PerformanceMode.Auto, null, null);
        Assert.Equal(PerformanceTier.Balanced, d.Tier);
    }

    [Fact]
    public void FailureAfterAGoodReadingKeepsTheTierInUse()
    {
        var fail = false;
        var c = new PerformanceController(() => fail ? throw new InvalidOperationException() : Runner);
        c.Evaluate(PerformanceMode.Auto, null, null);
        fail = true;
        Assert.False(c.Evaluate(PerformanceMode.Auto, null, null));
        Assert.Equal(PerformanceTier.Low, c.Tier);
    }

    [Theory]
    [InlineData("low", PerformanceMode.Low)]
    [InlineData("LOW ", PerformanceMode.Low)]
    [InlineData("Balanced", PerformanceMode.Balanced)]
    [InlineData("high", PerformanceMode.High)]
    [InlineData("auto", PerformanceMode.Auto)]
    [InlineData("nonsense", PerformanceMode.Auto)]
    [InlineData(null, PerformanceMode.Auto)]
    public void ModeParsing(string? text, PerformanceMode expected) => Assert.Equal(expected, PerformanceClassifier.ParseMode(text));

    [Fact]
    public void TierParsingForTheTestVariableAndTheHost()
    {
        Assert.Equal(PerformanceTier.Low, PerformanceClassifier.ParseTier("Low"));
        Assert.Equal(PerformanceTier.Balanced, PerformanceClassifier.ParseTier("medium"));
        Assert.Equal(PerformanceTier.High, PerformanceClassifier.ParseTier(" HIGH "));
        Assert.Null(PerformanceClassifier.ParseTier("turbo"));
        Assert.Null(PerformanceClassifier.ParseTier(null));
    }

    [Fact]
    public void TheConfigKeepsOnlyAKnownMode()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "config.json"), "{\"PerformanceMode\":\"turbo\"}");
            Assert.Equal("auto", AppConfig.Load(Path.Combine(dir, "config.json")).PerformanceMode);
            File.WriteAllText(Path.Combine(dir, "config.json"), "{\"PerformanceMode\":\"LOW\"}");
            Assert.Equal("low", AppConfig.Load(Path.Combine(dir, "config.json")).PerformanceMode);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EveryTextOfTheSettingsExistsInTheLanguageFiles()
    {
        // LocaleFilesTests checks the keys of all 14 languages against en.json; this one makes sure the performance keys are in the English file at all
        foreach (var key in new[] { "card.performance", "perf.mode", "perf.auto", "perf.low", "perf.balanced", "perf.high", "perf.note", "perf.detected", "perf.gpuYes", "perf.gpuNo", "perf.now",
                     "perf.r.cores", "perf.r.ram", "perf.r.gpu", "perf.r.saver", "perf.r.battery", "perf.r.enough", "perf.r.forced", "perf.r.setting", "perf.r.host", "perf.r.unknown" })
            Assert.False(string.IsNullOrEmpty(Loc.T(key)) || Loc.T(key) == key, key);
    }
}
