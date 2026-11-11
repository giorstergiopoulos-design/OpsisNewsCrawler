using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class TaskbarMaintenanceTests
{
    [Fact]
    public void AStillTaskbarAndABarOnTopAreNotTouchedExceptForTheCheapReposition()
    {
        for (var tick = 1; tick < 2000; tick++)
        {
            var a = TaskbarMaintenance.Decide(tick, taskbarStyleChanged: false, shellFlyoutHasFocus: false, alreadyOnTop: true);
            Assert.False(a.KeepOnTop);     // no z-order change when the bar is on top
            if (tick % TaskbarMaintenance.LookRefreshEvery != 0) Assert.False(a.ApplyLook);   // the backdrop is not set again every second
            Assert.Equal(tick % TaskbarMaintenance.CheckEvery == 0, a.Reposition);
        }
    }

    [Fact]
    public void TheBarGoesBackOnTopOnlyWhenSomethingCoveredItAndOnlyAtACheck()
    {
        Assert.True(TaskbarMaintenance.Decide(10, false, false, alreadyOnTop: false).KeepOnTop);
        Assert.True(TaskbarMaintenance.Decide(30, false, false, alreadyOnTop: false).KeepOnTop);
        Assert.False(TaskbarMaintenance.Decide(31, false, false, alreadyOnTop: false).KeepOnTop);
    }

    [Fact]
    public void NothingIsTouchedWhileTheUserUsesAShellFlyout()
    {
        // the tray overflow window is open: re-asserting the z-order and the backdrop then is what made the bar flicker
        foreach (var tick in new[] { 30, 60, 900, 1800 })
        {
            var a = TaskbarMaintenance.Decide(tick, taskbarStyleChanged: true, shellFlyoutHasFocus: true, alreadyOnTop: false);
            Assert.Equal(new TaskbarMaintenance.Actions(false, false, false), a);
        }
    }

    [Fact]
    public void AChangeOfTheTaskbarStyleAppliesTheLookAtOnce_AndAnUnconditionalRefreshIsRare()
    {
        Assert.True(TaskbarMaintenance.Decide(31, taskbarStyleChanged: true, shellFlyoutHasFocus: false, alreadyOnTop: true).ApplyLook);
        var refreshes = Enumerable.Range(1, 9000).Count(t => TaskbarMaintenance.Decide(t, false, false, true).ApplyLook);
        Assert.Equal(10, refreshes);   // 9000 ticks (many minutes): ten re-applications, not 300
    }

    [Theory]
    [InlineData("TopLevelWindowForOverflowXamlIsland", true)]
    [InlineData("NotifyIconOverflowWindow", true)]
    [InlineData("topLevelWindowForOverflowXamlIsland", true)]
    [InlineData("Notepad", false)]
    [InlineData("Shell_TrayWnd", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ShellFlyoutClassesAreRecognised(string? name, bool expected) => Assert.Equal(expected, TaskbarMaintenance.IsShellFlyout(name));
}
