using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpsisNewsCrawler.Core;

public sealed class FeedSource
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public bool Enabled { get; set; } = true;

    public FeedSource() { }
    public FeedSource(string name, string url) { Name = name; Url = url; }
}

/// <summary>A named set of feeds. The four built-in ones match <see cref="TickerMode"/>; users can add their own.</summary>
public sealed class FeedCategory
{
    public string Id { get; set; } = "";
    public string NameEl { get; set; } = "";
    public string NameEn { get; set; } = "";
    public bool BuiltIn { get; set; }
    public List<FeedSource> Feeds { get; set; } = new();

    /// <summary>The name in the language of the UI (Greek or English only: no mixed "Ελληνικά / English" labels).</summary>
    [JsonIgnore]
    public string DisplayName => Loc.Pick(NameEl, NameEn);
}

/// <summary>
/// User settings, stored as JSON in %AppData%\OpsisNewsCrawler\config.json.
/// </summary>
public sealed class AppConfig
{
    public const int MinRefreshSeconds = 30;
    public const int MaxRefreshSeconds = 3600;
    public const int MinScrollSpeed = 10;
    public const int MaxScrollSpeed = 400;

    public string Language { get; set; } = "el";
    public TickerMode Mode { get; set; } = TickerMode.Economy;
    public TickerPosition Position { get; set; } = TickerPosition.BottomTaskbar;
    public int NewsRefreshSeconds { get; set; } = 300;
    public bool PauseOnOtherWindow { get; set; } = true;
    public bool FollowTaskbar { get; set; } = true;
    public int MaxItemsPerFeed { get; set; } = 8;
    public int ScrollSpeed { get; set; } = 60; // pixels per second
    public bool PauseOnHover { get; set; } = true;
    public bool ShowPreview { get; set; } = true;
    public bool ShowIcons { get; set; } = true;
    public bool HideRead { get; set; } = true;

    /// <summary>Discard dated RSS items older than this many hours. Undated items are retained.</summary>
    public int NewsMaxAgeHours { get; set; } = 168;

    /// <summary>When true, show the newest stories first instead of rotating fairly between feeds.</summary>
    public bool NewsSortNewestFirst { get; set; }

    /// <summary>Only display headlines/summaries that mention an enabled Athens/Euronext watchlist instrument.</summary>
    public bool StockNewsOnly { get; set; }

    /// <summary>Words or phrases excluded from the ticker news and breaking-news detection.</summary>
    public List<string> MutedNewsKeywords { get; set; } = new();

    /// <summary>Master switch: false hides every bar and stops all downloads until it is turned on again.</summary>
    public bool BarsEnabled { get; set; } = true;
    /// <summary>Ctrl+Alt+N shows / hides all bars from anywhere (standalone app only).</summary>
    public bool ToggleHotkey { get; set; }

    /// <summary>The icon in the notification area shows a little bar with the four kinds of news passing by (newspaper, ball, shield, chart and coins).</summary>
    public bool TrayIconAnimated { get; set; } = true;

    /// <summary>The holdings shown in the portfolio window (quantity and average price; profit and loss are computed).</summary>
    public List<PortfolioPosition> Portfolio { get; set; } = new();

    /// <summary>Cities (ids of <see cref="GreekCities"/>) shown by a bar whose content is "Weather", in this order.</summary>
    public List<string> WeatherCities { get; set; } = GreekCities.DefaultSelection.ToList();

    /// <summary>The small weather bar on the desktop (one city: icon, temperature, wind, humidity). Click it for the next selected city.</summary>
    public bool MiniWeatherEnabled { get; set; }
    /// <summary>Position of the small weather bar; int.MinValue = in the middle of the desktop.</summary>
    public int MiniWeatherX { get; set; } = int.MinValue;
    public int MiniWeatherY { get; set; } = int.MinValue;
    public int MiniWeatherWidth { get; set; } = 620;
    public int MiniWeatherHeight { get; set; } = 56;
    /// <summary>False (default): the bar lies on the desktop and every other window covers it. True: it stays above other windows.</summary>
    public bool MiniWeatherOnTop { get; set; }
    /// <summary>True (default): while an auto-hiding taskbar is hidden (and a bar inside it with it) the desktop bar appears, at the place and size the user chose, even if it is switched off otherwise.</summary>
    public bool MiniWhenTaskbarHidden { get; set; } = true;

    /// <summary>What the desktop bar (the small free-floating one) shows; empty = weather (the old behaviour). Same spec strings as <see cref="MainSlotA"/>.</summary>
    public string MiniSlotA { get; set; } = "";
    public string MiniSlotB { get; set; } = "";

    /// <summary>Weather details shown after the temperature: "wind", "humidity", "feels", "rain", "cloud", "pressure", "gusts".</summary>
    public List<string> WeatherFields { get; set; } = new() { "wind", "humidity" };

    /// <summary>Ask GitHub once a day whether a newer release exists. Off until the user turns it on; nothing is installed without asking.</summary>
    public bool CheckForUpdates { get; set; }
    public DateTimeOffset LastUpdateCheck { get; set; } = DateTimeOffset.MinValue;

    /// <summary>Fade the text out at both ends of the bar.</summary>
    public bool EdgeFade { get; set; }

    /// <summary>"system", "light" or "dark".</summary>
    public string ThemeMode { get; set; } = "system";
    public int OpacityPercent { get; set; } = 94;

    /// <summary>Opacity of the settings window itself (the standalone one), like Opsis' window opacity: 60-100.</summary>
    public int SettingsOpacityPercent { get; set; } = 100;

    // Custom position (screen coordinates; CustomWidth 0 = not set yet).
    public int CustomMonitor { get; set; }

    /// <summary>Extra bars on the other monitors (one per screen, each with its own content). The main bar stays on the primary screen.</summary>
    public List<MonitorBar> MonitorBars { get; set; } = new();
    public int CustomX { get; set; }
    public int CustomY { get; set; }
    public int CustomWidth { get; set; }

    /// <summary>
    /// Bottom/Top only: make Windows keep maximized windows (and Opsis widgets / DeskZones) out of the strip.
    /// null = automatic: on when hosted by Opsis, off when standalone.
    /// </summary>
    public bool? ReserveScreenSpace { get; set; }

    // Taskbar-integrated mode (experimental): the free span of the taskbar, measured from its edges.
    /// <summary>Measure the free span of the taskbar automatically (UI Automation); the offsets below are the fallback.</summary>
    public bool TaskbarAutoDetect { get; set; } = true;
    public int TaskbarLeftOffset { get; set; } = 420;
    public int TaskbarRightOffset { get; set; } = 330;

    // ---- bar background material ----
    /// <summary>"acrylic" (Windows accent state 4), "blur" (state 3) or "gradient" (state 2, the light-blue look).</summary>
    public string BarBackdrop { get; set; } = "acrylic";
    public string TaskbarBarBackdrop { get; set; } = "blur";

    // ---- bar colour ----
    /// <summary>Colour of the main bar: "theme" (follows the Opsis / crawler / Windows theme), "lightblue", "accent" (the Windows accent colour) or "custom".</summary>
    public string BarColorMode { get; set; } = "theme";

    /// <summary>The user's own colour (0xRRGGBB) used when <see cref="BarColorMode"/> is "custom".</summary>
    public int BarCustomColor { get; set; } = 0x4A90D9;

    /// <summary>Colour of the bar inside the taskbar: "taskbar" (completely clear), "lightblue", "accent" or "custom".</summary>
    public string TaskbarBarColorMode { get; set; } = "taskbar";
    public int TaskbarBarCustomColor { get; set; } = 0x4A90D9;

    // ---- bars ----
    /// <summary>What the main bar shows: news, quotes, or both (the bar is split in the middle).</summary>
    public BarContent MainContent { get; set; } = BarContent.News;

    /// <summary>When the main bar shows both: quotes in the left half (true) or in the right half (false).</summary>
    public bool SplitStocksOnLeft { get; set; }

    /// <summary>A second bar inside the taskbar (the main bar then must not be the taskbar one).</summary>
    public bool SecondBarEnabled { get; set; }
    public BarContent SecondContent { get; set; } = BarContent.Stocks;

    // The same choices as above, but each bar can carry up to two parts and each part can be news of any category,
    // quotes or weather. Empty = not set yet: the old MainContent / SecondContent / SplitStocksOnLeft values apply.
    // Spec strings: "news", "news:<category id>", "stocks", "weather".
    public string MainSlotA { get; set; } = "";
    public string MainSlotB { get; set; } = "";
    public string SecondSlotA { get; set; } = "";
    public string SecondSlotB { get; set; } = "";

    public BarSlots MainSlots() => BarSpec.Resolve(MainSlotA, MainSlotB, MainContent, SplitStocksOnLeft);
    public BarSlots SecondSlots() => BarSpec.Resolve(SecondSlotA, SecondSlotB, SecondContent, false);
    public BarSlots MiniSlots() => BarSpec.Resolve(MiniSlotA, MiniSlotB, BarContent.Weather, false);

    // ---- breaking news ----
    /// <summary>An urgent headline turns the main bar red with capital letters and a short intro animation.</summary>
    public bool BreakingEnabled { get; set; } = true;

    /// <summary>A click on a city of the weather line opens a window with the weather and a map.</summary>
    public bool WeatherMapOnClick { get; set; } = true;

    /// <summary>A short alert sound when an urgent headline, a price alert or a severe-weather warning appears.</summary>
    public bool BreakingSound { get; set; } = true;

    /// <summary>Warnings of severe weather for the user's area (computed from the Open-Meteo forecast; not the official bulletin).</summary>
    public bool WeatherAlertsEnabled { get; set; } = true;

    /// <summary>Id of the city whose weather is watched; empty = the first city of the weather flow (Athens when none).</summary>
    public string AlertCityId { get; set; } = "";

    public GreekCity ResolveAlertCity() =>
        GreekCities.Find(AlertCityId) ?? GreekCities.Find(WeatherCities.FirstOrDefault()) ?? GreekCities.Find("athens") ?? GreekCities.All[0];

    /// <summary>Words that mark a headline as urgent (compared without accents and case).</summary>
    public List<string> BreakingKeywords { get; set; } = new(Breaking.DefaultKeywords);

    /// <summary>Only headlines published in the last N minutes count.</summary>
    public int BreakingMaxAgeMinutes { get; set; } = 180;

    /// <summary>The red state lasts this long, or until the user opens the urgent headline.</summary>
    public int BreakingDurationMinutes { get; set; } = 30;

    /// <summary>Live video sources for the preview window (shown on request while breaking news is active).</summary>
    public List<LiveFeed> LiveFeeds { get; set; } = new();

    // ---- quotes ----
    public List<QuoteInstrument> Watchlist { get; set; } = QuoteInstrument.Defaults();
    public int QuoteRefreshSeconds { get; set; } = 60;
    /// <summary>Price limits that raise an alert on the bar.</summary>
    public List<PriceAlert> PriceAlerts { get; set; } = new();

    /// <summary>Id of the category shown in the strip (a built-in mode name or a user category).</summary>
    public string ActiveCategory { get; set; } = nameof(TickerMode.Economy);
    public List<FeedCategory> Categories { get; set; } = new();

    /// <summary>Legacy: the pre-0.8 per-mode feed lists. Only read to migrate into <see cref="Categories"/>.</summary>
    public Dictionary<string, List<FeedSource>> Feeds { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpsisNewsCrawler", "config.json");

    public static Dictionary<string, List<FeedSource>> DefaultFeeds() => new()
    {
        [nameof(TickerMode.Economy)] = new()
        {
            new("Ναυτεμπορική", "https://www.naftemporiki.gr/feed/"),
            new("OT.gr", "https://www.ot.gr/feed/"),
            new("Bankingnews", "https://www.bankingnews.gr/rss"),
            new("Mononews", "https://www.mononews.gr/feed"),
        },
        [nameof(TickerMode.Market)] = new()
        {
            new("Bankingnews", "https://www.bankingnews.gr/rss"),
            new("Ναυτεμπορική", "https://www.naftemporiki.gr/feed/"),
        },
        [nameof(TickerMode.International)] = new()
        {
            new("in.gr – Κόσμος", "https://www.in.gr/world/feed/"),
            new("BBC World", "https://feeds.bbci.co.uk/news/world/rss.xml"),
        },
        [nameof(TickerMode.Greece)] = new()
        {
            new("ΕΡΤ", "https://www.ertnews.gr/feed/"),
            new("in.gr", "https://www.in.gr/feed/"),
            new("iefimerida", "https://www.iefimerida.gr/rss.xml"),
        },
        // Defence and diplomacy: Greek defence sites, the two big English defence desks and Politico Europe for diplomacy.
        [nameof(TickerMode.Defence)] = new()
        {
            new("Defence Point", "https://www.defence-point.gr/news/feed/"),
            new("Defence Review", "https://www.defencereview.gr/feed/"),
            new("Militaire.gr", "https://www.militaire.gr/feed/"),
            new("Breaking Defense", "https://breakingdefense.com/feed/"),
            new("Defense News", "https://www.defensenews.com/arc/outboundfeeds/rss/?outputType=xml"),
            new("POLITICO Europe", "https://www.politico.eu/feed/"),
        },
        [nameof(TickerMode.Sports)] = new()
        {
            new("Gazzetta", "https://www.gazzetta.gr/rss"),
            new("in.gr – Αθλητικά", "https://www.in.gr/sports/feed/"),
            new("BBC Sport", "https://feeds.bbci.co.uk/sport/rss.xml") { Enabled = false },
            new("ESPN", "https://www.espn.com/espn/rss/news") { Enabled = false },
        },
    };

    private static readonly (TickerMode Mode, string El, string En)[] BuiltInNames =
    {
        (TickerMode.Economy, "Οικονομία", "Economy"),
        (TickerMode.Market, "Αγορές", "Markets"),
        (TickerMode.International, "Διεθνή", "International"),
        (TickerMode.Greece, "Ελλάδα", "Greece"),
        (TickerMode.Defence, "Άμυνα & Διπλωματία", "Defence & Diplomacy"),
        (TickerMode.Sports, "Αθλητικά", "Sports"),
    };

    /// <summary>Suggested sites the user can add to any category with one click.</summary>
    public static IReadOnlyList<FeedSource> PresetFeeds { get; } = new List<FeedSource>
    {
        new("in.gr – Κόσμος", "https://www.in.gr/world/feed/"),
        new("BBC World", "https://feeds.bbci.co.uk/news/world/rss.xml"),
        new("The Guardian – World", "https://www.theguardian.com/world/rss"),
        new("Al Jazeera", "https://www.aljazeera.com/xml/rss/all.xml"),
        new("Deutsche Welle", "https://rss.dw.com/rdf/rss-en-all"),
        new("ΕΡΤ News", "https://www.ertnews.gr/feed/"),
        new("Καθημερινή", "https://www.kathimerini.gr/feed/"),
        new("Ναυτεμπορική", "https://www.naftemporiki.gr/feed/"),
        new("CNBC – Top News", "https://www.cnbc.com/id/100003114/device/rss/rss.html"),
        new("Euronews", "https://www.euronews.com/rss?format=mrss&level=theme&name=news"),
        new("Defence Point", "https://www.defence-point.gr/news/feed/"),
        new("Breaking Defense", "https://breakingdefense.com/feed/"),
        new("Gazzetta", "https://www.gazzetta.gr/rss"),
        new("BBC Sport", "https://feeds.bbci.co.uk/sport/rss.xml"),
    };

    public IReadOnlyList<FeedSource> GetFeeds(TickerMode mode) => GetFeeds(mode.ToString());

    /// <summary>Enabled feeds of a category (by id). Unknown id → first category.</summary>
    public IReadOnlyList<FeedSource> GetFeeds(string categoryId)
    {
        var cat = FindCategory(categoryId) ?? Categories.FirstOrDefault();
        return cat is null ? new List<FeedSource>() : cat.Feeds.Where(f => f.Enabled).ToList();
    }

    public FeedCategory? FindCategory(string? id) =>
        Categories.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    public int RefreshIntervalSeconds => Math.Clamp(NewsRefreshSeconds, MinRefreshSeconds, MaxRefreshSeconds);

    /// <summary>True when this run created the settings file (nothing was saved before): the app opens the settings once.</summary>
    [JsonIgnore]
    public bool IsFirstRun { get; private set; }

    public static AppConfig Load(string? path = null)
    {
        path ??= DefaultPath;
        AppConfig config;
        try
        {
            // Settings from the first versions (folder "DeskTicker") are copied once.
            if (path == DefaultPath && !File.Exists(path))
            {
                var legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskTicker", "config.json");
                if (File.Exists(legacy))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(legacy, path);
                }
            }

            if (!File.Exists(path))
            {
                config = new AppConfig { IsFirstRun = true };
                config.Normalize();
                config.Save(path);
                return config;
            }

            config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions) ?? new AppConfig();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            // An unreadable file is kept as config.json.bad instead of being overwritten by the defaults.
            if (ex is JsonException or NotSupportedException) SafeFile.KeepAsBad(path);
            config = new AppConfig();
        }

        config.Normalize();
        ApplyFirstRun(config, path);
        return config;
    }

    /// <summary>
    /// The installer writes `first-run.json` ({"Position":"BottomTaskbar"|"TaskbarIntegrated"}) next to the config with the
    /// bar the user chose as the default. It is applied once and deleted.
    /// </summary>
    private static void ApplyFirstRun(AppConfig config, string configPath)
    {
        var file = Path.Combine(Path.GetDirectoryName(configPath) ?? "", "first-run.json");
        try
        {
            if (!File.Exists(file)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (doc.RootElement.TryGetProperty("Language", out var lang) && lang.GetString() is { Length: > 0 } code)
            {
                config.Language = code;
                config.Save(configPath);
            }
            if (doc.RootElement.TryGetProperty("Position", out var p) &&
                Enum.TryParse<TickerPosition>(p.GetString(), true, out var position) &&
                position is TickerPosition.BottomTaskbar or TickerPosition.TaskbarIntegrated)
            {
                config.Position = position;
                if (position == TickerPosition.TaskbarIntegrated) config.SecondBarEnabled = false;
                config.Save(configPath);
            }
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            SafeFile.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings that cannot be saved must not crash the ticker.
        }
    }

    internal void Normalize()
    {
        NewsRefreshSeconds = RefreshIntervalSeconds;
        MaxItemsPerFeed = Math.Clamp(MaxItemsPerFeed, 1, 50);
        NewsMaxAgeHours = Math.Clamp(NewsMaxAgeHours, 1, 720);
        MutedNewsKeywords = (MutedNewsKeywords ?? new()).Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList();
        ScrollSpeed = Math.Clamp(ScrollSpeed, MinScrollSpeed, MaxScrollSpeed);
        if (!Enum.IsDefined(Mode)) Mode = TickerMode.Economy;
        if (!Enum.IsDefined(Position)) Position = TickerPosition.BottomTaskbar;
        OpacityPercent = Math.Clamp(OpacityPercent, 30, 100);
        SettingsOpacityPercent = Math.Clamp(SettingsOpacityPercent, 60, 100);
        if (ThemeMode is not ("system" or "light" or "dark")) ThemeMode = "system";

        Categories ??= new List<FeedCategory>();
        Feeds ??= new();
        var defaults = DefaultFeeds();

        // Older files only have the per-mode Feeds dictionary: migrate it into categories (once).
        foreach (var (mode, el, en) in BuiltInNames)
        {
            var id = mode.ToString();
            if (FindCategory(id) is not null) continue;
            var old = Feeds.TryGetValue(id, out var l) ? l?.Where(f => f is not null && !string.IsNullOrWhiteSpace(f.Url)).ToList() : null;
            var feeds = old is { Count: > 0 } ? old : defaults[id];
            Categories.Add(new FeedCategory
            {
                Id = id, NameEl = el, NameEn = en, BuiltIn = true,
                Feeds = feeds.Select(f => new FeedSource(f.Name, f.Url) { Enabled = f.Enabled }).ToList()
            });
        }

        Categories = Categories
            .Where(c => c is not null && !string.IsNullOrWhiteSpace(c.Id))
            .ToList();
        foreach (var c in Categories)
            c.Feeds = (c.Feeds ?? new()).Where(f => f is not null && !string.IsNullOrWhiteSpace(f.Url)).ToList();

        if (FindCategory(ActiveCategory) is null) ActiveCategory = Mode.ToString();

        // The watch list: keep the user's choices, add instruments introduced by newer versions.
        Watchlist = (Watchlist ?? new()).Where(i => i is not null && !string.IsNullOrWhiteSpace(i.Code)).ToList();
        foreach (var d in QuoteInstrument.Defaults())
            if (!Watchlist.Any(i => i.Source == d.Source && string.Equals(i.Code, d.Code, StringComparison.OrdinalIgnoreCase))) Watchlist.Add(d);
        foreach (var w in Watchlist.Where(w => w.Source == QuoteSource.Athens && string.IsNullOrEmpty(w.Market))) w.Market = "XATH";
        // Collapse duplicates saved by older settings panels. Preserve the user's enabled choice
        // if any duplicate was enabled, and keep distinct markets separate.
        Watchlist = Watchlist
            .GroupBy(i => (i.Source, Market: (i.Market ?? "").Trim().ToUpperInvariant(), Code: i.Code.Trim().ToUpperInvariant()))
            .Select(g => { var first = g.First(); first.Enabled = g.Any(i => i.Enabled); return first; })
            .ToList();
        QuoteRefreshSeconds = Math.Clamp(QuoteRefreshSeconds, 30, 3600);
        Portfolio = (Portfolio ?? new()).Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Code) && p.Quantity > 0 && p.AvgCost >= 0)
            .Select(p => { p.Currency = string.IsNullOrWhiteSpace(p.Currency) ? "EUR" : p.Currency.Trim().ToUpperInvariant(); p.Market ??= ""; return p; }).Take(500).ToList();
        PriceAlerts = (PriceAlerts ?? new()).Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Code) && x.Threshold > 0).ToList();
        BreakingKeywords = (BreakingKeywords ?? new()).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct().ToList();
        if (BreakingKeywords.Count == 0) BreakingKeywords = new(Breaking.DefaultKeywords);
        BreakingMaxAgeMinutes = Math.Clamp(BreakingMaxAgeMinutes, 10, 1440);
        if (BarBackdrop is not ("acrylic" or "blur" or "gradient")) BarBackdrop = "acrylic";
        if (TaskbarBarBackdrop is not ("acrylic" or "blur" or "gradient")) TaskbarBarBackdrop = "blur";
        if (BarColorMode is not ("theme" or "lightblue" or "accent" or "custom")) BarColorMode = "theme";
        if (TaskbarBarColorMode is not ("taskbar" or "lightblue" or "accent" or "custom")) TaskbarBarColorMode = "taskbar";
        BreakingDurationMinutes = Math.Clamp(BreakingDurationMinutes, 1, 240);
        if (GreekCities.Find(AlertCityId) is null) AlertCityId = "";
        LiveFeeds = (LiveFeeds ?? new()).Where(l => l is not null && !string.IsNullOrWhiteSpace(l.Url)).ToList();
        if (!Enum.IsDefined(MainContent)) MainContent = BarContent.News;
        WeatherCities = (WeatherCities ?? new()).Where(id => GreekCities.Find(id) is not null).Distinct().ToList();
        MiniWeatherWidth = Math.Clamp(MiniWeatherWidth, 200, 4000);
        MiniWeatherHeight = Math.Clamp(MiniWeatherHeight, 24, 200);
        if (!Enum.IsDefined(SecondContent) || SecondContent == BarContent.Both) SecondContent = BarContent.Stocks;
        if (Position == TickerPosition.TaskbarIntegrated) SecondBarEnabled = false;
        bool Exists(string id) => FindCategory(id) is not null;
        MainSlotA = BarSpec.Clean(MainSlotA, Exists); MainSlotB = BarSpec.Clean(MainSlotB, Exists);
        SecondSlotA = BarSpec.Clean(SecondSlotA, Exists); SecondSlotB = BarSpec.Clean(SecondSlotB, Exists);
        MiniSlotA = BarSpec.Clean(MiniSlotA, Exists); MiniSlotB = BarSpec.Clean(MiniSlotB, Exists);
        foreach (var m in MonitorBars ??= new()) { m.SlotA = BarSpec.Clean(m.SlotA, Exists); m.SlotB = BarSpec.Clean(m.SlotB, Exists); }
        WeatherFields = (WeatherFields ?? new()).Select(f => (f ?? "").Trim().ToLowerInvariant())
            .Where(f => f is "wind" or "humidity" or "feels" or "rain" or "cloud" or "pressure" or "gusts").Distinct().ToList();
        Feeds = new(); // migrated; not written back
    }
}

/// <summary>One extra bar on a monitor other than the primary one.</summary>
public sealed class MonitorBar
{
    /// <summary>Windows device name of the screen, e.g. \.\DISPLAY2.</summary>
    public string Device { get; set; } = "";
    public bool Enabled { get; set; }
    public BarContent Content { get; set; } = BarContent.News;

    /// <summary>Same as <see cref="AppConfig.MainSlotA"/>: empty = use <see cref="Content"/>.</summary>
    public string SlotA { get; set; } = "";
    public string SlotB { get; set; } = "";

    public BarSlots Slots() => BarSpec.Resolve(SlotA, SlotB, Content, false);
}
