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

    private FlatTabs tabs = null!;
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
    private FlatCheck icons = null!;
    private FlatCheck preview = null!;
    private FlatCheck pauseOther = null!;
    private FlatCheck hideRead = null!;

    // Bars
    private FlatCombo defaultBar = null!;
    private FlatCombo mainContent = null!;
    private FlatCheck stocksLeft = null!;
    private FlatCheck secondEnabled = null!;
    private FlatCombo secondContent = null!;
    private readonly List<(Screen Screen, FlatCheck Check, FlatCombo Content)> monitorRows = new();
    private FlatCheck autoUpdate = null!;
    private FlatText citySearch = null!;
    private FlatList cityList = null!;
    private FlatCheck miniWeather = null!;
    private FlatNumber miniX = null!, miniY = null!, miniW = null!, miniH = null!;
    private FlatButton miniMove = null!;
    private FlatCheck miniOnTop = null!;
    private List<string> workCities = new();
    private List<GreekCity> shownCities = new();
    private FlatCheck barsEnabled = null!;
    private Label updateStatus = null!;
    private FlatButton installUpdate = null!;
    private FlatButton openUpdatePage = null!;
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

    // Markets
    private List<QuoteInstrument> workWatch = new();
    private readonly Dictionary<QuoteSource, FlatList> watchLists = new();
    private FlatCombo marketCombo = null!;
    private FlatText stockSearch = null!;
    private FlatList stockList = null!;
    private FlatButton refreshCatalog = null!;
    private Label stockStatus = null!;
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

    private static BarContent SecondContentOf(int index) => index switch { 0 => BarContent.News, 2 => BarContent.Weather, _ => BarContent.Stocks };

    // ---- preview of the bars ----------------------------------------------------------------

    /// <summary>Tabs whose choices change how a bar looks: Bars, Position, Look, Breaking.</summary>
    private static readonly int[] PreviewTabs = { 1, 2, 5, 6 };

    private void UpdatePreview()
    {
        if (!Visible || !built || barPreview is null || barPreview.IsDisposed || tabs is null) return;
        var show = PreviewTabs.Contains(tabs.SelectedIndex);
        if (barPreview.Visible != show) barPreview.Visible = show;
        if (!show) return;
        var dark = themeMode.SelectedIndex switch { 1 => false, 2 => true, _ => Theme.IsDark };
        barPreview.SetState(new BarPreviewState(
            (TickerPosition)Math.Max(0, position.SelectedIndex), (BarContent)Math.Max(0, mainContent.SelectedIndex), stocksLeft.Checked,
            secondEnabled.Checked, SecondContentOf(secondContent.SelectedIndex),
            dark, opacity.Value, edgeFade.Checked,
            barColor.SelectedIndex switch { 1 => "lightblue", 2 => "accent", 3 => "custom", _ => "theme" }, barSwatch.Rgb,
            taskbarColor.SelectedIndex switch { 1 => "lightblue", 2 => "accent", 3 => "custom", _ => "taskbar" }, taskbarSwatch.Rgb,
            barBackdrop.SelectedIndex switch { 1 => "blur", 2 => "gradient", _ => "acrylic" },
            taskbarBackdrop.SelectedIndex switch { 0 => "acrylic", 2 => "gradient", _ => "blur" }));
    }

    // ---- building ------------------------------------------------------------------------

    private void Rebuild()
    {
        RightToLeft = Loc.IsRtl ? RightToLeft.Yes : RightToLeft.No;
        var selectedTab = built ? tabs.SelectedIndex : 0;
        SuspendLayout();
        foreach (var c in Controls.Cast<Control>().ToList()) c.Dispose();
        Controls.Clear();
        Build();
        tabs.SelectedIndex = Math.Max(0, selectedTab);
        ResumeLayout(true);
    }

    private void Build()
    {
        loading = true;
        watchLists.Clear();
        tabs = new FlatTabs { Dock = DockStyle.Fill };

        tabs.AddPage(L("tab.general"), BuildGeneral());
        tabs.AddPage(L("tab.bars"), BuildBars());
        tabs.AddPage(L("tab.position"), BuildPosition());
        tabs.AddPage(L("tab.sources"), BuildSources());
        tabs.AddPage(L("tab.markets"), BuildMarkets());
        tabs.AddPage(L("tab.look"), BuildLook());
        tabs.AddPage(L("tab.breaking"), BuildBreaking());
        tabs.AddPage(L("tab.readLater"), BuildReadLater());
        tabs.AddPage(L("tab.weather"), BuildWeather());
        if (showAboutTabs)
        {
            tabs.AddPage(L("tab.help"), BuildText(AboutTexts.Help));
            tabs.AddPage(L("tab.instructions"), BuildText(AboutTexts.Instructions));
            tabs.AddPage(L("tab.history"), BuildText(AboutTexts.History));
            tabs.AddPage(L("tab.license"), BuildText(AboutTexts.License));
        }

        var apply = new FlatButton { Text = L("btn.apply") };
        apply.SetPrimary(true);
        apply.Click += (_, _) => ApplySettings();
        status = new Label { AutoSize = true, Padding = new Padding(8, 11, 0, 0), Tag = "muted" };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(12, 8, 12, 10) };
        bar.Controls.Add(apply);
        if (showCloseButton)
        {
            var close = new FlatButton { Text = L("btn.close") };
            close.Click += (_, _) => CloseRequested?.Invoke();
            bar.Controls.Add(close);
        }
        bar.Controls.Add(status);

        barPreview = new BarPreview { Dock = DockStyle.Bottom, Visible = false };
        Controls.Add(tabs);
        Controls.Add(barPreview);
        Controls.Add(bar);
        built = true;

        LoadFromConfig();
        Theme.Apply(this);
        loading = false;
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

        // Updates: only on request or once a day when the user allows it; nothing is installed without a click.
        var updates = new FlatCard(L("card.updates"));
        var updFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        autoUpdate = new FlatCheck { Text = L("chk.autoUpdate") };
        updFlow.Controls.Add(autoUpdate);
        var checkNow = new FlatButton { Text = L("btn.checkNow") };
        installUpdate = new FlatButton { Text = L("btn.installUpdate"), Visible = false };
        openUpdatePage = new FlatButton { Text = L("btn.openPage"), Visible = false };
        var updButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
        updButtons.Controls.Add(checkNow);
        updButtons.Controls.Add(installUpdate);
        updButtons.Controls.Add(openUpdatePage);
        updFlow.Controls.Add(updButtons);
        updateStatus = new Label { AutoSize = true, Tag = "muted", MaximumSize = new Size(560, 0), Margin = new Padding(0, 6, 0, 0), Text = " " };
        updFlow.Controls.Add(updateStatus);
        updFlow.Controls.Add(Note(L("note.updates"), 560));
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

        var main = new FlatCard(L("card.mainBar"));
        var mainFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        mainContent = new FlatCombo { Width = 340 };
        mainContent.SetItems(new[] { L("content.news"), L("content.stocks"), L("content.both"), L("content.weather") }, 0);
        mainFlow.Controls.Add(Row(L("lbl.content"), mainContent, 200));
        stocksLeft = new FlatCheck { Text = L("chk.stocksLeft") };
        mainFlow.Controls.Add(stocksLeft);
        mainFlow.Controls.Add(Note(L("note.split"), 560));
        main.Controls.Add(mainFlow);
        flow.Controls.Add(main);

        var second = new FlatCard(L("card.secondBar"));
        var secondFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        secondEnabled = new FlatCheck { Text = L("chk.secondEnabled") };
        secondFlow.Controls.Add(secondEnabled);
        secondContent = new FlatCombo { Width = 340 };
        secondContent.SetItems(new[] { L("content.news"), L("content.stocks"), L("content.weather") }, 1);
        secondFlow.Controls.Add(Row(L("lbl.content"), secondContent, 200));
        secondNote = Note(L("note.secondBar"), 560);
        secondFlow.Controls.Add(secondNote);
        second.Controls.Add(secondFlow);
        flow.Controls.Add(second);

        // One bar on every other monitor, each with its own content.
        var monitors = new FlatCard(L("card.monitors"));
        var monitorFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        monitorRows.Clear();
        var others = Screen.AllScreens.Where(s => !s.Primary).ToList();
        for (var i = 0; i < others.Count; i++)
        {
            var s = others[i];
            var check = new FlatCheck { Text = Loc.T("fmt.monitor", Array.IndexOf(Screen.AllScreens, s) + 1, s.Bounds.Width, s.Bounds.Height) };
            var content = new FlatCombo { Width = 340 };
            content.SetItems(new[] { L("content.news"), L("content.stocks"), L("content.both"), L("content.weather") }, 0);
            monitorFlow.Controls.Add(check);
            monitorFlow.Controls.Add(Row(L("lbl.content"), content, 200));
            monitorRows.Add((s, check, content));
        }
        monitorFlow.Controls.Add(Note(others.Count > 0 ? L("note.monitors") : L("note.noOtherMonitor"), 560));
        monitors.Controls.Add(monitorFlow);
        flow.Controls.Add(monitors);

        mainContent.SelectedIndexChanged += (_, _) => UpdateBarsUi();
        secondEnabled.CheckedChanged += (_, _) => UpdateBarsUi();
        return page;
    }

    private Control BuildPosition()
    {
        var page = NewPage(out var flow);

        position = new FlatCombo { Width = 470 };
        position.SetItems(new[] { L("pos.bottom"), L("pos.top"), L("pos.center"), L("pos.custom"), L("pos.taskbar") }, 0);
        flow.Controls.Add(Row(L("lbl.position"), position, 200));
        followTaskbar = new FlatCheck { Text = L("chk.followTaskbar") };
        reserve = new FlatCheck { Text = L("chk.reserve") };
        flow.Controls.Add(followTaskbar);
        flow.Controls.Add(reserve);

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
        var removeFeed = new FlatButton { Text = L("btn.removeFeed") };
        removeFeed.Click += (_, _) => RemoveFeed();
        right.Controls.Add(removeFeed);

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
        cityList = new FlatList { Width = 680, Height = 300 };
        cityList.SetShowChecks(true);
        inner.Controls.Add(cityList);
        card.Controls.Add(inner);
        flow.Controls.Add(card);

        var mini = new FlatCard(L("card.miniWeather"));
        var miniFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        miniWeather = new FlatCheck { Text = L("chk.miniWeather") };
        miniFlow.Controls.Add(miniWeather);
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
        miniFlow.Controls.Add(Note(L("note.miniWeather2"), 560));
        mini.Controls.Add(miniFlow);
        flow.Controls.Add(mini);

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
        installUpdate.Visible = u is not null && !host.HostedByOpsis && u.SetupUrl is not null && u.ChecksumUrl is not null;
        openUpdatePage.Visible = u is not null;
        if (u is not null) updateStatus.Text = Loc.T("msg.updateAvailable", u.Version, AboutTexts.Version);
    }

    private async Task CheckUpdatesNowAsync(FlatButton button)
    {
        button.Enabled = false;
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
            if (!IsDisposed) updateStatus.Text = L("msg.updateFailed");
        }
        finally
        {
            if (!button.IsDisposed) button.Enabled = true;
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (host.AvailableUpdate is not { } u) return;
        installUpdate.Enabled = false;
        updateStatus.Text = L("msg.downloading");
        var result = await UpdateInstaller.DownloadAndStartAsync(u);
        if (IsDisposed) return;
        installUpdate.Enabled = true;
        updateStatus.Text = result switch
        {
            UpdateInstaller.Result.Started => L("msg.updateStarted"),
            UpdateInstaller.Result.ChecksumMismatch or UpdateInstaller.Result.ChecksumMissing => L("msg.verifyFailed"),
            _ => L("msg.updateFailed")
        };
    }

    // ---- read later ------------------------------------------------------------------------

    private Control BuildReadLater()
    {
        var page = NewPage(out var flow);
        flow.Controls.Add(Note(L("note.readLater"), 700));
        var card = new FlatCard(L("tab.readLater"));
        var inner = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        laterList = new FlatList { Width = 800, Height = 320 };
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
        flow.Controls.Add(Note(L("note.markets"), 700));

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
        stockList = new FlatList { Width = 680, Height = 340 };
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

        foreach (var (source, titleKey) in new[] { (QuoteSource.Metals, "card.metals"), (QuoteSource.Crypto, "card.crypto"), (QuoteSource.Currency, "card.currency"), (QuoteSource.Energy, "card.energy") })
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
        return page;
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
        keywords = new FlatText { Width = 520 };
        flow.Controls.Add(Row(L("lbl.keywords"), keywords, 330));
        maxAge = new FlatNumber();
        maxAge.Configure(10, 1440, 10);
        flow.Controls.Add(Row(L("lbl.maxAge"), maxAge, 330));
        duration = new FlatNumber();
        duration.Configure(1, 240, 5);
        flow.Controls.Add(Row(L("lbl.duration"), duration, 330));
        flow.Controls.Add(Note(L("note.breaking"), 700));

        var test = new FlatButton { Text = L("btn.testBreaking") };
        test.Click += (_, _) => host.SimulateBreaking(Loc.IsGreek ? "Έκτακτο: δοκιμαστική είδηση για τον έλεγχο της μπάρας" : "Breaking: test headline to check the bar");
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

    private static Control BuildText(string text)
    {
        var page = new Panel { Padding = new Padding(8, 6, 8, 6) };
        var frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 8, 12), Tag = "surface" };
        frame.Controls.Add(new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
            Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n"), Font = new Font("Segoe UI", 10f), TabStop = false, Tag = "surface"
        });
        page.Controls.Add(frame);
        return page;
    }

    // ---- loading / saving ----------------------------------------------------------------

    /// <summary>Shows the tab with the given index (0 = General).</summary>
    public int TabCount => tabs.Count;

    public void SelectTab(int index)
    {
        if (index >= 0 && index < tabs.Count) tabs.SelectedIndex = index;
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
            autoUpdate.Checked = c.CheckForUpdates;
            workCities = c.WeatherCities.ToList();
            ShowCities();
            miniWeather.Checked = c.MiniWeatherEnabled;
            LoadMiniBounds();
            miniOnTop.Checked = c.MiniWeatherOnTop;

            mainContent.SelectSilently((int)c.MainContent);
            stocksLeft.Checked = c.SplitStocksOnLeft;
            secondEnabled.Checked = c.SecondBarEnabled;
            secondContent.SelectSilently(c.SecondContent switch { BarContent.News => 0, BarContent.Weather => 2, _ => 1 });
            foreach (var (screen, check, content) in monitorRows)
            {
                var saved = c.MonitorBars.FirstOrDefault(m => string.Equals(m.Device, screen.DeviceName, StringComparison.OrdinalIgnoreCase));
                check.Checked = saved?.Enabled ?? false;
                content.SelectSilently(saved is null ? 0 : (int)saved.Content);
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
            keywords.Value = string.Join("; ", c.BreakingKeywords);
            maxAge.SetValueSilently(c.BreakingMaxAgeMinutes);
            duration.SetValueSilently(c.BreakingDurationMinutes);
            workLive = c.LiveFeeds.Select(f => new LiveFeed(f.Name, f.Url) { Enabled = f.Enabled }).ToList();
            RebuildLiveList();
            themeMode.Enabled = !HostThemeLocked;
            themeNote.Text = HostThemeLocked ? L("note.hostTheme") : "";

            workCategories = c.Categories.Select(Clone).ToList();
            RebuildCategoryLists(c.ActiveCategory);
            workWatch = c.Watchlist.Select(w => new QuoteInstrument(w.Source, w.Code, w.Symbol, w.NameEl, w.NameEn, w.Unit, w.Enabled)).ToList();
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
    }

    private static FeedCategory Clone(FeedCategory c) => new()
    {
        Id = c.Id, NameEl = c.NameEl, NameEn = c.NameEn, BuiltIn = c.BuiltIn,
        Feeds = c.Feeds.Select(f => new FeedSource(f.Name, f.Url) { Enabled = f.Enabled }).ToList()
    };

    private void RebuildCategoryLists(string? activeId)
    {
        category.SetItems(workCategories.Select(c => c.DisplayName), 0);
        categoryList.ClearRows();
        foreach (var cat in workCategories) categoryList.AddRow(cat.DisplayName);
        var index = Math.Max(0, workCategories.FindIndex(c => string.Equals(c.Id, activeId, StringComparison.OrdinalIgnoreCase)));
        category.SelectSilently(index);
        if (categoryList.Items.Count > 0) categoryList.SelectedIndex = index;
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
        c.Language = Loc.Available().ElementAtOrDefault(Math.Max(0, language.SelectedIndex)).Code ?? c.Language;
        c.NewsRefreshSeconds = refresh.Value;
        c.ScrollSpeed = speed.Value;
        c.PauseOnHover = hoverPause.Checked;
        c.ShowIcons = icons.Checked;
        c.ShowPreview = preview.Checked;
        c.PauseOnOtherWindow = pauseOther.Checked;
        c.HideRead = hideRead.Checked;
        c.CheckForUpdates = autoUpdate.Checked;
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

        c.MainContent = (BarContent)Math.Max(0, mainContent.SelectedIndex);
        c.SplitStocksOnLeft = stocksLeft.Checked;
        c.SecondBarEnabled = secondEnabled.Checked && position.SelectedIndex != (int)TickerPosition.TaskbarIntegrated;
        c.SecondContent = SecondContentOf(secondContent.SelectedIndex);
        // Extra bars of the monitors shown here; entries of monitors that are not connected right now are kept.
        var shown = monitorRows.Select(r => r.Screen.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        c.MonitorBars = c.MonitorBars.Where(m => !shown.Contains(m.Device)).Concat(monitorRows.Select(r =>
            new MonitorBar { Device = r.Screen.DeviceName, Enabled = r.Check.Checked, Content = (BarContent)Math.Max(0, r.Content.SelectedIndex) })).ToList();

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
        c.Watchlist = workWatch.Select(w => new QuoteInstrument(w.Source, w.Code, w.Symbol, w.NameEl, w.NameEn, w.Unit, w.Enabled)).ToList();

        host.ApplyConfig();
        host.SetBarsEnabled(barsEnabled.Checked);
        host.SetCategory(chosen);
        if (feedsChanged && string.Equals(chosen, previousActive, StringComparison.OrdinalIgnoreCase)) host.ReloadNews();
        host.SaveConfig();

        SetStatus(L("msg.saved"));
        Applied?.Invoke();
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

    private void UpdateBarsUi()
    {
        stocksLeft.Enabled = mainContent.SelectedIndex == (int)BarContent.Both;
        var integratedMain = position.SelectedIndex == (int)TickerPosition.TaskbarIntegrated;
        secondEnabled.Enabled = !integratedMain;
        secondContent.Enabled = secondEnabled.Checked && !integratedMain;
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
