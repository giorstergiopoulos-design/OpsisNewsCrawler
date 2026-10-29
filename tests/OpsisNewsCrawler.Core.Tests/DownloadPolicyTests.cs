using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class DownloadPolicyTests
{
    [Theory]
    [InlineData("https://github.com/o/r/releases/download/v1/a.exe", true)]
    [InlineData("https://release-assets.githubusercontent.com/x/y", true)]
    [InlineData("https://objects.githubusercontent.com/x", true)]
    [InlineData("http://github.com/o/r", false)]
    [InlineData("https://evil.example.com/githubusercontent.com", false)]
    [InlineData("https://githubusercontent.com.evil.com/x", false)]
    [InlineData("https://user:pw@github.com/x", false)]
    [InlineData("file:///C:/x.exe", false)]
    public void RedirectTargetsAreGitHubOnly(string url, bool expected) =>
        Assert.Equal(expected, DownloadPolicy.IsTrustedRedirectTarget(new Uri(url)));

    [Fact]
    public void NullRedirectTargetIsRefused() => Assert.False(DownloadPolicy.IsTrustedRedirectTarget(null));

    [Theory]
    [InlineData("OpsisNewsCrawler-Setup.exe", true)]
    [InlineData("..", false)]
    [InlineData(".", false)]
    [InlineData("..\\evil.exe", false)]
    [InlineData("a/b.exe", false)]
    [InlineData("C:evil.exe", false)]
    [InlineData("setup.exe.", false)]
    [InlineData("setup.exe ", false)]
    [InlineData("", false)]
    public void FileNamesArePlain(string name, bool expected) => Assert.Equal(expected, DownloadPolicy.IsSafeFileName(name));

    [Theory]
    [InlineData("CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", true)]
    [InlineData("O=Microsoft Corporation", true)]
    [InlineData("CN=Evil, O=Not Microsoft Corporation Ltd, C=US", false)]
    [InlineData("CN=Evil O=Microsoft Corporation", false)]
    [InlineData("CN=Evil, OU=O=Microsoft Corporation x", false)]
    [InlineData("", false)]
    public void OnlyMicrosoftCorporationIsAccepted(string subject, bool expected) => Assert.Equal(expected, DownloadPolicy.IsMicrosoftSigner(subject));

    [Fact]
    public void LogLinesLoseTheProfileFolder()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var scrubbed = DownloadPolicy.ScrubForLog($"cannot open {profile}/AppData/x.json");
        Assert.DoesNotContain(profile, scrubbed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", scrubbed);
    }

    [Fact]
    public void GdacsFeedWithADoctypeIsRefusedNotExpanded()
    {
        var xml = "<?xml version=\"1.0\"?><!DOCTYPE rss [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\">]><rss><channel><item><title>&b;</title></item></channel></rss>";
        Assert.Empty(HazardFeeds.ParseGdacs(xml));
    }

    [Fact]
    public void SuiteDownloadsWithUnsafeNamesAreIgnored()
    {
        var catalog = SuiteCatalogStore.Embedded();
        var app = catalog.Apps[0];
        var json = "{\"tag_name\":\"v1\",\"assets\":[{\"name\":\"..\",\"size\":1,\"browser_download_url\":\"https://github.com/" + catalog.Owner + "/" + app.Repo + "/releases/download/v1/x\"}]}";
        Assert.Null(SuiteReleases.PickDownload(json, catalog, app, out _));
    }
}
