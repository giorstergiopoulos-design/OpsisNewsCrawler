using OpsisNewsCrawler.OpsisPlugin;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public class TrayMenuTests
{
    [Fact]
    public void HostedInOpsisTheWelcomeGuideAndTheFamilyWindowAreNotOffered()
    {
        var keys = OpsisNewsCrawlerPlugin.BuildTrayMenu(() => { }, () => { }, () => { }, () => { }, () => { }).Select(i => i.Key).ToList();
        Assert.DoesNotContain("news.welcome", keys);
        Assert.DoesNotContain("news.suite", keys);
        Assert.Equal(new[] { "news.separator", "news.settings", "news.refresh", "news.pause", "news.close", "news.open" }, keys);
    }

    [Fact]
    public void ItemsRunTheirActionsAndHaveBothLanguages()
    {
        var calls = new List<string>();
        var items = OpsisNewsCrawlerPlugin.BuildTrayMenu(() => calls.Add("s"), () => calls.Add("r"), () => calls.Add("p"), () => calls.Add("c"), () => calls.Add("o"));
        foreach (var i in items.Where(x => !x.IsSeparator)) { Assert.False(string.IsNullOrWhiteSpace(i.TitleEl)); Assert.False(string.IsNullOrWhiteSpace(i.TitleEn)); i.Invoke(); }
        Assert.Equal(new[] { "s", "r", "p", "c", "o" }, calls);
    }

    [Fact]
    public void TheStandaloneProgramKeepsBothEntriesInItsOwnTrayMenu()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any()) dir = dir.Parent;
        var source = File.ReadAllText(Path.Combine(dir!.FullName, "src", "OpsisNewsCrawler.App", "Program.cs"), System.Text.Encoding.UTF8);
        Assert.Contains("tray.welcome", source);
        Assert.Contains("tray.suite", source);
    }
}
