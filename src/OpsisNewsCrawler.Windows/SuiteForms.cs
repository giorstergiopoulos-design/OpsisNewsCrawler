using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>Common look of the welcome guide and the Opsis family window: rounded borderless window, draggable header, Esc closes, closing shrinks into the target.</summary>
internal abstract class SplashBase : Form
{
    private readonly Func<Rectangle> target;
    private bool closingAnimated;

    protected SplashBase(Func<Rectangle> target)
    {
        this.target = target;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        DoubleBuffered = true;
        ShowInTaskbar = true;
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5f);
        Icon = AppIcon.Get();
        Text = Loc.T("app.name");
        if (Loc.IsRtl) RightToLeft = RightToLeft.No;   // text is laid out by hand (see Rtl); controls keep their own positions
    }

    /// <summary>Height of the band at the top that can be dragged.</summary>
    protected abstract int HeaderHeight { get; }

    protected static bool Rtl => Loc.IsRtl;

    /// <summary>The x of a block of the given width when the layout is written left-to-right: mirrored for Arabic.</summary>
    protected int X(int x, int width) => Rtl ? ClientSize.Width - x - width : x;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            var round = 2;   // DWMWCP_ROUND
            DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        Theme.ApplyTitleBar(this);
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);
            if (m.Result == (IntPtr)1)    // HTCLIENT
            {
                var p = PointToClient(new Point((short)((long)m.LParam & 0xFFFF), (short)(((long)m.LParam >> 16) & 0xFFFF)));
                if (p.Y < HeaderHeight && GetChildAtPoint(p) is null) m.Result = (IntPtr)2;   // HTCAPTION: drag the window by its header
            }
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // A program that was not in front may not take the foreground; the TopMost toggle brings the window above the others without keeping it there.
        TopMost = true;
        TopMost = false;
        Activate();
    }
    /// <summary>Closes without the animation (tests, shutdown).</summary>
    public void CloseNow()
    {
        closingAnimated = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!closingAnimated && Visible && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            closingAnimated = true;
            MinimiseAnimation.Run(this, target(), Close);
            return;
        }
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}

/// <summary>The welcome guide: animated icon, name and version, six short pages, Next / Back / Skip, "Don't show again".</summary>
internal sealed class WelcomeForm : SplashBase
{
    public const int PageCount = 6;

    private readonly AnimatedAppIcon icon;
    private readonly FlatButton back = new() { IsNavigation = true };
    private readonly FlatButton next = new() { IsNavigation = true };
    private readonly FlatButton skip = new() { IsNavigation = true };
    private readonly FlatCheck dontShow = new();
    private readonly FlatCheck showFamily = new() { Checked = true };
    private int page;
    private bool reachedLast;

    public WelcomeForm(Func<Rectangle> target) : base(target)
    {
        ClientSize = new Size(780, 470);
        icon = new AnimatedAppIcon(() => !MinimiseAnimation.Simple, 44);
        Controls.Add(icon);

        back.Text = Loc.T("welcome.back");
        next.Text = Loc.T("welcome.next");
        skip.Text = Loc.T("welcome.skip");
        next.SetPrimary(true);
        dontShow.Text = Loc.T("welcome.dontShow");
        showFamily.Text = Loc.T("welcome.family");
        foreach (var c in new Control[] { back, next, skip, dontShow, showFamily }) Controls.Add(c);
        Theme.Apply(this);
        BackColor = Theme.Background;

        back.Click += (_, _) => Go(page - 1);
        next.Click += (_, _) => { if (page == PageCount - 1) Close(); else Go(page + 1); };
        skip.Click += (_, _) => Close();
        PlaceControls();
        Go(0);
    }

    protected override int HeaderHeight => 104;

    /// <summary>"Don't show again" was ticked.</summary>
    public bool DontShowAgain => dontShow.Checked;

    /// <summary>The last page was reached and "show me the family afterwards" is still ticked.</summary>
    public bool ShowFamilyAfterwards => reachedLast && showFamily.Checked;

    public int CurrentPage => page;

    public void SetDontShowAgain(bool value) => dontShow.Checked = value;

    /// <summary>True when the title or the text of a page does not fit its area in the current language (checked by the snapshot switch).</summary>
    public bool PageOverflows(int index, out string detail)
    {
        var n = index + 1;
        using var body = new Font("Segoe UI", 12f);
        using var title = new Font("Segoe UI Semibold", 16f);
        var h = TextRenderer.MeasureText(Loc.T($"welcome.p{n}.b"), body, new Size(700, 0), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak).Height;
        var tw = TextRenderer.MeasureText(Loc.T($"welcome.p{n}.t"), title, new Size(0, 0), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
        detail = $"page {n}: body {h}px of 190, title {tw}px of 700";
        return h > 190 || tw > 700;
    }

    public void Go(int index)
    {
        page = Math.Clamp(index, 0, PageCount - 1);
        reachedLast |= page == PageCount - 1;
        back.Visible = page > 0;
        next.Text = Loc.T(page == PageCount - 1 ? "welcome.finish" : "welcome.next");
        showFamily.Visible = page == PageCount - 1;
        PlaceControls();
        Invalidate();
    }

    private void PlaceControls()
    {
        const int footer = 410;
        icon.Location = new Point(X(32, icon.Width), 30);
        skip.Location = new Point(X(40, skip.Width), footer);
        dontShow.Location = new Point(X(40 + skip.Width + 14, dontShow.Width), footer + 4);
        next.Location = new Point(X(780 - 40 - next.Width, next.Width), footer);
        back.Location = new Point(X(780 - 40 - next.Width - 10 - back.Width, back.Width), footer);
        showFamily.Location = new Point(X(40, showFamily.Width), 364);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | (Rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left);

        var headerLeft = 32 + icon.Width + 20;
        var headerRect = new Rectangle(X(headerLeft, 780 - headerLeft - 32), 26, 780 - headerLeft - 32, 40);
        using (var f = new Font("Segoe UI Semibold", 17f))
            TextRenderer.DrawText(g, Loc.T("welcome.title"), f, headerRect, Theme.Text, flags | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        using (var f = new Font("Segoe UI", 10f))
            TextRenderer.DrawText(g, Loc.T("welcome.version", AboutTexts.Version), f, new Rectangle(headerRect.X, 66, headerRect.Width, 24), Theme.TextMuted, flags | TextFormatFlags.SingleLine);

        using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 32, HeaderHeight, Width - 32, HeaderHeight);

        var n = page + 1;
        var body = new Rectangle(X(40, 700), 170, 700, 180);
        using (var f = new Font("Segoe UI Semibold", 16f))
            TextRenderer.DrawText(g, Loc.T($"welcome.p{n}.t"), f, new Rectangle(body.X, 128, body.Width, 36), Theme.Accent, flags | TextFormatFlags.SingleLine);
        using (var f = new Font("Segoe UI", 12f))
            TextRenderer.DrawText(g, Loc.T($"welcome.p{n}.b"), f, body, Theme.Text, flags);

        // page dots, and "n / 6" next to them
        const int dot = 9, gap = 9;
        var total = PageCount * dot + (PageCount - 1) * gap;
        var x0 = (Width - total) / 2;
        for (var i = 0; i < PageCount; i++)
        {
            using var brush = new SolidBrush(i == page ? Theme.Accent : Theme.Border);
            g.FillEllipse(brush, x0 + i * (dot + gap), 388 + (page == PageCount - 1 ? 0 : 0), dot, dot);
        }
        if (!showFamily.Visible)
            using (var f = new Font("Segoe UI", 9f))
                TextRenderer.DrawText(g, Loc.T("welcome.page", page + 1, PageCount), f, new Rectangle(x0 + total + 12, 382, 80, 20), Theme.TextMuted, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Right when !Rtl: case Keys.Left when Rtl: case Keys.Enter:
                if (page == PageCount - 1) { if (keyData == Keys.Enter) Close(); } else Go(page + 1);
                return true;
            case Keys.Left when !Rtl: case Keys.Right when Rtl:
                Go(page - 1);
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>A picture of one page (for the snapshot switch).</summary>
    public Bitmap Snapshot(int index)
    {
        Go(index);
        var bmp = new Bitmap(Width, Height);
        DrawToBitmap(bmp, new Rectangle(Point.Empty, bmp.Size));
        return bmp;
    }
}

/// <summary>One card of the Opsis family window.</summary>
internal sealed class SuiteCard : Panel
{
    private readonly SuiteApp app;
    private readonly bool isSelf;
    private readonly FlatButton open = new();
    private readonly FlatButton install = new();
    private readonly FlatButton details = new();
    private Bitmap? appIcon;
    private bool hot;
    private InstalledInfo info = InstalledInfo.None;
    private string? latestTag;
    private double phase;
    private Bitmap? selfFrame;
    private int bulletsBottom;

    public const int CardWidth = 428;

    public event Action<SuiteCard>? OpenClicked;
    public event Action<SuiteCard>? InstallClicked;
    public event Action<SuiteCard>? DetailsClicked;

    public SuiteCard(SuiteApp app, bool isSelf)
    {
        this.app = app;
        this.isSelf = isSelf;
        DoubleBuffered = true;
        Width = CardWidth;
        Margin = new Padding(0, 0, 14, 14);
        BackColor = Theme.Surface;      // the pill buttons clear their corners with the parent colour
        foreach (var b in new[] { open, install, details }) { b.Margin = new Padding(0); Controls.Add(b); }
        open.Text = Loc.T("suite.open");
        install.Text = Loc.T("suite.install");
        details.Text = Loc.T("suite.details");
        install.SetPrimary(true);
        open.Click += (_, _) => OpenClicked?.Invoke(this);
        install.Click += (_, _) => InstallClicked?.Invoke(this);
        details.Click += (_, _) => DetailsClicked?.Invoke(this);
        MouseEnter += (_, _) => SetHot(true);
        MouseLeave += (_, _) => SetHot(Bounds.Contains(Parent?.PointToClient(Cursor.Position) ?? Point.Empty));
        foreach (var b in new[] { open, install, details }) { b.MouseEnter += (_, _) => SetHot(true); b.MouseLeave += (_, _) => SetHot(ClientRectangle.Contains(PointToClient(Cursor.Position))); }
        if (isSelf) info = new InstalledInfo(true, AboutTexts.Version, null);
        Relayout();
    }

    private void SetHot(bool value)
    {
        if (hot == value) return;
        hot = value;
        BackColor = hot ? FlatDraw.SurfaceHover : Theme.Surface;
        Invalidate();
    }

    public SuiteApp App => app;
    public bool IsSelf => isSelf;
    public InstalledInfo Info => info;
    public FlatButton InstallButton => install;

    public bool UpdateAvailable => !isSelf && info.Installed && SuiteDetector.IsNewer(latestTag, info.Version);

    public void SetInstalled(InstalledInfo value)
    {
        info = isSelf ? new InstalledInfo(true, AboutTexts.Version, info.ExePath) : value;
        appIcon?.Dispose();
        appIcon = null;
        if (!isSelf && info.ExePath is { } exe)
        {
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(exe);
                if (icon is not null) appIcon = icon.ToBitmap();
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException) { }
        }
        Relayout();
    }

    public void SetLatest(string? tag)
    {
        latestTag = tag;
        Relayout();
    }

    /// <summary>Advances the shimmer / the animated icon (called by the window's timer).</summary>
    public void Tick(double t)
    {
        phase = hot ? (t * 3) % 1.0 : t % 1.0;
        if (isSelf)
        {
            selfFrame?.Dispose();
            selfFrame = AppIconArt.RenderWide(112, 28, MinimiseAnimation.Simple ? 0 : phase, showAllFour: MinimiseAnimation.Simple);
        }
        Invalidate(new Rectangle(0, 0, Width, 90));
    }

    private const int BulletsTop = 108;

    private int TileWidth => isSelf ? 112 : 56;

    private static TextFormatFlags Flags => TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | (Loc.IsRtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left);

    private int BulletsHeight(IReadOnlyList<string> bullets, Font font)
    {
        var y = 0;
        foreach (var b in bullets)
            y += TextRenderer.MeasureText(b, font, new Size(CardWidth - 32 - 14, 0), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak).Height + 3;
        return y;
    }

    private void Relayout()
    {
        using var font = new Font("Segoe UI", 9.5f);
        var bullets = app.CapabilitiesFor(Loc.Language);
        bulletsBottom = BulletsTop + BulletsHeight(bullets, font);
        var showOpen = info.Installed && info.ExePath is not null && !isSelf;
        var showInstall = !isSelf && (!info.Installed || UpdateAvailable);
        open.Visible = showOpen;
        install.Visible = showInstall;
        details.Visible = true;
        var y = bulletsBottom + 12;
        var buttons = new List<FlatButton>();
        if (showInstall) buttons.Add(install);
        if (showOpen) buttons.Add(open);
        buttons.Add(details);
        var x = 16;
        foreach (var b in buttons)
        {
            b.PerformLayout();
            var size = b.GetPreferredSize(Size.Empty);
            b.Size = size;
            b.Location = new Point(Loc.IsRtl ? CardWidth - x - size.Width : x, y);
            x += size.Width + 8;
        }
        Height = y + 32 + 16;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Background);   // the corners outside the rounded card
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = FlatDraw.Rounded(rect, 12))
        {
            using var fill = new SolidBrush(hot ? FlatDraw.SurfaceHover : Theme.Surface);
            g.FillPath(fill, path);
            using var pen = new Pen(isSelf ? Theme.Accent : Theme.Border);
            g.DrawPath(pen, path);
        }

        var rtl = Loc.IsRtl;
        int Ax(int x, int w) => rtl ? Width - x - w : x;

        // tile
        var tile = new Rectangle(Ax(16, TileWidth), isSelf ? 24 : 14, TileWidth, isSelf ? 28 : 56);
        if (isSelf)
        {
            if (selfFrame is null) selfFrame = AppIconArt.RenderWide(112, 28, 0, showAllFour: true);
            g.DrawImage(selfFrame, tile);
        }
        else DrawTile(g, tile);

        // name, tagline, state
        var textLeft = 16 + TileWidth + 14;
        var textWidth = CardWidth - textLeft - 16;
        using (var f = new Font("Segoe UI Semibold", 12.5f))
            TextRenderer.DrawText(g, app.Name, f, new Rectangle(Ax(textLeft, textWidth), 14, textWidth, 24), Theme.Text, Flags | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        using (var f = new Font("Segoe UI", 9f))
            TextRenderer.DrawText(g, app.TaglineFor(Loc.Language), f, new Rectangle(Ax(textLeft, textWidth), 38, textWidth, 38), Theme.TextMuted, Flags | TextFormatFlags.EndEllipsis);

        var (state, color) = State();
        using (var f = new Font("Segoe UI Semibold", 8.5f))
        {
            var size = TextRenderer.MeasureText(state, f);
            var pill = new Rectangle(Ax(16, size.Width + 16), 80, size.Width + 16, 20);
            using var path = FlatDraw.Rounded(pill, 10);
            using var back = new SolidBrush(Color.FromArgb(40, color));
            g.FillPath(back, path);
            TextRenderer.DrawText(g, state, f, pill, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        // bullets
        using (var f = new Font("Segoe UI", 9.5f))
        {
            var y = BulletsTop;
            foreach (var b in app.CapabilitiesFor(Loc.Language))
            {
                var h = TextRenderer.MeasureText(b, f, new Size(CardWidth - 32 - 14, 0), TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak).Height;
                using var dot = new SolidBrush(Theme.Accent);
                g.FillEllipse(dot, Ax(16, 5), y + 7, 5, 5);
                TextRenderer.DrawText(g, b, f, new Rectangle(Ax(30, CardWidth - 46), y, CardWidth - 46, h), Theme.Text, Flags);
                y += h + 3;
            }
        }
    }

    private (string Text, Color Color) State()
    {
        if (isSelf) return (Loc.T("suite.here"), Theme.Accent);
        if (!info.Installed) return (Loc.T("suite.notInstalled"), Theme.TextMuted);
        if (UpdateAvailable) return (Loc.T("suite.update"), Color.FromArgb(235, 160, 40));
        return (string.IsNullOrWhiteSpace(info.Version) ? Loc.T("suite.installedNoVer") : Loc.T("suite.installed", info.Version), Color.FromArgb(70, 190, 120));
    }

    private void DrawTile(Graphics g, Rectangle tile)
    {
        using var path = FlatDraw.Rounded(tile, 12);
        var h = Math.Abs(app.Id.Aggregate(17, (a, c) => a * 31 + c)) % 360;
        using (var brush = new LinearGradientBrush(tile, ColorFromHsv(h, .55, .62), ColorFromHsv((h + 40) % 360, .65, .42), 45f))
            g.FillPath(brush, path);
        if (appIcon is not null)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(appIcon, new Rectangle(tile.X + 4, tile.Y + 4, tile.Width - 8, tile.Height - 8));
        }
        else
        {
            var words = app.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !w.Equals("Opsis", StringComparison.OrdinalIgnoreCase)).ToList();
            if (words.Count == 0) words.Add(app.Name);
            var initials = string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
            using var f = new Font("Segoe UI Semibold", 15f);
            TextRenderer.DrawText(g, initials, f, tile, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        // the shimmer: a soft diagonal band that passes over the tile (faster while the mouse is on the card)
        var state = g.Save();
        g.SetClip(path);
        var bandX = tile.X - tile.Width + (int)(phase * tile.Width * 3);
        using (var band = new LinearGradientBrush(new Rectangle(bandX, tile.Y, tile.Width, tile.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(70, 255, 255, 255), LinearGradientMode.ForwardDiagonal))
        {
            var blend = new ColorBlend { Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(80, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) }, Positions = new[] { 0f, .5f, 1f } };
            band.InterpolationColors = blend;
            g.FillRectangle(band, bandX, tile.Y, tile.Width, tile.Height);
        }
        g.Restore(state);
    }

    private static Color ColorFromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        var (r, g, b) = h switch { < 60 => (c, x, 0.0), < 120 => (x, c, 0.0), < 180 => (0.0, c, x), < 240 => (0.0, x, c), < 300 => (x, 0.0, c), _ => (c, 0.0, x) };
        return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            appIcon?.Dispose();
            selfFrame?.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>The "Opsis family" window: one card per app of the shared catalogue, with the installed state read from this PC.</summary>
internal sealed class SuiteForm : SplashBase
{
    private readonly string dataDir;
    private readonly FlowLayoutPanel list = new();
    private readonly FlatButton close = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 33 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly CancellationTokenSource cts = new();
    private readonly List<SuiteCard> cards = new();
    private SuiteCatalog? catalog;

    public SuiteForm(string dataDir, Func<Rectangle> target) : base(target)
    {
        this.dataDir = dataDir;
        ClientSize = new Size(980, 700);
        close.Text = Loc.T("suite.close");
        close.SetPrimary(true);
        close.Click += (_, _) => Close();
        list.AutoScroll = true;
        list.FlowDirection = FlowDirection.LeftToRight;
        list.WrapContents = true;
        list.BackColor = Theme.Background;
        list.Padding = new Padding(2, 2, 2, 2);
        Controls.Add(list);
        Controls.Add(close);
        Theme.Apply(this);
        BackColor = Theme.Background;
        list.BackColor = Theme.Background;
        LayoutAll();
        Resize += (_, _) => LayoutAll();

        // The embedded copy is shown at once; the live copy (at most one request a day) replaces it when it arrives.
        SetCatalog(SuiteCatalogStore.Embedded());
        timer.Tick += (_, _) =>
        {
            var t = clock.ElapsedMilliseconds / 3200.0;
            foreach (var c in cards) c.Tick(t + c.App.Id.Length * 0.07);
        };
        timer.Start();
        Shown += async (_, _) => await RefreshAsync();
    }

    protected override int HeaderHeight => 92;

    public IReadOnlyList<SuiteCard> Cards => cards;

    private void LayoutAll()
    {
        list.SetBounds(28, 108, ClientSize.Width - 56, ClientSize.Height - 108 - 64);
        close.PerformLayout();
        close.Size = close.GetPreferredSize(Size.Empty);
        close.Location = new Point(X(28, close.Width), ClientSize.Height - 50);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | (Rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left);
        using (var f = new Font("Segoe UI Semibold", 18f))
            TextRenderer.DrawText(e.Graphics, Loc.T("suite.title"), f, new Rectangle(X(28, ClientSize.Width - 56), 18, ClientSize.Width - 56, 36), Theme.Text, flags);
        using (var f = new Font("Segoe UI", 9.5f))
            TextRenderer.DrawText(e.Graphics, Loc.T("suite.sub"), f, new Rectangle(X(28, ClientSize.Width - 56), 56, ClientSize.Width - 56, 24), Theme.TextMuted, flags);
    }

    private void SetCatalog(SuiteCatalog value)
    {
        catalog = value;
        list.SuspendLayout();
        foreach (var c in cards) { list.Controls.Remove(c); c.Dispose(); }
        cards.Clear();
        foreach (var app in value.Apps)
        {
            var card = new SuiteCard(app, isSelf: app.Id == "newscrawler");
            card.OpenClicked += OnOpen;
            card.InstallClicked += OnInstall;
            card.DetailsClicked += OnDetails;
            cards.Add(card);
            list.Controls.Add(card);
        }
        list.ResumeLayout();
    }

    /// <summary>Gets the live catalogue, reads the installed state of every app and asks GitHub for the latest versions. All failures are silent.</summary>
    public async Task RefreshAsync()
    {
        try
        {
            var live = await new SuiteCatalogStore(dataDir).GetAsync(cts.Token);
            if (IsDisposed) return;
            if (catalog is null || live.Updated != catalog.Updated || live.Apps.Count != catalog.Apps.Count) SetCatalog(live);

            var probe = new WindowsInstallProbe();
            var states = await Task.Run(() => cards.Select(c => (Card: c, Info: SuiteDetector.Detect(c.App, probe))).ToList(), cts.Token);
            if (IsDisposed) return;
            foreach (var (card, state) in states) card.SetInstalled(state);

            var current = catalog!;
            await Task.WhenAll(cards.Where(c => !c.IsSelf && c.Info.Installed).Select(async c =>
            {
                var (tag, _) = await SuiteDownloader.LatestAsync(current, c.App, cts.Token);
                if (!IsDisposed && tag is not null) BeginInvoke(new Action(() => c.SetLatest(tag)));
            }));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException) { }
    }

    private static void OpenUrl(string? url)
    {
        if (!SuiteCatalog.IsAllowedSource(url)) return;
        try { Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    private void OnDetails(SuiteCard card) => OpenUrl(SuiteReleases.RepoUrl(catalog!, card.App));

    private void OnOpen(SuiteCard card)
    {
        if (card.Info.ExePath is not { } exe) return;
        try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) ?? "" }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { }
    }

    private async void OnInstall(SuiteCard card)
    {
        var button = card.InstallButton;
        var original = button.Text;
        button.Enabled = false;
        button.Text = Loc.T("suite.checking");
        try
        {
            var current = catalog!;
            var (_, download) = await SuiteDownloader.LatestAsync(current, card.App, cts.Token);
            var releasesPage = SuiteReleases.RepoUrl(current, card.App) + "/releases";
            if (download is null || !download.CanVerify)
            {
                MessageBox.Show(this, Loc.T(download is null ? "suite.failed" : "suite.noHash", download is null ? "GitHub" : ""), card.App.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                OpenUrl(download?.PageUrl ?? releasesPage);
                return;
            }

            var body = Loc.T("suite.confirmBody", download.FileName, SuiteDownloader.FormatSize(download.Size), $"github.com/{current.Owner}/{card.App.Repo}", SuiteDownloader.DownloadsFolder());
            if (MessageBox.Show(this, body, Loc.T("suite.confirmTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            var progress = new Progress<int>(p => button.Text = Loc.T("suite.downloading", p));
            try
            {
                var saved = await SuiteDownloader.DownloadAsync(download, progress, cts.Token);
                var ask = Loc.T("suite.saved", saved) + "\n\n" + Loc.T("suite.showFolder") + "?";
                if (MessageBox.Show(this, ask, card.App.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{saved}\"") { UseShellExecute = true });
            }
            catch (SuiteDownloadException ex)
            {
                MessageBox.Show(this, ex.Mismatch ? Loc.T("suite.mismatch") : Loc.T("suite.failed", ex.Message), card.App.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!IsDisposed)
            {
                button.Text = original;
                button.Enabled = true;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            cts.Cancel();
            timer.Dispose();
        }
        base.Dispose(disposing);
    }
}
