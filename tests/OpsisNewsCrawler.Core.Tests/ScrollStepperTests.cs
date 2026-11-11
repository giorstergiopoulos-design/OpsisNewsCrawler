using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class ScrollStepperTests
{
    private const double Speed = 60;   // pixels per second

    private static double Distance(IEnumerable<double> times)
    {
        var s = new ScrollStepper();
        double sum = 0;
        foreach (var t in times) sum += Speed * s.Step(t);
        return sum;
    }

    [Fact]
    public void SteadyTicksMoveSpeedTimesTime()
    {
        var times = Enumerable.Range(0, 1001).Select(i => i * 0.016).ToList();   // 16 s of 16 ms ticks
        Assert.Equal(Speed * 16, Distance(times), 3);
    }

    [Fact]
    public void ABusyComputerDoesNotChangeTheSpeed()
    {
        // ticks that arrive late and unevenly (open windows, slow paints): 15 ms, 40 ms, 120 ms, 300 ms ... the distance after 20 s is still speed x time
        var rng = new Random(11);
        var t = 0.0;
        var times = new List<double> { 0 };
        while (t < 20) { t += new[] { 0.015, 0.04, 0.12, 0.3, 0.016 }[rng.Next(5)]; times.Add(t); }
        Assert.Equal(Speed * times[^1], Distance(times), 3);
    }

    [Fact]
    public void TheOldStepCutAt100msLostDistanceWhenTheInterfaceStalled()
    {
        // documents the bug: with a cut at 0.1 s a bar whose ticks arrive every 0.25 s moves at 40 % of the chosen speed
        var times = Enumerable.Range(0, 81).Select(i => i * 0.25).ToList();   // 20 s
        var old = 0.0; var last = 0.0;
        foreach (var t in times.Skip(1)) { old += Speed * Math.Min(t - last, 0.1); last = t; }
        Assert.True(old < Speed * 20 * 0.45);
        Assert.Equal(Speed * 20, Distance(times), 3);
    }

    [Fact]
    public void ALongStallIsLimitedAndAfterRestartTheNextStepIsSmall()
    {
        var s = new ScrollStepper();
        Assert.Equal(0, s.Step(10));                                   // the first call only starts the clock
        Assert.Equal(ScrollStepper.MaxStepSeconds, s.Step(3600));      // woke from sleep: no jump of an hour
        s.Restart(5000);
        Assert.Equal(0.016, s.Step(5000.016), 6);                      // paused and shown again: a normal small step
        Assert.Equal(0, s.Step(4000));                                 // a clock that goes back never moves the strip backwards
    }
}
