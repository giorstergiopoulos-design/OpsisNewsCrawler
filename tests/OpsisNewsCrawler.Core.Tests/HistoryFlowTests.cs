using System.Text.Json;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class HistoryFlowTests
{
    [Fact]
    public void DisabledPluginFeedsAreCleanedAndKept()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "config.json");
        try
        {
            File.WriteAllText(path, "{\"DisabledPluginFeeds\":[\" san-simera \",\"SAN-SIMERA\",\"\",\"other\"]}");
            var c = AppConfig.Load(path);
            Assert.Equal(new[] { "san-simera", "other" }, c.DisabledPluginFeeds);
            Assert.Empty(new AppConfig().DisabledPluginFeeds);
            c.Save(path);
            Assert.Equal(new[] { "san-simera", "other" }, AppConfig.Load(path).DisabledPluginFeeds);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EveryLanguageHasTheTextsOfTheHistoryPage()
    {
        var keys = new[] { "history.flow", "history.note", "history.card", "history.preview", "history.none", "history.loading", "history.count", "history.notUsed", "history.usedIn", "history.addToBar" };
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OpsisNewsCrawler.Core", "Locales");
        foreach (var f in Directory.GetFiles(dir, "*.json"))
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(f))!;
            foreach (var k in keys) Assert.True(d.TryGetValue(k, out var v) && v.Length > 0, $"{Path.GetFileName(f)}: {k}");
            Assert.Contains("{0}", d["history.count"]); Assert.Contains("{1}", d["history.count"]); Assert.Contains("{0}", d["history.usedIn"]);
        }
    }
}
