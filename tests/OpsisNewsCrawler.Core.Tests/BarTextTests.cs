using System.Text.Json;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class BarTextTests
{
    [Fact]
    public void TextAndSizeSettingsAreCleanedAndKept()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "config.json");
        try
        {
            File.WriteAllText(path, "{\"MainFontPercent\":500,\"SecondFontPercent\":10,\"MonitorFontPercent\":120,\"MainBarHeight\":10,\"MonitorBarHeight\":-5,\"MainFontFamily\":\"  Consolas \"}");
            var c = AppConfig.Load(path);
            Assert.Equal(AppConfig.MaxFontPercent, c.MainFontPercent);
            Assert.Equal(AppConfig.MinFontPercent, c.SecondFontPercent);
            Assert.Equal(120, c.MonitorFontPercent);
            Assert.Equal(24, c.MainBarHeight);            // too small: the smallest height
            Assert.Equal(0, c.MonitorBarHeight);          // zero or less: automatic
            Assert.Equal("Consolas", c.MainFontFamily);
            var fresh = new AppConfig();
            Assert.Equal(100, fresh.MainFontPercent); Assert.Equal(0, fresh.MainBarHeight); Assert.Equal("", fresh.MainFontFamily);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EveryLanguageHasTheTextsOfTheTextAndSizeCard()
    {
        var keys = new[] { "text.card", "text.note", "text.defaultFont", "text.font", "text.size", "text.heightAuto", "text.height", "text.secondHeight", "text.miniHeight", "text.monitors" };
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OpsisNewsCrawler.Core", "Locales");
        foreach (var f in Directory.GetFiles(dir, "*.json"))
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(f))!;
            foreach (var k in keys) Assert.True(d.TryGetValue(k, out var v) && v.Length > 0, $"{Path.GetFileName(f)}: {k}");
        }
    }
}
