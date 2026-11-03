namespace OpsisNewsCrawler.Core;

public enum PerformanceTier { Low, Balanced, High }

/// <summary>The setting "Performance": Automatic follows the detected hardware, the others force a tier.</summary>
public enum PerformanceMode { Auto, Low, Balanced, High }

/// <summary>What was found about this PC (a few quick Win32 readings, no WMI, no hardware monitor).</summary>
public sealed record HardwareSpecs(int LogicalCores, double RamGb, bool HasGpu, bool OnBattery, bool BatterySaver);

public enum TrayAnimation { Off, Slow, Full }

/// <summary>
/// What each tier changes. One table, applied in one place (<c>TickerHost.ApplyPerformance</c>):
/// scroll step (30 / 50 / 66 frames per second), how often the taskbar is polled, the splash and minimise animations, the notification-area icon
/// (still / every other frame / every frame) and how often feeds, prices and weather are refreshed (a factor on the user's intervals).
/// The scroll speed the user sees never changes: the scroll is time based.
/// </summary>
public sealed record PerformanceProfile(PerformanceTier Tier, int ScrollIntervalMs, int TaskbarPollMs, bool SimpleAnimations, TrayAnimation Tray, double RefreshFactor)
{
    public static PerformanceProfile For(PerformanceTier tier) => tier switch
    {
        PerformanceTier.Low => new(PerformanceTier.Low, 33, 90, true, TrayAnimation.Off, 2.0),
        PerformanceTier.Balanced => new(PerformanceTier.Balanced, 20, 45, false, TrayAnimation.Slow, 1.0),
        _ => new(PerformanceTier.High, 15, 30, false, TrayAnimation.Full, 1.0),
    };

    /// <summary>The profile in use (set by the host after each evaluation; read by the tray icon, the timers and the animations).</summary>
    public static PerformanceProfile Current { get; set; } = For(PerformanceTier.High);

    /// <summary>A refresh interval (seconds) scaled for the tier, never shorter than the user's own value.</summary>
    public int Scaled(int seconds) => (int)Math.Min(int.MaxValue / 1000, Math.Max(seconds, Math.Round(seconds * RefreshFactor)));
}

/// <summary>Which tier a set of specs gets, and why (a code the settings page turns into a sentence in the user's language).</summary>
public static class PerformanceClassifier
{
    public const string ReasonCores = "cores", ReasonRam = "ram", ReasonGpu = "gpu", ReasonSaver = "saver", ReasonBattery = "battery", ReasonEnough = "enough",
        ReasonForced = "forced", ReasonSetting = "setting", ReasonHost = "host", ReasonUnknown = "unknown";

    public static (PerformanceTier Tier, string Reason) Classify(HardwareSpecs s)
    {
        if (s.BatterySaver) return (PerformanceTier.Low, ReasonSaver);
        if (!s.HasGpu) return (PerformanceTier.Low, ReasonGpu);                // software rendering only
        if (s.LogicalCores <= 2) return (PerformanceTier.Low, ReasonCores);
        if (s.RamGb < 6) return (PerformanceTier.Low, ReasonRam);
        if (s.OnBattery) return (PerformanceTier.Balanced, ReasonBattery);     // the full effects are for a plugged-in PC
        if (s.LogicalCores < 8) return (PerformanceTier.Balanced, ReasonCores);
        if (s.RamGb < 12) return (PerformanceTier.Balanced, ReasonRam);
        return (PerformanceTier.High, ReasonEnough);
    }

    public static PerformanceMode ParseMode(string? text) => (text ?? "").Trim().ToLowerInvariant() switch
    {
        "low" => PerformanceMode.Low, "balanced" => PerformanceMode.Balanced, "high" => PerformanceMode.High, _ => PerformanceMode.Auto
    };

    public static string ModeText(PerformanceMode mode) => mode.ToString().ToLowerInvariant();

    /// <summary>"Low" / "Balanced" / "High" (any case) from a test variable or from the host; null when it is none of them.</summary>
    public static PerformanceTier? ParseTier(string? text) => (text ?? "").Trim().ToLowerInvariant() switch
    {
        "low" => PerformanceTier.Low, "balanced" or "medium" => PerformanceTier.Balanced, "high" => PerformanceTier.High, _ => null
    };
}

/// <summary>
/// Chooses the tier now and then: a test variable beats everything, then the user's setting, then (inside Opsis) the tier Opsis reports, then the
/// detection. Going down is immediate (a PC that gets slow must not wait); going up needs <see cref="UpgradeConfirmations"/> equal readings in a row,
/// so a PC does not flicker between two tiers.
/// </summary>
public sealed class PerformanceController
{
    public const int UpgradeConfirmations = 2;

    private readonly Func<HardwareSpecs> detect;
    private int upgradeVotes;

    public PerformanceController(Func<HardwareSpecs> detect) => this.detect = detect;

    public PerformanceTier Tier { get; private set; } = PerformanceTier.Balanced;
    public string Reason { get; private set; } = PerformanceClassifier.ReasonUnknown;
    public HardwareSpecs? Specs { get; private set; }
    public bool HasEvaluated { get; private set; }

    /// <summary>One evaluation; returns true when the tier changed.</summary>
    public bool Evaluate(PerformanceMode mode, PerformanceTier? hostTier, PerformanceTier? forced)
    {
        PerformanceTier target;
        string reason;
        if (forced is { } f) { target = f; reason = PerformanceClassifier.ReasonForced; upgradeVotes = UpgradeConfirmations; }
        else if (mode != PerformanceMode.Auto) { target = (PerformanceTier)(mode - 1); reason = PerformanceClassifier.ReasonSetting; upgradeVotes = UpgradeConfirmations; }
        else if (hostTier is { } h) { target = h; reason = PerformanceClassifier.ReasonHost; }
        else
        {
            HardwareSpecs specs;
            try { specs = detect(); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or DllNotFoundException or EntryPointNotFoundException or NotSupportedException or PlatformNotSupportedException)
            {
                // Detection failed: keep what is in use, or a middle tier at the very start; never an exception into the program.
                if (!HasEvaluated) { Tier = PerformanceTier.Balanced; Reason = PerformanceClassifier.ReasonUnknown; HasEvaluated = true; return true; }
                return false;
            }
            Specs = specs;
            (target, reason) = PerformanceClassifier.Classify(specs);
        }

        var first = !HasEvaluated;
        HasEvaluated = true;
        if (first || target < Tier || (target > Tier && forced is not null) || (target > Tier && mode != PerformanceMode.Auto))
        {
            upgradeVotes = 0;
            return Apply(target, reason);
        }
        if (target > Tier)
        {
            upgradeVotes++;
            if (upgradeVotes < UpgradeConfirmations) { return false; }
            upgradeVotes = 0;
            return Apply(target, reason);
        }
        upgradeVotes = 0;
        Reason = reason;            // same tier: the reason may have changed (the PC was unplugged and is still Balanced)
        return false;
    }

    private bool Apply(PerformanceTier target, string reason)
    {
        var changed = target != Tier || !HasChangedOnce;
        Tier = target;
        Reason = reason;
        HasChangedOnce = true;
        return changed;
    }

    private bool HasChangedOnce { get; set; }
}
