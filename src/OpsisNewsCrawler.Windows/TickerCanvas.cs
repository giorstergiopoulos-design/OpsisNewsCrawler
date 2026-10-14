using OpsisNewsCrawler.Core;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Draws the news strip itself (no child controls): icon + source + title for every item,
/// repeated twice so the loop is seamless. Also does hit-testing for hover and clicks.
/// Everything is drawn into an off-screen 32-bit bitmap WITH ALPHA and copied to the window as is, so the window's
/// backdrop (glass / clear taskbar) shows through wherever nothing is drawn, dark and light text are both opaque,
/// and the optional edge fade is a real fade to transparent.
/// Control subclasses expose methods instead of properties so the WinForms designer analyzers stay quiet.
/// </summary>
internal sealed class TickerCanvas : Control
{
    private const int LeftPad = 16;
    private const int IconSize = 16;
    private const int IconGap = 7;
    private const int SourceTitleGap = 5;
    private const int ItemGap = 44;
    private const int HoverPad = 7;
    private const int FadeWidth = 56;

    private Color sourceColor = Color.FromArgb(125, 190, 255);
    private Color titleColor = Color.FromArgb(235, 235, 240);
    private Color hoverTitleColor = Color.White;
    private Color hoverOverlay = Color.FromArgb(48, 255, 255, 255);
    private Color? solidFill;
    private bool passThrough;
    private bool editHint;
    private bool edgeFade;
    private bool uppercase;
    private bool darkSurface = true;
    private bool divider;

    // breaking news intro (seconds since it started; < 0 = not running) and the live button
    private double alertT = -1;
    private bool liveVisible;
    private long lastPulseInvalidate;
    private Bitmap? alertIcon;
    private const double AlertContentAt = 3.4;
    private const double AlertEnd = 3.9;

    private static readonly Color[] BadgeColors =
    {
        Color.FromArgb(52, 120, 246), Color.FromArgb(232, 93, 117), Color.FromArgb(46, 160, 120),
        Color.FromArgb(214, 140, 40), Color.FromArgb(140, 90, 220), Color.FromArgb(40, 150, 190)
    };

    private static readonly Bitmap MeasureBitmap = new(1, 1);
    private static readonly Graphics MeasureGraphics = CreateMeasureGraphics();
    private static readonly StringFormat TextFormat = CreateFormat();
    private static readonly StringFormat TextFormatRtl = CreateRtlFormat();

    private static StringFormat CreateRtlFormat()
    {
        var f = CreateFormat();
        f.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        return f;
    }

    private static Graphics CreateMeasureGraphics()
    {
        var g = Graphics.FromImage(MeasureBitmap);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        return g;
    }

    private static StringFormat CreateFormat() => new(StringFormat.GenericTypographic)
    {
        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip,
        Trimming = StringTrimming.None
    };

    private sealed class Entry
    {
        public Entry(NewsItem item, string host, string source, string title, int x, int sourceWidth, int width)
        {
            Item = item;
            Host = host;
            Source = source;
            Title = title;
            X = x;
            SourceWidth = sourceWidth;
            Width = width;
        }

        public NewsItem Item { get; }
        public string Host { get; }
        public string Source { get; }   // as drawn (maybe upper-cased)
        public string Title { get; }
        public int X { get; }
        public int SourceWidth { get; }
        public int Width { get; }   // includes the gap that separates it from the next item
    }

    private readonly List<Entry> entries = new();
    private IReadOnlyList<NewsItem> items = Array.Empty<NewsItem>();
    private Font titleFont = new("Segoe UI", 10f, FontStyle.Regular);
    private Font sourceFont = new("Segoe UI", 10f, FontStyle.Bold);
    private Font badgeFont = new("Segoe UI", 7.5f, FontStyle.Bold);
    private float fontScale = 1f;

    /// <summary>Text, icons and gaps follow the height of the bar: 1 = the normal 42 px strip.</summary>
    public void SetFontScale(float scale)
    {
        scale = Math.Clamp(scale, 0.7f, 3f);
        if (Math.Abs(scale - fontScale) < 0.01f) return;
        fontScale = scale;
        titleFont.Dispose();
        sourceFont.Dispose();
        badgeFont.Dispose();
        titleFont = new Font("Segoe UI", 10f * scale, FontStyle.Regular);
        sourceFont = new Font("Segoe UI", 10f * scale, FontStyle.Bold);
        badgeFont = new Font("Segoe UI", 7.5f * scale, FontStyle.Bold);
        BuildLayout();
        Invalidate();
    }

    private bool centerWhenStatic;

    /// <summary>A bar whose content fits stands still in the middle instead of at the left edge (the small weather bar).</summary>
    public void SetCenterWhenStatic(bool value)
    {
        centerWhenStatic = value;
        Invalidate();
    }

    private int WeatherIconSize => (int)Math.Round(22 * fontScale);
    private Bitmap? buffer;
    private int loop;
    private int textHeight = 16;
    private double offset;
    private int hoverIndex = -1;
    private int hoverCopy;
    private bool mouseInside;
    private bool showIcons = true;

    /// <summary>Raised when the item under the mouse changes. The rectangle is in screen coordinates.</summary>
    public event Action<NewsItem?, Rectangle>? HoverChanged;
    public event Action<NewsItem>? ItemClicked;

    /// <summary>Right mouse button released over the strip (used to open the settings).</summary>
    public event Action? ContextClicked;

    /// <summary>The pulsing live button at the right end of the bar was clicked.</summary>
    public event Action? LiveClicked;

    public TickerCanvas()
    {
        // No OptimizedDoubleBuffer: the frame is composed in our own alpha bitmap and copied once.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
        BackColor = Color.Black;
    }

    /// <summary>
    /// Colours of the strip. <paramref name="surfaceIsDark"/> tells which side of the contrast the text is on
    /// (used to pick readable up/down colours and favicon treatment).
    /// </summary>
    public void SetLook(Color title, Color source, Color hover, Color hoverText, bool surfaceIsDark)
    {
        titleColor = title;
        sourceColor = source;
        hoverOverlay = hover;
        hoverTitleColor = hoverText;
        darkSurface = surfaceIsDark;
        Invalidate();
    }

    /// <summary>Opaque fill behind the content (e.g. the red breaking-news state). null = transparent background.</summary>
    public void SetFill(Color? fill)
    {
        solidFill = fill;
        Invalidate();
    }

    /// <summary>Seconds into the breaking-news intro animation; a negative value ends it.</summary>
    public void SetAlertIntro(double seconds)
    {
        alertT = seconds >= AlertEnd ? -1 : seconds;
        Invalidate();
    }

    /// <summary>Show the pulsing "live" button (a live video source is available while breaking news is active).</summary>
    public void SetLiveButton(bool visible)
    {
        if (liveVisible == visible) return;
        liveVisible = visible;
        Invalidate();
    }

    /// <summary>Called by the window timer: keeps the live button pulsing even when nothing scrolls.</summary>
    public void TickAnimation()
    {
        if (!liveVisible) return;
        var now = Environment.TickCount64;
        if (now - lastPulseInvalidate < 60) return;
        lastPulseInvalidate = now;
        Invalidate(LiveRect());
    }

    private Rectangle LiveRect() => new(Width - 122, (Height - 26) / 2, 114, 26);

    /// <summary>Draw a thin vertical line at the right edge (between the two halves of a split bar).</summary>
    public void SetDivider(bool value)
    {
        if (divider == value) return;
        divider = value;
        Invalidate();
    }

    /// <summary>Fade the text out at both ends of the strip.</summary>
    public void SetEdgeFade(bool value)
    {
        if (edgeFade == value) return;
        edgeFade = value;
        Invalidate();
    }

    /// <summary>Draw sources and titles in capitals (breaking news).</summary>
    public void SetUppercase(bool value)
    {
        if (uppercase == value) return;
        uppercase = value;
        BuildLayout();
        Invalidate();
    }

    /// <summary>While true the strip lets mouse hit-tests fall through to the window (used to drag/resize it).</summary>
    public void SetPassThrough(bool value)
    {
        passThrough = value;
        editHint = value;
        Invalidate();
    }

    private const int WM_NCHITTEST = 0x84;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST && passThrough)
        {
            m.Result = new IntPtr(-1); // HTTRANSPARENT
            return;
        }
        base.WndProc(ref m);
    }

    public bool IsMouseInside() => mouseInside;
    public double GetOffset() => offset;
    public int GetLoopLength() => loop;
    public bool IsScrollable() => loop > ClientSize.Width;

    public void SetShowIcons(bool value)
    {
        if (showIcons == value) return;
        showIcons = value;
        BuildLayout();
        Invalidate();
    }

    public void SetItems(IReadOnlyList<NewsItem> newItems)
    {
        items = newItems;
        hoverIndex = -1;
        BuildLayout();

        foreach (var host in entries.Where(e => e.Item.Kind == NewsItemKind.News).Select(e => e.Host).Where(h => h.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            FaviconCache.Request(host, () =>
            {
                if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(() => Invalidate()));
            });
        }
        Invalidate();
    }

    /// <summary>Sets the scroll position. It wraps around the loop length, so any value is accepted.</summary>
    public void SetOffset(double value)
    {
        if (!double.IsFinite(value) || !IsScrollable())
        {
            offset = 0;
        }
        else
        {
            value %= loop;
            offset = value < 0 ? value + loop : value;
        }
        Invalidate();
    }

    private static int Measure(string text, Font font) =>
        (int)Math.Ceiling(MeasureGraphics.MeasureString(text, font, PointF.Empty, TextFormat).Width);

    /// <summary>Greek capitals are written without accents ("ΕΚΤΑΚΤΗ", not "ΈΚΤΑΚΤΗ").</summary>
    internal static string UpperNoAccents(string text) => Loc.Upper(text);

    private void BuildLayout()
    {
        entries.Clear();
        textHeight = (int)Math.Ceiling(titleFont.GetHeight(MeasureGraphics));

        var x = 0;
        foreach (var item in items)
        {
            var isQuote = item.Kind == NewsItemKind.Quote;
            var host = !isQuote && Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) ? uri.Host : "";
            var source = uppercase ? UpperNoAccents(item.Source) : item.Source;
            var title = uppercase ? UpperNoAccents(item.Title) : item.Title;
            var sourceWidth = Measure(source + ":", sourceFont);
            var titleWidth = Measure(title, titleFont);
            var iconWidth = item.Kind == NewsItemKind.Weather ? WeatherIconSize + IconGap : showIcons && !isQuote ? IconSize + IconGap : 0;
            var width = iconWidth + sourceWidth + SourceTitleGap + titleWidth + ItemGap;
            entries.Add(new Entry(item, host, source, title, x, sourceWidth, width));
            x += width;
        }
        loop = x;
    }

    private int Origin() => IsScrollable() ? 0 : centerWhenStatic ? Math.Max(LeftPad / 2, (Width - (loop - ItemGap)) / 2) : LeftPad;
    private int ScrollPixels() => IsScrollable() ? (int)Math.Round(offset) : 0;

    private Rectangle ItemRect(int index, int copy)
    {
        var e = entries[index];
        var left = Origin() + e.X + copy * loop - ScrollPixels() - HoverPad;
        // Right-to-left: the same layout mirrored, so the items run from the right edge and the strip moves to the right.
        if (Loc.IsRtl) left = Width - (Origin() + e.X + copy * loop - ScrollPixels()) - (e.Width - ItemGap) - HoverPad;
        return new Rectangle(left, 4, e.Width - ItemGap + 2 * HoverPad, Math.Max(1, Height - 8));
    }

    private (int Index, int Copy) HitTest(Point p)
    {
        if (liveVisible && LiveRect().Contains(p)) return (-1, 0);
        var copies = IsScrollable() ? 2 : 1;
        for (var copy = 0; copy < copies; copy++)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var r = ItemRect(i, copy);
                if (p.X >= r.Left && p.X < r.Right) return (i, copy);
            }
        }
        return (-1, 0);
    }

    /// <summary>Call after scrolling: the item under a stationary mouse changes while the strip moves.</summary>
    public void RefreshHoverFromCursor()
    {
        if (!mouseInside) return;
        var (index, copy) = HitTest(PointToClient(Cursor.Position));
        SetHover(index, copy);
    }

    private void SetHover(int index, int copy)
    {
        if (index == hoverIndex && (index < 0 || copy == hoverCopy)) return;
        hoverIndex = index;
        hoverCopy = copy;

        var clickable = index >= 0 && !string.IsNullOrEmpty(entries[index].Item.Url);
        Cursor = clickable ? Cursors.Hand : Cursors.Default;
        Invalidate();

        if (index >= 0) HoverChanged?.Invoke(entries[index].Item, RectangleToScreen(ItemRect(index, copy)));
        else HoverChanged?.Invoke(null, Rectangle.Empty);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        mouseInside = true;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        mouseInside = false;
        SetHover(-1, 0);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        mouseInside = true;
        var (index, copy) = HitTest(e.Location);
        SetHover(index, copy);
        if (liveVisible && LiveRect().Contains(e.Location)) Cursor = Cursors.Hand;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Right)
        {
            ContextClicked?.Invoke();
            return;
        }
        if (e.Button == MouseButtons.Left && liveVisible && LiveRect().Contains(e.Location))
        {
            LiveClicked?.Invoke();
            return;
        }
        if (e.Button != MouseButtons.Left || hoverIndex < 0 || hoverIndex >= entries.Count) return;
        var item = entries[hoverIndex].Item;
        if (!string.IsNullOrEmpty(item.Url)) ItemClicked?.Invoke(item);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Everything is painted in OnPaint.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 0 || Height <= 0) return;
        if (buffer is null || buffer.Width != Width || buffer.Height != Height)
        {
            buffer?.Dispose();
            buffer = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        }

        using (var g = Graphics.FromImage(buffer))
        {
            g.Clear(FillColor());
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            DrawContent(g);
            if (liveVisible && alertT < 0) DrawLiveButton(g);
            if (toastText is not null && Environment.TickCount64 < toastUntil) DrawToast(g);
        }

        if (edgeFade && !editHint && IsScrollable() && solidFill is null) ApplyEdgeFade(buffer);

        if (divider)
        {
            using var g2 = Graphics.FromImage(buffer);
            using var pen = new Pen(Color.FromArgb(110, titleColor), 1f);
            g2.DrawLine(pen, Width - 1, 8, Width - 1, Height - 8);
        }

        // Copy, don't blend: the alpha channel of the frame goes to the window as it is.
        e.Graphics.CompositingMode = CompositingMode.SourceCopy;
        e.Graphics.DrawImageUnscaled(buffer, 0, 0);
    }

    private string? toastText;
    private long toastUntil;

    /// <summary>A short message in the middle of the bar (about 2 seconds), e.g. "saved to read later".</summary>
    public void ShowToast(string text)
    {
        toastText = text;
        toastUntil = Environment.TickCount64 + 2000;
        Invalidate();
    }

    private void DrawToast(Graphics g)
    {
        using var font = new Font("Segoe UI Semibold", 10f);
        var size = g.MeasureString(toastText!, font);
        var rect = new RectangleF((Width - size.Width) / 2f - 14, (Height - size.Height) / 2f - 3, size.Width + 28, size.Height + 6);
        using var path = RoundedRect(Rectangle.Round(rect), 8);
        using var back = new SolidBrush(Color.FromArgb(235, 24, 24, 28));
        g.FillPath(back, path);
        using var text = new SolidBrush(Color.White);
        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(toastText!, font, text, rect, center);
    }

    private Color FillColor()
    {
        if (solidFill is not { } c) return Color.Transparent;
        // The red fades in during the first 0.4 s of the intro.
        var a = alertT >= 0 ? Math.Clamp(alertT / 0.4, 0, 1) : 1;
        return Color.FromArgb((int)(c.A * a), c);
    }

    private static double Ease(double x) => 1 - Math.Pow(1 - Math.Clamp(x, 0, 1), 3);

    /// <summary>
    /// The intro: from the middle of the bar the app icon grows, then fades while the words "BREAKING NEWS" appear and
    /// fade; only after that the red ticker with the urgent headlines shows.
    /// </summary>
    private void DrawAlertIntro(Graphics g)
    {
        var cx = Width / 2f;
        var cy = Height / 2f;

        // expanding ring behind the icon
        var ringT = (alertT - 0.15) / 1.1;
        if (ringT is > 0 and < 1)
        {
            var radius = (float)(Ease(ringT) * Height * 1.6);
            using var ring = new Pen(Color.FromArgb((int)(180 * (1 - ringT)), Color.White), 2f);
            g.DrawEllipse(ring, cx - radius, cy - radius, radius * 2, radius * 2);
        }

        // the app icon, growing from the centre
        var iconAlpha = alertT < 1.5 ? 1.0 : 1 - (alertT - 1.5) / 0.6;
        if (iconAlpha > 0)
        {
            alertIcon ??= LoadIcon();
            var size = (float)(6 + Ease((alertT - 0.15) / 0.9) * (Height - 4 - 6));
            var rect = new RectangleF(cx - size / 2, cy - size / 2, size, size);
            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(new ColorMatrix { Matrix33 = (float)Math.Clamp(iconAlpha, 0, 1) });
            g.DrawImage(alertIcon, Rectangle.Round(rect), 0, 0, alertIcon.Width, alertIcon.Height, GraphicsUnit.Pixel, attrs);
        }

        // the words
        var textAlpha = alertT < 3.1 ? (alertT - 1.7) / 0.5 : 1 - (alertT - 3.1) / 0.4;
        textAlpha = Math.Clamp(textAlpha, 0, 1);
        if (textAlpha > 0)
        {
            using var font = new Font("Segoe UI", 15f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb((int)(255 * textAlpha), Color.White));
            using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(UpperNoAccents(Loc.T("breaking.title")), font, brush, new RectangleF(0, 0, Width, Height), center);
        }
    }

    private static Bitmap LoadIcon()
    {
        using var icon = new Icon(AppIcon.Get(), 64, 64);
        return icon.ToBitmap();
    }

    private void DrawLiveButton(Graphics g)
    {
        var r = LiveRect();
        using (var path = RoundedRect(r, 13))
        using (var back = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
            g.FillPath(back, path);

        // the dot pulses so the button catches the eye without taking over
        var pulse = 0.55 + 0.45 * Math.Sin(Environment.TickCount64 / 260.0);
        using (var dot = new SolidBrush(Color.FromArgb((int)(255 * pulse), 200, 20, 20)))
            g.FillEllipse(dot, r.X + 12, r.Y + r.Height / 2f - 5, 10, 10);

        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var text = new SolidBrush(Color.FromArgb(185, 20, 20));
        using var left = new StringFormat { LineAlignment = StringAlignment.Center };
        g.DrawString(UpperNoAccents(Loc.T("live.button")), font, text, new RectangleF(r.X + 28, r.Y, r.Width - 30, r.Height), left);
    }

    private void DrawContent(Graphics g)
    {
        if (alertT >= 0 && alertT < AlertContentAt)
        {
            DrawAlertIntro(g);
            return;
        }
        if (editHint)
        {
            using var pen = new Pen(titleColor) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            using var brush = new SolidBrush(titleColor);
            using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(Loc.T("hint.edit"), titleFont, brush, ClientRectangle, center);
            return;
        }
        if (entries.Count == 0) return;

        var y = (Height - textHeight) / 2f;
        var copies = IsScrollable() ? 2 : 1;
        for (var copy = 0; copy < copies; copy++)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var left = Origin() + entry.X + copy * loop - ScrollPixels();
                if (left > Width || left + entry.Width < 0) continue;
                DrawEntry(g, entry, left, y, i == hoverIndex && copy == hoverCopy, ItemRect(i, copy));
            }
        }
    }

    private Color TrendColor(int trend)
    {
        if (trend > 0) return darkSurface ? Color.FromArgb(110, 222, 142) : Color.FromArgb(18, 130, 60);
        if (trend < 0) return darkSurface ? Color.FromArgb(255, 112, 112) : Color.FromArgb(190, 28, 28);
        return titleColor;
    }

    private void DrawEntry(Graphics g, Entry entry, int left, float textY, bool hovered, Rectangle highlight)
    {
        if (hovered)
        {
            using var path = RoundedRect(highlight, 6);
            using var brush = new SolidBrush(hoverOverlay);
            g.FillPath(brush, path);
        }

        var isQuote = entry.Item.Kind == NewsItemKind.Quote;
        if (Loc.IsRtl)
        {
            DrawEntryRtl(g, entry, left, textY, hovered, isQuote);
            return;
        }
        float x = left;
        if (entry.Item.Kind == NewsItemKind.Weather)
        {
            WeatherIcons.Draw(g, new Rectangle(left, (Height - WeatherIconSize) / 2, WeatherIconSize, WeatherIconSize), entry.Item.Icon, darkSurface);
            x += WeatherIconSize + IconGap;
        }
        else if (showIcons && !isQuote)
        {
            DrawIcon(g, entry, new Rectangle(left, (Height - IconSize) / 2, IconSize, IconSize));
            x += IconSize + IconGap;
        }

        using (var sourceBrush = new SolidBrush(sourceColor))
            g.DrawString(entry.Source + ":", sourceFont, sourceBrush, x, textY, TextFormat);
        x += entry.SourceWidth + SourceTitleGap;

        var color = isQuote ? TrendColor(entry.Item.Trend) : hovered ? hoverTitleColor : titleColor;
        using var titleBrush = new SolidBrush(color);
        g.DrawString(entry.Title, titleFont, titleBrush, x, textY, TextFormat);
    }

    /// <summary>Mirrored entry: the icon is at the right end, then the source, then the title towards the left.</summary>
    private void DrawEntryRtl(Graphics g, Entry entry, int logicalLeft, float textY, bool hovered, bool isQuote)
    {
        var contentWidth = entry.Width - ItemGap;
        var x = (float)(Width - logicalLeft); // right edge of the entry
        var isWeather = entry.Item.Kind == NewsItemKind.Weather;
        var iconSize = isWeather ? WeatherIconSize : IconSize;
        var hasIcon = isWeather || showIcons && !isQuote;
        if (hasIcon)
        {
            var iconRect = new Rectangle((int)x - iconSize, (Height - iconSize) / 2, iconSize, iconSize);
            if (isWeather) WeatherIcons.Draw(g, iconRect, entry.Item.Icon, darkSurface);
            else DrawIcon(g, entry, iconRect);
            x -= iconSize + IconGap;
        }

        var sourceWidth = entry.SourceWidth;
        using (var sourceBrush = new SolidBrush(sourceColor))
            g.DrawString(entry.Source + ":", sourceFont, sourceBrush, new RectangleF(x - sourceWidth - 4, textY, sourceWidth + 8, textHeight + 2), TextFormatRtl);
        x -= sourceWidth + SourceTitleGap;

        var titleWidth = Math.Max(10, contentWidth - (hasIcon ? iconSize + IconGap : 0) - sourceWidth - SourceTitleGap);
        var color = isQuote ? TrendColor(entry.Item.Trend) : hovered ? hoverTitleColor : titleColor;
        using var titleBrush = new SolidBrush(color);
        g.DrawString(entry.Title, titleFont, titleBrush, new RectangleF(x - titleWidth - 4, textY, titleWidth + 8, textHeight + 2), TextFormatRtl);
    }

    private void DrawIcon(Graphics g, Entry entry, Rectangle rect)
    {
        var icon = entry.Host.Length > 0 ? FaviconCache.TryGetFor(entry.Host, darkSurface) : null;
        if (icon is not null)
        {
            g.DrawImage(icon, rect);
            return;
        }

        var name = entry.Item.Source;
        var hash = 0;
        foreach (var ch in name) hash = (hash * 31 + ch) & 0x7fffffff;
        using var brush = new SolidBrush(BadgeColors[hash % BadgeColors.Length]);
        g.FillEllipse(brush, rect);

        var letter = name.FirstOrDefault(char.IsLetterOrDigit);
        if (letter != default)
        {
            using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(char.ToUpperInvariant(letter).ToString(), badgeFont, Brushes.White, rect, center);
        }
    }

    /// <summary>Multiplies the pixels near the left and right end by a ramp 0..1 (the frame is premultiplied, so this is a true fade).</summary>
    private static void ApplyEdgeFade(Bitmap bmp)
    {
        var fade = Math.Min(FadeWidth, bmp.Width / 3);
        if (fade < 4) return;
        FadeRegion(bmp, new Rectangle(0, 0, fade, bmp.Height), leftEdge: true);
        FadeRegion(bmp, new Rectangle(bmp.Width - fade, 0, fade, bmp.Height), leftEdge: false);
    }

    private static void FadeRegion(Bitmap bmp, Rectangle area, bool leftEdge)
    {
        var data = bmp.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        try
        {
            // Copy row by row, only the width of the region: the last row of the bitmap has no bytes after it.
            var rowBytes = area.Width * 4;
            var row = new byte[rowBytes];
            for (var y = 0; y < area.Height; y++)
            {
                var rowPtr = IntPtr.Add(data.Scan0, y * data.Stride);
                Marshal.Copy(rowPtr, row, 0, rowBytes);
                for (var x = 0; x < area.Width; x++)
                {
                    var t = (x + 0.5f) / area.Width;           // 0 at the outer end of the strip, 1 at the inner end
                    var factor = leftEdge ? t : 1 - t;
                    factor *= factor;                          // gentle start
                    var i = x * 4;
                    row[i] = (byte)(row[i] * factor);
                    row[i + 1] = (byte)(row[i + 1] * factor);
                    row[i + 2] = (byte)(row[i + 2] * factor);
                    row[i + 3] = (byte)(row[i + 3] * factor);
                }
                Marshal.Copy(row, 0, rowPtr, rowBytes);
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            titleFont.Dispose();
            sourceFont.Dispose();
            badgeFont.Dispose();
            buffer?.Dispose();
            alertIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
