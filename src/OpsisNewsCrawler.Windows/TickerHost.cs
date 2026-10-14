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
    private readonly CancellationTokenSource cts = new();
    private readonly ReadStore readStore = new();

    /// <summary>Articles kept with Ctrl+click.</summary>
    public ReadLaterStore ReadLater { get; } = new();
    private List<NewsItem> allItems = new();
    private IReadOnlyList<NewsItem> stockItems = Array.Empty<NewsItem>();
    private bool refreshing;
    private bool refreshingQuotes;
    private bool pendingForced;
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
            if (!Window.IsPausedByForeground) _ = RefreshQuotesAsync();
        };
        Loc.Changed += OnLanguageChanged;
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
        w.ItemSaveRequested += item =>
        {
            var added = ReadLater.Add(item);
            w.ShowToast(Loc.T(added ? "msg.savedLater" : "msg.alreadySaved"));
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
    }

    /// <summary>Set by the Opsis plugin: the UI language then follows Opsis, not the crawler's own setting.</summary>
    public bool LanguageFollowsHost { get; set; }

    /// <summary>Pushes the values of <see cref="Config"/> into the windows and timers. Does not save.</summary>
    public void ApplyConfig()
    {
        if (!LanguageFollowsHost) Loc.SetLanguage(Config.Language);
        Theme.SetMode(Config.ThemeMode, Config.OpacityPercent / 100.0);

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
        quoteTimer.Interval = Config.QuoteRefreshSeconds * 1000;
        PushAll();
        if (BarsOn) _ = RefreshQuotesAsync();
    }

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

    private bool BarsOn => started && Config.BarsEnabled;

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
        breakingTimer.Start();
        _ = RefreshAsync(force: true);
        _ = RefreshQuotesAsync();
    }

    private void StopBars()
    {
        breakingTimer.Stop();
        CloseLivePreview();
        refreshTimer.Stop();
        quoteTimer.Stop();
        Window.StopTicker();
        SecondWindow?.StopTicker();
        foreach (var extra in ExtraWindows) extra.StopTicker();
    }

    /// <summary>Raised after the master switch changed (tray menu and settings keep their check marks in step).</summary>
    public event Action? BarsEnabledChanged;

    /// <summary>Turns every bar on or off. Off: the bars disappear and nothing is downloaded.</summary>
    public void SetBarsEnabled(bool on)
    {
        if (Config.BarsEnabled == on) return;
        Config.BarsEnabled = on;
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
        breakingTimer.Stop();
        CloseLivePreview();
        refreshTimer.Stop();
        quoteTimer.Stop();
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

    private bool NeedsNews => Config.MainContent != BarContent.Stocks || (Config.SecondBarEnabled && Config.SecondContent != BarContent.Stocks) || ExtraContents().Any(c => c != BarContent.Stocks);
    private bool NeedsStocks => Config.MainContent != BarContent.News || (Config.SecondBarEnabled && Config.SecondContent != BarContent.News) || ExtraContents().Any(c => c != BarContent.News);

    private IEnumerable<BarContent> ExtraContents() => WantedMonitors().Select(x => x.Cfg.Content);

    private IReadOnlyList<NewsItem> NewsForDisplay()
    {
        if (allItems.Count == 0)
            return new[] { Placeholder(hasRealItems ? Loc.T("ph.newsUnavailable") : Loc.T("ph.loadingNews")) };
        var visible = Config.HideRead ? allItems.Where(i => !readStore.IsRead(i.Url)).ToList() : allItems;
        return visible.Count == 0 ? new[] { Placeholder(Loc.T("ph.allRead")) } : visible;
    }

    private IReadOnlyList<NewsItem> StocksForDisplay() =>
        stockItems.Count > 0 ? stockItems : new[] { Placeholder(hasQuotes ? Loc.T("ph.quotesUnavailable") + (quotes.LastFailed.Count > 0 ? " (" + string.Join(", ", quotes.LastFailed) + ")" : "") : Loc.T("ph.loadingQuotes")) };

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
            PushTo(Window, State, Config.MainContent);
        }
        if (SecondWindow is not null) PushTo(SecondWindow, State2, Config.SecondContent);
        foreach (var (cfg, _) in WantedMonitors())
            if (monitorWindows.TryGetValue(cfg.Device, out var extra)) PushTo(extra.Window, extra.State, cfg.Content);
    }

    private void PushTo(TickerWindow w, TickerState st, BarContent content)
    {
        switch (content)
        {
            case BarContent.News:
                w.SetSplit(false);
                w.SetItems(NewsForDisplay(), st.ScrollOffset);
                break;
            case BarContent.Stocks:
                w.SetSplit(false);
                w.SetItems(StocksForDisplay(), st.ScrollOffset);
                break;
            default:
                w.SetSplit(true);
                var stocksLeft = Config.SplitStocksOnLeft;
                w.SetItems(stocksLeft ? StocksForDisplay() : NewsForDisplay(), st.ScrollOffset);
                w.SetItemsB(stocksLeft ? NewsForDisplay() : StocksForDisplay(), st.ScrollOffsetB);
                break;
        }
    }

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
            var items = await news.FetchAsync(Config.GetFeeds(category), Config.MaxItemsPerFeed, cts.Token);
            if (disposed || !string.Equals(category, Config.ActiveCategory, StringComparison.OrdinalIgnoreCase)) return; // the category changed meanwhile; a new refresh is running

            void Apply()
            {
                if (disposed) return;
                State.MarkNewsRefresh();
                if (items.Count > 0)
                {
                    hasRealItems = true;
                    allItems = items.ToList();
                    DetectBreaking(allItems);
                }
                PushAll();
            }

            if (Window.IsHandleCreated && Window.InvokeRequired) Window.Invoke(new Action(Apply));
            else Apply();
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
        if (!Config.BreakingEnabled || Config.MainContent == BarContent.Stocks && !breakingActive) return;
        var maxAge = TimeSpan.FromMinutes(Config.BreakingMaxAgeMinutes);
        var urgent = items.Where(i => Breaking.IsBreaking(i, Config.BreakingKeywords, maxAge) && !readStore.IsRead(i.Url))
            .Select(i => i with { Breaking = true }).ToList();
        if (urgent.Count == 0) return;

        var isNew = urgent.Where(i => !readStore.IsRead("breaking:" + i.Url)).ToList();
        foreach (var i in isNew) readStore.MarkRead("breaking:" + i.Url); // remembered, so a restart does not replay the intro
        breakingItems = urgent;
        breakingUntil = DateTimeOffset.Now.AddMinutes(Config.BreakingDurationMinutes);
        if (!breakingActive && isNew.Count > 0) StartBreaking();
        else if (breakingActive) Window.SetLiveAvailable(HasLive);
    }

    private void StartBreaking()
    {
        breakingActive = true;
        Window.StartBreaking();
        Window.SetLiveAvailable(HasLive);
    }

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
                    stockItems = list;
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
        breakingTimer.Dispose();
        CloseLivePreview();
        news.Dispose();
        SecondWindow?.Dispose();
        foreach (var extra in ExtraWindows) extra.Dispose();
        monitorWindows.Clear();
        Window.Dispose();
        cts.Dispose();
    }
}
