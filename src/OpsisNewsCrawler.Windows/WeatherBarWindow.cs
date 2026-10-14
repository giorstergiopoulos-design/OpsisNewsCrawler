using System.Drawing.Drawing2D;
using System.Drawing.Text;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The small weather bar for the desktop: icon, city, temperature with one decimal, wind speed and direction, humidity.
/// Drag it to move it; click it for the next selected city; right-click opens the settings.
/// </summary>
internal sealed class WeatherBarWindow : Form
{
    private const int BarWidth = 400;
    private const int BarHeight = 56;

    private GreekCity? city;
    private WeatherReading? reading;
    private Point dragStart;
    private Point windowStart;
    private bool dragging;
    private bool pressed;

    /// <summary>Raised after the user dragged the bar somewhere (screen position of its top-left corner).</summary>
    public event Action<Point>? Moved;
    public event Action? NextCityRequested;
    public event Action? SettingsRequested;

    public WeatherBarWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(BarWidth, BarHeight);
        Text = "Opsis News Crawler";
        Icon = AppIcon.Get();
        DoubleBuffered = true;
        BackColor = Theme.Surface;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Region = new Region(RoundedPath(new Rectangle(0, 0, BarWidth, BarHeight), 14));
        Theme.Changed += OnThemeChanged;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080 | 0x08000000; // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.MarkNoSnap(Handle);
    }

    private void OnThemeChanged()
    {
        BackColor = Theme.Surface;
        Invalidate();
    }

    public void SetOnTop(bool onTop) => TopMost = onTop;

    public void Place(int x, int y)
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.AllScreens[0].WorkingArea;
        if (x == int.MinValue || y == int.MinValue || !Screen.AllScreens.Any(s => s.WorkingArea.Contains(x + 20, y + 20)))
        {
            x = area.Right - BarWidth - 24;
            y = area.Top + 24;
        }
        Location = new Point(x, y);
    }

    public void SetReading(GreekCity? newCity, WeatherReading? newReading)
    {
        city = newCity;
        reading = newReading;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        using (var border = new Pen(Theme.Border))
        using (var path = RoundedPath(new Rectangle(0, 0, BarWidth - 1, BarHeight - 1), 14))
            g.DrawPath(border, path);

        var rtl = Loc.IsRtl;
        var greek = Loc.IsGreek;
        if (city is null || reading is null)
        {
            using var wait = new SolidBrush(Theme.TextMuted);
            using var f = new Font("Segoe UI", 10f);
            using var sfWait = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };
            g.DrawString(Loc.T("ph.loadingWeather"), f, wait, new RectangleF(0, 0, BarWidth, BarHeight), sfWait);
            return;
        }

        var item = WeatherInfo.ToItem(city, reading, greek);
        var iconRect = new Rectangle(rtl ? BarWidth - 14 - 36 : 14, (BarHeight - 36) / 2, 36, 36);
        WeatherIcons.Draw(g, iconRect, item.Icon, Theme.IsDark);

        using var name = new Font("Segoe UI Semibold", 11f);
        using var temp = new Font("Segoe UI Semibold", 16f);
        using var small = new Font("Segoe UI", 8.5f);
        using var text = new SolidBrush(Theme.Text);
        using var muted = new SolidBrush(Theme.TextMuted);
        var culture = System.Globalization.CultureInfo.GetCultureInfo("el-GR");
        var tempText = reading.TemperatureC.ToString("0.0", culture) + " °C";
        var detail = Loc.T("weather.wind") + " " + reading.WindKmh.ToString("0", culture) + " km/h " + Loc.T(CompassKey(reading.WindFromDegrees))
            + "  ·  " + Loc.T("weather.humidity") + " " + reading.HumidityPercent + "%";

        var textLeft = rtl ? 150f : 60f;
        var textRight = rtl ? BarWidth - 60f : BarWidth - 150f;
        var tempLeft = rtl ? 12f : BarWidth - 140f;
        using var fmt = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
        if (rtl) fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        g.DrawString(item.Source, name, text, new RectangleF(textLeft, 7, textRight - textLeft + 80, 22), fmt);
        g.DrawString(detail, small, muted, new RectangleF(textLeft, 31, rtl ? textRight - textLeft : BarWidth - 60f, 18), fmt);
        using var tf = new StringFormat(StringFormatFlags.NoWrap) { Alignment = rtl ? StringAlignment.Near : StringAlignment.Far, LineAlignment = StringAlignment.Center };
        g.DrawString(tempText, temp, text, new RectangleF(tempLeft, 0, 128, BarHeight), tf);
    }

    private static string CompassKey(int degrees) => new[] { "wind.n", "wind.ne", "wind.e", "wind.se", "wind.s", "wind.sw", "wind.w", "wind.nw" }[WeatherInfo.CompassIndex(degrees)];

    // ---- mouse: drag to move, click for the next city, right-click for the settings --------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        pressed = true;
        dragging = false;
        dragStart = Cursor.Position;
        windowStart = Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!pressed) return;
        var now = Cursor.Position;
        if (!dragging && (Math.Abs(now.X - dragStart.X) > 4 || Math.Abs(now.Y - dragStart.Y) > 4)) dragging = true;
        if (dragging) Location = new Point(windowStart.X + now.X - dragStart.X, windowStart.Y + now.Y - dragStart.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right)
        {
            SettingsRequested?.Invoke();
            return;
        }
        if (!pressed) return;
        pressed = false;
        if (dragging) Moved?.Invoke(Location);
        else NextCityRequested?.Invoke();
        dragging = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Theme.Changed -= OnThemeChanged;
        base.Dispose(disposing);
    }

    private static GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
