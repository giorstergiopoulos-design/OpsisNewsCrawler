using System.Net;
using System.Text;
using System.Text.Json;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public sealed class SuiteCatalogTests
{
    private const string Good = """
    { "schema": 1, "updated": "2026-10-23", "owner": "someone", "future": 5,
      "apps": [ { "id": "a", "name": "App A", "repo": "AppA", "installerAsset": "^A-Setup\\.exe$", "checksumAsset": "A-Setup.exe.sha256",
                  "paths": ["%LocalAppData%\\A\\a.exe"], "tagline": { "en": "tag" }, "capabilities": { "en": ["x", "y"] }, "unknownField": true } ] }
    """;

    [Fact]
    public void EmbeddedCatalogueIsValidAndHasTheWholeFamily()
    {
        var c = SuiteCatalogStore.Embedded();
        Assert.Equal(7, c.Apps.Count);
        Assert.NotNull(c.Find("newscrawler"));
        Assert.All(c.Apps, a => Assert.NotEmpty(a.CapabilitiesFor("el")));
    }

    [Fact]
    public void ParsesAndIgnoresUnknownFields()
    {
        Assert.True(SuiteCatalog.TryParse(Good, out var c, out _));
        Assert.Equal("someone", c!.Owner);
        Assert.Equal("tag", c.Apps[0].TaglineFor("el"));            // falls back to English
        Assert.Equal(2, c.Apps[0].CapabilitiesFor("en").Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"schema\":2,\"apps\":[]}")]
    [InlineData("{\"schema\":1}")]
    [InlineData("{\"schema\":1,\"apps\":[]}")]
    [InlineData("{\"schema\":1,\"owner\":\"bad owner!\",\"apps\":[{\"id\":\"a\",\"name\":\"A\",\"repo\":\"A\"}]}")]
    public void RejectsMalformedCatalogues(string json) => Assert.False(SuiteCatalog.TryParse(json, out _, out _));

    [Fact]
    public void RejectsOversizeCatalogue()
    {
        var json = Good.Replace("\"future\": 5", "\"future\": \"" + new string('x', SuiteCatalog.MaxBytes) + "\"");
        Assert.False(SuiteCatalog.TryParse(json, out _, out var error));
        Assert.Equal("too large", error);
    }

    [Fact]
    public void SkipsAppsWithUnsafeRepoOrId()
    {
        var json = """{ "schema":1, "apps":[ {"id":"ok","name":"Ok","repo":"Ok"}, {"id":"x","name":"X","repo":"../evil"}, {"id":"Bad Id","name":"B","repo":"B"} ] }""";
        Assert.True(SuiteCatalog.TryParse(json, out var c, out _));
        Assert.Single(c!.Apps);
    }

    [Fact]
    public void BadInstallerPatternIsDropped()
    {
        var json = """{ "schema":1, "apps":[ {"id":"a","name":"A","repo":"A","installerAsset":"([unclosed"} ] }""";
        Assert.True(SuiteCatalog.TryParse(json, out var c, out _));
        Assert.Null(c!.Apps[0].InstallerAsset);
    }

    [Theory]
    [InlineData("https://raw.githubusercontent.com/o/r/master/a.json", true)]
    [InlineData("https://github.com/o/r", true)]
    [InlineData("https://api.github.com/repos/o/r/releases/latest", true)]
    [InlineData("http://github.com/o/r", false)]
    [InlineData("https://evil.example/github.com", false)]
    [InlineData("https://github.com.evil.example/x", false)]
    [InlineData("https://user:pw@github.com/x", false)]
    [InlineData("ftp://github.com/x", false)]
    [InlineData(null, false)]
    public void OnlyGitHubHostsOverHttpsAreAllowed(string? url, bool allowed) => Assert.Equal(allowed, SuiteCatalog.IsAllowedSource(url));
}

public sealed class SuiteDetectorTests
{
    private sealed class FakeProbe : IInstallProbe
    {
        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Versions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<UninstallEntry> Entries { get; } = new();
        public bool FileExists(string path) => Files.Contains(path);
        public string? FileVersion(string path) => Versions.TryGetValue(path, out var v) ? v : null;
        public string Expand(string text) => text.Replace("%LocalAppData%", @"C:\Users\u\AppData\Local").Replace("%ProgramFiles%", @"C:\Program Files");
        public IReadOnlyList<UninstallEntry> UninstallEntries() => Entries;
    }

    private static SuiteApp App(string id, string name, params string[] paths) =>
        new(id, name, name.Replace(" ", ""), null, null, null, paths, new Dictionary<string, string>(), new Dictionary<string, IReadOnlyList<string>>());

    [Fact]
    public void FindsAnAppByItsFileAndReadsTheVersion()
    {
        var p = new FakeProbe();
        p.Files.Add(@"C:\Users\u\AppData\Local\A\a.exe");
        p.Versions[@"C:\Users\u\AppData\Local\A\a.exe"] = "1.2.3";
        var info = SuiteDetector.Detect(App("a", "App A", @"%LocalAppData%\A\a.exe"), p);
        Assert.True(info.Installed);
        Assert.Equal("1.2.3", info.Version);
    }

    [Fact]
    public void NothingFoundMeansNotInstalled() =>
        Assert.False(SuiteDetector.Detect(App("a", "App A", @"%LocalAppData%\A\a.exe"), new FakeProbe()).Installed);

    [Fact]
    public void UnknownVariablesAreSkipped()
    {
        var p = new FakeProbe();
        Assert.False(SuiteDetector.Detect(App("a", "App A", @"%NoSuchVar%\a.exe"), p).Installed);
    }

    [Theory]
    [InlineData("Opsis", "Opsis - Desktop Customization", true)]
    [InlineData("GearWin", "GearWin - Complete PC Care", true)]
    [InlineData("Opsis", "Opsis News Crawler", false)]           // another app of the family must not count as Opsis
    [InlineData("Opsis", "Opsis Appearance", false)]
    [InlineData("Kombos", "Κόμβος", true)]                        // registered under its Greek name
    [InlineData("Kombos", "Something else", false)]
    public void FindsAnAppInTheUninstallList(string name, string displayName, bool expected)
    {
        var p = new FakeProbe();
        p.Entries.Add(new UninstallEntry(displayName, "6.5.4", null));
        var app = App(name.ToLowerInvariant(), name);
        Assert.Equal(expected, SuiteDetector.Detect(app, p).Installed);
    }

    [Fact]
    public void UninstallEntryGivesTheExeFromTheInstallLocation()
    {
        var p = new FakeProbe();
        p.Entries.Add(new UninstallEntry("GearWin - Complete PC Care", "6.5.4", @"C:\Program Files\GearWin - Complete PC Care"));
        p.Files.Add(@"C:\Program Files\GearWin - Complete PC Care\GearWin.exe");
        var info = SuiteDetector.Detect(App("gearwin", "GearWin", @"%LocalAppData%\Programs\GearWin\GearWin.exe"), p);
        Assert.Equal(@"C:\Program Files\GearWin - Complete PC Care\GearWin.exe", info.ExePath);
        Assert.Equal("6.5.4", info.Version);
    }

    [Theory]
    [InlineData("v6.5.5", "6.5.4", true)]
    [InlineData("v6.5.4", "6.5.4.0", false)]
    [InlineData("v0.15.0", "0.14.7", true)]
    [InlineData("v0.9.0", "0.14.7", false)]
    [InlineData("v1.0.0-beta", "0.9.9", true)]
    [InlineData("junk", "1.0.0", false)]
    [InlineData("v2.0.0", null, false)]
    public void VersionComparisonIsSemantic(string tag, string? installed, bool newer) => Assert.Equal(newer, SuiteDetector.IsNewer(tag, installed));
}

public sealed class SuiteReleaseTests
{
    private static readonly SuiteCatalog Catalog = SuiteCatalogStore.Embedded();

    private static string Release(string assetUrl, string name = "OpsisNewsCrawler-Setup.exe", string? digest = null, bool draft = false, bool pre = false) =>
        JsonSerializer.Serialize(new
        {
            tag_name = "v1.2.3", draft, prerelease = pre,
            html_url = "https://github.com/giorstergiopoulos-design/OpsisNewsCrawler/releases/tag/v1.2.3",
            assets = new object[]
            {
                new { name, browser_download_url = assetUrl, size = 1234, digest },
                new { name = name + ".sha256", browser_download_url = assetUrl + ".sha256", size = 90 },
            }
        });

    private const string Good = "https://github.com/giorstergiopoulos-design/OpsisNewsCrawler/releases/download/v1.2.3/OpsisNewsCrawler-Setup.exe";
    private static readonly string Hex = new('a', 64);

    [Fact]
    public void PicksTheSetupAndItsChecksum()
    {
        var app = Catalog.Find("newscrawler")!;
        var d = SuiteReleases.PickDownload(Release(Good, digest: "sha256:" + Hex), Catalog, app, out var tag);
        Assert.Equal("v1.2.3", tag);
        Assert.Equal("OpsisNewsCrawler-Setup.exe", d!.FileName);
        Assert.Equal(1234, d.Size);
        Assert.Equal(Hex, d.Sha256);
        Assert.Equal(Good + ".sha256", d.ChecksumUrl);
        Assert.True(d.CanVerify);
    }

    [Fact]
    public void IgnoresFilesOfOtherRepositories()
    {
        var app = Catalog.Find("newscrawler")!;
        var evil = "https://github.com/someone-else/OpsisNewsCrawler/releases/download/v1.2.3/OpsisNewsCrawler-Setup.exe";
        Assert.Null(SuiteReleases.PickDownload(Release(evil), Catalog, app, out _));
        Assert.Null(SuiteReleases.PickDownload(Release("https://evil.example/OpsisNewsCrawler-Setup.exe"), Catalog, app, out _));
    }

    [Fact]
    public void IgnoresDraftsAndPrereleases()
    {
        var app = Catalog.Find("newscrawler")!;
        Assert.Null(SuiteReleases.PickDownload(Release(Good, draft: true), Catalog, app, out _));
        Assert.Null(SuiteReleases.PickDownload(Release(Good, pre: true), Catalog, app, out _));
    }

    [Fact]
    public void WithoutAnyHashTheDownloadCannotBeVerified()
    {
        var app = Catalog.Find("gearwin")!;      // no checksum asset in the catalogue
        var json = Release("https://github.com/giorstergiopoulos-design/GearWin/releases/download/v1.2.3/GearWin-Setup-1.2.3.exe", "GearWin-Setup-1.2.3.exe");
        var d = SuiteReleases.PickDownload(json, Catalog, app, out _);
        Assert.NotNull(d);
        Assert.False(d!.CanVerify);
    }

    [Fact]
    public void GarbageGivesNothing() => Assert.Null(SuiteReleases.PickDownload("{ not json", Catalog, Catalog.Apps[0], out _));
}

public sealed class Sha256CheckTests
{
    private static string TempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"opsis-sha-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
        return path;
    }

    // SHA-256 of "hello"
    private const string Hello = "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";

    [Fact]
    public void MatchingHashIsAccepted()
    {
        var f = TempFile("hello");
        try { Assert.True(Sha256Check.Matches(f, Hello)); Assert.True(Sha256Check.Matches(f, Hello.ToUpperInvariant())); }
        finally { File.Delete(f); }
    }

    [Fact]
    public void MismatchIsRefused()
    {
        var f = TempFile("hello!");
        try { Assert.False(Sha256Check.Matches(f, Hello)); }
        finally { File.Delete(f); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zz24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824")]
    public void MissingOrMalformedExpectationIsAMismatch(string? expected)
    {
        var f = TempFile("hello");
        try { Assert.False(Sha256Check.Matches(f, expected)); }
        finally { File.Delete(f); }
    }

    [Fact]
    public void MissingFileIsAMismatch() => Assert.False(Sha256Check.Matches(@"C:\no\such\file.exe", Hello));

    [Theory]
    [InlineData("2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824  setup.exe\n", Hello)]
    [InlineData("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", Hello)]
    [InlineData("SHA256 (setup.exe) = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", Hello)]
    [InlineData("no hash here", null)]
    [InlineData("", null)]
    public void ParsesChecksumFiles(string text, string? expected) => Assert.Equal(expected, Sha256Check.ParseChecksumFile(text));
}

public sealed class WelcomePolicyTests
{
    private static readonly Version Now = new(0, 15, 0);

    [Fact] public void ShowsOnTheVeryFirstStart() => Assert.True(WelcomePolicy.ShouldShow(Now, "", false, false));
    [Fact] public void ShowsOnANewMinorVersion() => Assert.True(WelcomePolicy.ShouldShow(Now, "0.14.7", false, false));
    [Fact] public void ShowsOnANewMajorVersion() => Assert.True(WelcomePolicy.ShouldShow(new Version(1, 0, 0), "0.15.0", false, false));
    [Fact] public void DoesNotShowAgainForPatchReleases() => Assert.False(WelcomePolicy.ShouldShow(new Version(0, 15, 3), "0.15.0", false, false));
    [Fact] public void DoesNotShowWhenSwitchedOff() => Assert.False(WelcomePolicy.ShouldShow(Now, "", true, false));
    [Fact] public void NeverShowsInsideOpsis() => Assert.False(WelcomePolicy.ShouldShow(Now, "", false, true));
    [Fact] public void GarbageSeenVersionCountsAsNever() => Assert.True(WelcomePolicy.ShouldShow(Now, "garbage", false, false));

    [Fact]
    public void RememberedVersionsSurviveASaveAndLoad()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opsis-welcome-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new AppConfig { WelcomeSeenVersion = "0.15.0", WelcomeDisabled = true, SuiteOfferedVersion = "0.15.0", LastNotifiedUpdate = "v0.15.1" }));
            var c = AppConfig.Load(path);
            Assert.Equal("0.15.0", c.WelcomeSeenVersion);
            Assert.True(c.WelcomeDisabled);
            Assert.Equal("0.15.0", c.SuiteOfferedVersion);
            Assert.Equal("v0.15.1", c.LastNotifiedUpdate);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

public sealed class SuiteCatalogStoreTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "opsis-suite-" + Guid.NewGuid().ToString("N"));

    public void Dispose() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

    private sealed class Handler : HttpMessageHandler
    {
        public int Calls;
        public Func<HttpResponseMessage> Respond = () => new HttpResponseMessage(HttpStatusCode.NotFound);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Respond());
        }
    }

    private const string Remote = """{ "schema":1, "updated":"remote", "apps":[ {"id":"only","name":"Only","repo":"Only"} ] }""";

    [Fact]
    public async Task FetchedCatalogueIsUsedAndCached()
    {
        var h = new Handler { Respond = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Remote) } };
        var store = new SuiteCatalogStore(dir, h);
        var c = await store.GetAsync();
        Assert.Equal("remote", c.Updated);
        Assert.Equal(1, h.Calls);
        var again = await store.GetAsync();           // fresh cache: no second request
        Assert.Equal("remote", again.Updated);
        Assert.Equal(1, h.Calls);
    }

    [Fact]
    public async Task StaleCacheIsFetchedAgain()
    {
        var h = new Handler { Respond = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Remote) } };
        var clock = DateTimeOffset.Now;
        var store = new SuiteCatalogStore(dir, h, () => clock);
        await store.GetAsync();
        clock = clock.AddHours(25);
        await store.GetAsync();
        Assert.Equal(2, h.Calls);
    }

    [Fact]
    public async Task OfflineGivesTheEmbeddedCopy()
    {
        var h = new Handler { Respond = () => throw new HttpRequestException("offline") };
        var c = await new SuiteCatalogStore(dir, h).GetAsync();
        Assert.Equal(7, c.Apps.Count);
    }

    [Fact]
    public async Task InvalidRemoteAnswerIsIgnored()
    {
        var h = new Handler { Respond = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{ \"schema\": 9 }") } };
        var c = await new SuiteCatalogStore(dir, h).GetAsync();
        Assert.Equal(7, c.Apps.Count);
        Assert.False(File.Exists(Path.Combine(dir, "suite-apps.json")));
    }

    [Fact]
    public async Task OversizeRemoteAnswerIsIgnored()
    {
        var big = new string(' ', SuiteCatalog.MaxBytes + 10) + Remote;
        var h = new Handler { Respond = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(big) } };
        var c = await new SuiteCatalogStore(dir, h).GetAsync();
        Assert.Equal(7, c.Apps.Count);
    }

    [Fact]
    public async Task StaleCacheIsKeptWhenTheNetworkFails()
    {
        var ok = new Handler { Respond = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Remote) } };
        var clock = DateTimeOffset.Now;
        await new SuiteCatalogStore(dir, ok, () => clock).GetAsync();
        var down = new Handler { Respond = () => throw new HttpRequestException("offline") };
        var c = await new SuiteCatalogStore(dir, down, () => clock.AddDays(3)).GetAsync();
        Assert.Equal("remote", c.Updated);
    }
}

public sealed class SuiteIconTests
{
    [Fact]
    public void EveryAppOfTheCatalogueHasItsOwnEmbeddedIcon()
    {
        var names = typeof(SuiteCatalog).Assembly.GetManifestResourceNames();
        foreach (var app in SuiteCatalogStore.Embedded().Apps)
        {
            var resource = $"OpsisNewsCrawler.Suite.icons.{app.Id}.png";
            Assert.Contains(resource, names);
            using var stream = typeof(SuiteCatalog).Assembly.GetManifestResourceStream(resource)!;
            var header = new byte[8];
            Assert.Equal(8, stream.Read(header, 0, 8));
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, header);   // a real PNG
        }
    }
}
