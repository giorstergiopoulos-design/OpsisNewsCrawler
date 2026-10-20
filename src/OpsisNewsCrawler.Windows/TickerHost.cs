using OpsisNewsCrawler.Core;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Owns the bars (the main one, and optionally a second one inside the taskbar), their state, the settings and the
/// news / quotes refresh loops. Used by the standalone app and by the Opsis plugin. Create and use it on the UI thread.
/// </summary>
public sealed class TickerHost : IDisposable
{
    private static readonly TimeSpan MinRefreshGap = TimeSpan.FromSeconds(30);

    private readonly string configPath;
    private readonly Func<IntPtr, bool>? hostWindowDetector;
    private readonly RssNewsProvider news = new();
    private readonly QuoteService quotes = new();
    private readonly System.Windows.Forms.Timer refreshTimer = new();
    private readonly System.Windows.Forms.Timer quoteTimer = new();
    private readonly System.Windows.Forms.Timer snoozeTimer = new();
    private bool snoozed;
    private readonly CancellationTokenSource cts = new();
    private readonly ReadStore readStore = new();

    /// <summary>Articles kept with Ctrl+click.</summary>
    public ReadLaterStore ReadLater { get; } = new();
    private List<NewsItem> allItems = new();
    private IReadOnlyList<NewsItem> stockItems = Array.Empty<NewsItem>();
    private bool refreshing;
    private bool refreshingQuotes;
    private bool pendingForced;
    private DateTimeOffset? newsDataAt;
    private List<string> newsFailedFeeds = new();
    private bool hasRealItems;
    private bool hasQuotes;
    private bool disposed;
    private bool started;

    // breaking news
    private readonly System.Windows.Forms.Timer breakingTimer = new() { Interval = 5000 };
    private List<NewsItem> breakingItems = new();
    private bool breakingActive;
    private DateTimeOffset breakingUntil;
    private LivePreviewForm? livePreview;

    public AppConfig Config { get; }

    /// <summary>Scroll state of the main bar.</summary>
    public TickerState State { get; } = new();

    /// <summary>Scroll state of the second bar (inside the taskbar).</summary>
    public TickerState State2 { get; } = new();

    /// <summary>The main bar.</summary>
    public TickerWindow Window { get; }

    /// <summary>The second bar, inside the taskbar; null while it is switched off.</summary>
    public TickerWindow? SecondWindow { get; private set; }

    // One extra bar per other monitor (settings "Monitors"), each with its own scroll state.
    private readonly Dictionary<string, (TickerWindow Window, TickerState State)> monitorWindows = new();

    private IEnumerable<TickerWindow> ExtraWindows => monitorWindows.Values.Select(v => v.Window);

    /// <summary>A newer release found by the last check (automatic or "Check now"), or null.</summary>
    public UpdateInfo? AvailableUpdate { get; private set; }

    /// <summary>Raised on the UI thread when an update check found a newer release.</summary>
    public event Action<UpdateInfo>? UpdateFound;

    /// <summary>Asks GitHub about a newer release. Returns null for "up to date", throws only network exceptions.</summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        var current = Version.TryParse(AboutTexts.Version, out var v) ? v : new Version(0, 0);
        var found = await UpdateChecker.CheckAsync(current, cts.Token);
        Config.LastUpdateCheck = DateTimeOffset.Now;
        SaveConfig();
        AvailableUpdate = found;
        if (found is not null && !disposed)
        {
            void Raise() => UpdateFound?.Invoke(found);
            if (Window.IsHandleCreated && Window.InvokeRequired) Window.BeginInvoke(new Action(Raise)); else Raise();
        }
        return found;
    }

    private async Task AutoCheckForUpdatesAsync()
    {
        if (!Config.CheckForUpdates || DateTimeOffset.Now - Config.LastUpdateCheck < TimeSpan.FromHours(24)) return;
        try
        {
            var found = await CheckForUpdatesAsync();
            if (found is not null && Window.IsHandleCreated)
                Window.BeginInvoke(new Action(() => Window.ShowToast(Loc.T("msg.updateAvailable", found.Version, AboutTexts.Version))));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // offline or GitHub unreachable: try again at the next start
        }
    }

    public string ConfigPath => configPath;

    /// <summary>Raised when the user right-clicks a bar. The standalone app opens its window, the plugin asks Opsis to show its tab.</summary>
    public event Action? SettingsRequested;

    public TickerHost(string? configPath = null, Func<IntPtr, bool>? hostWindowDetector = null)
    {
        this.configPath = configPath ?? AppConfig.DefaultPath;
        this.hostWindowDetector = hostWindowDetector;
        Config = AppConfig.Load(this.configPath);

        State.SetMode(Config.Mode);
        Theme.SetMode(Config.ThemeMode, Config.OpacityPercent / 100.0);
        Loc.SetLanguage(Config.Language);

        LoadCachedNews();
        LoadCachedWeather();

        Window = new TickerWindow(State, hostWindowDetector);
        WireWindow(Window);
        Window.PlacementEdited += b =>
        {
            Config.CustomX = b.X;
            Config.CustomY = b.Y;
            Config.CustomWidth = b.Width;
            SaveConfig();
            CustomBoundsChanged?.Invoke();
        };

        refreshTimer.Tick += (_, _) =>
        {
            // While another program is in front the ticker is frozen; it refreshes when the desktop is back.
            if (!Window.IsPausedByForeground) _ = RefreshAsync(force: false);
        };
        quoteTimer.Tick += (_, _) =>
        {
            // Re-evaluate the interval as the exchange session opens/closes.
            quoteTimer.Interval = GetQuoteRefreshIntervalMilliseconds();
            if (!Window.IsPausedByForeground) _ = RefreshQuotesAsync();
        };
        Loc.Changed += OnLanguageChanged;
        weatherTimer.Tick += (_, _) => _ = RefreshWeatherAsync();
        alertTimer.Tick += (_, _) => _ = CheckWeatherAlertsAsync();
        snoozeTimer.Tick += (_, _) =>
        {
            snoozeTimer.Stop();
            if (disposed || !started || !Config.BarsEnabled) return;
            snoozed = false;
            StartBars();
            SnoozeChanged?.Invoke(false);
        };
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
        Window.LiveRequested += ToggleLivePreview;
        breakingTimer.Tick += (_, _) =>
        {
            if (breakingActive && DateTimeOffset.Now > breakingUntil) EndBreaking();
        };

        ApplyConfig();
    }

    private void WireWindow(TickerWindow w)
    {
        w.RefreshRequested += () =>
        {
            _ = RefreshAsync(force: false);
            _ = RefreshQuotesAsync();
        };
        w.ItemOpened += item =>
        {
            if (item.Kind != NewsItemKind.News) return;
            readStore.MarkRead(item.Url);
            if (breakingActive)
            {
                breakingItems.RemoveAll(i => i.Url == item.Url);
                if (breakingItems.Count == 0)
                {
                    EndBreaking();
                    return;
                }
            }
            if (Config.HideRead && allItems.Count > 0) PushAll();
        };
        w.SettingsRequested += () => SettingsRequested?.Invoke();
        w.WeatherRequested += ShowWeatherMap;
        w.ItemSaveRequested += item =>
        {
            var added = ReadLater.Add(item);
            w.ShowToast(Loc.T(added ? "msg.savedLater" : "msg.alreadySaved"));
        };
        w.ItemMarkReadRequested += item =>
        {
            readStore.MarkRead(item.Url);
            if (breakingActive)
            {
                breakingItems.RemoveAll(i => string.Equals(i.Url, item.Url, StringComparison.OrdinalIgnoreCase));
                if (breakingItems.Count == 0) EndBreaking();
            }
            if (Config.HideRead) PushAll();
            w.ShowToast(Loc.IsGreek ? "Σημειώθηκε ως διαβασμένο" : "Marked as read");
        };
        w.ScrollSpeedChanged += speed =>
        {
            Config.ScrollSpeed = (int)Math.Round(speed);
            Window.ScrollSpeed = Config.ScrollSpeed;
            if (SecondWindow is not null) SecondWindow.ScrollSpeed = Config.ScrollSpeed;
            foreach (var extra in ExtraWindows) extra.ScrollSpeed = Config.ScrollSpeed;
            SaveConfig();
        };
    }

    private void OnLanguageChanged()
    {
        // Texts built from the language (placeholders, quote labels) are rebuilt.
        _ = RefreshQuotesAsync(force: true);
        PushAll();
        UpdateMiniWeather();
    }

    /// <summary>Set by the Opsis plugin: the UI language then follows Opsis, not the crawler's own setting.</summary>
    public bool LanguageFollowsHost { get; set; }

    /// <summary>Pushes the values of <see cref="Config"/> into the windows and timers. Does not save.</summary>
    private GlobalHotkey? hotkey;

    /// <summary>Registers or removes the Ctrl+Alt+N hot key that shows / hides the bars.</summary>
    private void UpdateHotkey()
    {
        var want = Config.ToggleHotkey && !HostedByOpsis && !disposed;
        if (want && hotkey is null)
        {
            hotkey = new GlobalHotkey();
            hotkey.Pressed += () => SetBarsEnabled(!Config.BarsEnabled);
            if (!hotkey.Register(GlobalHotkey.ModControl | GlobalHotkey.ModAlt, 0x4E))
                Log.Write("Hot key Ctrl+Alt+N could not be registered (another program uses it).");
        }
        else if (!want && hotkey is not null)
        {
            hotkey.Dispose();
            hotkey = null;
        }
    }

    public void ApplyConfig()
    {
        if (!LanguageFollowsHost) Loc.SetLanguage(Config.Language);
        Theme.SetMode(Config.ThemeMode, Config.OpacityPercent / 100.0);
        UpdateHotkey();

        ConfigureWindow(Window, Config.Position, main: true);
        Window.SetBackdropStyle(Config.BarBackdrop);
        Window.SetBarColor(Config.BarColorMode, Config.BarCustomColor);
        Window.ReserveSpace = EffectiveReserveSpace;
        Window.SetCustomBounds(GetCustomBounds());
        Window.SetPosition(Config.Position);

        var wantSecond = Config.SecondBarEnabled && Config.Position != TickerPosition.TaskbarIntegrated;
        if (wantSecond && SecondWindow is null)
        {
            SecondWindow = new TickerWindow(State2, hostWindowDetector);
            WireWindow(SecondWindow);
            if (BarsOn) SecondWindow.StartTicker();
        }
        else if (!wantSecond && SecondWindow is not null)
        {
            SecondWindow.StopTicker();
            SecondWindow.Dispose();
            SecondWindow = null;
        }
        if (SecondWindow is not null)
        {
            ConfigureWindow(SecondWindow, TickerPosition.TaskbarIntegrated, main: false);
            SecondWindow.SetBackdropStyle(Config.TaskbarBarBackdrop);
            SecondWindow.SetBarColor(Config.TaskbarBarColorMode, Config.TaskbarBarCustomColor);
            SecondWindow.ReserveSpace = false;
            SecondWindow.SetPosition(TickerPosition.TaskbarIntegrated);
        }

        SyncMonitorWindows();

        refreshTimer.Interval = Config.RefreshIntervalSeconds * 1000;
        quoteTimer.Interval = GetQuoteRefreshIntervalMilliseconds();
        PushAll();
        if (BarsOn)
        {
            _ = RefreshQuotesAsync();
            _ = RefreshWeatherAsync();
            _ = RefreshExtraNewsAsync();
            UpdateMiniWeather();
        }
    }

    private int GetQuoteRefreshIntervalMilliseconds() =>
        QuoteRefreshPolicy.GetIntervalSeconds(Config.Watchlist, Config.QuoteRefreshSeconds, DateTimeOffset.Now) * 1000;

    /// <summary>Screens that currently get an extra bar: enabled in the settings, still connected and not the primary one.</summary>
    private IEnumerable<(MonitorBar Cfg, Screen Screen)> WantedMonitors() =>
        Config.MonitorBars.Where(m => m.Enabled)
            .Select(m => (Cfg: m, Screen: Screen.AllScreens.FirstOrDefault(s => string.Equals(s.DeviceName, m.Device, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Screen is { Primary: false })
            .Select(x => (x.Cfg, x.Screen!));

    private void SyncMonitorWindows()
    {
        var wanted = WantedMonitors().ToDictionary(x => x.Cfg.Device, StringComparer.OrdinalIgnoreCase);
        foreach (var gone in monitorWindows.Keys.Where(k => !wanted.ContainsKey(k)).ToList())
        {
            var (w, _) = monitorWindows[gone];
            w.StopTicker();
            w.Dispose();
            monitorWindows.Remove(gone);
        }
        foreach (var (device, x) in wanted)
        {
            if (!monitorWindows.TryGetValue(device, out var entry))
            {
                var state = new TickerState();
                state.SetMode(Config.Mode);
                var w = new TickerWindow(state, hostWindowDetector) { TargetScreen = x.Screen };
                WireWindow(w);
                entry = (w, state);
                monitorWindows[device] = entry;
                ConfigureWindow(w, TickerPosition.BottomTaskbar, main: false);
                w.FollowTaskbar = true;
                w.ReserveSpace = false;
                w.SetBackdropStyle(Config.BarBackdrop);
                w.SetBarColor(Config.BarColorMode, Config.BarCustomColor);
                w.SetPosition(TickerPosition.BottomTaskbar);
                if (BarsOn) w.StartTicker();
            }
            else
            {
                entry.Window.TargetScreen = x.Screen; // the screen may have moved or changed size
                ConfigureWindow(entry.Window, TickerPosition.BottomTaskbar, main: false);
                entry.Window.FollowTaskbar = true;
                entry.Window.ReserveSpace = false;
                entry.Window.SetBackdropStyle(Config.BarBackdrop);
                entry.Window.SetBarColor(Config.BarColorMode, Config.BarCustomColor);
                entry.Window.SetPosition(TickerPosition.BottomTaskbar);
            }
        }
    }

    private void OnDisplaysChanged(object? sender, EventArgs e)
    {
        // A monitor was connected, disconnected or rearranged: add, remove and move the extra bars.
        if (disposed || !Window.IsHandleCreated) return;
        try { Window.BeginInvoke(new Action(() => { if (!disposed) { SyncMonitorWindows(); PushAll(); } })); }
        catch (InvalidOperationException) { }
    }

    private void ConfigureWindow(TickerWindow w, TickerPosition position, bool main)
    {
        w.PauseOnForeground = Config.PauseOnOtherWindow;
        w.FollowTaskbar = Config.FollowTaskbar;
        w.PauseOnHover = Config.PauseOnHover;
        w.ShowPreview = Config.ShowPreview;
        w.ShowIcons = Config.ShowIcons;
        w.ScrollSpeed = Config.ScrollSpeed;
        w.SetThemeModeName(Config.ThemeMode);
        w.EdgeFade = Config.EdgeFade;
        w.TaskbarAutoDetect = Config.TaskbarAutoDetect;
        w.TaskbarLeftOffset = Config.TaskbarLeftOffset;
        w.TaskbarRightOffset = Config.TaskbarRightOffset;
    }

    /// <summary>The custom strip in screen coordinates; a first-time default sits at the bottom of the primary screen.</summary>
    public Rectangle GetCustomBounds()
    {
        if (Config.CustomWidth >= 200) return new Rectangle(Config.CustomX, Config.CustomY, Config.CustomWidth, 42);
        var wa = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        return new Rectangle(wa.Left + 100, wa.Bottom - 42 - 40, Math.Min(900, wa.Width - 200), 42);
    }

    /// <summary>Stores and applies a custom strip given in screen coordinates (width at least 200).</summary>
    public void SetCustomBounds(int x, int y, int width)
    {
        Config.CustomX = x;
        Config.CustomY = y;
        Config.CustomWidth = Math.Max(200, width);
        Window.SetCustomBounds(GetCustomBounds());
        SaveConfig();
    }

    /// <summary>Lets the user move/resize the strip with the mouse (Custom position only).</summary>
    public void SetPositionEditMode(bool on)
    {
        if (on && Config.Position != TickerPosition.Custom) SetPosition(TickerPosition.Custom);
        Window.SetEditMode(on);
    }

    public bool IsPositionEditMode => Window.IsEditMode;

    /// <summary>Raised after the user dragged/resized the strip with the mouse, so a settings page can refresh its boxes.</summary>
    public event Action? CustomBoundsChanged;

    /// <summary>Set by the Opsis plugin before the first ApplyConfig: Opsis widgets, DeskStrip and DeskZones follow the work area.</summary>
    public bool HostedByOpsis { get; set; }

    public bool EffectiveReserveSpace => Config.ReserveScreenSpace ?? HostedByOpsis;

    public void SaveConfig() => Config.Save(configPath);

    private bool BarsOn => started && Config.BarsEnabled && !snoozed;

    public void Start()
    {
        if (disposed) return;
        started = true;
        PushAll();
        if (Config.BarsEnabled) StartBars();
        _ = AutoCheckForUpdatesAsync();
    }

    private void StartBars()
    {
        Window.StartTicker();
        SecondWindow?.StartTicker();
        foreach (var extra in ExtraWindows) extra.StartTicker();
        refreshTimer.Start();
        quoteTimer.Start();
        weatherTimer.Start();
        alertTimer.Start();
        breakingTimer.Start();
        _ = RefreshAsync(force: true);
        _ = RefreshQuotesAsync();
        _ = RefreshWeatherAsync();
        _ = CheckWeatherAlertsAsync();
        UpdateMiniWeather();
    }

    private void StopBars()
    {
        breakingTimer.Stop();
        CloseLivePreview();
        weatherMap?.Close();
        weatherMap = null;
        refreshTimer.Stop();
        quoteTimer.Stop();
        weatherTimer.Stop();
        alertTimer.Stop();
        Window.StopTicker();
        SecondWindow?.StopTicker();
        foreach (var extra in ExtraWindows) extra.StopTicker();
        UpdateMiniWeather(); // BarsOn is false now: the small bar goes away too
    }

    /// <summary>Raised after the master switch changed (tray menu and settings keep their check marks in step).</summary>
    public event Action? BarsEnabledChanged;

    /// <summary>Raised when a temporary snooze starts or ends.</summary>
    public event Action<bool>? SnoozeChanged;

    /// <summary>Temporarily hides all bars without changing the saved enabled setting.</summary>
    public void SnoozeBars(TimeSpan duration)
    {
        if (disposed || !started || !Config.BarsEnabled) return;
        var milliseconds = (int)Math.Clamp(duration.TotalMilliseconds, 1000, 60 * 60 * 1000);
        snoozeTimer.Stop();
        snoozeTimer.Interval = milliseconds;
        snoozed = true;
        StopBars();
        snoozeTimer.Start();
        SnoozeChanged?.Invoke(true);
    }

    /// <summary>Cancels a temporary snooze and restores the bars if they are enabled.</summary>
    public void CancelSnooze()
    {
        if (!snoozed) return;
        snoozeTimer.Stop();
        snoozed = false;
        if (started && Config.BarsEnabled) StartBars();
        SnoozeChanged?.Invoke(false);
    }

    /// <summary>Turns every bar on or off. Off: the bars disappear and nothing is downloaded.</summary>
    public void SetBarsEnabled(bool on)
    {
        if (Config.BarsEnabled == on) return;
        Config.BarsEnabled = on;
        var wasSnoozed = snoozed;
        snoozeTimer.Stop();
        snoozed = false;
        if (wasSnoozed) SnoozeChanged?.Invoke(false);
        SaveConfig();
        if (started)
        {
            if (on) StartBars(); else StopBars();
        }
        BarsEnabledChanged?.Invoke();
    }

    public void Stop()
    {
        started = false;
        snoozeTimer.Stop();
        snoozed = false;
        breakingTimer.Stop();
        CloseLivePreview();
        weatherMap?.Close();
        weatherMap = null;
        refreshTimer.Stop();
        quoteTimer.Stop();
        alertTimer.Stop();
        Window.StopTicker();
        SecondWindow?.StopTicker();
        foreach (var extra in ExtraWindows) extra.StopTicker();
    }

    public void Pause()
    {
        Window.PauseTicker();
        SecondWindow?.PauseTicker();
        foreach (var extra in ExtraWindows) extra.PauseTicker();
    }

    public void Resume()
    {
        Window.ResumeTicker();
        SecondWindow?.ResumeTicker();
        foreach (var extra in ExtraWindows) extra.ResumeTicker();
    }

    public void Refresh()
    {
        _ = RefreshAsync(force: true);
        _ = RefreshQuotesAsync(force: true);
    }

    public void SetMode(TickerMode mode) => SetCategory(mode.ToString());

    /// <summary>Shows another category (built-in mode name or user category id). Unknown ids are ignored.</summary>
    public void SetCategory(string id)
    {
        var cat = Config.FindCategory(id);
        if (cat is null || string.Equals(Config.ActiveCategory, cat.Id, StringComparison.OrdinalIgnoreCase)) return;
        Config.ActiveCategory = cat.Id;
        if (Enum.TryParse<TickerMode>(cat.Id, true, out var mode))
        {
            Config.Mode = mode;
            State.SetMode(mode);
        }
        SaveConfig();
        ReloadNews();
    }

    /// <summary>Call after the feed list of the active category was edited: fetch again from the start.</summary>
    public void ReloadNews()
    {
        // A different set of news: start from the beginning.
        State.SetScrollOffset(0);
        State2.SetScrollOffset(0);
        allItems = new();
        newsFailedFeeds = new();
        LoadCachedNews();
        _ = RefreshAsync(force: true);
    }

    public void SetPosition(TickerPosition position)
    {
        Config.Position = position;
        if (position == TickerPosition.TaskbarIntegrated) Config.SecondBarEnabled = false;
        Window.SetPosition(position);
        ApplyConfig();
        SaveConfig();
    }

    // ---- what the bars show ---------------------------------------------------------------

    /// <summary>What every bar that is on shows right now (main, second, monitors, desktop bar).</summary>
    private IEnumerable<BarSlots> AllBarSlots()
    {
        yield return Config.MainSlots();
        if (Config.SecondBarEnabled) yield return Config.SecondSlots();
        foreach (var (cfg, _) in WantedMonitors()) yield return cfg.Slots();
        if (Config.MiniWeatherEnabled) yield return Config.MiniSlots();
    }

    private bool Needs(SlotKind kind) => AllBarSlots().Any(slots => slots.Has(kind));
    private bool NeedsNews => Needs(SlotKind.News);
    private bool NeedsStocks => Needs(SlotKind.Stocks);

    private bool IsActiveCategory(string? id) => string.IsNullOrWhiteSpace(id) || string.Equals(id, Config.ActiveCategory, StringComparison.OrdinalIgnoreCase);

    /// <summary>News categories other than the active one that some bar shows (a half of a split bar, or a whole bar).</summary>
    private List<string> ExtraNewsCategories() => AllBarSlots().SelectMany(slots => slots.All())
        .Where(slot => slot.Kind == SlotKind.News && !IsActiveCategory(slot.Category))
        .Select(slot => slot.Category!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // ---- no internet / a provider is down: the last stored data plus a message ----------------

    private static bool InternetAvailable()
    {
        try { return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(); }
        catch (Exception ex) when (ex is System.Net.NetworkInformation.NetworkInformationException or InvalidOperationException) { return true; }
    }

    /// <summary>A scrolling warning: no internet, or which provider does not answer, and the time of the data still shown.</summary>
    private static NewsItem? Notice(IReadOnlyCollection<string> failedProviders, DateTimeOffset? dataAt)
    {
        if (failedProviders.Count == 0) return null;
        var head = InternetAvailable() ? Loc.T("net.providerDown", string.Join(", ", failedProviders)) : Loc.T("net.offline");
        var tail = dataAt is { } at ? Loc.T("net.showingStored", at.ToLocalTime().ToString("dd/MM HH:mm")) : Loc.T("net.noStored");
        return Placeholder(head + " — " + tail);
    }

    private static IReadOnlyList<NewsItem> WithNotice(NewsItem? notice, IReadOnlyList<NewsItem> items) =>
        notice is null ? items : new[] { notice }.Concat(items).ToList();

    private void LoadCachedNews()
    {
        var cached = DataCache.Load<List<NewsItem>>("news-" + Config.ActiveCategory);
        if (cached?.Data is not { Count: > 0 } items) return;
        allItems = items;
        newsDataAt = cached.At;
        hasRealItems = true;
    }

    private void LoadCachedWeather()
    {
        var cached = DataCache.Load<List<WeatherReading>>("weather");
        if (cached?.Data is not { Count: > 0 } readings) return;
        weatherReadings = readings;
        weatherDataAt = cached.At;
        hasWeather = true;
    }

    private IReadOnlyList<NewsItem> NewsForDisplay() =>
        WithNotice(Notice(newsFailedFeeds, newsDataAt), NewsItemsOnly());

    private IReadOnlyList<NewsItem> NewsItemsOnly() => FilterNews(allItems, hasRealItems);

    private IReadOnlyList<NewsItem> FilterNews(List<NewsItem> source, bool hasReal)
    {
        if (source.Count == 0)
            return new[] { Placeholder(hasReal ? Loc.T("ph.newsUnavailable") : Loc.T("ph.loadingNews")) };
        IReadOnlyList<NewsItem> candidates = NewsKeywordFilter.Filter(source, Config.MutedNewsKeywords);
        if (Config.StockNewsOnly) candidates = StockNewsFilter.Filter(candidates, Config.Watchlist);
        var visible = Config.HideRead ? candidates.Where(i => !readStore.IsRead(i.Url)).ToList() : candidates.ToList();
        if (visible.Count > 0) return visible;
        if (Config.StockNewsOnly && candidates.Count == 0)
            return new[] { Placeholder(Loc.IsGreek ? "Δεν βρέθηκαν ειδήσεις για τις επιλεγμένες μετοχές" : "No stories matched your selected shares") };
        return new[] { Placeholder(Loc.T("ph.allRead")) };
    }

    // News of categories that are not the active one (shown in a bar or in one half of a split bar).
    private readonly Dictionary<string, List<NewsItem>> extraNews = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> extraNewsAt = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> extraNewsFailed = new(StringComparer.OrdinalIgnoreCase);
    private readonly RssNewsProvider extraNewsProvider = new();
    private bool refreshingExtra;

    private IReadOnlyList<NewsItem> CategoryNewsForDisplay(string id)
    {
        if (!extraNews.TryGetValue(id, out var items))
        {
            // First time: the stored copy of the last run, if any.
            var cached = DataCache.Load<List<NewsItem>>("news-" + id);
            items = cached?.Data ?? new List<NewsItem>();
            extraNews[id] = items;
            if (cached is not null && items.Count > 0) extraNewsAt[id] = cached.At;
        }
        var failed = extraNewsFailed.TryGetValue(id, out var f) ? f : new List<string>();
        DateTimeOffset? at = extraNewsAt.TryGetValue(id, out var t) ? t : null;
        return WithNotice(Notice(failed, at), FilterNews(items, items.Count > 0));
    }

    /// <summary>Fetches the categories of <see cref="ExtraNewsCategories"/>, one after the other, and keeps (and stores) the last good result of each.</summary>
    private async Task RefreshExtraNewsAsync()
    {
        if (disposed || refreshingExtra) return;
        var wanted = ExtraNewsCategories();
        if (wanted.Count == 0) return;
        refreshingExtra = true;
        try
        {
            foreach (var id in wanted)
            {
                if (disposed || cts.IsCancellationRequested) return;
                var items = await extraNewsProvider.FetchAsync(Config.GetFeeds(id), Config.MaxItemsPerFeed, cts.Token, Config.NewsMaxAgeHours, Config.NewsSortNewestFirst);
                var failedFeeds = extraNewsProvider.LastFailedFeeds.ToList();
                var category = id;
                void Apply()
                {
                    if (disposed) return;
                    if (items.Count > 0)
                    {
                        extraNews[category] = items.ToList();
                        extraNewsAt[category] = DateTimeOffset.Now;
                        extraNewsFailed.Remove(category);
                        DataCache.Save("news-" + category, extraNews[category]);
                    }
                    else extraNewsFailed[category] = failedFeeds;
                    PushAll();
                }
                if (Window.IsHandleCreated && Window.InvokeRequired) Window.Invoke(new Action(Apply)); else Apply();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Write($"extra news failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            refreshingExtra = false;
        }
    }

    private IReadOnlyList<NewsItem> StocksForDisplay()
    {
        var failed = quotes.LastFailed.Select(QuoteService.ProviderName).Distinct().ToList();
        var storedAt = quotes.StaleSources.Count > 0 ? quotes.StaleSources.Values.Min() : (DateTimeOffset?)null;
        var notice = Notice(failed, storedAt);
        if (stockItems.Count > 0) return WithNotice(notice, stockItems);
        return new[] { notice ?? Placeholder(hasQuotes ? Loc.T("ph.quotesUnavailable") : Loc.T("ph.loadingQuotes")) };
    }

    private static NewsItem Placeholder(string text) => new(text, "", Loc.T("app.name"), DateTimeOffset.Now);

    /// <summary>Puts the current news / quotes into every bar, keeping the scroll positions.</summary>
    private void PushAll()
    {
        if (disposed) return;
        if (breakingActive && breakingItems.Count > 0)
        {
            // Urgent headlines take over the main bar until they are read or time out.
            Window.SetSplit(false);
            Window.SetItems(breakingItems, State.ScrollOffset);
        }
        else
        {
            PushTo(Window, State, Config.MainSlots());
        }
        if (SecondWindow is not null) PushTo(SecondWindow, State2, Config.SecondSlots());
        foreach (var (cfg, _) in WantedMonitors())
            if (monitorWindows.TryGetValue(cfg.Device, out var extra)) PushTo(extra.Window, extra.State, cfg.Slots());
        if (miniWeather is not null) PushTo(miniWeather, miniState, Config.MiniSlots());
    }

    private void PushTo(TickerWindow w, TickerState st, BarSlots slots)
    {
        if (slots.B is not { } second)
        {
            w.SetSplit(false);
            w.SetItems(ItemsFor(slots.A), st.ScrollOffset);
            return;
        }
        w.SetSplit(true);
        w.SetItems(ItemsFor(slots.A), st.ScrollOffset);
        w.SetItemsB(ItemsFor(second), st.ScrollOffsetB);
    }

    private IReadOnlyList<NewsItem> ItemsFor(BarSlot slot) => slot.Kind switch
    {
        SlotKind.Stocks => StocksForDisplay(),
        SlotKind.Weather => WeatherForDisplay(),
        _ => IsActiveCategory(slot.Category) ? NewsForDisplay() : CategoryNewsForDisplay(slot.Category!)
    };

    /// <summary>Forgets which articles were read; they show up in the strip again.</summary>
    public void ResetReadList()
    {
        readStore.Clear();
        if (allItems.Count > 0) PushAll();
    }

    private async Task RefreshAsync(bool force)
    {
        if (disposed) return;
        if (!NeedsNews) return;
        if (refreshing)
        {
            // Remember a forced request (e.g. mode change) and run it as soon as the current one ends.
            if (force) pendingForced = true;
            return;
        }
        if (!force && State.LastNewsRefresh is { } last && DateTimeOffset.UtcNow - last < MinRefreshGap) return;

        refreshing = true;
        try
        {
            var category = Config.ActiveCategory;
            var items = await news.FetchAsync(Config.GetFeeds(category), Config.MaxItemsPerFeed, cts.Token, Config.NewsMaxAgeHours, Config.NewsSortNewestFirst);
            var failedFeeds = news.LastFailedFeeds.ToList();
            if (disposed || !string.Equals(category, Config.ActiveCategory, StringComparison.OrdinalIgnoreCase)) return; // the category changed meanwhile; a new refresh is running

            void Apply()
            {
                if (disposed) return;
                State.MarkNewsRefresh();
                if (items.Count > 0)
                {
                    hasRealItems = true;
                    allItems = items.ToList();
                    newsDataAt = DateTimeOffset.Now;
                    newsFailedFeeds = new();
                    DataCache.Save("news-" + category, allItems);
                    DetectBreaking(allItems);
                }
                else
                {
                    // Nothing came back: keep (and say that we keep) what we showed or stored before.
                    newsFailedFeeds = failedFeeds;
                }
                PushAll();
            }

            if (Window.IsHandleCreated && Window.InvokeRequired) Window.Invoke(new Action(Apply));
            else Apply();
            await RefreshExtraNewsAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Opsis News Crawler: refresh failed: {ex}");
        }
        finally
        {
            refreshing = false;
            if (pendingForced && !disposed)
            {
                pendingForced = false;
                _ = RefreshAsync(force: true);
            }
        }
    }

    // ---- breaking news and the live preview ----------------------------------------------

    private bool HasLive => Config.LiveFeeds.Any(f => f.Enabled && Breaking.ToPlayableUrl(f.Url) is not null);

    /// <summary>Finds new urgent headlines; the first time one is seen the intro plays and the bar turns red.</summary>
    private void DetectBreaking(List<NewsItem> items)
    {
        if (!Config.BreakingEnabled || (!Config.MainSlots().Has(SlotKind.News) && !breakingActive)) return;
        var maxAge = TimeSpan.FromMinutes(Config.BreakingMaxAgeMinutes);
        var urgent = items.Where(i => !NewsKeywordFilter.IsMuted(i, Config.MutedNewsKeywords) && Breaking.IsBreaking(i, Config.BreakingKeywords, maxAge) && !readStore.IsRead(i.Url))
            .Select(i => i with { Breaking = true }).ToList();
        if (urgent.Count == 0) return;

        var isNew = urgent.Where(i => !readStore.IsRead("breaking:" + i.Url)).ToList();
        foreach (var i in isNew) readStore.MarkRead("breaking:" + i.Url); // remembered, so a restart does not replay the intro
        // Price and weather alerts have no link: they stay on the bar next to the urgent headlines.
        breakingItems = urgent.Concat(breakingItems.Where(i => string.IsNullOrEmpty(i.Url))).ToList();
        breakingUntil = DateTimeOffset.Now.AddMinutes(Config.BreakingDurationMinutes);
        if (!breakingActive && isNew.Count > 0) StartBreaking();
        else if (breakingActive)
        {
            if (isNew.Count > 0) PlayAlertSound();
            Window.SetLiveAvailable(HasLive);
        }
    }

    /// <summary>Shows an alert on the bar (like an urgent headline) for every price limit that was just passed.</summary>
    private void CheckPriceAlerts()
    {
        if (disposed || Config.PriceAlerts.Count == 0) return;
        var hits = PriceAlertCheck.Evaluate(Config.PriceAlerts, quotes.LastQuotes);
        if (hits.Count == 0) return;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("el-GR");
        foreach (var (alert, quote) in hits)
        {
            var label = string.IsNullOrWhiteSpace(alert.Label) ? quote.Symbol : alert.Label;
            var text = Loc.Pick(
                $"🔔 {label} {(alert.Above ? "πάνω από" : "κάτω από")} {alert.Threshold.ToString("0.00##", culture)}: τώρα {quote.Price.ToString("0.00##", culture)}",
                $"🔔 {label} {(alert.Above ? "above" : "below")} {alert.Threshold.ToString("0.00##", culture)}: now {quote.Price.ToString("0.00##", culture)}");
            breakingItems.Add(new NewsItem(text, "", Loc.Pick("Ειδοποίηση τιμής", "Price alert"), DateTimeOffset.Now,
                Summary: label, Kind: NewsItemKind.News, Trend: alert.Above ? 1 : -1, Breaking: true));
        }
        breakingUntil = DateTimeOffset.Now.AddMinutes(Config.BreakingDurationMinutes);
        if (!breakingActive) StartBreaking();
        PushAll();
    }

    // ---- weather and map of a city (click on the weather line) ---------------------------------

    private WeatherMapForm? weatherMap;

    private void ShowWeatherMap(NewsItem item, Rectangle barBounds)
    {
        if (disposed || !Config.WeatherMapOnClick || GreekCities.Find(item.Key) is not { } city) return;
        var reading = weatherReadings.FirstOrDefault(r => string.Equals(r.CityId, city.Id, StringComparison.OrdinalIgnoreCase));
        weatherMap?.Close();
        weatherMap = new WeatherMapForm(city, reading, weatherFailed, barBounds);
        var shown = weatherMap;
        shown.FormClosed += (_, _) => { if (ReferenceEquals(weatherMap, shown)) weatherMap = null; };
        shown.Show();
    }

    private void StartBreaking()
    {
        breakingActive = true;
        PlayAlertSound();
        Window.StartBreaking();
        Window.SetLiveAvailable(HasLive);
    }

    /// <summary>The short sound of an urgent alert, when the user wants it.</summary>
    private void PlayAlertSound()
    {
        if (!Config.BreakingSound) return;
        AlertSound.Play();
    }

    // ---- severe weather for the user's area ---------------------------------------------------

    private readonly WeatherAlertService alertService = new();
    private readonly System.Windows.Forms.Timer alertTimer = new() { Interval = 30 * 60 * 1000 };
    private HashSet<string> weatherAlertKeys = new();
    private bool checkingAlerts;

    /// <summary>
    /// Every 30 minutes: warnings of severe weather in the next 12 hours for the chosen city. A warning shows once like an urgent headline
    /// (with the sound) and again only after the condition has passed. These are computed from the forecast, not the official bulletin.
    /// </summary>
    private async Task CheckWeatherAlertsAsync()
    {
        if (disposed || checkingAlerts || !Config.WeatherAlertsEnabled || !Config.BreakingEnabled) return;
        checkingAlerts = true;
        try
        {
            var city = Config.ResolveAlertCity();
            var found = await alertService.FetchAsync(city, cts.Token);
            if (found is null || disposed) return;
            void Apply()
            {
                if (disposed) return;
                var keys = found.Select(a => city.Id + ":" + a.Kind).ToHashSet();
                var fresh = found.Where(a => !weatherAlertKeys.Contains(city.Id + ":" + a.Kind)).ToList();
                weatherAlertKeys = keys;
                if (fresh.Count == 0) return;
                AddWeatherAlertItems(city, fresh);
            }
            if (Window.IsHandleCreated && Window.InvokeRequired) Window.BeginInvoke(new Action(Apply)); else Apply();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Write($"weather alerts failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            checkingAlerts = false;
        }
    }

    private void AddWeatherAlertItems(GreekCity city, IReadOnlyList<WeatherAlert> alerts)
    {
        var name = Loc.IsGreek ? city.NameEl : city.NameEn;
        foreach (var a in alerts)
        {
            var text = $"⚠ {name}: {Loc.Pick(a.TextEl, a.TextEn)}";
            breakingItems.Add(new NewsItem(text, "", Loc.Pick("Έκτακτο καιρικό φαινόμενο", "Severe weather"), DateTimeOffset.Now,
                Summary: Loc.Pick("Αυτόματη ειδοποίηση από την πρόγνωση Open-Meteo για τις επόμενες 12 ώρες· δεν είναι επίσημο δελτίο της ΕΜΥ.",
                    "Automatic warning from the Open-Meteo forecast for the next 12 hours; not the official HNMS bulletin."),
                Kind: NewsItemKind.News, Trend: -1, Breaking: true));
        }
        breakingUntil = DateTimeOffset.Now.AddMinutes(Config.BreakingDurationMinutes);
        if (!breakingActive) StartBreaking(); else PlayAlertSound();
        PushAll();
    }

    /// <summary>For testing: shows a sample severe-weather warning for the chosen city.</summary>
    public void SimulateWeatherAlert() =>
        AddWeatherAlertItems(Config.ResolveAlertCity(), new[] { new WeatherAlert("test", "Δοκιμαστική ειδοποίηση: ισχυροί άνεμοι, ριπές έως 80 km/h", "Test warning: strong winds, gusts up to 80 km/h", DateTimeOffset.Now) });

    private void EndBreaking()
    {
        if (!breakingActive) return;
        breakingActive = false;
        breakingItems = new();
        CloseLivePreview();
        Window.EndBreaking();
        PushAll();
    }

    /// <summary>For testing: behaves as if the given headline had just been published as urgent.</summary>
    public void SimulateBreaking(string title)
    {
        var item = new NewsItem(title, "https://example.com/breaking-test", "Test", DateTimeOffset.Now, "Breaking news test", null, NewsItemKind.News, 0, true);
        breakingItems = new List<NewsItem> { item };
        breakingUntil = DateTimeOffset.Now.AddMinutes(Config.BreakingDurationMinutes);
        if (!breakingActive) StartBreaking();
        PushAll();
    }

    private void ToggleLivePreview()
    {
        if (livePreview is { IsDisposed: false })
        {
            CloseLivePreview();
            return;
        }
        var feed = Config.LiveFeeds.FirstOrDefault(f => f.Enabled && Breaking.ToPlayableUrl(f.Url) is not null);
        if (feed is null) return;
        var playable = Breaking.ToPlayableUrl(feed.Url)!;
        livePreview = new LivePreviewForm(string.IsNullOrWhiteSpace(feed.Name) ? new Uri(playable).Host : feed.Name, playable, Window.Bounds);
        livePreview.FormClosed += (_, _) => livePreview = null;
        livePreview.Show();
    }

    private void CloseLivePreview()
    {
        if (livePreview is { IsDisposed: false }) livePreview.Close();
        livePreview = null;
    }

    private bool quotesFailed;
    private bool quoteRetryPending;

    // ---- weather ---------------------------------------------------------------------------------

    private readonly WeatherService weather = new();
    private readonly System.Windows.Forms.Timer weatherTimer = new() { Interval = 15 * 60 * 1000 };
    private IReadOnlyList<WeatherReading> weatherReadings = Array.Empty<WeatherReading>();
    private bool refreshingWeather;
    private bool hasWeather;
    private bool weatherFailed;
    private DateTimeOffset? weatherDataAt;
    private TickerWindow? miniWeather;
    private readonly TickerState miniState = new();

    private bool NeedsWeather => Needs(SlotKind.Weather);

    private IReadOnlyList<NewsItem> WeatherForDisplay()
    {
        if (Config.WeatherCities.Count == 0) return new[] { Placeholder(Loc.T("ph.noCities")) };
        var items = WeatherService.ToItems(Config.WeatherCities, weatherReadings, Loc.IsGreek, Config.WeatherFields);
        var notice = weatherFailed ? Notice(new[] { "Open-Meteo" }, weatherDataAt) : null;
        if (items.Count > 0) return WithNotice(notice, items);
        return new[] { notice ?? Placeholder(hasWeather ? Loc.T("ph.weatherUnavailable") : Loc.T("ph.loadingWeather")) };
    }

    private async Task RefreshWeatherAsync()
    {
        if (disposed || !NeedsWeather || refreshingWeather) return;
        refreshingWeather = true;
        try
        {
            var ids = Config.WeatherCities.Distinct().ToList();
            var readings = await weather.FetchAsync(ids, cts.Token);
            var failed = weather.LastFailed;
            void Apply()
            {
                if (disposed) return;
                hasWeather = true;
                weatherFailed = failed;
                if (readings.Count > 0)
                {
                    weatherReadings = readings; // a failed request returns the previous readings: they stay, with a message
                    if (!failed)
                    {
                        weatherDataAt = DateTimeOffset.Now;
                        DataCache.Save("weather", readings.ToList());
                    }
                }
                PushAll();
                UpdateMiniWeather();
            }
            if (!Window.IsHandleCreated) return;
            if (Window.InvokeRequired) Window.BeginInvoke(new Action(Apply)); else Apply();
            if (readings.Count == 0 || failed) _ = RetryWeatherLater();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Write($"weather failed: {ex.GetType().Name}: {ex.Message}");
            hasWeather = true;
            weatherFailed = true;
            _ = RetryWeatherLater();
        }
        finally
        {
            refreshingWeather = false;
        }
    }

    private async Task RetryWeatherLater()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), cts.Token);
            if (!disposed && BarsOn) _ = RefreshWeatherAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Where the small weather bar is: the saved place, or the middle of the primary desktop.</summary>
    public Rectangle GetMiniWeatherBounds()
    {
        var wa = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        var w = Math.Min(Config.MiniWeatherWidth, wa.Width);
        var h = Config.MiniWeatherHeight;
        var onScreen = Config.MiniWeatherX != int.MinValue && Config.MiniWeatherY != int.MinValue &&
                       Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(new Rectangle(Config.MiniWeatherX, Config.MiniWeatherY, w, h)));
        return onScreen
            ? new Rectangle(Config.MiniWeatherX, Config.MiniWeatherY, w, h)
            : new Rectangle(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2, w, h);
    }

    /// <summary>Puts the small weather bar back in the middle of the desktop.</summary>
    public void CenterMiniWeather()
    {
        Config.MiniWeatherX = int.MinValue;
        Config.MiniWeatherY = int.MinValue;
        SaveConfig();
        UpdateMiniWeather();
        MiniWeatherBoundsChanged?.Invoke();
    }

    /// <summary>Raised after the small weather bar was moved or resized with the mouse.</summary>
    public event Action? MiniWeatherBoundsChanged;

    /// <summary>Lets the user move the small weather bar with the mouse (and change its width by the ends).</summary>
    public void SetMiniWeatherEditMode(bool on) => miniWeather?.SetEditMode(on);

    public bool IsMiniWeatherEditMode => miniWeather?.IsEditMode == true;

    /// <summary>Creates, updates or removes the small weather bar of the desktop according to the settings. It is an ordinary bar (same colour, transparency, material, fading ends), only smaller, centred and on the desktop.</summary>
    private void UpdateMiniWeather()
    {
        if (disposed) return;
        if (!(BarsOn && Config.MiniWeatherEnabled))
        {
            if (miniWeather is not null)
            {
                miniWeather.StopTicker();
                miniWeather.Dispose();
                miniWeather = null;
            }
            return;
        }
        var created = miniWeather is null;
        if (created)
        {
            miniWeather = new TickerWindow(miniState, hostWindowDetector);
            WireWindow(miniWeather);
            miniWeather.PlacementEdited += b =>
            {
                Config.MiniWeatherX = b.X;
                Config.MiniWeatherY = b.Y;
                Config.MiniWeatherWidth = b.Width;
                SaveConfig();
                MiniWeatherBoundsChanged?.Invoke();
            };
        }
        var w = miniWeather!;
        ConfigureWindow(w, TickerPosition.Custom, main: false);
        w.FollowTaskbar = false;
        w.ReserveSpace = false;
        w.SetCenterWhenStatic(true);
        w.SetBackdropStyle(Config.BarBackdrop);
        w.SetBarColor(Config.BarColorMode, Config.BarCustomColor);
        w.SetBarHeight(Config.MiniWeatherHeight);
        w.SetDesktopLayer(!Config.MiniWeatherOnTop);
        w.SetCustomBounds(GetMiniWeatherBounds());
        w.SetPosition(TickerPosition.Custom);
        PushTo(w, miniState, Config.MiniSlots());
        if (created) w.StartTicker();
    }

    /// <summary>After a failed run try again in a minute instead of waiting for the normal interval.</summary>
    private async void ScheduleQuoteRetry()
    {
        if (quoteRetryPending || disposed) return;
        quoteRetryPending = true;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), cts.Token);
            if (!disposed && BarsOn) _ = RefreshQuotesAsync();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            quoteRetryPending = false;
        }
    }

    private async Task RefreshQuotesAsync(bool force = false)
    {
        if (disposed || !NeedsStocks || refreshingQuotes) return;
        refreshingQuotes = true;
        try
        {
            void Show(IReadOnlyList<NewsItem> list)
            {
                void Apply()
                {
                    if (disposed) return;
                    hasQuotes = true;
                    if (list.Count > 0 || stockItems.Count == 0) stockItems = list; // nothing came back: keep what is shown
                    PushAll();
                }

                if (!Window.IsHandleCreated) return;
                if (Window.InvokeRequired) Window.BeginInvoke(new Action(Apply));
                else Apply();
            }

            // Each source shows up as soon as it has answered; a slow one cannot keep the others waiting.
            var items = await quotes.FetchItemsAsync(Config.Watchlist, Loc.IsGreek, cts.Token, Show);
            quotesFailed = quotes.LastFailed.Count > 0 || items.Count == 0 && Config.Watchlist.Any(w => w.Enabled);
            Show(items);
            CheckPriceAlerts();
            if (quotesFailed) ScheduleQuoteRetry();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Opsis News Crawler: quotes failed: {ex}");
            Log.Write($"quotes failed: {ex.GetType().Name}: {ex.Message}");
            hasQuotes = true; // the bar says "unavailable" instead of "loading" for ever
            quotesFailed = true;
            ScheduleQuoteRetry();
        }
        finally
        {
            refreshingQuotes = false;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Loc.Changed -= OnLanguageChanged;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
        cts.Cancel();
        refreshTimer.Dispose();
        quoteTimer.Dispose();
        snoozeTimer.Dispose();
        hotkey?.Dispose();
        weatherTimer.Dispose();
        alertTimer.Dispose();
        miniWeather?.Dispose();
        breakingTimer.Dispose();
        CloseLivePreview();
        weatherMap?.Close();
        weatherMap = null;
        news.Dispose();
        SecondWindow?.Dispose();
        foreach (var extra in ExtraWindows) extra.Dispose();
        monitorWindows.Clear();
        Window.Dispose();
        cts.Dispose();
    }
}
