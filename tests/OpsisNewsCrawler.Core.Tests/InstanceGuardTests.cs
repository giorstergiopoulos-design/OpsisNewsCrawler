using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class InstanceGuardTests
{
    private static string Unique() => "OpsisNewsCrawler.Test." + Guid.NewGuid().ToString("N");

    [Fact]
    public void OnlyOneOwnerAtATime()
    {
        var name = Unique();
        using var standalone = new InstanceGuard();
        using var plugin = new InstanceGuard();
        Assert.True(standalone.TryAcquire(name));
        Assert.False(plugin.TryAcquire(name));        // the plugin must not start a second bar
        Assert.False(plugin.IsOwner);
        standalone.Release();
        Assert.True(plugin.TryAcquire(name));          // after the first one left, the other may start
    }

    [Fact]
    public void AcquiringTwiceBySameOwnerIsHarmless()
    {
        var name = Unique();
        using var guard = new InstanceGuard();
        Assert.True(guard.TryAcquire(name));
        Assert.True(guard.TryAcquire(name));
        guard.Release();
        guard.Release();
        Assert.False(guard.IsOwner);
    }

    [Fact]
    public void DefaultNameIsTheOneOfTheInstaller() => Assert.Equal("OpsisNewsCrawler.SingleInstance", InstanceGuard.DefaultName);

    [Fact]
    public void CopyScalarsKeepsListsAndCopiesSettings()
    {
        var current = new AppConfig { ScrollSpeed = 99, PauseOnHover = false };
        current.Watchlist.Add(new QuoteInstrument { Code = "TEST", Symbol = "TEST" });
        AppConfig.CopyScalars(new AppConfig(), current);
        Assert.Equal(new AppConfig().ScrollSpeed, current.ScrollSpeed);
        Assert.Equal(new AppConfig().PauseOnHover, current.PauseOnHover);
        Assert.Contains(current.Watchlist, w => w.Code == "TEST");   // lists are not touched
    }
}
