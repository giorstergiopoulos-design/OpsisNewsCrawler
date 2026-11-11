using System.Reflection;
using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

[Collection("plugin-feeds")]
public class RichFeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
    private static PluginFeedOptions Opts(int max = 50, string order = "priority", string country = "", string lang = "el") => new(max, order, country, lang);

    private static PluginRichFeedItem R(string id, string text, int priority = 0, int? year = null, string? country = null, string? lang = null, string? summary = null, string? kind = null) =>
        new(id, text, null, kind, summary, null, country, lang, year, null, priority);

    [Fact]
    public void RichLinesBecomeCalendarEntriesWithTheirKeyAndSummaryAndNeverBreakingNews()
    {
        var items = PluginFeedMapper.FromRich(new[] { R("h-1", "Η μάχη του Μαραθώνα", 5, -490, "GR", "el", "Πρώτη μεγάλη νίκη των Αθηναίων") }, "Σαν σήμερα", Now, Opts());
        var item = Assert.Single(items);
        Assert.Equal(NewsItemKind.Calendar, item.Kind);
        Assert.Equal("h-1", item.Key);
        Assert.Equal("Πρώτη μεγάλη νίκη των Αθηναίων", item.Summary);
        Assert.Equal("Σαν σήμερα", item.Source);
        Assert.Equal(Now, item.PublishedAt);          // the time of the snapshot, not the date of the event
        Assert.False(item.Breaking);
        // the urgent-news engine looks at news only: a calendar line is never assessed
        Assert.Empty(new BreakingEngine().Urgent(items, null, Now));
    }

    [Fact]
    public void TheOrderIsTheSameEveryTimeWhateverTheOrderOfTheInput()
    {
        var items = new[] { R("c", "C", 1, 1900), R("a", "A", 5, 1800), R("b", "B", 5, 1950), R("d", "D", 0, null), R("e", "E", 5, 1950) };
        var rng = new Random(3);
        foreach (var order in new[] { "priority", "yearAsc", "yearDesc" })
        {
            var expected = PluginFeedMapper.FromRich(items, "x", Now, Opts(order: order)).Select(i => i.Key).ToList();
            for (var round = 0; round < 20; round++)
                Assert.Equal(expected, PluginFeedMapper.FromRich(items.OrderBy(_ => rng.Next()).ToList(), "x", Now, Opts(order: order)).Select(i => i.Key).ToList());
        }
        Assert.Equal(new[] { "b", "e", "a", "c", "d" }, PluginFeedMapper.FromRich(items, "x", Now, Opts()).Select(i => i.Key).ToArray());          // importance, then newer year, then id
        Assert.Equal(new[] { "a", "c", "b", "e", "d" }, PluginFeedMapper.FromRich(items, "x", Now, Opts(order: "yearAsc")).Select(i => i.Key).ToArray());   // no year last
        Assert.Equal(new[] { "b", "e", "c", "a", "d" }, PluginFeedMapper.FromRich(items, "x", Now, Opts(order: "yearDesc")).Select(i => i.Key).ToArray());
    }

    [Fact]
    public void ADuplicateLineIsShownOnce_TheMostImportantStays()
    {
        var items = new[] { R("feast-1", "Ευαγγελισμός της Θεοτόκου", 1), R("hist-9", "Ευαγγελισμός της Θεοτόκου", 9), R("hist-9", "Άλλη γραμμή με το ίδιο id", 2) };
        var result = PluginFeedMapper.FromRich(items, "x", Now, Opts());
        Assert.Single(result);
        Assert.Equal("hist-9", result[0].Key);
    }

    [Fact]
    public void TheCountryAndTheLanguageOfTheUserFilterTheLines()
    {
        var items = new[] { R("1", "Ελλάδα", country: "GR"), R("2", "Γαλλία", country: "FR"), R("3", "Όλοι"), R("4", "English line", lang: "en") };
        Assert.Equal(new[] { "1", "3" }, PluginFeedMapper.FromRich(items, "x", Now, Opts(country: "GR")).Select(i => i.Key).OrderBy(k => k).ToArray());
        Assert.Equal(3, PluginFeedMapper.FromRich(items, "x", Now, Opts()).Count);                               // all countries: the English line is left out for a Greek user
        Assert.Equal(4, PluginFeedMapper.FromRich(items, "x", Now, Opts(lang: "en")).Count);                   // an English user sees the lines without a language and the English one
        Assert.Single(PluginFeedMapper.FromRich(new[] { R("4", "English line", lang: "en") }, "x", Now, Opts(lang: "el")));   // never an empty bar only because of the language
    }

    [Fact]
    public void TheMostLinesIsKept_AndEmptyTextsAreDropped()
    {
        var items = Enumerable.Range(0, 100).Select(i => R("id" + i, "Line " + i, i)).Append(R("blank", "  ")).ToList();
        Assert.Equal(7, PluginFeedMapper.FromRich(items, "x", Now, Opts(max: 7)).Count);
        Assert.DoesNotContain(PluginFeedMapper.FromRich(items, "x", Now, Opts(max: 500)), i => i.Key == "blank");
    }

    [Fact]
    public void ATextFeedKeepsItsKindAndOnlyGetsTheLimit()
    {
        var text = Enumerable.Range(0, 30).Select(i => new PluginFeedItem("Line " + i, i % 2 == 0 ? "https://example.com/" + i : null)).ToList();
        var mapped = PluginFeedMapper.FromText(text, "Feed", Now, Opts(max: 12));
        Assert.Equal(12, mapped.Count);
        Assert.All(mapped, i => Assert.Equal(NewsItemKind.News, i.Kind));      // the 0.20.1 behaviour is unchanged
        Assert.Equal("https://example.com/0", mapped[0].Url);
        Assert.Equal("", mapped[1].Url);
    }

    [Fact]
    public void ARichFeedWinsOverATextFeedOfTheSameKeyInTheSettingsLists()
    {
        PluginFeeds.Register(new PluginFeed("rich-test", "Text el", "Text", () => new[] { new PluginFeedItem("from the text feed") }));
        PluginFeeds.RegisterRich(new PluginRichFeed("rich-test", "Rich el", "Rich", () => new[] { R("r1", "from the rich feed") }));
        try
        {
            var all = PluginFeedMapper.AllFeeds().Where(f => f.Key == "rich-test").ToList();
            var feed = Assert.Single(all);
            Assert.Equal("Rich", feed.TitleEn);
            Assert.Equal("from the rich feed", Assert.Single(feed.GetItems()).Text);
            Assert.NotNull(PluginFeeds.FindRich("RICH-TEST"));
        }
        finally { PluginFeeds.Unregister("rich-test"); }
        Assert.Null(PluginFeeds.FindRich("rich-test"));         // one call removes both
        Assert.Null(PluginFeeds.Find("rich-test"));
    }

    [Fact]
    public void TheNewTypesAreOnlyAdditions_TheOldSignaturesAreUntouched()
    {
        // an old plugin builds a PluginFeedItem(Text, Url) and calls Register: both still exist with the same shape
        var ctor = typeof(PluginFeedItem).GetConstructor(new[] { typeof(string), typeof(string) });
        Assert.NotNull(ctor);
        Assert.NotNull(typeof(PluginFeeds).GetMethod("Register", new[] { typeof(PluginFeed) }));
        Assert.NotNull(typeof(PluginFeeds).GetMethod("RegisterRich", new[] { typeof(PluginRichFeed) }));
        // the rich types live in their own classes, so a plugin that does not use them never loads them
        Assert.Equal(new Version(0, 8, 0, 0), typeof(PluginRichFeed).Assembly.GetName().Version);
        Assert.DoesNotContain(typeof(PluginFeedItem).GetProperties(), p => p.Name is "EntryId" or "Summary" or "HistoricalYear");
    }

    [Fact]
    public void TheSettingsAreCleaned()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "config.json");
        try
        {
            File.WriteAllText(path, "{\"PluginFeedMaxLines\":9999,\"PluginFeedOrder\":\"banana\",\"PluginFeedCountry\":\"gr\"}");
            var c = AppConfig.Load(path);
            Assert.Equal(200, c.PluginFeedMaxLines); Assert.Equal("priority", c.PluginFeedOrder); Assert.Equal("GR", c.PluginFeedCountry);
            File.WriteAllText(path, "{\"PluginFeedMaxLines\":1,\"PluginFeedOrder\":\"yearDesc\",\"PluginFeedCountry\":\"Greece\"}");
            c = AppConfig.Load(path);
            Assert.Equal(5, c.PluginFeedMaxLines); Assert.Equal("yearDesc", c.PluginFeedOrder); Assert.Equal("", c.PluginFeedCountry);
        }
        finally { Directory.Delete(dir, true); }
    }
}
