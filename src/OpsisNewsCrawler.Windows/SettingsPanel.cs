using OpsisNewsCrawler.Core;
using System.ComponentModel;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// All crawler settings in one tabbed control, drawn like the Opsis - Desktop Customization UI. The standalone
/// <see cref="SettingsForm"/> hosts it in a window and the Opsis plugin hosts the very same control as the
/// "News Crawler" page of Opsis. Edits are kept in memory and written to the config only when the user presses Apply.
/// Every text is in the one language chosen in <see cref="Loc"/>; the panel rebuilds itself when the language changes.
/// </summary>
public sealed class SettingsPanel : UserControl
{
    private readonly TickerHost host;
    private readonly bool showCloseButton;
    private readonly bool showAboutTabs;
    private bool loading;
    private bool hasUnsavedChanges;
    private bool dirtyTrackingAttached;

    private FlatNav tabs = null!;
    private Label status = null!;
    private bool built;
    private BarPreview barPreview = null!;
    private readonly System.Windows.Forms.Timer previewTimer = new() { Interval = 250 };

    // General
    private FlatCombo language = null!;
    private FlatCombo category = null!;
    private FlatNumber refresh = null!;
    private FlatNumber speed = null!;
    private FlatCheck hoverPause = null!;
    private FlatCheck startWithWindows = null!;
    private FlatCheck trayAnimated = null!;
    private FlatCheck toggleHotkey = null!;
    private FlatCheck icons = null!;
    private FlatCheck preview = null!;
    private FlatCheck pauseOther = null!;
    private FlatCheck hideRead = null!;

    // Bars
    private FlatCombo defaultBar = null!;
    private SlotPicker mainPicker = null!;
    private SlotPicker secondPicker = null!;
    private SlotPicker miniPicker = null!;
    private readonly Dictionary<string, FlatCheck> weatherFieldChecks = new();
    private FlatCheck secondEnabled = null!;
    private readonly List<(Screen Screen, FlatCheck Check, SlotPicker Picker)> monitorRows = new();
    private FlatCheck autoUpdate = null!;
    private FlatText citySearch = null!;
    private FlatList cityList = null!;
    private FlatCheck miniWeather = null!;
    private FlatNumber miniX = null!, miniY = null!, miniW = null!, miniH = null!;
    private FlatButton miniMove = null!;
    private FlatCheck miniOnTop = null!;
    private FlatCheck miniWhenHidden = null!;
    private FlatCheck yahooHistory = null!;
    private FlatText yahooSearch = null!;
    private FlatButton yahooSearchButton = null!, yahooAddButton = null!, yahooRemoveButton = null!;
    private FlatList yahooHitList = null!;
    private Label yahooStatus = null!;
    private readonly List<FlatButton> yahooPresetButtons = new();
    private List<YahooHit> yahooHits = new();
    private Control yahooCatalog = null!;
    private FlatCombo miniShape = null!;
    private FlatCombo barShape = null!;
    private List<string> workCities = new();
    private List<GreekCity> shownCities = new();
    private FlatCheck barsEnabled = null!;
    private Label updateStatus = null!;
    private FlatButton installUpdate = null!;
    private FlatButton openUpdatePage = null!;
    private FlatProgress updateProgress = null!;
    private FlatList laterList = null!;
    private Label laterStatus = null!;
    private List<SavedArticle> laterShown = new();
    private Label secondNote = null!;

    // Position
    private FlatCombo position = null!;
    private FlatCheck followTaskbar = null!;
    private FlatCheck reserve = null!;
    private FlatNumber posX = null!;
    private FlatNumber posY = null!;
    private FlatNumber posW = null!;
    private FlatCombo monitor = null!;
    private FlatButton moveWithMouse = null!;
    private FlatCheck tbAuto = null!;
    private FlatNumber tbLeft = null!;
    private FlatNumber tbRight = null!;
    private FlatCard customCard = null!;
    private FlatCard taskbarCard = null!;

    // Sources
    private List<FeedCategory> workCategories = new();
    private FlatList categoryList = null!;
    private FlatList feedList = null!;
    private FlatText siteBox = null!;
    private FlatCombo presets = null!;
    private Label discoverStatus = null!;
    private FlatNumber newsMaxAge = null!;
    private FlatCheck newsNewestFirst = null!;
    private FlatCheck stockNewsOnly = null!;
    private FlatText mutedNewsKeywords = null!;

    // Markets
    private List<QuoteInstrument> workWatch = new();
    private List<PriceAlert> workAlerts = new();
    private List<QuoteInstrument> alertChoices = new();
    private string alertChoicesSignature = "";
    private FlatList alertList = null!;
    private FlatCombo alertInstrument = null!;
    private FlatCombo alertDirection = null!;
    private FlatText alertThreshold = null!;
    private readonly Dictionary<QuoteSource, FlatList> watchLists = new();
    private FlatCombo marketCombo = null!;
    private FlatText stockSearch = null!;
    private FlatList stockList = null!;
    private FlatButton refreshCatalog = null!;
    private Label stockStatus = null!;
    private Label sourceStatus = null!;
    private readonly System.Windows.Forms.Timer sourceTimer = new() { Interval = 3000 };
    private List<QuoteInstrument> visibleStocks = new();
    private readonly Dictionary<string, IReadOnlyList<QuoteInstrument>> catalogs = new();
    private string SelectedMic => EuronextCatalog.Markets[Math.Clamp(marketCombo.SelectedIndex, 0, EuronextCatalog.Markets.Count - 1)].Mic;

    // Look
    private FlatCombo themeMode = null!;
    private FlatSlider opacity = null!;
    private Label opacityLabel = null!;
    private FlatCheck edgeFade = null!;
    private FlatSlider windowOpacity = null!;
    private Label windowOpacityLabel = null!;
    private Control windowOpacityRow = null!;
    private Label themeNote = null!;
    private FlatCombo barColor = null!;
    private FlatSwatch barSwatch = null!;
    private FlatCombo taskbarColor = null!;
    private FlatSwatch taskbarSwatch = null!;
    private FlatCombo barBackdrop = null!;
    private FlatCombo taskbarBackdrop = null!;

    // Breaking
    private FlatCheck breakingEnabled = null!;
    private FlatCheck breakingSound = null!;
    private FlatCheck weatherMapOnClick = null!;
    private FlatCheck weatherAlerts = null!;
    private FlatCombo alertCity = null!;
    private FlatText keywords = null!;
    private FlatNumber maxAge = null!;
    private FlatNumber duration = null!;
    private List<LiveFeed> workLive = new();
    private FlatList liveList = null!;
    private FlatText addLiveBox = null!;
    private Label liveStatus = null!;

    /// <summary>Raised after Apply succeeded.</summary>
    public event Action? Applied;

    /// <summary>Raised when the user presses the close button (the window hides itself).</summary>
    public event Action? CloseRequested;

    /// <summary>True when Opsis pushes its own colours and language; then those two choices are locked.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HostThemeLocked { get; set; }

    public SettingsPanel(TickerHost host, bool showCloseButton, bool showAboutTabs = true)
    {
        this.host = host;
        this.showCloseButton = showCloseButton;
        this.showAboutTabs = showAboutTabs;
        Dock = DockStyle.Fill;
        RightToLeft = Loc.IsRtl ? RightToLeft.Yes : RightToLeft.No;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;

        host.CustomBoundsChanged += OnCustomBoundsChanged;
        host.ReadLater.Changed += OnReadLaterChanged;
        host.BarsEnabledChanged += OnBarsEnabledChanged;
        host.MiniWeatherBoundsChanged += OnMiniBoundsChanged;
        Theme.Changed += OnThemeChanged;
        Loc.Changed += OnLanguageChanged;
        previewTimer.Tick += (_, _) => UpdatePreview();
        previewTimer.Start();
        Build();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            previewTimer.Dispose();
            sourceTimer.Dispose();
            host.CustomBoundsChanged -= OnCustomBoundsChanged;
            host.ReadLater.Changed -= OnReadLaterChanged;
            host.BarsEnabledChanged -= OnBarsEnabledChanged;
            host.MiniWeatherBoundsChanged -= OnMiniBoundsChanged;
            Theme.Changed -= OnThemeChanged;
            Loc.Changed -= OnLanguageChanged;
        }
        base.Dispose(disposing);
    }

    private void OnThemeChanged()
    {
        if (IsDisposed) return;
        Theme.Apply(this);
        Invalidate(true);
    }

    private void OnLanguageChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(new Action(Rebuild));
        else Rebuild();
    }

    private static string L(string key) => Loc.T(key);

    // ---- preview of the bars ----------------------------------------------------------------

    /// <summary>Sub-pages of the Settings tab whose choices change how a bar looks: Bars, Position, Look, Breaking.</summary>
    private static readonly int[] PreviewSubs = { 1, 2, 3 };

    /// <summary>The desktop bar's place and size as fractions of the primary screen (for the preview).</summary>
    private RectangleF DesktopRelative()
    {
        var b = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).Bounds;
        return new RectangleF((miniX.Value - b.Left) / (float)b.Width, (miniY.Value - b.Top) / (float)b.Height, miniW.Value / (float)b.Width, miniH.Value / (float)b.Height);
    }

    private void UpdatePreview()
    {
        if (!Visible || !built || barPreview is null || barPreview.IsDisposed || tabs is null) return;
        var show = PreviewSubs.Contains(tabs.SelectedIndex);
        if (barPreview.Visible != show) barPreview.Visible = show;
        if (!show) return;
        var dark = themeMode.SelectedIndex switch { 1 => false, 2 => true, _ => Theme.IsDark };
        barPreview.SetState(new BarPreviewState(
            (TickerPosition)Math.Max(0, position.SelectedIndex), mainPicker.Get(),
            secondEnabled.Checked, secondPicker.Get(),
            dark, opacity.Value, edgeFade.Checked,
            barColor.SelectedIndex switch { 1 => "lightblue", 2 => "accent", 3 => "custom", _ => "theme" }, barSwatch.Rgb,
            taskbarColor.SelectedIndex switch { 1 => "lightblue", 2 => "accent", 3 => "custom", _ => "taskbar" }, taskbarSwatch.Rgb,
            barBackdrop.SelectedIndex switch { 1 => "blur", 2 => "gradient", _ => "acrylic" },
            taskbarBackdrop.SelectedIndex switch { 0 => "acrylic", 2 => "gradient", _ => "blur" },
            miniWeather.Checked, miniPicker.Get(), DesktopRelative(), miniShape.SelectedIndex switch { 1 => "rounded", 2 => "pill", _ => "rect" },
            barShape.SelectedIndex switch { 1 => "rounded", 2 => "pill", _ => "rect" }));
    }

    // ---- building ------------------------------------------------------------------------

    private void Rebuild()
    {
        RightToLeft = Loc.IsRtl ? RightToLeft.Yes : RightToLeft.No;
        var selectedTab = built ? CurrentLeaf() : 0;
        SuspendLayout();
        foreach (var c in Controls.Cast<Control>().ToList()) c.Dispose();
        Controls.Clear();
        dirtyTrackingAttached = false;
        Build();
        SelectTab(selectedTab);
        ResumeLayout(true);
    }

    private void Build()
    {
        loading = true;
        watchLists.Clear();
        tabs = new FlatNav { Dock = DockStyle.Fill };
        tabs.Badge = new AnimatedAppIcon(() => host.Config.TrayIconAnimated && host.Config.BarsEnabled);

        // One navigation rail: three groups (Settings, Sources, Information), every page under its group.
        AddGroup(Loc.T("pk.settings"),
            (Loc.T("pk.general"), BuildGeneral()),
            // Every bar setting is on one page: placement, what each bar shows, the desktop bar.
            (L("tab.bars"), Combine(
                (Loc.T("pk.placement.and.behaviour.where"), BuildPosition()),
                (Loc.T("pk.what.each.bar.shows"), BuildBars()),
                (Loc.T("pk.desktop.bar.a.free"), BuildDesktopBar()),
                (Loc.T("pk.other.monitors.one.bar"), BuildMonitors()))),
            (L("tab.look"), BuildLook()),
            (L("tab.breaking"), BuildBreaking()));
        AddGroup(L("tab.sources"),
            (Loc.T("pk.news.flow"), Combine((null, BuildSources()), (L("tab.readLater"), BuildReadLater()))),
            (Loc.T("pk.markets.flow"), BuildMarkets()),
            (Loc.T("pk.weather.flow"), BuildWeather()));
        var info = new List<(string, Control)>();
        if (showAboutTabs)
        {
            info.Add((L("tab.help"), BuildInfoPage(() => AboutTexts.Help)));
            info.Add((L("tab.instructions"), BuildInfoPage(() => AboutTexts.Instructions)));
            info.Add((L("tab.history"), BuildInfoPage(() => AboutTexts.History)));
            info.Add((L("tab.license"), BuildInfoPage(() => AboutTexts.License)));
        }
        info.Add((L("tab.updates"), BuildUpdates()));
        info.Add((L("tab.dependencies"), BuildDependencies()));
        AddGroup(Loc.T("pk.information"), info.ToArray());

        var apply = new FlatButton { Text = L("btn.apply") };
        apply.SetPrimary(true);
        apply.Click += (_, _) => ApplySettings();
        var export = new FlatButton { Text = L("settings.export") };
        export.Click += (_, _) => ExportSettings();
        var import = new FlatButton { Text = L("settings.import") };
        import.Click += (_, _) => ImportSettings();
        status = new Label { AutoSize = true, MaximumSize = new Size(420, 0), Padding = new Padding(8, 11, 0, 0), Tag = "muted" };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(12, 8, 12, 10) };
        bar.Controls.Add(apply);
        bar.Controls.Add(export);
        bar.Controls.Add(import);
        if (showCloseButton)
        {
            var close = new FlatButton { Text = L("btn.close") };
            close.Click += (_, _) =>
            {
                if (ConfirmCloseIfNeeded()) CloseRequested?.Invoke();
            };
            bar.Controls.Add(close);
        }
        bar.Controls.Add(status);

        barPreview = new BarPreview { Dock = DockStyle.Bottom, Visible = false };
        Controls.Add(tabs);
        Controls.Add(barPreview);
        Controls.Add(bar);
        built = true;

        LoadFromConfig();
        AttachDirtyTracking();
        Theme.Apply(this);
        loading = false;
        hasUnsavedChanges = false;
    }

    /// <summary>A group heading of the navigation rail with its pages; every page is one of the Build... pages.</summary>
    private void AddGroup(string title, params (string Title, Control Page)[] pages)
    {
        tabs.AddGroup(title);
        foreach (var (pageTitle, page) in pages) tabs.AddPage(pageTitle, page);
    }

    private int CurrentLeaf() => Math.Max(0, tabs.SelectedIndex);

    /// <summary>Puts the content of several pages one under the other on a single scrolling page.</summary>
    private static Panel Combine(params (string? Heading, Control Page)[] parts)
    {
        var page = new Panel { AutoScroll = true, Padding = new Padding(8, 6, 8, 6) };
        var stack = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        foreach (var (heading, part) in parts)
        {
            if (heading is not null)
            {
                // "Title|description": a section heading with a muted line under it
                var split = heading.Split('|', 2);
                stack.Controls.Add(new Label { Text = split[0], AutoSize = true, Font = new Font("Segoe UI Semibold", 13f), Margin = new Padding(0, stack.Controls.Count == 0 ? 6 : 26, 0, 0) });
                if (split.Length > 1)
                    stack.Controls.Add(new Label { Text = split[1], AutoSize = true, MaximumSize = new Size(FlatCard.CardWidth, 0), Tag = "muted", Margin = new Padding(0, 2, 0, 4) });
            }
            foreach (var child in part.Controls.Cast<Control>().ToList())
            {
                part.Controls.Remove(child);
                child.Dock = DockStyle.None;
                stack.Controls.Add(child);
            }
            part.Dispose();
        }
        page.Controls.Add(stack);
        return page;
    }

    private static Panel NewPage(out FlowLayoutPanel flow)
    {
        var page = new Panel { AutoScroll = true, Padding = new Padding(8, 6, 8, 6) };
        flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        page.Controls.Add(flow);
        return page;
    }

    /// <summary>"Label ...... control" row.</summary>
    private static Control Row(string label, Control control, int labelWidth = 300)
    {
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        row.Controls.Add(new Label { Text = label, AutoSize = false, Width = labelWidth, Height = 32, TextAlign = ContentAlignment.MiddleLeft });
        row.Controls.Add(control);
        return row;
    }

    private static Label Note(string text, int width = 640) =>
        new() { Text = text, AutoSize = true, MaximumSize = new Size(width, 0), Tag = "muted", Margin = new Padding(0, 4, 0, 8) };

    private Control BuildGeneral()
    {
        var page = NewPage(out var flow);

        language = new FlatCombo { Width = 260 };
        var langs = Loc.Available();
        language.SetItems(langs.Select(l => l.Native), 0);
        language.SelectedIndexChanged += (_, _) =>
        {
            if (loading || language.SelectedIndex < 0) return;
            // The language is applied at once (the panel rebuilds in the new language); it is saved with Apply.
            host.Config.Language = langs[language.SelectedIndex].Code;
            if (!host.LanguageFollowsHost) Loc.SetLanguage(host.Config.Language);
        };
        barsEnabled = new FlatCheck { Text = L("chk.barsEnabled") };
        flow.Controls.Add(barsEnabled);
        startWithWindows = new FlatCheck { Text = Loc.T("pk.start.with.windows"), Visible = !host.HostedByOpsis };
        flow.Controls.Add(startWithWindows);
        trayAnimated = new FlatCheck { Text = L("chk.trayAnimated"), Visible = !host.HostedByOpsis };
        flow.Controls.Add(trayAnimated);
        toggleHotkey = new FlatCheck { Text = Loc.T("pk.hot.key.ctrl.alt"), Visible = !host.HostedByOpsis };
        flow.Controls.Add(toggleHotkey);
        var languageRow = Row(L("lbl.language"), language);
        languageRow.Visible = !host.LanguageFollowsHost;
        flow.Controls.Add(languageRow);

        category = new FlatCombo { Width = 320 };
        flow.Controls.Add(Row(L("lbl.category"), category));
        refresh = new FlatNumber();
        refresh.Configure(AppConfig.MinRefreshSeconds, AppConfig.MaxRefreshSeconds, 30);
        flow.Controls.Add(Row(L("lbl.refresh"), refresh));
        speed = new FlatNumber();
        speed.Configure(AppConfig.MinScrollSpeed, AppConfig.MaxScrollSpeed, 10);
        flow.Controls.Add(Row(L("lbl.speed"), speed));

        hoverPause = new FlatCheck { Text = L("chk.hoverPause") };
        icons = new FlatCheck { Text = L("chk.icons") };
        preview = new FlatCheck { Text = L("chk.preview") };
        pauseOther = new FlatCheck { Text = L("chk.pauseOther") };
        hideRead = new FlatCheck { Text = L("chk.hideRead") };
        foreach (var c in new Control[] { hoverPause, icons, preview, pauseOther, hideRead }) flow.Controls.Add(c);

        var reset = new FlatButton { Text = L("btn.resetRead") };
        reset.Margin = new Padding(0, 10, 0, 3);
        var openLog = new FlatButton { Text = L("btn.openLog") };
        openLog.Margin = new Padding(0, 3, 0, 3);
        openLog.Click += (_, _) => OpenLog();
        reset.Click += (_, _) =>
        {
            host.ResetReadList();
            SetStatus(L("msg.readCleared"));
        };
        flow.Controls.Add(reset);
        flow.Controls.Add(openLog);

        return page;
    }

    /// <summary>The update frame (Information tab).</summary>
    // ---- dependencies ----------------------------------------------------------------------------

    private Label webViewStatus = null!;
    private FlatCheck webViewCheck = null!;
    private FlatButton webViewInstall = null!;

    private Control BuildDependencies()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Note(L("note.dependencies"), 700));
        var card = new FlatCard(L("tab.dependencies"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        // .NET: the program is running, so it is there (shown ticked, nothing to choose).
        var dotNet = new FlatCheck { Text = L("dep.dotnet") + "  —  " + Dependencies.DotNetVersion + "  (" + L("dep.installed") + ")", Checked = true, Enabled = false };
        inner.Controls.Add(dotNet);

        // Present: ticked and locked, like .NET. Missing: unticked, with an install button.
        webViewCheck = new FlatCheck { Enabled = false };
        webViewStatus = new Label { AutoSize = true, Margin = new Padding(0, 4, 0, 0), Tag = "muted" };
        inner.Controls.Add(webViewCheck);
        webViewInstall = new FlatButton { Text = L("btn.installDep"), Margin = new Padding(0, 4, 0, 0) };
        inner.Controls.Add(webViewStatus);
        inner.Controls.Add(webViewInstall);
        webViewInstall.Click += async (_, _) =>
        {
            webViewInstall.Enabled = false;
            webViewStatus.Text = L("msg.depInstalling");
            var result = await Dependencies.InstallWebView2Async();
            if (IsDisposed) return;
            webViewInstall.Enabled = true;
            RefreshDependencies();
            if (result != Dependencies.InstallResult.Installed)
                webViewStatus.Text += "  —  " + L(result == Dependencies.InstallResult.NotSignedByMicrosoft ? "msg.depNotSigned" : "msg.depFailed");
        };
        card.Controls.Add(inner);
        flow.Controls.Add(card);
        RefreshDependencies();
        return page;
    }

    private void RefreshDependencies()
    {
        if (webViewStatus is null || webViewStatus.IsDisposed) return;
        var version = Dependencies.WebView2Version();
        webViewCheck.Text = L("dep.webview2") + "  —  " + (version is null ? L("dep.missing") : version + "  (" + L("dep.installed") + ")");
        webViewCheck.Checked = version is not null;
        webViewStatus.Text = "";
        webViewInstall.Visible = version is null; // installed: shown, nothing to choose
    }

    private Control BuildUpdates()
    {
        var page = NewPage(out var flow);
        // Same logic as GearWin: look at every start (and every 12 hours), tell once per version, install only after a click and a confirmation,
        // from this repository's GitHub releases only, verified with SHA-256, silently after the program has ended, then open it again.
        var updates = new FlatCard(L("card.updates"));
        var updFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        updFlow.Controls.Add(Note(L("note.updates"), 700));
        autoUpdate = new FlatCheck { Text = L("chk.autoUpdate") };
        updFlow.Controls.Add(autoUpdate);
        var checkNow = new FlatButton { Text = L("btn.checkNow") };
        installUpdate = new FlatButton { Text = L("btn.installUpdate"), Visible = false };
        installUpdate.SetPrimary(true);
        openUpdatePage = new FlatButton { Text = L("btn.openPage"), Visible = false };
        var updButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
        updButtons.Controls.Add(checkNow);
        updButtons.Controls.Add(installUpdate);
        updButtons.Controls.Add(openUpdatePage);
        updFlow.Controls.Add(updButtons);
        updateProgress = new FlatProgress { Visible = false, Width = 420 };
        updFlow.Controls.Add(updateProgress);
        updateStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(700, 0), Margin = new Padding(0, 6, 0, 0), UseMnemonic = false, Text = Loc.T("upd.current", AboutTexts.Version) };
        updFlow.Controls.Add(updateStatus);
        updates.Controls.Add(updFlow);
        flow.Controls.Add(updates);
        checkNow.Click += async (_, _) => await CheckUpdatesNowAsync(checkNow);
        installUpdate.Click += async (_, _) => await InstallUpdateAsync();
        openUpdatePage.Click += (_, _) => { if (host.AvailableUpdate is { } u) OpenInBrowser(u.PageUrl); };
        ShowUpdateState();
        return page;
    }

    private Control BuildBars()
    {
        var page = NewPage(out var flow);

        var main = new FlatCard(L("card.mainBar"));
        var mainFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        mainPicker = new SlotPicker();
        mainFlow.Controls.Add(mainPicker.Build());
        mainFlow.Controls.Add(Note(SplitNote(), 600));
        main.Controls.Add(mainFlow);
        flow.Controls.Add(main);


        var second = new FlatCard(L("card.secondBar"));
        var secondFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        secondEnabled = new FlatCheck { Text = L("chk.secondEnabled") };
        secondFlow.Controls.Add(secondEnabled);
        secondPicker = new SlotPicker();
        secondFlow.Controls.Add(secondPicker.Build());
        secondNote = Note(L("note.secondBar"), 560);
        secondFlow.Controls.Add(secondNote);
        second.Controls.Add(secondFlow);
        flow.Controls.Add(second);

        secondEnabled.CheckedChanged += (_, _) => UpdateBarsUi();
        return page;
    }

    /// <summary>The bars of the other monitors (one per screen, each with its own content): the last section of the Bars page.</summary>
    private Control BuildMonitors()
    {
        var page = NewPage(out var flow);
        // One bar on every other monitor, each with its own content.
        var monitors = new FlatCard(L("card.monitors"));
        var monitorFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        monitorRows.Clear();
        var others = Screen.AllScreens.Where(s => !s.Primary).ToList();
        for (var i = 0; i < others.Count; i++)
        {
            var s = others[i];
            var check = new FlatCheck { Text = Loc.T("fmt.monitor", Array.IndexOf(Screen.AllScreens, s) + 1, s.Bounds.Width, s.Bounds.Height) };
            var picker = new SlotPicker();
            monitorFlow.Controls.Add(check);
            monitorFlow.Controls.Add(picker.Build());
            monitorRows.Add((s, check, picker));
        }
        monitorFlow.Controls.Add(Note(others.Count > 0 ? L("note.monitors") : L("note.noOtherMonitor"), 560));
        monitors.Controls.Add(monitorFlow);
        flow.Controls.Add(monitors);
        return page;
    }

    private Control BuildPosition()
    {
        var page = NewPage(out var flow);

        var def = new FlatCard(L("card.defaultBar"));
        var defFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        defaultBar = new FlatCombo { Width = 470 };
        defaultBar.SetItems(new[] { L("defaultbar.above"), L("defaultbar.inside"), L("defaultbar.other") }, 0);
        defFlow.Controls.Add(Row(L("lbl.defaultBar"), defaultBar, 200));
        defFlow.Controls.Add(Note(L("note.defaultBar"), 600));
        def.Controls.Add(defFlow);
        flow.Controls.Add(def);
        defaultBar.SelectedIndexChanged += (_, _) =>
        {
            if (loading) return;
            if (defaultBar.SelectedIndex == 0) position.SelectedIndex = (int)TickerPosition.BottomTaskbar;
            else if (defaultBar.SelectedIndex == 1) position.SelectedIndex = (int)TickerPosition.TaskbarIntegrated;
        };


        position = new FlatCombo { Width = 470 };
        position.SetItems(new[] { L("pos.bottom"), L("pos.top"), L("pos.center"), L("pos.custom"), L("pos.taskbar") }, 0);
        followTaskbar = new FlatCheck { Text = L("chk.followTaskbar") };
        reserve = new FlatCheck { Text = L("chk.reserve") };
        var placement = new FlatCard(Loc.T("pk.exact.placement.and.behaviour"));
        var placementFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        placementFlow.Controls.Add(Row(L("lbl.position"), position, 200));
        barShape = new FlatCombo { Width = 470 };
        barShape.SetItems(new[] { L("shape.rect"), L("shape.rounded"), L("shape.pill") }, 0);
        placementFlow.Controls.Add(Row(L("lbl.barShape"), barShape, 270));
        placementFlow.Controls.Add(followTaskbar);
        placementFlow.Controls.Add(reserve);
        placement.Controls.Add(placementFlow);
        flow.Controls.Add(placement);

        customCard = new FlatCard(L("card.custom"));
        var cf = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        posX = new FlatNumber();
        posX.Configure(-20000, 20000, 10);
        posY = new FlatNumber();
        posY.Configure(-20000, 20000, 10);
        posW = new FlatNumber();
        posW.Configure(200, 20000, 50);
        monitor = new FlatCombo { Width = 340 };
        monitor.SetItems(Screen.AllScreens.Select((s, i) => L("fmt.monitor").Replace("{0}", (i + 1).ToString()).Replace("{1}", s.Bounds.Width.ToString()).Replace("{2}", s.Bounds.Height.ToString())), -1);
        cf.Controls.Add(Row(L("lbl.x"), posX, 240));
        cf.Controls.Add(Row(L("lbl.y"), posY, 240));
        cf.Controls.Add(Row(L("lbl.width"), posW, 240));
        cf.Controls.Add(Row(L("lbl.monitor"), monitor, 240));
        moveWithMouse = new FlatButton { Text = L("btn.moveMouse") };
        cf.Controls.Add(moveWithMouse);
        customCard.Controls.Add(cf);
        flow.Controls.Add(customCard);

        taskbarCard = new FlatCard(L("card.taskbar"));
        var tf = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        tbAuto = new FlatCheck { Text = L("chk.tbAuto") };
        tbLeft = new FlatNumber();
        tbLeft.Configure(0, 8000, 10);
        tbRight = new FlatNumber();
        tbRight.Configure(0, 8000, 10);
        tf.Controls.Add(tbAuto);
        tf.Controls.Add(Row(L("lbl.tbLeft"), tbLeft, 280));
        tf.Controls.Add(Row(L("lbl.tbRight"), tbRight, 280));
        tf.Controls.Add(Note(L("note.taskbar"), 560));
        taskbarCard.Controls.Add(tf);
        flow.Controls.Add(taskbarCard);

        position.SelectedIndexChanged += (_, _) =>
        {
            UpdatePositionGroups();
            SyncDefaultBar();
        };
        tbAuto.CheckedChanged += (_, _) => UpdatePositionGroups();
        monitor.SelectedIndexChanged += (_, _) => MoveToSelectedMonitor();
        moveWithMouse.Click += (_, _) => ToggleMouseMove();
        foreach (var n in new[] { posX, posY, posW }) n.ValueChanged += (_, _) => ApplyCustomBoxes();
        return page;
    }

    private Control BuildSources()
    {
        var page = new Panel { AutoScroll = true, Padding = new Padding(8, 6, 8, 6) };
        var root = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };

        var left = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 18, 0) };
        left.Controls.Add(new Label { Text = L("lbl.categories"), AutoSize = true, Margin = new Padding(0, 0, 0, 4) });
        categoryList = new FlatList { Width = 240, Height = 260 };
        left.Controls.Add(categoryList);
        var catButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(260, 0) };
        var addCat = new FlatButton { Text = L("btn.new") };
        var renCat = new FlatButton { Text = L("btn.rename") };
        var delCat = new FlatButton { Text = L("btn.delete") };
        addCat.Click += (_, _) => AddCategory();
        renCat.Click += (_, _) => RenameCategory();
        delCat.Click += (_, _) => DeleteCategory();
        catButtons.Controls.AddRange(new Control[] { addCat, renCat, delCat });
        left.Controls.Add(catButtons);

        var right = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        right.Controls.Add(new Label { Text = L("lbl.sites"), AutoSize = true, Margin = new Padding(0, 0, 0, 4) });
        feedList = new FlatList { Width = 520, Height = 260 };
        feedList.SetShowChecks(true);
        right.Controls.Add(feedList);
        var sourceActions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(520, 0) };
        var removeFeed = new FlatButton { Text = L("btn.removeFeed") };
        removeFeed.Click += (_, _) => RemoveFeed();
        var importOpml = new FlatButton { Text = Loc.T("pk.import.opml") };
        importOpml.Click += (_, _) => ImportOpml();
        var exportOpml = new FlatButton { Text = Loc.T("pk.export.opml") };
        exportOpml.Click += (_, _) => ExportOpml();
        var testFeed = new FlatButton { Text = Loc.T("pk.test") };
        testFeed.Click += (_, _) => TestFeeds(selectedOnly: true);
        var testAll = new FlatButton { Text = Loc.T("pk.test.all") };
        testAll.Click += (_, _) => TestFeeds(selectedOnly: false);
        var moveUp = new FlatButton { Text = "▲" };
        moveUp.Click += (_, _) => MoveFeed(-1);
        var moveDown = new FlatButton { Text = "▼" };
        moveDown.Click += (_, _) => MoveFeed(1);
        sourceActions.Controls.AddRange(new Control[] { removeFeed, moveUp, moveDown, testFeed, testAll, importOpml, exportOpml });
        right.Controls.Add(sourceActions);

        var newsOptions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 8, 0, 4) };
        newsOptions.Controls.Add(new Label { Text = Loc.T("pk.maximum.story.age.hours"), AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        newsMaxAge = new FlatNumber();
        newsMaxAge.Configure(1, 720, 24);
        newsOptions.Controls.Add(newsMaxAge);
        newsNewestFirst = new FlatCheck { Text = Loc.T("pk.newest.stories.first"), AutoSize = true, Margin = new Padding(8, 5, 0, 0) };
        newsOptions.Controls.Add(newsNewestFirst);
        stockNewsOnly = new FlatCheck { Text = Loc.T("pk.only.news.about.my"), AutoSize = true, Margin = new Padding(0, 5, 0, 0) };
        right.Controls.Add(newsOptions);
        right.Controls.Add(stockNewsOnly);
        right.Controls.Add(new Label { Text = Loc.T("pk.mute.words.phrases.separate"), AutoSize = true, Margin = new Padding(0, 8, 0, 3) });
        mutedNewsKeywords = new FlatText { Width = 500 };
        right.Controls.Add(mutedNewsKeywords);
        right.Controls.Add(new Label { Text = Loc.T("pk.matches.title.and.summary"), AutoSize = true, MaximumSize = new Size(520, 0), ForeColor = Theme.TextMuted, Margin = new Padding(0, 0, 0, 6) });

        right.Controls.Add(new Label { Text = L("lbl.addSite"), AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 10, 0, 4) });
        var addRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        siteBox = new FlatText { Width = 300 };
        var addSite = new FlatButton { Text = L("btn.addSite") };
        addSite.Click += (_, _) => AddFromSite();
        addRow.Controls.Add(siteBox);
        addRow.Controls.Add(addSite);
        right.Controls.Add(addRow);
        discoverStatus = new Label { AutoSize = true, Tag = "muted" };
        right.Controls.Add(discoverStatus);

        right.Controls.Add(new Label { Text = L("lbl.presets"), AutoSize = true, Margin = new Padding(0, 10, 0, 4) });
        var presetRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        presets = new FlatCombo { Width = 300 };
        presets.SetItems(AppConfig.PresetFeeds.Select(p => p.Name), -1);
        var addPreset = new FlatButton { Text = L("btn.addPreset") };
        addPreset.Click += (_, _) => AddPreset();
        presetRow.Controls.Add(presets);
        presetRow.Controls.Add(addPreset);
        right.Controls.Add(presetRow);

        root.Controls.Add(left);
        root.Controls.Add(right);
        page.Controls.Add(root);

        categoryList.SelectedIndexChanged += (_, _) => ShowFeedsOfSelectedCategory();
        feedList.ItemChecked += (index, isChecked) =>
        {
            if (SelectedCategory() is { } cat && index < cat.Feeds.Count) cat.Feeds[index].Enabled = isChecked;
        };
        return page;
    }

    private static void OpenLog()
    {
        try
        {
            var path = Log.Path;
            if (File.Exists(path)) System.Diagnostics.Process.Start("notepad.exe", "\"" + path + "\"");
            else if (Directory.Exists(Path.GetDirectoryName(path))) System.Diagnostics.Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(path) + "\"");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
        }
    }

    private void OnBarsEnabledChanged()
    {
        if (IsDisposed || barsEnabled is null) return;
        void Set()
        {
            var was = loading;
            loading = true;
            barsEnabled.Checked = host.Config.BarsEnabled;
            loading = was;
        }
        if (InvokeRequired) BeginInvoke(new Action(Set)); else Set();
    }

    // ---- weather ---------------------------------------------------------------------------------

    private static string CityName(GreekCity c) => Loc.IsGreek ? c.NameEl : c.NameEn;

    private void LoadMiniBounds()
    {
        if (miniW is null) return;
        var b = host.GetMiniWeatherBounds();
        miniW.SetValueSilently(host.Config.MiniWeatherWidth);
        miniH.SetValueSilently(host.Config.MiniWeatherHeight);
        miniX.SetValueSilently(b.X);
        miniY.SetValueSilently(b.Y);
    }

    private void OnMiniBoundsChanged()
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(new Action(LoadMiniBounds));
    }

    private Control BuildDesktopBar()
    {
        var page = NewPage(out var flow);
        var mini = new FlatCard(Loc.T("pk.desktop.bar"));
        var miniFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        miniWeather = new FlatCheck { Text = Loc.T("pk.show.a.bar.on") };
        miniFlow.Controls.Add(miniWeather);
        miniPicker = new SlotPicker();
        miniFlow.Controls.Add(miniPicker.Build());
        miniW = new FlatNumber();
        miniW.Configure(200, 4000, 10);
        miniH = new FlatNumber();
        miniH.Configure(24, 200, 2);
        miniX = new FlatNumber();
        miniX.Configure(-20000, 20000, 10);
        miniY = new FlatNumber();
        miniY.Configure(-20000, 20000, 10);
        miniFlow.Controls.Add(Row(L("lbl.width"), miniW, 240));
        miniFlow.Controls.Add(Row(L("lbl.height"), miniH, 240));
        miniFlow.Controls.Add(Row(L("lbl.x"), miniX, 240));
        miniFlow.Controls.Add(Row(L("lbl.y"), miniY, 240));
        var miniButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        miniMove = new FlatButton { Text = L("btn.moveMouse") };
        var miniCenter = new FlatButton { Text = L("btn.centerDesktop") };
        miniButtons.Controls.Add(miniMove);
        miniButtons.Controls.Add(miniCenter);
        miniFlow.Controls.Add(miniButtons);
        miniMove.Click += (_, _) =>
        {
            var on = !host.IsMiniWeatherEditMode;
            host.SetMiniWeatherEditMode(on);
            miniMove.Text = on ? L("btn.moveMouseDone") : L("btn.moveMouse");
        };
        miniCenter.Click += (_, _) =>
        {
            host.CenterMiniWeather();
            LoadMiniBounds();
        };
        miniOnTop = new FlatCheck { Text = L("chk.miniOnTop") };
        miniFlow.Controls.Add(miniOnTop);
        miniShape = new FlatCombo { Width = 300 };
        miniShape.SetItems(new[] { L("shape.rect"), L("shape.rounded"), L("shape.pill") }, 0);
        miniFlow.Controls.Add(Row(L("lbl.miniShape"), miniShape, 200));
        miniWhenHidden = new FlatCheck { Text = L("chk.miniWhenHidden") };
        miniFlow.Controls.Add(miniWhenHidden);
        miniFlow.Controls.Add(Note(Loc.T("pk.a.free.bar.on"), 600));
        mini.Controls.Add(miniFlow);
        flow.Controls.Add(mini);

        return page;
    }

    private Control BuildWeather()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Note(L("note.weather"), 700));

        var card = new FlatCard(L("tab.weather"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        citySearch = new FlatText { Width = 300 };
        var all = new FlatButton { Text = L("btn.selectAll") };
        var none = new FlatButton { Text = L("btn.selectNone") };
        var searchRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        searchRow.Controls.Add(citySearch);
        searchRow.Controls.Add(all);
        searchRow.Controls.Add(none);
        inner.Controls.Add(Row(L("lbl.search"), searchRow, 150));
        cityList = new FlatList { Width = 740, Height = 300 };
        cityList.SetShowChecks(true);
        inner.Controls.Add(cityList);
        card.Controls.Add(inner);
        flow.Controls.Add(card);

        weatherMapOnClick = new FlatCheck { Text = Loc.T("pk.click.a.city.on") };
        flow.Controls.Add(weatherMapOnClick);
        flow.Controls.Add(Note(Loc.T("pk.the.map.openstreetmap.needs"), 640));

        // What the weather line shows besides the temperature.
        var fields = new FlatCard(Loc.T("pk.details.shown"));
        var fieldsFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        weatherFieldChecks.Clear();
        foreach (var id in WeatherInfo.FieldIds)
        {
            var check = new FlatCheck { Text = Loc.T("weather." + (id == "feels" ? "feels" : id)) };
            weatherFieldChecks[id] = check;
            fieldsFlow.Controls.Add(check);
        }
        fieldsFlow.Controls.Add(Note(Loc.T("pk.the.temperature.is.always"), 600));
        fields.Controls.Add(fieldsFlow);
        flow.Controls.Add(fields);

        citySearch.TextValueChanged += (_, _) => ShowCities();
        cityList.ItemChecked += (index, isChecked) =>
        {
            if (index < 0 || index >= shownCities.Count) return;
            var id = shownCities[index].Id;
            if (isChecked) { if (!workCities.Contains(id)) workCities.Add(id); }
            else workCities.Remove(id);
        };
        all.Click += (_, _) => { foreach (var c in shownCities) if (!workCities.Contains(c.Id)) workCities.Add(c.Id); ShowCities(); };
        none.Click += (_, _) => { foreach (var c in shownCities) workCities.Remove(c.Id); ShowCities(); };
        ShowCities();
        return page;
    }

    private void ShowCities()
    {
        if (cityList is null || cityList.IsDisposed) return;
        var term = citySearch.Value.Trim();
        shownCities = GreekCities.All.Where(c => term.Length == 0 || c.NameEl.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                                 c.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        cityList.ClearRows();
        foreach (var c in shownCities) cityList.AddRow(CityName(c), workCities.Contains(c.Id));
    }

    // ---- updates -----------------------------------------------------------------------------

    private void ShowUpdateState()
    {
        if (updateStatus is null || updateStatus.IsDisposed) return;
        var u = host.AvailableUpdate;
        installUpdate.Visible = u is not null;
        openUpdatePage.Visible = u is not null;
        if (u is not null) updateStatus.Text = Loc.T("msg.updateAvailable", u.Version, AboutTexts.Version);
    }

    private async Task CheckUpdatesNowAsync(FlatButton button)
    {
        button.Enabled = false;
        installUpdate.Visible = false;
        openUpdatePage.Visible = false;
        updateStatus.Text = L("msg.checking");
        try
        {
            var found = await host.CheckForUpdatesAsync();
            if (IsDisposed) return;
            if (found is null) updateStatus.Text = Loc.T("msg.upToDate", AboutTexts.Version);
            ShowUpdateState();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            if (!IsDisposed) updateStatus.Text = Loc.T("msg.updateFailed", ex.Message);
        }
        finally
        {
            if (!button.IsDisposed) button.Enabled = true;
        }
    }

    private bool installingUpdate;

    private async Task InstallUpdateAsync()
    {
        if (host.AvailableUpdate is not { } u || installingUpdate) return;
        // Nothing to verify the file against: nothing is run, the release page opens for a manual download.
        if (!u.CanInstallAutomatically)
        {
            updateStatus.Text = L("upd.noDigest");
            OpenInBrowser(u.PageUrl);
            return;
        }
        var hosted = host.HostedByOpsis;   // inside Opsis the plugin files are in use: the update waits until Opsis is closed
        var question = hosted ? L("msg.confirmUpdateOnExit") : Loc.T("upd.confirm", u.Version);
        if (MessageBox.Show(this, question, hosted ? L("app.name") : L("upd.confirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        installingUpdate = true;
        installUpdate.Enabled = false;
        updateProgress.Value = 0;
        updateProgress.Visible = true;
        try
        {
            var progress = new Progress<double>(p =>
            {
                if (IsDisposed) return;
                updateProgress.Value = p;
                updateStatus.Text = Loc.T("msg.downloading", (int)Math.Round(p * 100));
            });
            var (path, sha256) = await UpdateInstaller.DownloadAsync(u, progress);
            if (IsDisposed) return;
            updateStatus.Text = L(hosted ? "msg.updateScheduled" : "msg.updateStarted");
            UpdateInstaller.LaunchAfterExit(path, sha256, hosted);
            if (!hosted) host.RequestExit();
        }
        catch (InvalidDataException)
        {
            if (!IsDisposed) updateStatus.Text = L("msg.verifyFailed");
        }
        catch (InvalidOperationException)
        {
            if (!IsDisposed) { updateStatus.Text = L("upd.noDigest"); OpenInBrowser(u.PageUrl); }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            if (!IsDisposed) updateStatus.Text = Loc.T("msg.updateFailed", ex.Message);
        }
        finally
        {
            installingUpdate = false;
            if (!IsDisposed)
            {
                updateProgress.Visible = false;
                installUpdate.Enabled = true;
            }
        }
    }

    // ---- read later ------------------------------------------------------------------------

    private Control BuildReadLater()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Note(L("note.readLater"), 700));
        var card = new FlatCard(L("tab.readLater"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        laterList = new FlatList { Width = 740, Height = 320 };
        laterList.SetShowChecks(true);
        inner.Controls.Add(laterList);
        var open = new FlatButton { Text = L("btn.open") };
        var remove = new FlatButton { Text = L("btn.removeFeed") };
        var all = new FlatButton { Text = L("btn.selectAll") };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(open);
        buttons.Controls.Add(remove);
        buttons.Controls.Add(all);
        inner.Controls.Add(buttons);
        laterStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(780, 0), Margin = new Padding(0, 6, 0, 0) };
        inner.Controls.Add(laterStatus);
        card.Controls.Add(inner);
        flow.Controls.Add(card);

        open.Click += (_, _) =>
        {
            foreach (var a in CheckedLater().Take(20)) OpenInBrowser(a.Url);
        };
        remove.Click += (_, _) => host.ReadLater.Remove(CheckedLater().Select(a => a.Url));
        all.Click += (_, _) =>
        {
            for (var i = 0; i < laterShown.Count; i++) laterList.SetChecked(i, true);
        };
        RefreshReadLater();
        return page;
    }

    private List<SavedArticle> CheckedLater() =>
        laterShown.Where((_, i) => laterList.GetChecked(i)).ToList();

    private void OnReadLaterChanged()
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(new Action(RefreshReadLater));
        else RefreshReadLater();
    }

    private void RefreshReadLater()
    {
        if (laterList is null || laterList.IsDisposed) return;
        laterShown = host.ReadLater.Items.ToList();
        laterList.ClearRows();
        foreach (var a in laterShown)
            laterList.AddRow($"{a.Source}  —  {a.Title}", false);
        laterStatus.Text = laterShown.Count == 0 ? L("msg.readLaterEmpty") : Loc.T("fmt.readLaterCount", laterShown.Count);
    }

    /// <summary>Opens a saved article in the browser (http/https only).</summary>
    private static void OpenInBrowser(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    private Control BuildMarkets()
    {
        var page = NewPage(out var flow);
        var openPortfolio = new FlatButton { Text = L("btn.openPortfolio") };
        openPortfolio.SetPrimary(true);
        openPortfolio.Click += (_, _) => host.ShowPortfolio();
        flow.Controls.Add(openPortfolio);
        var openAnalysis = new FlatButton { Text = L("btn.openAnalysis") };
        openAnalysis.Click += (_, _) => host.ShowAnalysis();
        flow.Controls.Add(openAnalysis);
        var historyCard = new FlatCard(L("card.history"));
        var historyFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        yahooHistory = new FlatCheck { Text = L("chk.yahooHistory") };
        historyFlow.Controls.Add(yahooHistory);
        historyFlow.Controls.Add(Note(L("note.yahooHistory"), 700));
        historyCard.Controls.Add(historyFlow);
        flow.Controls.Add(historyCard);

        // The Yahoo Finance catalogue: search any share, index, currency or commodity it offers, or add a ready-made group.
        var catalog = new FlatCard(L("card.yahooCatalog"));
        var catalogFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        catalogFlow.Controls.Add(Note(L("yahoo.note"), 700));
        yahooSearch = new FlatText { Width = 380 };
        yahooSearchButton = new FlatButton { Text = L("btn.yahooSearch") };
        yahooSearchButton.SetPrimary(true);
        var searchRow2 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        searchRow2.Controls.Add(yahooSearch);
        searchRow2.Controls.Add(yahooSearchButton);
        catalogFlow.Controls.Add(searchRow2);
        yahooHitList = new FlatList { Width = 740, Height = 30 * 6 + 4 };
        yahooHitList.SetShowChecks(true);
        catalogFlow.Controls.Add(yahooHitList);
        yahooAddButton = new FlatButton { Text = L("btn.yahooAdd") };
        yahooRemoveButton = new FlatButton { Text = L("btn.yahooRemove") };
        var buttonRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(740, 0) };
        buttonRow.Controls.Add(yahooAddButton);
        foreach (var group in YahooPresets.All)
        {
            var presetButton = new FlatButton { Text = L("yahoo.preset." + group.Key) };
            var g = group;
            presetButton.Click += (_, _) => AddYahoo(g.Items.Select(i => (i.Symbol, i.NameEl, i.NameEn)));
            yahooPresetButtons.Add(presetButton);
            buttonRow.Controls.Add(presetButton);
        }
        buttonRow.Controls.Add(yahooRemoveButton);
        catalogFlow.Controls.Add(buttonRow);
        yahooStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(700, 0), Margin = new Padding(0, 6, 0, 0), UseMnemonic = false };
        catalogFlow.Controls.Add(yahooStatus);
        catalog.Controls.Add(catalogFlow);
        flow.Controls.Add(catalog);
        yahooCatalog = catalog;
        yahooSearchButton.Click += async (_, _) => await RunYahooSearchAsync();
        yahooAddButton.Click += (_, _) =>
            AddYahoo(Enumerable.Range(0, yahooHits.Count).Where(i => yahooHitList.GetChecked(i)).Select(i => (yahooHits[i].Symbol, yahooHits[i].Name, yahooHits[i].Name)));
        yahooRemoveButton.Click += (_, _) => RemoveSelectedYahoo();
        yahooHistory.CheckedChanged += (_, _) => UpdateYahooUi();
        UpdateYahooUi();
        flow.Controls.Add(Note(L("note.markets"), 700));
        flow.Controls.Add(Note(Loc.T("pk.data.limits.athens.prices"), 700));

        // All the shares of one Euronext market: pick the market, search, tick the ones to show.
        var card = new FlatCard(L("card.shares"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        marketCombo = new FlatCombo { Width = 360 };
        marketCombo.SetItems(EuronextCatalog.Markets.Select(m => m.DisplayName), 0);
        refreshCatalog = new FlatButton { Text = L("btn.refreshList") };
        var marketRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        marketRow.Controls.Add(marketCombo);
        marketRow.Controls.Add(refreshCatalog);
        inner.Controls.Add(Row(L("lbl.market"), marketRow, 150));
        stockSearch = new FlatText { Width = 360 };
        var selectAll = new FlatButton { Text = L("btn.selectAll") };
        var selectNone = new FlatButton { Text = L("btn.selectNone") };
        var searchRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        searchRow.Controls.Add(stockSearch);
        searchRow.Controls.Add(selectAll);
        searchRow.Controls.Add(selectNone);
        selectAll.Click += (_, _) => SetAllVisibleStocks(true);
        selectNone.Click += (_, _) => SetAllVisibleStocks(false);
        inner.Controls.Add(Row(L("lbl.search"), searchRow, 150));
        stockList = new FlatList { Width = 740, Height = 340 };
        stockList.SetShowChecks(true);
        inner.Controls.Add(stockList);
        stockStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(680, 0), Margin = new Padding(0, 6, 0, 0) };
        inner.Controls.Add(stockStatus);
        card.Controls.Add(inner);
        flow.Controls.Add(card);

        marketCombo.SelectedIndexChanged += (_, _) =>
        {
            stockSearch.Value = "";
            LoadCatalog(force: false);
        };
        refreshCatalog.Click += (_, _) => LoadCatalog(force: true);
        stockSearch.TextValueChanged += (_, _) => ShowStocks();
        stockList.ItemChecked += (index, isChecked) => ToggleStock(index, isChecked);

        foreach (var (source, titleKey) in new[] { (QuoteSource.Metals, "card.metals"), (QuoteSource.Crypto, "card.crypto"), (QuoteSource.Currency, "card.currency"), (QuoteSource.Energy, "card.energy"), (QuoteSource.Yahoo, "card.yahoo") })
        {
            var small = new FlatCard(L(titleKey));
            var list = new FlatList { Width = 640, Height = 30 * 5 + 4 };
            list.SetShowChecks(true);
            var src = source;
            list.ItemChecked += (index, isChecked) =>
            {
                var items = workWatch.Where(i => i.Source == src).ToList();
                if (index < items.Count) items[index].Enabled = isChecked;
            };
            watchLists[source] = list;
            small.Controls.Add(list);
            flow.Controls.Add(small);
        }

        // Price alerts: a limit per instrument; when it is passed the bar shows an alert like a breaking headline.
        var alertCard = new FlatCard(Loc.T("pk.price.alerts"));
        var alertFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        alertFlow.Controls.Add(Note(Loc.T("pk.when.the.price.passes"), 640));
        alertList = new FlatList { Width = 640, Height = 30 * 4 + 4 };
        alertList.SetShowChecks(true);
        alertList.ItemChecked += (index, isChecked) =>
        {
            if (index >= 0 && index < workAlerts.Count) workAlerts[index].Enabled = isChecked;
        };
        alertFlow.Controls.Add(alertList);
        alertInstrument = new FlatCombo { Width = 250 };
        alertDirection = new FlatCombo { Width = 80 };
        alertDirection.SetItems(new[] { "≥", "≤" }, 0);
        alertThreshold = new FlatText { Width = 100 };
        var addAlert = new FlatButton { Text = L("btn.new") };
        addAlert.Click += (_, _) => AddAlert();
        var removeAlert = new FlatButton { Text = L("btn.delete") };
        removeAlert.Click += (_, _) => RemoveAlert();
        var alertRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        alertRow.Controls.AddRange(new Control[] { alertInstrument, alertDirection, alertThreshold, addAlert, removeAlert });
        alertFlow.Controls.Add(alertRow);
        alertCard.Controls.Add(alertFlow);
        flow.Controls.Add(alertCard);

        // Which price sources answered, how many prices and how fresh (updates by itself while the window is open).
        var health = new FlatCard(Loc.T("pk.price.source.status"));
        sourceStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(680, 0) };
        health.Controls.Add(sourceStatus);
        flow.Controls.Add(health);
        sourceTimer.Tick -= UpdateSourceStatus;
        sourceTimer.Tick += UpdateSourceStatus;
        sourceTimer.Start();
        UpdateSourceStatus(null, EventArgs.Empty);
        return page;
    }

    /// <summary>The instruments an alert can be set for: the ticked ones. Rebuilt only when that set changes.</summary>
    private void RefreshAlertChoices()
    {
        if (alertInstrument is null || alertInstrument.IsDisposed) return;
        var choices = workWatch.Where(w => w.Enabled).ToList();
        var signature = string.Join("|", choices.Select(c => c.Source + ":" + c.Market + ":" + c.Code));
        if (signature == alertChoicesSignature) return;
        alertChoicesSignature = signature;
        alertChoices = choices;
        alertInstrument.SetItems(choices.Select(c => string.IsNullOrEmpty(c.Symbol) ? c.DisplayName(Loc.IsGreek) : c.Symbol + " — " + c.DisplayName(Loc.IsGreek)), choices.Count > 0 ? 0 : -1);
    }

    private void ShowAlerts()
    {
        if (alertList is null || alertList.IsDisposed) return;
        alertList.ClearRows();
        foreach (var a in workAlerts)
            alertList.AddRow($"{a.Label}  {(a.Above ? "≥" : "≤")}  {a.Threshold.ToString("0.00##", System.Globalization.CultureInfo.CurrentCulture)}", a.Enabled);
    }

    private void AddAlert()
    {
        RefreshAlertChoices();
        var index = alertInstrument.SelectedIndex;
        if (index < 0 || index >= alertChoices.Count) return;
        var text = (alertThreshold.Value ?? "").Trim().Replace(',', '.');
        if (!decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var limit) || limit <= 0)
        {
            SetStatus(Loc.T("pk.enter.a.valid.limit"));
            return;
        }
        var inst = alertChoices[index];
        workAlerts.Add(new PriceAlert
        {
            Source = inst.Source, Code = inst.Code, Market = inst.Market,
            Label = string.IsNullOrEmpty(inst.Symbol) ? inst.DisplayName(Loc.IsGreek) : inst.Symbol,
            Above = alertDirection.SelectedIndex != 1, Threshold = limit, Enabled = true
        });
        alertThreshold.Value = "";
        ShowAlerts();
        MarkDirty();
    }

    private void RemoveAlert()
    {
        var index = alertList.SelectedIndex;
        if (index < 0 || index >= workAlerts.Count) return;
        workAlerts.RemoveAt(index);
        ShowAlerts();
        MarkDirty();
    }

    private static string SourceName(QuoteSource source) => source switch
    {
        QuoteSource.Athens => Loc.T("pk.euronext.athens.15.min"),
        QuoteSource.Euronext => Loc.T("pk.other.euronext.markets.end"),
        QuoteSource.Metals => Loc.T("pk.metals.gold.api.com"),
        QuoteSource.Crypto => Loc.T("pk.crypto.gold.api.com"),
        QuoteSource.Currency => Loc.T("pk.currencies.ecb.once.a"),
        QuoteSource.Energy => Loc.T("pk.energy.pyth.fred.fallback"),
        QuoteSource.Yahoo => "Yahoo Finance",
        _ => source.ToString()
    };

    private void UpdateSourceStatus(object? sender, EventArgs e)
    {
        if (sourceStatus is null || sourceStatus.IsDisposed) return;
        RefreshAlertChoices();
        var rows = QuoteService.Statuses();
        if (rows.Count == 0)
        {
            sourceStatus.Text = Loc.T("pk.no.prices.have.been");
            return;
        }
        var lines = rows.Select(s =>
        {
            var state = s.Failed ? Loc.T("pk.failed") : Loc.T("pk.ok");
            var line = $"{(s.Failed ? "✖" : "✔")} {SourceName(s.Source)}: {state}, {s.Count} {Loc.T("pk.prices")}, "
                + $"{Loc.T("pk.checked")} {s.CheckedAt:HH:mm:ss}";
            if (s.Newest is { } newest) line += $", {Loc.T("pk.newest.price")} {newest.ToLocalTime():dd/MM HH:mm}";
            return line;
        });
        sourceStatus.Text = string.Join(Environment.NewLine, lines);
    }

    // ---- the shares of a Euronext market ----------------------------------------------------

    /// <summary>The market's catalog (downloaded earlier or built in) plus whatever is already chosen from it.</summary>
    private IReadOnlyList<QuoteInstrument> CatalogFor(string mic)
    {
        var map = new Dictionary<string, QuoteInstrument>(StringComparer.Ordinal);
        if (mic == "XATH")
            foreach (var d in QuoteInstrument.Defaults().Where(d => d.Source == QuoteSource.Athens)) { d.Market = "XATH"; d.Enabled = false; map[d.Code] = d; }
        foreach (var c in catalogs.TryGetValue(mic, out var loaded) ? loaded : EuronextCatalog.LoadCached(mic)) map[c.Code] = c;
        foreach (var w in workWatch.Where(w => w.Market == mic && w.Source is QuoteSource.Athens or QuoteSource.Euronext)) map[w.Code] = w;
        return map.Values.OrderBy(i => i.DisplayName(Loc.IsGreek), StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async void LoadCatalog(bool force)
    {
        var mic = SelectedMic;
        var cached = EuronextCatalog.LoadCached(mic);
        if (!force && (cached.Count > 0 || mic == "XATH" && catalogs.ContainsKey(mic)))
        {
            catalogs[mic] = cached;
            ShowStocks();
            return;
        }
        // Athens has a built-in list of the main shares; the rest is downloaded (names via OpenFIGI, about half a minute).
        ShowStocks();
        stockStatus.Text = L("msg.loadingList");
        refreshCatalog.Enabled = false;
        var progress = new Progress<string>(p => { if (!IsDisposed && SelectedMic == mic) stockStatus.Text = L("msg.loadingList") + "  " + p; });
        IReadOnlyList<QuoteInstrument> list;
        try
        {
            list = await EuronextCatalog.FetchAsync(mic, progress);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            list = Array.Empty<QuoteInstrument>(); // shown as "could not be downloaded"
        }
        if (IsDisposed) return;
        refreshCatalog.Enabled = true;
        catalogs[mic] = list;
        if (SelectedMic == mic)
        {
            ShowStocks();
            if (list.Count == 0) stockStatus.Text = L("msg.listFailed");
        }
    }

    private void ShowStocks()
    {
        if (stockList is null) return;
        var mic = SelectedMic;
        var term = stockSearch.Value.Trim();
        var all = CatalogFor(mic);
        visibleStocks = all.Where(i => term.Length == 0 ||
            i.Symbol.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            i.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            i.NameEl.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            i.Code.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        stockList.ClearRows();
        foreach (var i in visibleStocks)
            stockList.AddRow($"{i.Symbol}  —  {i.DisplayName(Loc.IsGreek)}   ({i.Unit})", IsChosen(i));
        UpdateStockStatus(all.Count);
    }

    private bool IsChosen(QuoteInstrument i) => workWatch.Any(w => w.Code == i.Code && w.Market == i.Market && w.Enabled);

    private void UpdateStockStatus(int total)
    {
        var chosen = workWatch.Count(w => w.Market == SelectedMic && w.Enabled);
        var stamp = EuronextCatalog.CacheTime(SelectedMic);
        stockStatus.Text = Loc.T("fmt.sharesCount", chosen, total) + (stamp is { } t ? "  ·  " + Loc.T("fmt.listDate", t.ToString("dd/MM/yyyy")) : "");
    }

    /// <summary>Ticks or unticks every share that is listed right now (the search filter applies).</summary>
    private void SetAllVisibleStocks(bool isChecked)
    {
        for (var i = 0; i < visibleStocks.Count; i++) ToggleStock(i, isChecked);
        ShowStocks();
    }

    private void ToggleStock(int index, bool isChecked)
    {
        if (index < 0 || index >= visibleStocks.Count) return;
        var inst = visibleStocks[index];
        var existing = workWatch.FirstOrDefault(w => w.Code == inst.Code && w.Market == inst.Market);
        var isDefault = QuoteInstrument.Defaults().Any(d => d.Code == inst.Code);
        if (isChecked)
        {
            if (existing is not null) existing.Enabled = true;
            else workWatch.Add(new QuoteInstrument(inst.Source, inst.Code, inst.Symbol, inst.NameEl, inst.NameEn, inst.Unit) { Market = inst.Market });
        }
        else if (existing is not null)
        {
            if (isDefault) existing.Enabled = false;
            else workWatch.Remove(existing); // the watch list only keeps what is shown
        }
        UpdateStockStatus(CatalogFor(SelectedMic).Count);
    }

    private Control BuildLook()
    {
        var page = NewPage(out var flow);
        themeMode = new FlatCombo { Width = 300 };
        themeMode.SetItems(new[] { L("theme.system"), L("theme.light"), L("theme.dark") }, 0);
        flow.Controls.Add(Row(L("lbl.theme"), themeMode, 240));

        opacity = new FlatSlider { Width = 300 };
        opacity.SetRange(30, 100);
        opacityLabel = new Label { AutoSize = false, Width = 60, Height = 26, TextAlign = ContentAlignment.MiddleLeft };
        var op = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        op.Controls.Add(opacity);
        op.Controls.Add(opacityLabel);
        flow.Controls.Add(Row(L("lbl.transparency"), op, 240));
        opacity.ValueChanged += (_, _) => opacityLabel.Text = $"{opacity.Value} %";

        barColor = new FlatCombo { Width = 470 };
        barColor.SetItems(new[] { L("color.theme"), L("color.lightblue"), L("color.accent"), L("color.custom") }, 0);
        barSwatch = new FlatSwatch();
        var barColorRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        barColorRow.Controls.Add(barColor);
        barColorRow.Controls.Add(barSwatch);
        flow.Controls.Add(Row(L("lbl.barColor"), barColorRow, 240));

        taskbarColor = new FlatCombo { Width = 470 };
        taskbarColor.SetItems(new[] { L("color.taskbar"), L("color.lightblue"), L("color.accent"), L("color.custom") }, 0);
        taskbarSwatch = new FlatSwatch();
        var taskbarColorRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        taskbarColorRow.Controls.Add(taskbarColor);
        taskbarColorRow.Controls.Add(taskbarSwatch);
        flow.Controls.Add(Row(L("lbl.taskbarBarColor"), taskbarColorRow, 240));
        var materials = new[] { L("backdrop.acrylic"), L("backdrop.blur"), L("backdrop.gradient") };
        barBackdrop = new FlatCombo { Width = 470 };
        barBackdrop.SetItems(materials, 0);
        flow.Controls.Add(Row(L("lbl.barBackdrop"), barBackdrop, 240));
        taskbarBackdrop = new FlatCombo { Width = 470 };
        taskbarBackdrop.SetItems(materials, 1);
        flow.Controls.Add(Row(L("lbl.taskbarBackdrop"), taskbarBackdrop, 240));
        flow.Controls.Add(Note(L("note.backdrop"), 640));
        barColor.SelectedIndexChanged += (_, _) => barSwatch.Enabled = barColor.SelectedIndex == 3;
        taskbarColor.SelectedIndexChanged += (_, _) => taskbarSwatch.Enabled = taskbarColor.SelectedIndex == 3;

        windowOpacity = new FlatSlider { Width = 300 };
        windowOpacity.SetRange(60, 100);
        windowOpacityLabel = new Label { AutoSize = false, Width = 60, Height = 26, TextAlign = ContentAlignment.MiddleLeft };
        var wop = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        wop.Controls.Add(windowOpacity);
        wop.Controls.Add(windowOpacityLabel);
        windowOpacityRow = Row(L("lbl.windowOpacity"), wop, 240);
        windowOpacity.ValueChanged += (_, _) =>
        {
            windowOpacityLabel.Text = $"{windowOpacity.Value} %";
            // live preview on the settings window itself
            if (!loading && FindForm() is { } form && form.Parent is null && !HostThemeLocked) form.Opacity = windowOpacity.Value / 100.0;
        };
        flow.Controls.Add(windowOpacityRow);

        edgeFade = new FlatCheck { Text = L("chk.edgeFade") };
        flow.Controls.Add(edgeFade);
        flow.Controls.Add(Note(L("note.transparency"), 640));
        themeNote = Note("", 640);
        flow.Controls.Add(themeNote);
        return page;
    }

    private Control BuildBreaking()
    {
        var page = NewPage(out var flow);
        breakingEnabled = new FlatCheck { Text = L("chk.breakingEnabled") };
        flow.Controls.Add(breakingEnabled);
        breakingSound = new FlatCheck { Text = Loc.T("pk.alert.sound.when.something") };
        flow.Controls.Add(breakingSound);
        keywords = new FlatText { Width = 520 };
        flow.Controls.Add(Row(L("lbl.keywords"), keywords, 330));
        maxAge = new FlatNumber();
        maxAge.Configure(10, 1440, 10);
        flow.Controls.Add(Row(L("lbl.maxAge"), maxAge, 330));
        duration = new FlatNumber();
        duration.Configure(1, 240, 5);
        flow.Controls.Add(Row(L("lbl.duration"), duration, 330));
        flow.Controls.Add(Note(L("note.breaking"), 700));

        // Severe weather for the user's area.
        var weatherCard = new FlatCard(Loc.T("pk.severe.weather.in.my"));
        var weatherFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        weatherAlerts = new FlatCheck { Text = Loc.T("pk.warn.me.about.severe") };
        weatherFlow.Controls.Add(weatherAlerts);
        alertCity = new FlatCombo { Width = 360 };
        alertCity.SetItems(new[] { Loc.T("pk.automatic.the.first.city") }
            .Concat(GreekCities.All.Select(CityName)), 0);
        weatherFlow.Controls.Add(Row(Loc.T("pk.my.area"), alertCity, 200));
        weatherFlow.Controls.Add(Note(Loc.T("pk.every.30.minutes.the"), 640));
        var testWeather = new FlatButton { Text = Loc.T("pk.test.the.weather.warning") };
        testWeather.Click += (_, _) => host.SimulateWeatherAlert();
        weatherFlow.Controls.Add(testWeather);
        weatherCard.Controls.Add(weatherFlow);
        flow.Controls.Add(weatherCard);

        var test = new FlatButton { Text = L("btn.testBreaking") };
        test.Click += (_, _) => host.SimulateBreaking(Loc.T("pk.breaking.test.headline.to"));
        flow.Controls.Add(test);

        var card = new FlatCard(L("card.live"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        inner.Controls.Add(Note(L("note.live"), 620));
        liveList = new FlatList { Width = 640, Height = 30 * 4 + 4 };
        liveList.SetShowChecks(true);
        liveList.ItemChecked += (index, isChecked) =>
        {
            if (index < workLive.Count) workLive[index].Enabled = isChecked;
        };
        inner.Controls.Add(liveList);
        var removeLive = new FlatButton { Text = L("btn.removeLive") };
        removeLive.Click += (_, _) =>
        {
            if (liveList.SelectedIndex < 0 || liveList.SelectedIndex >= workLive.Count) return;
            workLive.RemoveAt(liveList.SelectedIndex);
            RebuildLiveList();
        };
        inner.Controls.Add(removeLive);
        inner.Controls.Add(new Label { Text = L("lbl.addLive"), AutoSize = true, Margin = new Padding(0, 10, 0, 4) });
        var add = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        addLiveBox = new FlatText { Width = 440 };
        var addBtn = new FlatButton { Text = L("btn.addLive") };
        addBtn.Click += (_, _) => AddLive();
        add.Controls.Add(addLiveBox);
        add.Controls.Add(addBtn);
        inner.Controls.Add(add);
        liveStatus = new Label { AutoSize = true, Tag = "muted" };
        inner.Controls.Add(liveStatus);
        card.Controls.Add(inner);
        flow.Controls.Add(card);
        return page;
    }

    private void RebuildLiveList()
    {
        liveList.ClearRows();
        foreach (var f in workLive) liveList.AddRow($"{(string.IsNullOrWhiteSpace(f.Name) ? f.Url : f.Name)}   —   {f.Url}", f.Enabled);
    }

    private void AddLive()
    {
        // "Name;Address" (the address may contain ';' only after the first one)
        var split = addLiveBox.Value.Split(';', 2, StringSplitOptions.TrimEntries);
        var name = split.Length > 1 ? split[0] : "";
        var url = split.Length > 1 ? split[1] : split[0];
        if (Breaking.ToPlayableUrl(url) is null)
        {
            liveStatus.Text = L("msg.badLive");
            return;
        }
        workLive.Add(new LiveFeed(name, url));
        RebuildLiveList();
        addLiveBox.Value = "";
        liveStatus.Text = "";
    }

    /// <summary>A read-only page with one of the long texts (help, instructions, history, license).</summary>
    private static Control BuildInfoPage(Func<string> text)
    {
        var page = new Panel { Padding = new Padding(8, 6, 8, 6) };
        var box = new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10f), TabStop = false, Tag = "surface",
            Text = text().Replace("\r\n", "\n").Replace("\n", "\r\n")
        };
        var frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 8, 12), Tag = "surface" };
        frame.Controls.Add(box);
        page.Controls.Add(frame);
        return page;
    }

    // ---- loading / saving ----------------------------------------------------------------

    /// <summary>Number of pages that can be shown (every sub-page of every tab counts once; used by the snapshot test).</summary>
    public int TabCount => tabs.Count;

    /// <summary>Shows the page with the given index (0 = Settings / General).</summary>
    public void SelectTab(int index)
    {
        if (index < 0 || index >= tabs.Count) return;
        tabs.SelectedIndex = index;
    }

    public void LoadFromConfig()
    {
        var was = loading;
        loading = true;
        try
        {
            var c = host.Config;
            var langs = Loc.Available();
            language.SelectSilently(Math.Max(0, langs.ToList().FindIndex(l => l.Code == Loc.Language)));
            refresh.SetValueSilently(c.RefreshIntervalSeconds);
            speed.SetValueSilently(c.ScrollSpeed);
            hoverPause.Checked = c.PauseOnHover;
            icons.Checked = c.ShowIcons;
            preview.Checked = c.ShowPreview;
            pauseOther.Checked = c.PauseOnOtherWindow;
            hideRead.Checked = c.HideRead;
            barsEnabled.Checked = c.BarsEnabled;
            startWithWindows.Checked = StartupRegistration.IsEnabled();
            trayAnimated.Checked = c.TrayIconAnimated;
            toggleHotkey.Checked = c.ToggleHotkey;
            autoUpdate.Checked = c.CheckForUpdates;
            yahooHistory.Checked = c.HistoryFromYahoo;
            UpdateYahooUi();
            workCities = c.WeatherCities.ToList();
            ShowCities();
            miniWeather.Checked = c.MiniWeatherEnabled;
            LoadMiniBounds();
            miniOnTop.Checked = c.MiniWeatherOnTop;
            miniWhenHidden.Checked = c.MiniWhenTaskbarHidden;
            barShape.SelectSilently(c.BarShape switch { "rounded" => 1, "pill" => 2, _ => 0 });
            miniShape.SelectSilently(c.DesktopBarShape switch { "rounded" => 1, "pill" => 2, _ => 0 });

            mainPicker.Set(c.MainSlots());
            miniPicker.Set(c.MiniSlots());
            foreach (var (id, check) in weatherFieldChecks) check.Checked = c.WeatherFields.Contains(id);
            secondEnabled.Checked = c.SecondBarEnabled;
            secondPicker.Set(c.SecondSlots());
            foreach (var (screen, check, picker) in monitorRows)
            {
                var saved = c.MonitorBars.FirstOrDefault(m => string.Equals(m.Device, screen.DeviceName, StringComparison.OrdinalIgnoreCase));
                check.Checked = saved?.Enabled ?? false;
                picker.Set(saved?.Slots() ?? new BarSlots(new BarSlot(SlotKind.News), null));
            }

            position.SelectSilently(Math.Clamp((int)c.Position, 0, 4));
            followTaskbar.Checked = c.FollowTaskbar;
            reserve.Checked = host.EffectiveReserveSpace;
            var custom = host.GetCustomBounds();
            posX.SetValueSilently(custom.X);
            posY.SetValueSilently(custom.Y);
            posW.SetValueSilently(custom.Width);
            tbAuto.Checked = c.TaskbarAutoDetect;
            tbLeft.SetValueSilently(c.TaskbarLeftOffset);
            tbRight.SetValueSilently(c.TaskbarRightOffset);

            themeMode.SelectSilently(c.ThemeMode switch { "light" => 1, "dark" => 2, _ => 0 });
            opacity.Value = Math.Clamp(c.OpacityPercent, 30, 100);
            opacityLabel.Text = $"{opacity.Value} %";
            edgeFade.Checked = c.EdgeFade;
            windowOpacity.Value = Math.Clamp(c.SettingsOpacityPercent, 60, 100);
            windowOpacityLabel.Text = $"{windowOpacity.Value} %";
            // Inside Opsis the page is part of Opsis' own window, whose opacity is Opsis' setting.
            windowOpacityRow.Visible = !HostThemeLocked;
            barColor.SelectSilently(c.BarColorMode switch { "lightblue" => 1, "accent" => 2, "custom" => 3, _ => 0 });
            barSwatch.Rgb = c.BarCustomColor;
            barSwatch.Enabled = barColor.SelectedIndex == 3;
            taskbarColor.SelectSilently(c.TaskbarBarColorMode switch { "lightblue" => 1, "accent" => 2, "custom" => 3, _ => 0 });
            taskbarSwatch.Rgb = c.TaskbarBarCustomColor;
            taskbarSwatch.Enabled = taskbarColor.SelectedIndex == 3;
            barBackdrop.SelectSilently(c.BarBackdrop switch { "blur" => 1, "gradient" => 2, _ => 0 });
            taskbarBackdrop.SelectSilently(c.TaskbarBarBackdrop switch { "acrylic" => 0, "gradient" => 2, _ => 1 });
            breakingEnabled.Checked = c.BreakingEnabled;
            breakingSound.Checked = c.BreakingSound;
            weatherMapOnClick.Checked = c.WeatherMapOnClick;
            weatherAlerts.Checked = c.WeatherAlertsEnabled;
            alertCity.SelectSilently(string.IsNullOrEmpty(c.AlertCityId) ? 0 : Math.Max(0, GreekCities.All.ToList().FindIndex(x => x.Id == c.AlertCityId) + 1));
            keywords.Value = string.Join("; ", c.BreakingKeywords);
            maxAge.SetValueSilently(c.BreakingMaxAgeMinutes);
            duration.SetValueSilently(c.BreakingDurationMinutes);
            workLive = c.LiveFeeds.Select(f => new LiveFeed(f.Name, f.Url) { Enabled = f.Enabled }).ToList();
            RebuildLiveList();
            themeMode.Enabled = !HostThemeLocked;
            themeNote.Text = HostThemeLocked ? L("note.hostTheme") : "";

            newsMaxAge.SetValueSilently(c.NewsMaxAgeHours);
            newsNewestFirst.Checked = c.NewsSortNewestFirst;
            stockNewsOnly.Checked = c.StockNewsOnly;
            mutedNewsKeywords.Value = string.Join("; ", c.MutedNewsKeywords);
            workCategories = c.Categories.Select(Clone).ToList();
            RebuildCategoryLists(c.ActiveCategory);
            workWatch = c.Watchlist.Select(w => w.Clone()).ToList();
            workAlerts = c.PriceAlerts.Select(a => a.Clone()).ToList();
            alertChoicesSignature = "";
            RefreshAlertChoices();
            ShowAlerts();
            RebuildWatchLists();
            UpdatePositionGroups();
            UpdateBarsUi();
            SyncDefaultBar();
        }
        finally
        {
            loading = was;
        }
        moveWithMouse.Text = host.IsPositionEditMode ? L("btn.moveMouseDone") : L("btn.moveMouse");
        hasUnsavedChanges = false;
        if (status is not null && !status.IsDisposed) status.Text = "";
    }

    private void AttachDirtyTracking()
    {
        if (dirtyTrackingAttached || tabs is null) return;
        dirtyTrackingAttached = true;
        foreach (var control in Descendants(tabs))
        {
            switch (control)
            {
                case FlatCheck check:
                    check.CheckedChanged += (_, _) => MarkDirty();
                    break;
                case FlatCombo combo:
                    combo.SelectedIndexChanged += (_, _) => MarkDirty();
                    break;
                case FlatNumber number:
                    number.ValueChanged += (_, _) => MarkDirty();
                    break;
                case FlatText text:
                    text.TextValueChanged += (_, _) => MarkDirty();
                    break;
                case FlatSlider slider:
                    slider.ValueChanged += (_, _) => MarkDirty();
                    break;
                case FlatList list:
                    list.ItemChecked += (_, _) => MarkDirty();
                    break;
                case FlatButton button when !button.IsNavigation:
                    button.Click += (_, _) =>
                    {
                        // Actions that only open dialogs, refresh data, or perform maintenance do not
                        // make the editable settings dirty by themselves.
                        var nonEditingActions = new[]
                        {
                            L("btn.resetRead"), L("btn.openLog"), L("btn.checkNow"),
                            L("btn.installUpdate"), L("btn.openPage"), L("btn.refreshList"),
                            L("btn.testBreaking"), L("btn.open"), L("settings.exportOpml")
                        };
                        if (!nonEditingActions.Contains(button.Text, StringComparer.CurrentCultureIgnoreCase)
                            && !button.Text.Contains("Export OPML", StringComparison.OrdinalIgnoreCase)
                            && !button.Text.Contains("Εξαγωγή OPML", StringComparison.OrdinalIgnoreCase))
                            MarkDirty();
                    };
                    break;
            }
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private void MarkDirty()
    {
        if (loading || !built) return;
        hasUnsavedChanges = true;
        if (status is not null && !status.IsDisposed) status.Text = L("settings.unsavedChanges");
    }

    /// <summary>Prompts before closing when the user has edits that have not been saved.</summary>
    public bool ConfirmCloseIfNeeded()
    {
        if (!hasUnsavedChanges) return true;
        var result = MessageBox.Show(FindForm(), L("settings.unsavedPrompt"), L("window.settings"),
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
        if (result == DialogResult.Yes)
        {
            ApplySettings();
            return !hasUnsavedChanges;
        }
        if (result == DialogResult.No)
        {
            LoadFromConfig();
            return true;
        }
        return false;
    }

    private static FeedCategory Clone(FeedCategory c) => new()
    {
        Id = c.Id, NameEl = c.NameEl, NameEn = c.NameEn, BuiltIn = c.BuiltIn,
        Feeds = c.Feeds.Select(f => new FeedSource(f.Name, f.Url) { Enabled = f.Enabled }).ToList()
    };

    private IEnumerable<SlotPicker> AllPickers()
    {
        if (mainPicker is not null) yield return mainPicker;
        if (secondPicker is not null) yield return secondPicker;
        if (miniPicker is not null) yield return miniPicker;
        foreach (var row in monitorRows) yield return row.Picker;
    }

    private void RebuildCategoryLists(string? activeId)
    {
        category.SetItems(workCategories.Select(c => c.DisplayName), 0);
        categoryList.ClearRows();
        foreach (var cat in workCategories) categoryList.AddRow(cat.DisplayName);
        var index = Math.Max(0, workCategories.FindIndex(c => string.Equals(c.Id, activeId, StringComparison.OrdinalIgnoreCase)));
        category.SelectSilently(index);
        if (categoryList.Items.Count > 0) categoryList.SelectedIndex = index;
        foreach (var picker in AllPickers()) picker.SetChoices(workCategories);
    }

    private void UpdateYahooUi()
    {
        var on = yahooHistory.Checked;
        foreach (var c in new Control[] { yahooSearch, yahooSearchButton, yahooAddButton, yahooRemoveButton, yahooHitList }.Concat(yahooPresetButtons)) c.Enabled = on;
        if (!on) yahooStatus.Text = L("yahoo.off");
        else if (yahooStatus.Text == L("yahoo.off")) yahooStatus.Text = "";
    }

    private async Task RunYahooSearchAsync()
    {
        var query = yahooSearch.Value.Trim();
        if (query.Length < 2 || !yahooHistory.Checked) return;
        // the switch is applied with the other settings; a search needs it now
        YahooAccess.Enabled = true;
        yahooSearchButton.Enabled = false;
        yahooStatus.Text = Loc.T("pk.testing");
        try
        {
            yahooHits = (await YahooSearch.SearchAsync(query, CancellationToken.None)).ToList();
            yahooHitList.ClearRows();
            foreach (var h in yahooHits) yahooHitList.AddRow($"{h.Symbol}  —  {h.Name}   ({h.Type}, {h.Exchange})", false);
            yahooStatus.Text = yahooHits.Count == 0 ? L("yahoo.none") : Loc.T("yahoo.found", yahooHits.Count);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            yahooStatus.Text = Loc.T("yahoo.error", ex.Message);
        }
        finally
        {
            if (!IsDisposed) { yahooSearchButton.Enabled = yahooHistory.Checked; YahooAccess.Enabled = host.Config.HistoryFromYahoo; }
        }
    }

    /// <summary>Adds Yahoo instruments to the watch list (shown on the bars); at most <see cref="YahooAccess.MaxInstruments"/> of them.</summary>
    private void AddYahoo(IEnumerable<(string Symbol, string NameEl, string NameEn)> items)
    {
        var added = 0;
        foreach (var (symbol, el, en) in items)
        {
            if (workWatch.Any(w => w.Source == QuoteSource.Yahoo && string.Equals(w.Code, symbol, StringComparison.OrdinalIgnoreCase))) continue;
            if (workWatch.Count(w => w.Source == QuoteSource.Yahoo) >= YahooAccess.MaxInstruments)
            {
                yahooStatus.Text = Loc.T("yahoo.limit", YahooAccess.MaxInstruments);
                break;
            }
            workWatch.Add(new QuoteInstrument(QuoteSource.Yahoo, symbol, symbol, el, en, ""));
            added++;
        }
        if (added > 0)
        {
            RebuildWatchLists();
            MarkDirty();
            yahooStatus.Text = Loc.T("yahoo.added", added);
        }
    }

    private void RemoveSelectedYahoo()
    {
        if (!watchLists.TryGetValue(QuoteSource.Yahoo, out var list) || list.SelectedIndex < 0) return;
        var items = workWatch.Where(i => i.Source == QuoteSource.Yahoo).ToList();
        if (list.SelectedIndex >= items.Count) return;
        workWatch.Remove(items[list.SelectedIndex]);
        RebuildWatchLists();
        MarkDirty();
    }

    private void RebuildWatchLists()
    {
        foreach (var (source, list) in watchLists)
        {
            list.ClearRows();
            foreach (var w in workWatch.Where(i => i.Source == source))
                list.AddRow($"{w.Symbol}  —  {w.DisplayName(Loc.IsGreek)}   ({w.Unit})", w.Enabled);
        }
        ShowStocks();
    }

    private void ApplySettings()
    {
        var c = host.Config;
        var mutedKeywords = mutedNewsKeywords.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList();
        var newsSettingsChanged = c.NewsMaxAgeHours != newsMaxAge.Value || c.NewsSortNewestFirst != newsNewestFirst.Checked || c.StockNewsOnly != stockNewsOnly.Checked || !c.MutedNewsKeywords.SequenceEqual(mutedKeywords, StringComparer.OrdinalIgnoreCase);
        c.Language = Loc.Available().ElementAtOrDefault(Math.Max(0, language.SelectedIndex)).Code ?? c.Language;
        c.NewsRefreshSeconds = refresh.Value;
        c.ScrollSpeed = speed.Value;
        c.PauseOnHover = hoverPause.Checked;
        c.ShowIcons = icons.Checked;
        c.ShowPreview = preview.Checked;
        c.PauseOnOtherWindow = pauseOther.Checked;
        c.HideRead = hideRead.Checked;
        c.NewsMaxAgeHours = newsMaxAge.Value;
        c.NewsSortNewestFirst = newsNewestFirst.Checked;
        c.StockNewsOnly = stockNewsOnly.Checked;
        c.MutedNewsKeywords = mutedKeywords;
        c.CheckForUpdates = autoUpdate.Checked;
        c.HistoryFromYahoo = yahooHistory.Checked;
        c.ToggleHotkey = toggleHotkey.Checked;
        c.WeatherCities = workCities.ToList();
        c.MiniWeatherEnabled = miniWeather.Checked;
        c.MiniWeatherWidth = miniW.Value;
        c.MiniWeatherHeight = miniH.Value;
        // Still in the middle of the desktop? Then keep "centred" (it follows the screen) instead of storing numbers.
        var work = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        var cw = Math.Min(miniW.Value, work.Width);
        var centredX = work.Left + (work.Width - cw) / 2;
        var centredY = work.Top + (work.Height - miniH.Value) / 2;
        if (miniX.Value == centredX && miniY.Value == centredY) { c.MiniWeatherX = int.MinValue; c.MiniWeatherY = int.MinValue; }
        else { c.MiniWeatherX = miniX.Value; c.MiniWeatherY = miniY.Value; }
        c.MiniWeatherOnTop = miniOnTop.Checked;
        c.MiniWhenTaskbarHidden = miniWhenHidden.Checked;
        c.BarShape = barShape.SelectedIndex switch { 1 => "rounded", 2 => "pill", _ => "rect" };
        c.DesktopBarShape = miniShape.SelectedIndex switch { 1 => "rounded", 2 => "pill", _ => "rect" };

        var mainSlots = mainPicker.Get();
        c.MainSlotA = mainSlots.A.ToSpec();
        c.MainSlotB = mainSlots.B?.ToSpec() ?? "";
        c.MainContent = mainSlots.ToLegacy();
        var miniSlots = miniPicker.Get();
        c.MiniSlotA = miniSlots.A.ToSpec();
        c.MiniSlotB = miniSlots.B?.ToSpec() ?? "";
        c.WeatherFields = weatherFieldChecks.Where(kv => kv.Value.Checked).Select(kv => kv.Key).ToList();
        c.SecondBarEnabled = secondEnabled.Checked && position.SelectedIndex != (int)TickerPosition.TaskbarIntegrated;
        var secondSlots = secondPicker.Get();
        c.SecondSlotA = secondSlots.A.ToSpec();
        c.SecondSlotB = secondSlots.B?.ToSpec() ?? "";
        c.SecondContent = secondSlots.ToLegacy() == BarContent.Both ? BarContent.Stocks : secondSlots.ToLegacy();
        // Extra bars of the monitors shown here; entries of monitors that are not connected right now are kept.
        var shown = monitorRows.Select(r => r.Screen.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        c.MonitorBars = c.MonitorBars.Where(m => !shown.Contains(m.Device)).Concat(monitorRows.Select(r =>
            new MonitorBar { Device = r.Screen.DeviceName, Enabled = r.Check.Checked, Content = r.Picker.Get().ToLegacy(), SlotA = r.Picker.Get().A.ToSpec(), SlotB = r.Picker.Get().B?.ToSpec() ?? "" })).ToList();

        c.Position = (TickerPosition)Math.Max(0, position.SelectedIndex);
        c.FollowTaskbar = followTaskbar.Checked;
        c.ReserveScreenSpace = reserve.Checked;
        c.CustomX = posX.Value;
        c.CustomY = posY.Value;
        c.CustomWidth = posW.Value;
        c.TaskbarAutoDetect = tbAuto.Checked;
        c.TaskbarLeftOffset = tbLeft.Value;
        c.TaskbarRightOffset = tbRight.Value;

        if (!HostThemeLocked) c.ThemeMode = themeMode.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };
        c.OpacityPercent = opacity.Value;
        c.EdgeFade = edgeFade.Checked;
        if (!HostThemeLocked) c.SettingsOpacityPercent = windowOpacity.Value;
        c.BarBackdrop = barBackdrop.SelectedIndex switch { 1 => "blur", 2 => "gradient", _ => "acrylic" };
        c.TaskbarBarBackdrop = taskbarBackdrop.SelectedIndex switch { 0 => "acrylic", 2 => "gradient", _ => "blur" };
        c.BarColorMode = barColor.SelectedIndex switch { 1 => "lightblue", 2 => "accent", 3 => "custom", _ => "theme" };
        c.BarCustomColor = barSwatch.Rgb;
        c.TaskbarBarColorMode = taskbarColor.SelectedIndex switch { 1 => "lightblue", 2 => "accent", 3 => "custom", _ => "taskbar" };
        c.TaskbarBarCustomColor = taskbarSwatch.Rgb;
        c.BreakingEnabled = breakingEnabled.Checked;
        c.BreakingSound = breakingSound.Checked;
        c.WeatherMapOnClick = weatherMapOnClick.Checked;
        c.WeatherAlertsEnabled = weatherAlerts.Checked;
        c.AlertCityId = alertCity.SelectedIndex > 0 && alertCity.SelectedIndex <= GreekCities.All.Count ? GreekCities.All[alertCity.SelectedIndex - 1].Id : "";
        c.BreakingKeywords = keywords.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        c.BreakingMaxAgeMinutes = maxAge.Value;
        c.BreakingDurationMinutes = duration.Value;
        c.LiveFeeds = workLive.Select(f => new LiveFeed(f.Name, f.Url) { Enabled = f.Enabled }).ToList();

        // Feeds: write the edited copy back and fetch again when the active category's sources changed.
        var previousActive = c.ActiveCategory;
        var before = FeedSignature(c.Categories);
        c.Categories = workCategories.Select(Clone).ToList();
        var chosen = category.SelectedIndex >= 0 && category.SelectedIndex < c.Categories.Count ? c.Categories[category.SelectedIndex].Id : previousActive;
        var feedsChanged = before != FeedSignature(c.Categories);
        c.Watchlist = workWatch.Select(w => w.Clone()).ToList();
        c.PriceAlerts = workAlerts.Select(a => a.Clone()).ToList();

        host.ApplyConfig();
        host.StartHistoryBackfill();
        host.SetBarsEnabled(barsEnabled.Checked);
        if (!host.HostedByOpsis) StartupRegistration.Set(startWithWindows.Checked);
        c.TrayIconAnimated = trayAnimated.Checked;
        host.SetCategory(chosen);
        if ((feedsChanged || newsSettingsChanged) && string.Equals(chosen, previousActive, StringComparison.OrdinalIgnoreCase)) host.ReloadNews();
        host.SaveConfig();
        hasUnsavedChanges = false;

        SetStatus(L("msg.saved"));
        Applied?.Invoke();
    }

    private void ExportSettings()
    {
        using var dialog = new SaveFileDialog
        {
            Title = L("settings.exportTitle"),
            Filter = "JSON settings (*.json)|*.json|All files (*.*)|*.*",
            FileName = "opsis-newscrawler-settings.json",
            DefaultExt = "json",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "OpsisNewsCrawlerExport", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var tempFile = Path.Combine(tempDirectory, "settings.json");
            host.Config.Save(tempFile);
            if (!File.Exists(tempFile) || new FileInfo(tempFile).Length == 0)
                throw new IOException("The settings file could not be written.");
            File.Copy(tempFile, dialog.FileName, overwrite: true);
            SetStatus(L("settings.exported"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(FindForm(), ex.Message, L("settings.exportTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private void ImportSettings()
    {
        using var dialog = new OpenFileDialog
        {
            Title = L("settings.importTitle"),
            Filter = "JSON settings (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        if (MessageBox.Show(FindForm(), L("settings.importConfirm"), L("settings.importTitle"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

        try
        {
            // Validate the JSON before AppConfig.Load applies defaults; malformed imports must never silently
            // replace the user's current configuration with a fresh default profile.
            using (var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dialog.FileName)))
            {
                if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                    throw new System.Text.Json.JsonException("The settings file must contain a JSON object.");
            }
            var tempDirectory = Path.Combine(Path.GetTempPath(), "OpsisNewsCrawlerImport", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            AppConfig imported;
            try
            {
                var tempConfig = Path.Combine(tempDirectory, "config.json");
                File.Copy(dialog.FileName, tempConfig, overwrite: true);
                imported = AppConfig.Load(tempConfig);
            }
            finally
            {
                try { Directory.Delete(tempDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            foreach (var property in typeof(AppConfig).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                if (!property.CanRead || property.SetMethod is null || !property.SetMethod.IsPublic || property.GetIndexParameters().Length != 0) continue;
                property.SetValue(host.Config, property.GetValue(imported));
            }
            host.ApplyConfig();
            host.SetBarsEnabled(host.Config.BarsEnabled);
            host.SetCategory(host.Config.ActiveCategory);
            host.ReloadNews();
            host.SaveConfig();
            LoadFromConfig();
            SetStatus(L("settings.imported"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or System.Reflection.TargetInvocationException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(FindForm(), L("settings.importFailed") + Environment.NewLine + ex.Message,
                L("settings.importTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string FeedSignature(IEnumerable<FeedCategory> cats) =>
        string.Join("|", cats.Select(c => c.Id + ":" + string.Join(",", c.Feeds.Select(f => f.Url + (f.Enabled ? "+" : "-")))));

    private void SetStatus(string text)
    {
        status.Text = text;
        var timer = new System.Windows.Forms.Timer { Interval = 4000 };
        timer.Tick += (_, _) =>
        {
            if (!status.IsDisposed) status.Text = "";
            timer.Dispose();
        };
        timer.Start();
    }

    // ---- bars / position handlers ---------------------------------------------------------

    private static string SplitNote() => Loc.T("pk.split.in.the.middle");

    /// <summary>Two drop-downs that say what a bar shows: the first part (or the whole bar) and an optional second part (the bar is then split in the middle).</summary>
    private sealed class SlotPicker
    {
        private readonly FlatCombo a = new() { Width = 380 };
        private readonly FlatCombo b = new() { Width = 380 };
        private List<BarSlot> choices = new();
        private BarSlots current = new(new BarSlot(SlotKind.News), null);
        private bool suppress;

        public SlotPicker()
        {
            a.SelectedIndexChanged += (_, _) => Read();
            b.SelectedIndexChanged += (_, _) => Read();
        }

        public BarSlots Get() => current;

        public void Set(BarSlots value)
        {
            current = value;
            Apply();
        }

        public void SetEnabled(bool on) => a.Enabled = b.Enabled = on;

        public Control Build()
        {
            var box = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            box.Controls.Add(Row(Loc.T("pk.content.or.left.half"), a, 260));
            box.Controls.Add(Row(Loc.T("pk.right.half.split.in"), b, 260));
            return box;
        }

        public void SetChoices(IEnumerable<FeedCategory> categories)
        {
            choices = new List<BarSlot> { new(SlotKind.News) };
            var labels = new List<string> { Loc.T("pk.news.active.category") };
            foreach (var c in categories)
            {
                choices.Add(new BarSlot(SlotKind.News, c.Id));
                labels.Add(Loc.T("pk.news") + c.DisplayName);
            }
            choices.Add(new BarSlot(SlotKind.Stocks));
            labels.Add(Loc.T("content.stocks"));
            choices.Add(new BarSlot(SlotKind.Weather));
            labels.Add(Loc.T("content.weather"));

            suppress = true;
            try
            {
                a.SetItems(labels, 0);
                b.SetItems(new[] { Loc.T("pk.none.the.whole.bar") }.Concat(labels), 0);
            }
            finally { suppress = false; }
            Apply();
        }

        private int IndexOf(BarSlot slot)
        {
            var i = choices.FindIndex(x => x.Kind == slot.Kind && (slot.Kind != SlotKind.News || string.Equals(x.Category ?? "", slot.Category ?? "", StringComparison.OrdinalIgnoreCase)));
            return Math.Max(0, i);
        }

        private void Apply()
        {
            if (choices.Count == 0) return;
            suppress = true;
            try
            {
                a.SelectSilently(IndexOf(current.A));
                b.SelectSilently(current.B is { } second ? IndexOf(second) + 1 : 0);
            }
            finally { suppress = false; }
        }

        private void Read()
        {
            if (suppress || choices.Count == 0 || a.SelectedIndex < 0) return;
            var first = choices[Math.Clamp(a.SelectedIndex, 0, choices.Count - 1)];
            BarSlot? second = b.SelectedIndex > 0 ? choices[Math.Clamp(b.SelectedIndex - 1, 0, choices.Count - 1)] : null;
            if (second is { } s && s == first) second = null;
            current = new BarSlots(first, second);
        }
    }

    private void UpdateBarsUi()
    {
        var integratedMain = position.SelectedIndex == (int)TickerPosition.TaskbarIntegrated;
        secondEnabled.Enabled = !integratedMain;
        secondPicker.SetEnabled(secondEnabled.Checked && !integratedMain);
        secondNote.Text = integratedMain ? L("note.secondBarBlocked") : L("note.secondBar");
    }

    /// <summary>The "default bar" choice of the Bars tab mirrors the position: above the taskbar, inside it, or something else.</summary>
    private void SyncDefaultBar()
    {
        if (defaultBar is null || position is null) return;
        var index = position.SelectedIndex switch
        {
            (int)TickerPosition.BottomTaskbar => 0,
            (int)TickerPosition.TaskbarIntegrated => 1,
            _ => 2
        };
        defaultBar.SelectSilently(index);
    }

    private void UpdatePositionGroups()
    {
        var p = (TickerPosition)Math.Max(0, position.SelectedIndex);
        customCard.Visible = p == TickerPosition.Custom;
        taskbarCard.Visible = p == TickerPosition.TaskbarIntegrated;
        reserve.Enabled = p is TickerPosition.BottomTaskbar or TickerPosition.Top;
        followTaskbar.Enabled = p == TickerPosition.BottomTaskbar;
        tbLeft.Enabled = tbRight.Enabled = !tbAuto.Checked;
        if (built) UpdateBarsUi();
    }

    private void ApplyCustomBoxes()
    {
        if (loading) return;
        host.SetCustomBounds(posX.Value, posY.Value, posW.Value);
        if (host.Config.Position == TickerPosition.Custom) host.Window.Reposition();
    }

    private void MoveToSelectedMonitor()
    {
        if (loading || monitor.SelectedIndex < 0 || monitor.SelectedIndex >= Screen.AllScreens.Length) return;
        var wa = Screen.AllScreens[monitor.SelectedIndex].WorkingArea;
        loading = true;
        posX.SetValueSilently(wa.Left + 100);
        posY.SetValueSilently(wa.Bottom - 42 - 40);
        posW.SetValueSilently(Math.Min(900, wa.Width - 200));
        loading = false;
        ApplyCustomBoxes();
    }

    private void ToggleMouseMove()
    {
        var on = !host.IsPositionEditMode;
        if (on)
        {
            // Make sure the strip is in Custom position right now, with the boxes' values.
            position.SelectSilently((int)TickerPosition.Custom);
            UpdatePositionGroups();
            host.Config.Position = TickerPosition.Custom;
            host.SetCustomBounds(posX.Value, posY.Value, posW.Value);
            host.SetPosition(TickerPosition.Custom);
        }
        host.SetPositionEditMode(on);
        moveWithMouse.Text = on ? L("btn.moveMouseDone") : L("btn.moveMouse");
    }

    private void OnCustomBoundsChanged()
    {
        if (!IsHandleCreated || IsDisposed) return;
        BeginInvoke(new Action(() =>
        {
            posX.SetValueSilently(host.Config.CustomX);
            posY.SetValueSilently(host.Config.CustomY);
            posW.SetValueSilently(host.Config.CustomWidth);
        }));
    }

    // ---- source handlers -------------------------------------------------------------------

    private FeedCategory? SelectedCategory() =>
        categoryList.SelectedIndex >= 0 && categoryList.SelectedIndex < workCategories.Count ? workCategories[categoryList.SelectedIndex] : null;

    private void ShowFeedsOfSelectedCategory()
    {
        feedList.ClearRows();
        if (SelectedCategory() is { } cat)
            foreach (var f in cat.Feeds) feedList.AddRow($"{f.Name}   —   {f.Url}", f.Enabled);
    }

    private void AddCategory()
    {
        var name = Prompt.Ask(this, L("prompt.newCategory"), L("prompt.name"), "");
        if (string.IsNullOrWhiteSpace(name)) return;
        var activeId = category.SelectedIndex >= 0 && category.SelectedIndex < workCategories.Count ? workCategories[category.SelectedIndex].Id : null;
        workCategories.Add(new FeedCategory { Id = "user-" + Guid.NewGuid().ToString("N")[..8], NameEl = name.Trim(), NameEn = name.Trim() });
        RebuildCategoryLists(activeId);
        categoryList.SelectedIndex = workCategories.Count - 1;
    }

    private void RenameCategory()
    {
        if (SelectedCategory() is not { } cat) return;
        var name = Prompt.Ask(this, L("prompt.rename"), L("prompt.name"), cat.DisplayName);
        if (string.IsNullOrWhiteSpace(name)) return;
        // Only the name of the language in use changes: the other language keeps its own name.
        if (Loc.IsGreek) cat.NameEl = name.Trim();
        else cat.NameEn = name.Trim();
        if (!cat.BuiltIn)
        {
            cat.NameEl = name.Trim();
            cat.NameEn = name.Trim();
        }
        var i = categoryList.SelectedIndex;
        var activeId = category.SelectedIndex >= 0 && category.SelectedIndex < workCategories.Count ? workCategories[category.SelectedIndex].Id : null;
        RebuildCategoryLists(activeId);
        categoryList.SelectedIndex = i;
    }

    private void DeleteCategory()
    {
        if (SelectedCategory() is not { } cat) return;
        if (cat.BuiltIn)
        {
            SetStatus(L("msg.builtinNoDelete"));
            return;
        }
        workCategories.Remove(cat);
        RebuildCategoryLists(workCategories.FirstOrDefault()?.Id);
    }

    private void RemoveFeed()
    {
        if (SelectedCategory() is not { } cat || feedList.SelectedIndex < 0 || feedList.SelectedIndex >= cat.Feeds.Count) return;
        cat.Feeds.RemoveAt(feedList.SelectedIndex);
        ShowFeedsOfSelectedCategory();
        MarkDirty();
    }

    private void MoveFeed(int step)
    {
        if (SelectedCategory() is not { } cat) return;
        var from = feedList.SelectedIndex;
        var to = from + step;
        if (from < 0 || from >= cat.Feeds.Count || to < 0 || to >= cat.Feeds.Count) return;
        (cat.Feeds[from], cat.Feeds[to]) = (cat.Feeds[to], cat.Feeds[from]);
        ShowFeedsOfSelectedCategory();
        feedList.SelectedIndex = to;
        MarkDirty();
    }

    /// <summary>Downloads the selected feed (or every feed of the category) and reports whether it works and how fresh it is.</summary>
    private async void TestFeeds(bool selectedOnly)
    {
        if (SelectedCategory() is not { } cat) return;
        var feeds = selectedOnly
            ? (feedList.SelectedIndex >= 0 && feedList.SelectedIndex < cat.Feeds.Count ? new List<FeedSource> { cat.Feeds[feedList.SelectedIndex] } : new List<FeedSource>())
            : cat.Feeds.ToList();
        if (feeds.Count == 0) return;
        discoverStatus.Text = Loc.T("pk.testing");
        try
        {
            using var provider = new RssNewsProvider();
            var results = await Task.WhenAll(feeds.Select(f => provider.TestAsync(f.Url))).ConfigureAwait(true);
            if (IsDisposed) return;
            if (feeds.Count == 1)
            {
                var r = results[0];
                discoverStatus.Text = r.Ok
                    ? Loc.T("feed.ok", r.Count, (r.Newest?.ToString("dd/MM HH:mm") ?? ""))
                    : Loc.T("feed.fail", r.Error ?? "");
                return;
            }
            var bad = feeds.Where((f, i) => !results[i].Ok).Select(f => f.Name).ToList();
            discoverStatus.Text = bad.Count == 0
                ? Loc.T("feed.allOk", feeds.Count)
                : Loc.T("feed.some", feeds.Count - bad.Count, feeds.Count, string.Join(", ", bad));
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
        {
            if (!IsDisposed) discoverStatus.Text = ex.Message;
        }
    }

    private void ImportOpml()
    {
        if (SelectedCategory() is not { } cat) return;
        using var dialog = new OpenFileDialog { Filter = "OPML files (*.opml;*.xml)|*.opml;*.xml|All files (*.*)|*.*", Title = Loc.T("pk.import.opml.subscriptions") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var input = File.OpenRead(dialog.FileName);
            var imported = OpmlExchange.Import(input);
            var before = cat.Feeds.Count;
            foreach (var feed in imported) AddFeed(cat, feed.Name, feed.Url);
            ShowFeedsOfSelectedCategory();
            discoverStatus.Text = Loc.T("opml.imported", cat.Feeds.Count - before, imported.Count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, Loc.T("pk.opml.import.failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportOpml()
    {
        if (SelectedCategory() is not { } cat) return;
        using var dialog = new SaveFileDialog { Filter = "OPML files (*.opml)|*.opml|XML files (*.xml)|*.xml", DefaultExt = "opml", AddExtension = true, FileName = $"{cat.Id}.opml", Title = Loc.T("pk.export.opml.subscriptions") };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var output = File.Create(dialog.FileName);
            OpmlExchange.Export(cat.Feeds, output, cat.DisplayName);
            discoverStatus.Text = Loc.T("pk.opml.export.completed");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(this, ex.Message, Loc.T("pk.opml.export.failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AddPreset()
    {
        if (SelectedCategory() is not { } cat || presets.SelectedIndex < 0) return;
        var p = AppConfig.PresetFeeds[presets.SelectedIndex];
        AddFeed(cat, p.Name, p.Url);
    }

    private async void AddFromSite()
    {
        if (SelectedCategory() is not { } cat) return;
        var text = siteBox.Value.Trim();
        if (text.Length == 0) return;
        discoverStatus.Text = L("msg.searching");
        siteBox.Enabled = false;
        try
        {
            var found = await FeedDiscovery.DiscoverAsync(text);
            if (IsDisposed) return;
            if (found.Count == 0)
            {
                discoverStatus.Text = L("msg.noRss");
                return;
            }
            foreach (var f in found.Take(3)) AddFeed(cat, f.Name, f.Url);
            discoverStatus.Text = Loc.T("msg.added", Math.Min(3, found.Count));
            siteBox.Value = "";
        }
        finally
        {
            if (!IsDisposed) siteBox.Enabled = true;
        }
    }

    private void AddFeed(FeedCategory cat, string name, string url)
    {
        if (cat.Feeds.Any(f => string.Equals(f.Url, url, StringComparison.OrdinalIgnoreCase)))
        {
            discoverStatus.Text = L("msg.exists");
            return;
        }
        cat.Feeds.Add(new FeedSource(string.IsNullOrWhiteSpace(name) ? new Uri(url).Host : name, url));
        ShowFeedsOfSelectedCategory();
    }

}

/// <summary>Tiny modal "enter a text" dialog in the flat style.</summary>
internal static class Prompt
{
    public static string? Ask(Control owner, string title, string label, string initial)
    {
        using var form = new Form
        {
            Text = title, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false, MaximizeBox = false, ClientSize = new Size(420, 130), ShowInTaskbar = false,
            Font = new Font("Segoe UI", 9.5f)
        };
        var l = new Label { Text = label, AutoSize = true, Left = 16, Top = 14 };
        var box = new FlatText { Left = 16, Top = 40, Width = 388, Value = initial };
        var ok = new FlatButton { Text = Loc.T("btn.ok"), Left = 16, Top = 86, DialogResult = DialogResult.OK };
        ok.SetPrimary(true);
        var cancel = new FlatButton { Text = Loc.T("btn.cancel"), Left = 110, Top = 86, DialogResult = DialogResult.Cancel };
        form.Controls.AddRange(new Control[] { l, box, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        form.BackColor = Theme.Background;
        form.ForeColor = Theme.Text;
        Theme.Apply(form);
        form.HandleCreated += (_, _) => Theme.ApplyTitleBar(form);
        form.Shown += (_, _) => box.FocusInner();
        return form.ShowDialog(owner.FindForm()) == DialogResult.OK ? box.Value : null;
    }
}
