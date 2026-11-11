namespace OpsisNewsCrawler.Core;

/// <summary>
/// The time step of the scroll: the distance moved is always speed x real time, so the speed the user chose does not change when the computer is busy
/// (open windows, a slow paint, a late timer). The old step was cut at 0.1 s: when the interface stalled for longer, the strip lost the rest and looked slower.
/// A longer stall is still limited (<see cref="MaxStepSeconds"/>) so that waking from sleep or a debugger break does not throw the strip far ahead.
/// </summary>
public sealed class ScrollStepper
{
    public const double MaxStepSeconds = 0.75;

    private double last;
    private bool started;

    /// <summary>Starts again from this moment (after a pause, when the bar is shown again): the next step is small.</summary>
    public void Restart(double nowSeconds)
    {
        last = nowSeconds;
        started = true;
    }

    /// <summary>The seconds that passed since the previous call, never negative and never above <see cref="MaxStepSeconds"/>.</summary>
    public double Step(double nowSeconds)
    {
        if (!started) { Restart(nowSeconds); return 0; }
        var dt = Math.Clamp(nowSeconds - last, 0, MaxStepSeconds);
        last = nowSeconds;
        return dt;
    }
}
