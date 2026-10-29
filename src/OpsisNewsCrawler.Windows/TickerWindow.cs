using OpsisNewsCrawler.Core;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;

namespace OpsisNewsCrawler.Windows;

public sealed class TickerWindow : Form
{
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private int TickerHeight { get; set; } = 42;

    /// <summary>The height of the strip in pixels (42 normally); text and icons grow and shrink with it.</summary>
    public void SetBarHeight(int height)
    {
        height = Math.Clamp(height, 24, 200);
        if (height == TickerHeight) return;
        TickerHeight = height;
        var scale = height / 42f;
        canvas.SetFontScale(scale);
        canvasB.SetFontScale(scale);
        Height = height;
        if (position == TickerPosition.Custom)
        {
            MinimumSize = editMode ? new Size(200, height) : Size.Empty;
            MaximumSize = editMode ? new Size(0, height) : Size.Empty;
        }
        Reposition();
    }

    public void SetCenterWhenStatic(bool value)
    {
        canvas.SetCenterWhenStatic(value);
        canvasB.SetCenterWhenStatic(value);
    }

    private bool desktopLayer;
    private Rectangle lastLoggedIntegrated;

    /// <summary>True: the bar lies on the desktop, below all other windows. False (normal): always on top.</summary>
    public void SetDesktopLayer(bool value)
    {
        desktopLayer = value;
        TopMost = !value;
        if (value && IsHandleCreated) Native.SendToBottom(Handle);
    }
    private const int HoverPreviewDelayMs = 400;
    private const double SpeedWheelStep = 10;

    private readonly TickerCanvas canvas = new();
    private readonly TickerCanvas canvasB = new() { Visible = false };
    private bool split;
    private readonly Stopwatch alertClock = new();
    private bool breaking;
    private static readonly Color BreakingRed = Color.FromArgb(255, 188, 22, 28);
    // Low-end PCs (Opsis hardware tier "Low"): scroll at ~30 fps instead of ~66 fps and poll the taskbar every ~90 ms instead
    // of 30 ms. The scroll is time based (see ScrollTicker), so the speed the user sees does not change.
    private static bool lowPower;
    private static event Action? PowerChanged;
    public static bool LowPower
    {
        get => lowPower;
        set { if (lowPower == value) return; lowPower = value; PowerChanged?.Invoke(); }
    }
    private void ApplyPowerProfile()
    {
        scrollTimer.Interval = lowPower ? 33 : 15;
        taskbarTimer.Interval = lowPower ? 90 : 30;
    }

    private readonly System.Windows.Forms.Timer scrollTimer = new() { Interval = 15 };
    private readonly System.Windows.Forms.Timer focusTimer = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer hoverTimer = new() { Interval = HoverPreviewDelayMs };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly TickerState state;
    private readonly Func<IntPtr, bool> hostWindowDetector;
    private ArticlePreviewForm? preview;
    private NewsItem? pendingHoverItem;
    private Rectangle pendingHoverRect;
    private long lastTickMs;
    private bool pausedByForeground;
    private bool manuallyPaused;
    private bool running;
    private double scrollSpeed = 60;
    private TickerPosition position = TickerPosition.BottomTaskbar;
    private readonly System.Windows.Forms.Timer taskbarTimer = new() { Interval = 30 };
    private Rectangle customBounds;
    private bool editMode;
    private bool appBarRegistered;
    private bool autoHide;
    private int taskbarTick;
    private bool hiddenByTaskbar;      // the taskbar auto-hides and is hidden right now: a bar inside it hides with it
    /// <summary>True while this bar is hidden because its (auto-hiding) taskbar is hidden.</summary>
    public bool HiddenByTaskbar => hiddenByTaskbar;
    /// <summary>Raised when <see cref="HiddenByTaskbar"/> changes.</summary>
    public event Action? HiddenByTaskbarChanged;
    private bool hiddenByFullscreen;   // another app is fullscreen
    private int animToY = int.MinValue, animFromY;
    private long animStartMs;
    private string themeModeName = "system";
    private string colorMode = "theme";
    private string backdropStyle = "acrylic";
    private Color customColor = Color.FromArgb(0x4A, 0x90, 0xD9);

    public event Action? RefreshRequested;

    /// <summary>The user finished dragging/resizing the strip in edit mode (new bounds in screen coordinates).</summary>
    public event Action<Rectangle>? PlacementEdited;

    /// <summary>The user pressed the pulsing live button (breaking news with a live source).</summary>
    public event Action? LiveRequested;

    /// <summary>The user clicked an article (it was opened in the browser).</summary>
    public event Action<NewsItem>? ItemOpened;

    /// <summary>A city of the weather line was clicked: the item and the bounds of this bar.</summary>
    public event Action<NewsItem, Rectangle>? WeatherRequested;

    /// <summary>The user Ctrl+clicked an article: save it to the read-later list.</summary>
    public event Action<NewsItem>? ItemSaveRequested;

    /// <summary>Mark an article as read from its context menu.</summary>
    public event Action<NewsItem>? ItemMarkReadRequested;

    /// <summary>Shows a short message on the bar.</summary>
    public void ShowToast(string text)
    {
        canvas.ShowToast(text);
        if (canvasB.Visible) canvasB.ShowToast(text);
        var t = new System.Windows.Forms.Timer { Interval = 2100 };
        t.Tick += (_, _) => { t.Dispose(); if (!IsDisposed) { canvas.Invalidate(); canvasB.Invalidate(); } };
        t.Start();
    }

    /// <summary>The user right-clicked the strip and wants the settings.</summary>
    public event Action? SettingsRequested;

    /// <summary>Raised when the user changes the speed with the mouse wheel (pixels per second).</summary>
    public event Action<double>? ScrollSpeedChanged;

    /// <summary>The wheel was turned over the second half of a split bar (pixels per second).</summary>
    public event Action<double>? ScrollSpeedBChanged;

    /// <summary>Pause scrolling while another application's window is in the foreground.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PauseOnForeground { get; set; } = true;

    /// <summary>Sit above the taskbar (true) or at the very bottom of the screen (false).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool FollowTaskbar { get; set; } = true;

    /// <summary>Stop scrolling while the mouse is over the ticker.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PauseOnHover { get; set; } = true;

    /// <summary>Show the article card when the mouse rests on a news item.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowPreview { get; set; } = true;

    /// <summary>Show the source icon in front of every news item.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowIcons
    {
        get => showIcons;
        set
        {
            showIcons = value;
            canvas.SetShowIcons(value);
            canvasB.SetShowIcons(value);
        }
    }

    /// <summary>Scroll speed in pixels per second.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    private double scrollSpeedB;

    /// <summary>Speed of the second half of a split bar; 0 = the same as <see cref="ScrollSpeed"/>.</summary>
    public double ScrollSpeedB
    {
        get => scrollSpeedB;
        set => scrollSpeedB = value <= 0 ? 0 : Math.Clamp(value, AppConfig.MinScrollSpeed, AppConfig.MaxScrollSpeed);
    }

    public double ScrollSpeed
    {
        get => scrollSpeed;
        set => scrollSpeed = Math.Clamp(value, AppConfig.MinScrollSpeed, AppConfig.MaxScrollSpeed);
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsPausedByForeground => pausedByForeground;

    /// <summary>Bottom/Top only: reserve the strip so maximized windows stop at its edge (Windows AppBar).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ReserveSpace { get; set; }

    /// <summary>Taskbar-integrated mode: distance of the strip from the left end of the taskbar.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TaskbarLeftOffset { get; set; } = 420;

    /// <summary>Taskbar-integrated mode: distance of the strip from the right end of the taskbar.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TaskbarRightOffset { get; set; } = 330;

    /// <summary>Fade the text out at both ends of the strip.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool EdgeFade
    {
        get => edgeFade;
        set
        {
            edgeFade = value;
            ApplyEdgeFadeToCanvases();
        }
    }

    private bool edgeFade;
    private string shape = "rect";

    // A pill has round ends: the text always fades out there so nothing is cut off by the curve.
    private void ApplyEdgeFadeToCanvases()
    {
        var fade = edgeFade || shape == "pill";
        canvas.SetEdgeFade(fade);
        canvasB.SetEdgeFade(fade);
    }

    /// <summary>The desktop bar: same glass as the Opsis DeskStrip (blur behind + a dark 40 % tint) instead of the theme material.</summary>
    public bool MatchDeskStrip { get; set; }

    /// <summary>The outline of the bar: "rect", "rounded" or "pill" (the window is clipped to the shape).</summary>
    public void SetShape(string value)
    {
        var changed = shape != (value is "rounded" or "pill" ? value : "rect");
        shape = value is "rounded" or "pill" ? value : "rect";
        ApplyEdgeFadeToCanvases();
        ApplyShapeRegion();
        if (changed && IsHandleCreated && Visible) Reposition();   // the margin of a full-width bar changes with the shape
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private void ApplyShapeRegion()
    {
        // "rounded": the corners are cut by Windows itself (DWM, anti-aliased, 8 px) - a GDI Region clips the backdrop with jagged edges.
        // "pill": a Region (DWM has no capsule), the border is drawn anti-aliased by the canvas.
        var outline = shape == "rect" || split ? 0 : shape == "pill" ? Height / 2 : Math.Min(8, Height / 2);
        if (canvas.OutlineRadius != outline) { canvas.OutlineRadius = outline; canvas.Invalidate(); }
        if (!IsHandleCreated) return;
        var pref = shape == "rounded" && !split ? 2 /*DWMWCP_ROUND*/ : 1 /*DWMWCP_DONOTROUND*/;
        try { DwmSetWindowAttribute(Handle, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, ref pref, sizeof(int)); } catch (Exception) { }
        var old = Region;
        if (shape != "pill" || split || Width < 20 || Height < 10)
        {
            Region = null;
        }
        else
        {
            var radius = shape == "pill" ? Height / 2 : Math.Min(14, Height / 2);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            var d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(Width - d, 0, d, d, 270, 90);
            path.AddArc(Width - d, Height - d, d, d, 0, 90);
            path.AddArc(0, Height - d, d, d, 90, 90);
            path.CloseFigure();
            Region = new Region(path);
        }
        old?.Dispose();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (shape == "pill") ApplyShapeRegion();
    }

    /// <summary>Taskbar-integrated mode: find the empty part of the taskbar by itself instead of using the offsets.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool TaskbarAutoDetect { get; set; } = true;

    private (int Left, int Right)? measuredSpan;
    private bool measuring;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsEditMode => editMode;

    private bool showIcons = true;

    public TickerWindow(TickerState state, Func<IntPtr, bool>? hostWindowDetector = null)
    {
        this.state = state;
        this.hostWindowDetector = hostWindowDetector ?? (_ => false);

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Height = TickerHeight;
        Text = "Opsis News Crawler";
        Icon = AppIcon.Get();
        Controls.Add(canvas);
        Controls.Add(canvasB);
        Resize += (_, _) => LayoutCanvases();

        canvas.LiveClicked += () => LiveRequested?.Invoke();
        foreach (var c in new[] { canvas, canvasB })
        {
            c.HoverChanged += OnHoverChanged;
            c.WeatherClicked += item => WeatherRequested?.Invoke(item, Bounds);
            c.ItemClicked += item =>
            {
                if ((ModifierKeys & Keys.Control) != 0 && item.Kind == NewsItemKind.News)
                {
                    ItemSaveRequested?.Invoke(item); // Ctrl+click: keep it for later instead of opening it
                    return;
                }
                OpenUrl(item.Url);
                ItemOpened?.Invoke(item);
            };
            c.MouseWheel += OnCanvasMouseWheel;
            c.ContextClicked += item =>
            {
                HidePreview();
                if (item is null || string.IsNullOrWhiteSpace(item.Url))
                {
                    SettingsRequested?.Invoke();
                    return;
                }
                ShowArticleMenu(c, item);
            };
        }
        LayoutCanvases();

        scrollTimer.Tick += (_, _) => ScrollTicker();
        PowerChanged += ApplyPowerProfile;
        ApplyPowerProfile();
        focusTimer.Tick += (_, _) =>
        {
            CheckForeground();
            if (desktopLayer && !editMode && Visible) Native.SendToBottom(Handle);
        };
        hoverTimer.Tick += (_, _) => ShowPendingPreview();
        taskbarTimer.Tick += (_, _) => FollowTaskbarTick();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Theme.Changed += ApplyLook;
        ApplyLook();
    }

    private void ShowArticleMenu(Control owner, NewsItem item)
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add(Loc.T("menu.openArticle"), null, (_, _) =>
        {
            OpenUrl(item.Url);
            ItemOpened?.Invoke(item);
        });
        if (item.Kind == NewsItemKind.News)
        {
            menu.Items.Add(Loc.T("menu.readLater"), null, (_, _) => ItemSaveRequested?.Invoke(item));
            menu.Items.Add(Loc.T("menu.markRead"), null, (_, _) => ItemMarkReadRequested?.Invoke(item));
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.T("menu.copyLink"), null, (_, _) =>
        {
            try { Clipboard.SetText(item.Url); ShowToast(Loc.T("menu.linkCopied")); }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or ThreadStateException) { }
        });
        menu.Items.Add(Loc.T("menu.settings"), null, (_, _) => SettingsRequested?.Invoke());
        // Not disposed inside Closed: the menu is still handling the click that closed it (that caused "Cannot access a disposed object").
        menu.Closed += (_, _) =>
        {
            if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(menu.Dispose));
        };
        menu.Show(owner, owner.PointToClient(Cursor.Position));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        ApplyShapeRegion();   // the shape was chosen before the window existed
        base.OnHandleCreated(e);
        Native.MarkNoSnap(Handle); // DeskZones must not snap the strip while the user moves it
        ApplyLook();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        Native.ClearNoSnap(Handle);
        base.OnHandleDestroyed(e);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // The user changed the Windows theme / transparency / accent colour: follow it (raised on another thread).
        if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(() =>
        {
            Theme.SetMode(themeModeName, Theme.Opacity);
            ApplyLook();
        }));
    }

    /// <summary>
    /// Colour of this bar: "theme" / "taskbar" (the defaults: follow the theme, or be clear inside the taskbar),
    /// "lightblue", "accent" (the Windows accent colour, followed live) or "custom".
    /// </summary>
    public void SetBarColor(string mode, int rgb)
    {
        colorMode = mode;
        customColor = Color.FromArgb(255, Color.FromArgb(rgb));
        ApplyLook();
    }

    /// <summary>Background material preset: "acrylic" (state 4), "blur" (state 3) or "gradient" (state 2).</summary>
    public void SetBackdropStyle(string style)
    {
        backdropStyle = style;
        ApplyLook();
    }

    /// <summary>The tint chosen by the user for this bar, or null for the default look of the position.</summary>
    private Color? ResolveTint() => colorMode switch
    {
        "lightblue" => Color.FromArgb(176, 196, 214),
        "accent" => TaskbarStyle.AccentColor(),
        "custom" => customColor,
        _ => null
    };

    private void SetDarkTextLook()
    {
        var text = Color.FromArgb(24, 26, 30);
        var source = Color.FromArgb(0, 70, 140);
        var hover = Color.FromArgb(34, 0, 0, 0);
        canvas.SetLook(text, source, hover, text, false);
        canvasB.SetLook(text, source, hover, text, false);
    }

    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    /// <summary>Remember which theme mode the host configured, so Windows theme changes can be followed.</summary>
    public void SetThemeModeName(string mode) => themeModeName = mode;

    /// <summary>
    /// Colours, transparency and backdrop of the strip. On the taskbar it copies the taskbar's own material
    /// (light/dark, acrylic, accent) so the two look like one surface; elsewhere it uses the Opsis/crawler theme.
    /// </summary>
    public void ApplyLook()
    {
        if (IsDisposed) return;
        // The whole window is opaque; transparency is a property of the BACKGROUND only (DWM accent), so text and logos
        // stay crisp at any transparency setting. Black GDI pixels are where the backdrop shows through.
        Opacity = 1;
        BackColor = Color.Black;

        if (ResolveTint() is { } tint && !breaking)
        {
            // A chosen colour: translucent background in that colour, text in whichever of dark / white reads best on it.
            var light = Luminance(tint) > 0.55;
            var text = light ? Color.FromArgb(24, 26, 30) : Color.White;
            var tintSource = light ? Color.FromArgb(0, 70, 140) : Color.FromArgb(205, 225, 255);
            var hoverOverlay = light ? Color.FromArgb(34, 0, 0, 0) : Color.FromArgb(48, 255, 255, 255);
            canvas.SetLook(text, tintSource, hoverOverlay, text, !light);
            canvasB.SetLook(text, tintSource, hoverOverlay, text, !light);
            if (IsHandleCreated) Native.SetBackdrop(Handle, backdropStyle, tint, (int)Math.Round(255 * Math.Clamp(Theme.Opacity, 0.3, 1.0)));
            return;
        }

        if (position == TickerPosition.TaskbarIntegrated)
        {
            // On the taskbar: completely clear, only the text and the logos are drawn, in the taskbar's own text colour.
            var st = TaskbarStyle.Read();
            var darkSurface = !st.Light || st.AccentOnTaskbar;
            if (backdropStyle == "gradient")
            {
                // The transparent-gradient material is the light blue: dark text reads on it.
                SetDarkTextLook();
            }
            else
            {
                canvas.SetLook(st.Text, st.SourceText, st.HoverOverlay, st.Text, darkSurface);
                canvasB.SetLook(st.Text, st.SourceText, st.HoverOverlay, st.Text, darkSurface);
            }
            if (IsHandleCreated) Native.SetBackdrop(Handle, backdropStyle, Color.Black, 0);
            return;
        }

        if (MatchDeskStrip)
        {
            // The bar on the desktop looks like the Opsis DeskStrip: the same dark glass (blur behind, tint 0x202228 at 40 %), white text.
            canvas.SetLook(Color.White, Color.FromArgb(150, 205, 255), Color.FromArgb(48, 255, 255, 255), Color.White, true);
            canvasB.SetLook(Color.White, Color.FromArgb(150, 205, 255), Color.FromArgb(48, 255, 255, 255), Color.White, true);
            if (IsHandleCreated) Native.SetBackdrop(Handle, "blur", Color.FromArgb(0x28, 0x22, 0x20), 0x66);
            return;
        }

        var source = Theme.IsDark ? Color.FromArgb(125, 190, 255) : Color.FromArgb(0, 95, 170);
        var hover = Theme.IsDark ? Color.FromArgb(48, 255, 255, 255) : Color.FromArgb(34, 0, 0, 0);
        canvas.SetLook(Theme.Text, source, hover, Theme.Text, Theme.IsDark);
        canvasB.SetLook(Theme.Text, source, hover, Theme.Text, Theme.IsDark);
        if (backdropStyle == "gradient") SetDarkTextLook();
        if (breaking)
        {
            // Breaking news: red bar, white capital letters.
            canvas.SetLook(Color.White, Color.FromArgb(255, 220, 220), Color.FromArgb(60, 255, 255, 255), Color.White, true);
            canvas.SetFill(BreakingRed);
        }
        if (IsHandleCreated)
        {
            var baseColor = Theme.IsDark ? Color.FromArgb(0x20, 0x20, 0x20) : Color.FromArgb(0xF3, 0xF3, 0xF3);
            // Dark text needs a light surface behind it: the light theme never gets more transparent than ~75 %.
            var (glass, glassAlpha) = Native.GlassTint(baseColor, Theme.Accent, Theme.Opacity, Theme.IsDark ? 18 : 0xC0);
            Native.SetBackdrop(Handle, backdropStyle, glass, glassAlpha);
        }
    }

    // The ticker must never steal focus from the application the user is working in,
    // and it should not appear in Alt+Tab.
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            Theme.Changed -= ApplyLook;
            if (appBarRegistered && IsHandleCreated) Native.AppBarRemove(Handle);
            appBarRegistered = false;
            PowerChanged -= ApplyPowerProfile;
            taskbarTimer.Dispose();
            scrollTimer.Dispose();
            focusTimer.Dispose();
            hoverTimer.Dispose();
            preview?.Dispose();
            canvas.Dispose();
            canvasB.Dispose();
        }
        base.Dispose(disposing);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Raised on a different thread.
        if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(Reposition));
    }

    /// <summary>Rebuilds the strip (the left half when split). The scroll position given in <paramref name="savedOffset"/> is restored.</summary>
    public void SetItems(IEnumerable<NewsItem> items, double savedOffset)
    {
        HidePreview();
        canvas.SetItems(items.ToList());
        canvas.SetShowIcons(showIcons);
        canvas.SetOffset(savedOffset);
        state.RestoreScroll(new TickerScrollState(canvas.GetOffset()));
    }

    /// <summary>Rebuilds the right half of a split bar.</summary>
    public void SetItemsB(IEnumerable<NewsItem> items, double savedOffset)
    {
        HidePreview();
        canvasB.SetItems(items.ToList());
        canvasB.SetShowIcons(showIcons);
        canvasB.SetOffset(savedOffset);
        state.SetScrollOffsetB(canvasB.GetOffset());
    }

    /// <summary>Splits the bar in the middle: two independent strips side by side.</summary>
    public void SetSplit(bool value)
    {
        if (split == value) return;
        split = value;
        canvasB.Visible = value;
        canvas.SetDivider(value);
        LayoutCanvases();
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsSplit => split;

    private void LayoutCanvases()
    {
        if (split)
        {
            var half = ClientSize.Width / 2;
            canvas.SetBounds(0, 0, half, ClientSize.Height);
            canvasB.SetBounds(half, 0, ClientSize.Width - half, ClientSize.Height);
        }
        else
        {
            canvas.SetBounds(0, 0, ClientSize.Width, ClientSize.Height);
        }
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return;
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Debug.WriteLine($"Opsis News Crawler: cannot open link: {ex.Message}");
        }
    }

    // ---- hover preview -------------------------------------------------------------------

    private void OnHoverChanged(NewsItem? item, Rectangle screenRect)
    {
        hoverTimer.Stop();
        if (item is null || !ShowPreview || string.IsNullOrEmpty(item.Url) || item.Kind != NewsItemKind.News)
        {
            HidePreview();
            return;
        }

        // Wait a moment so that moving the mouse across the strip does not flash cards.
        pendingHoverItem = item;
        pendingHoverRect = screenRect;
        hoverTimer.Start();
    }

    private void ShowPendingPreview()
    {
        hoverTimer.Stop();
        if (pendingHoverItem is null || !ShowPreview || !(canvas.IsMouseInside() || canvasB.IsMouseInside()) || pausedByForeground) return;

        preview ??= new ArticlePreviewForm();
        preview.ShowFor(pendingHoverItem, pendingHoverRect, preferAbove: position != TickerPosition.Top);
    }

    private void HidePreview()
    {
        hoverTimer.Stop();
        pendingHoverItem = null;
        preview?.HidePreview();
    }

    private void OnCanvasMouseWheel(object? sender, MouseEventArgs e)
    {
        if (e.Delta == 0) return;
        // Over the second half of a split bar only that half changes; everywhere else the first half (or the whole bar) does.
        if (split && ReferenceEquals(sender, canvasB))
        {
            ScrollSpeedB = (scrollSpeedB > 0 ? scrollSpeedB : scrollSpeed) + Math.Sign(e.Delta) * SpeedWheelStep;
            ScrollSpeedBChanged?.Invoke(scrollSpeedB);
            return;
        }
        ScrollSpeed += Math.Sign(e.Delta) * SpeedWheelStep;
        ScrollSpeedChanged?.Invoke(scrollSpeed);
    }

    // ---- scrolling -----------------------------------------------------------------------

    private void ScrollTicker()
    {
        // Time based, so the speed does not depend on how precisely the timer fires.
        var now = clock.ElapsedMilliseconds;
        var dt = Math.Min(now - lastTickMs, 100) / 1000.0;
        lastTickMs = now;

        if (alertClock.IsRunning)
        {
            var t = alertClock.Elapsed.TotalSeconds;
            canvas.SetAlertIntro(t);
            if (t >= 3.9) alertClock.Stop();
        }
        canvas.TickAnimation();

        if (pausedByForeground || manuallyPaused) return;
        if (PauseOnHover && (canvas.IsMouseInside() || canvasB.IsMouseInside())) return;

        Advance(canvas, dt, scrollSpeed, () => state.ScrollOffset, state.SetScrollOffset);
        if (split) Advance(canvasB, dt, scrollSpeedB > 0 ? scrollSpeedB : scrollSpeed, () => state.ScrollOffsetB, state.SetScrollOffsetB);
    }

    private void Advance(TickerCanvas c, double dt, double speed, Func<double> getOffset, Action<double> setOffset)
    {
        if (!c.IsScrollable())
        {
            if (getOffset() != 0)
            {
                setOffset(0);
                c.SetOffset(0);
            }
            return;
        }

        var next = getOffset() + speed * dt;
        var loop = c.GetLoopLength();
        if (next >= loop) next -= loop; // seamless: the strip is drawn twice
        setOffset(next);
        c.SetOffset(next);

        if (!PauseOnHover) c.RefreshHoverFromCursor();
    }

    private void SyncPauseState()
    {
        if (pausedByForeground || manuallyPaused) state.Pause();
        else state.Resume();
    }

    private void CheckForeground()
    {
        if (!running) return;

        // Never draw over a fullscreen video / browser / game: hide, and come back afterwards.
        var fullscreen = Native.IsFullscreenAppActive(Handle, hostWindowDetector);
        hiddenByFullscreen = fullscreen;
        if (fullscreen && Visible)
        {
            HidePreview();
            Hide();
        }
        else if (!fullscreen && !Visible && !hiddenByTaskbar)
        {
            Show();
            Reposition();
        }
        if (fullscreen) return;

        if (!PauseOnForeground)
        {
            if (pausedByForeground)
            {
                pausedByForeground = false;
                SyncPauseState();
            }
            return;
        }

        var desktop = Native.IsDesktopForeground(Handle, hostWindowDetector);
        if (!desktop && !pausedByForeground)
        {
            // Another program took over: freeze exactly where we are (the offset stays in the state).
            pausedByForeground = true;
            SyncPauseState();
            HidePreview();
        }
        else if (desktop && pausedByForeground)
        {
            // Back on the desktop: refresh the news (which keeps the position) and continue.
            pausedByForeground = false;
            SyncPauseState();
            RefreshRequested?.Invoke();
        }
    }

    // ---- placement -----------------------------------------------------------------------

    public void SetPosition(TickerPosition value)
    {
        position = value;
        if (value != TickerPosition.Custom) SetEditMode(false);
        ApplyLook();
        Reposition();
    }

    /// <summary>Position used by <see cref="TickerPosition.Custom"/> (screen coordinates; the height is fixed).</summary>
    public void SetCustomBounds(Rectangle bounds)
    {
        customBounds = bounds;
        if (position == TickerPosition.Custom) Reposition();
    }

    /// <summary>Lets the user drag the strip with the mouse (move) and by its ends (width). Only in Custom position.</summary>
    public void SetEditMode(bool on)
    {
        editMode = on && position == TickerPosition.Custom;
        canvas.SetPassThrough(editMode);
        canvasB.SetPassThrough(editMode);
        if (editMode)
        {
            MinimumSize = new Size(200, TickerHeight);
            MaximumSize = new Size(0, TickerHeight);
        }
        else
        {
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
        }
        if (desktopLayer && IsHandleCreated)
        {
            // A bar that lies on the desktop must come to the front while it is moved, otherwise other windows hide it.
            if (editMode) Native.KeepTopMost(Handle); else Native.SendToBottom(Handle);
        }
        if (editMode) SnapCursorToBar();
    }

    /// <summary>
    /// When moving starts, the mouse pointer jumps onto the middle of the bar and shows the four-arrow move cursor,
    /// so the user can drag the bar at once (the settings button that started the move is somewhere else on the screen).
    /// </summary>
    private void SnapCursorToBar()
    {
        try
        {
            var b = Bounds;
            Cursor.Position = new Point(b.Left + b.Width / 2, b.Top + b.Height / 2);
            Cursor.Current = Cursors.SizeAll;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Not allowed to move the pointer (e.g. a secure desktop): the bar can still be dragged.
        }
    }

    /// <summary>A bar for another monitor: sits at the bottom of that screen's work area, nothing else of the primary taskbar logic applies.</summary>
    public Screen? TargetScreen { get; set; }

    private Rectangle BottomStrip(Screen screen, int h)
    {
        if (TargetScreen is not null) return TaskbarPlacement.GetBottomWorkArea(screen, h);
        var b = screen.Bounds;
        if (!FollowTaskbar) return TaskbarPlacement.GetScreenBottom(screen, h);
        if (Native.IsTaskbarAutoHide())
        {
            // The work area ignores an auto-hide taskbar; follow its visible part instead.
            var visible = Native.GetVisibleBottomTaskbarHeight(b);
            return new Rectangle(b.Left, b.Bottom - visible - h - AutoHideGap(visible), b.Width, h);
        }
        return TaskbarPlacement.GetBottomWorkArea(screen, h);
    }

    public void Reposition()
    {
        var screen = TargetScreen ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var wa = screen.WorkingArea;
        var h = TickerHeight;
        var wantAppBar = running && ReserveSpace && position is TickerPosition.Top or TickerPosition.BottomTaskbar;

        if (!wantAppBar && appBarRegistered)
        {
            Native.AppBarRemove(Handle);
            appBarRegistered = false;
        }

        Rectangle target;
        switch (position)
        {
            case TickerPosition.Top:
                if (wantAppBar)
                {
                    if (!appBarRegistered) appBarRegistered = Native.AppBarRegister(Handle);
                    target = Native.AppBarSetPos(Handle, screen.Bounds, Native.ABE_TOP, h);
                }
                else target = new Rectangle(wa.Left, wa.Top, wa.Width, h);
                break;
            case TickerPosition.Center:
                var w = Math.Min(1000, wa.Width);
                target = new Rectangle(wa.Left + (wa.Width - w) / 2, wa.Top + (wa.Height - h) / 2, w, h);
                break;
            case TickerPosition.Custom:
                target = customBounds.Width >= 200 && Screen.AllScreens.Any(s => s.Bounds.IntersectsWith(customBounds))
                    ? new Rectangle(customBounds.Left, customBounds.Top, customBounds.Width, h)
                    : BottomStrip(screen, h);
                break;
            case TickerPosition.TaskbarIntegrated:
                var tb = Native.GetTaskbarRect();
                if (tb.IsEmpty || tb.Width < tb.Height)
                {
                    target = BottomStrip(screen, h);
                    break;
                }
                var left = tb.Left + Math.Max(0, TaskbarLeftOffset);
                var right = tb.Right - Math.Max(0, TaskbarRightOffset);
                if (TaskbarAutoDetect && measuredSpan is { } span && span.Right - span.Left >= 180)
                {
                    left = Math.Max(tb.Left, span.Left);
                    right = Math.Min(tb.Right, span.Right);
                }
                target = new Rectangle(left, tb.Top, Math.Max(200, right - left), tb.Height);
                if (target != lastLoggedIntegrated)
                {
                    // Help to find out why a bar inside the taskbar might not match its height on another PC.
                    lastLoggedIntegrated = target;
                    var (tbDpi, ownDpi) = IsHandleCreated ? Native.DescribeDpi(Handle) : (0u, 0u);
                    Log.Write($"Bar inside the taskbar: taskbar {tb.Left},{tb.Top} {tb.Width}x{tb.Height} (dpi {tbDpi}), bar {target.Left},{target.Top} {target.Width}x{target.Height} (window dpi {ownDpi}, screen {screen.Bounds.Width}x{screen.Bounds.Height})");
                }
                break;
            default:
                if (wantAppBar)
                {
                    if (!appBarRegistered) appBarRegistered = Native.AppBarRegister(Handle);
                    target = Native.AppBarSetPos(Handle, screen.Bounds, Native.ABE_BOTTOM, h);
                    // With an auto-hide taskbar the reserved strip is at the very bottom; lift it together with the taskbar.
                    if (FollowTaskbar && Native.IsTaskbarAutoHide()) target = BottomStrip(screen, h);
                }
                else target = BottomStrip(screen, h);
                break;
        }

        // A rounded or pill bar that spans the whole screen would show its curves outside the screen: it floats with a margin instead.
        if (ShapeInset > 0 && (position is TickerPosition.Top or TickerPosition.BottomTaskbar || TargetScreen is not null) && target.Width > 2 * ShapeInset + 200)
            target = new Rectangle(target.Left + ShapeInset, target.Top, target.Width - 2 * ShapeInset, target.Height);

        if (Bounds != target) Bounds = target;
    }

    /// <summary>Horizontal margin of a full-width bar that has a rounded or pill shape (0 for the plain rectangle).</summary>
    private int ShapeInset => shape == "rect" ? 0 : 28;

    private void FollowTaskbarTick()
    {
        if (!running || editMode || TargetScreen is not null) return;
        taskbarTick++;
        if (taskbarTick % 30 == 1) autoHide = Native.IsTaskbarAutoHide();

        if (position == TickerPosition.TaskbarIntegrated)
        {
            // A bar inside the taskbar hides and shows together with it - and slides out and in like the taskbar does,
            // not a jump (the bar above the taskbar already does the same).
            var cloaked = autoHide && Native.IsTaskbarHiddenByAutoHide();
            if (cloaked != hiddenByTaskbar)
            {
                hiddenByTaskbar = cloaked;
                HiddenByTaskbarChanged?.Invoke();
                var screenBottom = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).Bounds.Bottom;
                if (cloaked)
                {
                    if (Visible)
                    {
                        HidePreview();
                        StartSlide(Top, screenBottom);
                    }
                }
                else if (!hiddenByFullscreen)
                {
                    if (!Visible)
                    {
                        Reposition();
                        Top = screenBottom;
                        Show();
                    }
                    var tbTop = Native.GetTaskbarRect();
                    StartSlide(Top, tbTop.IsEmpty ? Top : tbTop.Top);
                }
            }
            if (slideActive)
            {
                StepSlide();
                return;
            }
            if (hiddenByTaskbar)
            {
                if (Visible) Hide();
                return;
            }
            if (!Visible) return;
            // The taskbar steals the top of the z-order and its size/icons change: re-assert about once a second.
            if (taskbarTick % 30 == 0)
            {
                Native.KeepTopMost(Handle);
                ApplyLook();
                Reposition();
            }
            if (TaskbarAutoDetect && !measuring && (taskbarTick % 66 == 2 || measuredSpan is null && taskbarTick % 10 == 2))
                MeasureTaskbarInBackground();
            return;
        }

        if (!Visible) return;
        if (position != TickerPosition.BottomTaskbar || !FollowTaskbar) return;
        if (!autoHide)
        {
            animToY = int.MinValue;
            return;
        }

        // The bar rides on top of the taskbar: when the taskbar slides out of the way the bar goes down to the screen edge,
        // when it comes back the bar goes up again - with the same short ease-out, not a jump.
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var b = screen.Bounds;
        var visibleHeight = Native.GetVisibleBottomTaskbarHeight(b);
        var targetY = b.Bottom - visibleHeight - TickerHeight - AutoHideGap(visibleHeight);
        var inset = ShapeInset;
        if (Left != b.Left + inset || Width != b.Width - 2 * inset || Height != TickerHeight)
            Bounds = new Rectangle(b.Left + inset, Top, b.Width - 2 * inset, TickerHeight);

        if (animToY != targetY)
        {
            animFromY = Top;
            animToY = targetY;
            animStartMs = clock.ElapsedMilliseconds;
        }
        if (Top != animToY)
        {
            var t = Math.Min(1.0, (clock.ElapsedMilliseconds - animStartMs) / 170.0);
            var eased = 1 - Math.Pow(1 - t, 3);
            Top = animFromY + (int)Math.Round((animToY - animFromY) * eased);
        }
    }

    /// <summary>
    /// While an auto-hide taskbar is hidden the bar must not cover the last pixel rows of the screen: the pointer has to reach
    /// the edge for the taskbar to come back.
    /// </summary>
    private static int AutoHideGap(int visibleTaskbarHeight) => visibleTaskbarHeight == 0 ? 2 : 0;

    private bool slideActive;
    private long slideStartMs;
    private int slideFrom, slideTo;

    private void StartSlide(int from, int to)
    {
        slideFrom = from;
        slideTo = to;
        slideStartMs = clock.ElapsedMilliseconds;
        slideActive = from != to;
    }

    /// <summary>One step of the slide of a bar inside an auto-hiding taskbar (170 ms, ease-out).</summary>
    private void StepSlide()
    {
        var t = Math.Min(1.0, (clock.ElapsedMilliseconds - slideStartMs) / 170.0);
        var eased = 1 - Math.Pow(1 - t, 3);
        Top = slideFrom + (int)Math.Round((slideTo - slideFrom) * eased);
        if (t < 1.0) return;
        slideActive = false;
        if (hiddenByTaskbar) Hide();
        else Reposition();
    }

    private void SetHiddenByTaskbar(bool hidden)
    {
        if (hidden == hiddenByTaskbar) return;
        hiddenByTaskbar = hidden;
        HiddenByTaskbarChanged?.Invoke();
        if (hidden)
        {
            if (Visible)
            {
                HidePreview();
                Hide();
            }
        }
        else if (!Visible && !hiddenByFullscreen)
        {
            Show();
            Reposition();
        }
    }

    private void MeasureTaskbarInBackground()
    {
        measuring = true;
        _ = Task.Run(() => TaskbarMetrics.MeasureFreeSpan()).ContinueWith(t =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                measuring = false;
                var span = t.IsCompletedSuccessfully ? t.Result : null;
                if (span is null || span == measuredSpan) return;
                measuredSpan = span;
                Reposition();
            }));
        });
    }

    private const int WM_NCHITTEST = 0x84, WM_EXITSIZEMOVE = 0x232, WM_SETCURSOR = 0x20;

    protected override void WndProc(ref Message m)
    {
        // While the bar is being moved, the whole bar (except the two width handles) shows the four-arrow move cursor.
        if (editMode && m.Msg == WM_SETCURSOR && (int)(m.LParam.ToInt64() & 0xFFFF) == 2) // HTCAPTION
        {
            Cursor.Current = Cursors.SizeAll;
            m.Result = new IntPtr(1);
            return;
        }
        if (editMode && m.Msg == WM_NCHITTEST)
        {
            var lp = m.LParam.ToInt64();
            var pt = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            m.Result = new IntPtr(pt.X < 10 ? 10 : pt.X >= Width - 10 ? 11 : 2); // HTLEFT / HTRIGHT / HTCAPTION
            return;
        }
        if (m.Msg == WM_EXITSIZEMOVE && editMode)
            PlacementEdited?.Invoke(Bounds);
        if (appBarRegistered && m.Msg == Native.AppBarCallbackMessage && m.WParam.ToInt32() == Native.ABN_POSCHANGED)
            BeginInvoke(new Action(Reposition));
        base.WndProc(ref m);
    }

    /// <summary>
    /// Breaking news: the bar turns red, text goes to capitals and the intro plays (the app icon grows from the middle,
    /// then "BREAKING NEWS" fades in and out). Only meaningful on the main bar.
    /// </summary>
    public void StartBreaking()
    {
        breaking = true;
        canvas.SetUppercase(true);
        ApplyLook();
        canvas.SetAlertIntro(0);
        alertClock.Restart();
    }

    public void EndBreaking()
    {
        breaking = false;
        alertClock.Reset();
        canvas.SetAlertIntro(-1);
        canvas.SetUppercase(false);
        canvas.SetFill(null);
        canvas.SetLiveButton(false);
        ApplyLook();
    }

    /// <summary>Shows the pulsing live button (only while breaking news is active).</summary>
    public void SetLiveAvailable(bool available) => canvas.SetLiveButton(available && breaking);

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsBreaking => breaking;

    public void RequestRefresh() => RefreshRequested?.Invoke();

    public void StartTicker()
    {
        running = true;
        Reposition();
        Show();
        taskbarTimer.Start();
        lastTickMs = clock.ElapsedMilliseconds;
        focusTimer.Start();
        scrollTimer.Start();
    }

    public void StopTicker()
    {
        running = false;
        taskbarTimer.Stop();
        if (appBarRegistered)
        {
            Native.AppBarRemove(Handle);
            appBarRegistered = false;
        }
        HidePreview();
        scrollTimer.Stop();
        focusTimer.Stop();
        Hide();
    }

    public void PauseTicker()
    {
        manuallyPaused = true;
        SyncPauseState();
    }

    public void ResumeTicker()
    {
        manuallyPaused = false;
        SyncPauseState();
    }
}
