using System.Text.Json;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class ThemeConfigTests
{
    private static AppConfig RoundTrip(AppConfig cfg)
    {
        var path = Path.Combine(Path.GetTempPath(), $"opsis-theme-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(cfg));
            return AppConfig.Load(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Midnight")]
    [InlineData("Forest")]
    [InlineData("Dawn")]
    [InlineData("Contrast")]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("system")]
    public void KnownThemesAreKept(string mode) => Assert.Equal(mode, RoundTrip(new AppConfig { ThemeMode = mode }).ThemeMode);

    [Fact]
    public void UnknownThemeFallsBackToSystem() => Assert.Equal("system", RoundTrip(new AppConfig { ThemeMode = "Neon" }).ThemeMode);

    [Fact]
    public void WindowsAccentChoiceIsKept() => Assert.True(RoundTrip(new AppConfig { ThemeFollowAccent = true }).ThemeFollowAccent);
}
