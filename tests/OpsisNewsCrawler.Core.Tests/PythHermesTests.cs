using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class PythHermesTests
{
    private const string Brent = "f33ce961935076ef4dc98be75cf2126046eac1bffdcd7a0fa05ccf18b746fda6";

    private static string Json(string id, string price, string conf, int expo, long time) =>
        "{\"binary\":{\"encoding\":\"hex\",\"data\":[]},\"parsed\":[{\"id\":\"" + id + "\",\"price\":{\"price\":\"" + price + "\",\"conf\":\"" + conf + "\",\"expo\":" + expo + ",\"publish_time\":" + time + "}}]}";

    [Fact]
    public void Parse_AppliesExponentAndTimestamp()
    {
        var list = PythHermes.Parse(Json("0x" + Brent, "7512345678", "1000", -8, 1760000000));
        var price = Assert.Single(list);
        Assert.Equal(Brent, price.Id);
        Assert.Equal(75.12345678m, price.Value);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1760000000), price.PublishedAt);
    }

    [Fact]
    public void Parse_SkipsWideConfidenceAndBadValues()
    {
        Assert.Empty(PythHermes.Parse(Json(Brent, "7500000000", "500000000", -8, 1760000000))); // confidence 6.7 %
        Assert.Empty(PythHermes.Parse(Json(Brent, "-5", "0", -8, 1760000000)));
        Assert.Empty(PythHermes.Parse(Json(Brent, "abc", "0", -8, 1760000000)));
        Assert.Empty(PythHermes.Parse("{\"parsed\":\"nope\"}"));
        Assert.Empty(PythHermes.Parse("{}"));
    }

    [Fact]
    public void BuildUrl_ListsEveryId()
    {
        var url = PythHermes.BuildUrl(new[] { "aa", "bb" });
        Assert.Contains("&ids[]=aa", url);
        Assert.Contains("&ids[]=bb", url);
        Assert.StartsWith("https://hermes.pyth.network/", url);
    }

    [Fact]
    public void EveryEnergyInstrumentHasAFeed()
    {
        foreach (var i in QuoteInstrument.Defaults().Where(i => i.Source == QuoteSource.Energy))
            Assert.True(PythHermes.FeedIds.ContainsKey(i.Code), i.Code);
    }
}
