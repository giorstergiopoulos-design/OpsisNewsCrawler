using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class BuiltInCategoriesTests
{
    [Fact]
    public void A_new_config_has_all_six_built_in_categories()
    {
        var config = new AppConfig();
        config.Normalize();
        var ids = config.Categories.Where(c => c.BuiltIn).Select(c => c.Id).ToList();
        foreach (var expected in new[] { "Economy", "Market", "International", "Greece", "Defence", "Sports" })
            Assert.Contains(expected, ids);
    }

    [Fact]
    public void Defence_and_Sports_have_names_in_both_languages_and_enabled_feeds()
    {
        var config = new AppConfig();
        config.Normalize();
        var defence = config.FindCategory("Defence")!;
        var sports = config.FindCategory("Sports")!;
        Assert.Equal("Άμυνα & Διπλωματία", defence.NameEl);
        Assert.Equal("Defence & Diplomacy", defence.NameEn);
        Assert.Equal("Αθλητικά", sports.NameEl);
        Assert.Equal("Sports", sports.NameEn);
        Assert.True(config.GetFeeds("Defence").Count >= 4);
        Assert.True(config.GetFeeds("Sports").Count >= 2);
    }

    [Fact]
    public void An_older_config_without_the_new_categories_gets_them_and_keeps_its_own()
    {
        var config = new AppConfig();
        config.Categories = new List<FeedCategory> { new() { Id = "Mine", NameEl = "Δικό μου", NameEn = "Mine", Feeds = new() { new FeedSource("X", "https://example.com/rss") } } };
        config.Normalize();
        Assert.NotNull(config.FindCategory("Mine"));
        Assert.NotNull(config.FindCategory("Defence"));
        Assert.NotNull(config.FindCategory("Sports"));
    }

    [Fact]
    public void Feeds_switched_off_by_default_stay_switched_off()
    {
        var config = new AppConfig();
        config.Normalize();
        Assert.Contains(config.FindCategory("Sports")!.Feeds, f => !f.Enabled);
    }

    [Theory]
    [InlineData("Άμυνα", TickerMode.Defence)]
    [InlineData("defence & diplomacy", TickerMode.Defence)]
    [InlineData("Αθλητικά", TickerMode.Sports)]
    [InlineData("Sports", TickerMode.Sports)]
    public void Display_strings_are_parsed(string text, TickerMode expected) => Assert.Equal(expected, TickerParsing.ParseMode(text));
}
