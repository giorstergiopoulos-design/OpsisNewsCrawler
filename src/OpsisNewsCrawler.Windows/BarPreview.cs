using System.Drawing.Drawing2D;
using System.Drawing.Text;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>What the preview shows: the choices of the settings window as they are right now (not yet applied).</summary>
internal sealed record BarPreviewState(
    TickerPosition Position, BarContent Main, bool StocksLeft, bool SecondEnabled, BarContent Second,
    bool Dark, int OpacityPercent, bool EdgeFade,
    string BarColorMode, int BarCustom, string TaskbarColorMode, int TaskbarCustom,
    string BarBackdrop, string TaskbarBackdrop);

/// <summary>
/// A small mock desktop (wallpaper, taskbar with a few apps and the tray) with the bars drawn on it the way the choices
/// of the settings window would show them: where the bar sits, what it carries (news, prices, both), its colour, transparency,
/// background material and the fading ends. Purely drawn, no windows and no network.
/// </summary>
internal sealed class BarPreview : Control
{
    private const int TaskbarHeight = 30;
    private const int StripHeight = 24;
    private BarPreviewState? state;

    public BarPreview()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 128;
    }

    public void SetState(BarPreviewState next)
    {
        if (next == state) return;
        state = next;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Background);
        if (state is null) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var title = Loc.T("preview.title");
        using (var titleFont = new Font("Segoe UI Semibold", 8.5f))
        using (var muted = new SolidBrush(Theme.Accent))
            g.DrawString(Loc.Upper(title), titleFont, muted, 4, 2);

        var desk = new Rectangle(0, 20, Width - 1, Height - 21);
        using (var path = Round(desk, 10))
        {
            g.SetClip(path);
            DrawWallpaper(g, desk);
            DrawDesktop(g, desk, state);
            g.ResetClip();
            using var border = new Pen(Theme.Border);
            g.DrawPath(border, path);
        }
    }

    private static void DrawWallpaper(Graphics g, Rectangle r)
    {
        using var brush = new LinearGradientBrush(r, Color.FromArgb(28, 52, 92), Color.FromArgb(116, 74, 138), 35f);
        g.FillRectangle(brush, r);
        using var glow = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
        g.FillEllipse(glow, r.Right - r.Width / 3, r.Top - r.Height / 3, r.Width / 2, r.Height / 1);
    }

    private void DrawDesktop(Graphics g, Rectangle desk, BarPreviewState s)
    {
        var taskbar = new Rectangle(desk.Left, desk.Bottom - TaskbarHeight, desk.Width, TaskbarHeight);
        var style = TaskbarStyle.Read();
        var tbTint = s.Dark == !style.Light ? style.Tint : (s.Dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243));
        using (var tb = new SolidBrush(Color.FromArgb(225, tbTint))) g.FillRectangle(tb, taskbar);

        // Start button, a few apps, and the tray on the right.
        var iconColor = s.Dark ? Color.FromArgb(220, 255, 255, 255) : Color.FromArgb(200, 40, 40, 40);
        using var icon = new SolidBrush(iconColor);
        var x = taskbar.Left + 10;
        g.FillRectangle(icon, x, taskbar.Top + 9, 5, 5); g.FillRectangle(icon, x + 7, taskbar.Top + 9, 5, 5);
        g.FillRectangle(icon, x, taskbar.Top + 16, 5, 5); g.FillRectangle(icon, x + 7, taskbar.Top + 16, 5, 5);
        x += 26;
        var appColors = new[] { Color.FromArgb(0, 120, 212), Color.FromArgb(232, 140, 30), Color.FromArgb(60, 170, 90), Color.FromArgb(200, 60, 80), Color.FromArgb(130, 100, 200) };
        foreach (var c in appColors)
        {
            using var b = new SolidBrush(c);
            using var p = Round(new Rectangle(x, taskbar.Top + 7, 16, 16), 4);
            g.FillPath(b, p);
            x += 24;
        }
        var trayWidth = 78;
        for (var i = 0; i < 3; i++) g.FillEllipse(icon, taskbar.Right - trayWidth + i * 12, taskbar.Top + 11, 8, 8);
        using (var clockFont = new Font("Segoe UI", 7f))
        using (var clock = new SolidBrush(iconColor))
            g.DrawString("14:05", clockFont, clock, taskbar.Right - 40, taskbar.Top + 9);

        var freeSpan = new Rectangle(x + 8, taskbar.Top + 3, Math.Max(40, taskbar.Right - trayWidth - 12 - (x + 8)), TaskbarHeight - 6);

        // The main bar.
        var mainRect = s.Position switch
        {
            TickerPosition.Top => new Rectangle(desk.Left, desk.Top, desk.Width, StripHeight),
            TickerPosition.Center => new Rectangle(desk.Left, desk.Top + (taskbar.Top - desk.Top - StripHeight) / 2, desk.Width, StripHeight),
            TickerPosition.Custom => new Rectangle(desk.Left + desk.Width / 6, desk.Top + (taskbar.Top - desk.Top) / 4, desk.Width * 2 / 3, StripHeight),
            TickerPosition.TaskbarIntegrated => freeSpan,
            _ => new Rectangle(desk.Left, taskbar.Top - StripHeight, desk.Width, StripHeight)
        };
        var integratedMain = s.Position == TickerPosition.TaskbarIntegrated;
        DrawStrip(g, mainRect, s.Main, s.StocksLeft, integratedMain, integratedMain ? s.TaskbarColorMode : s.BarColorMode,
            integratedMain ? s.TaskbarCustom : s.BarCustom, integratedMain ? s.TaskbarBackdrop : s.BarBackdrop, s, tbTint, style);
        Caption(g, mainRect, Loc.T("card.mainBar"), integratedMain ? freeSpan : mainRect, integratedMain);

        // The second bar always lives inside the taskbar.
        if (s.SecondEnabled && !integratedMain)
        {
            DrawStrip(g, freeSpan, s.Second, false, true, s.TaskbarColorMode, s.TaskbarCustom, s.TaskbarBackdrop, s, tbTint, style);
            Caption(g, freeSpan, Loc.T("card.secondBar"), freeSpan, true);
        }
    }

    private static void Caption(Graphics g, Rectangle strip, string text, Rectangle around, bool inTaskbar)
    {
        // A tiny label next to the bar so it is clear which one is which.
        using var font = new Font("Segoe UI", 7f);
        var size = g.MeasureString(text, font);
        var y = inTaskbar ? around.Top - size.Height - 1 : strip.Top - size.Height - 1;
        if (y < 22) y = strip.Bottom + 1;
        using var back = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
        g.FillRectangle(back, around.Left + 3, y, size.Width, size.Height);
        g.DrawString(text, font, Brushes.White, around.Left + 3, y);
    }

    private void DrawStrip(Graphics g, Rectangle r, BarContent content, bool stocksLeft, bool inTaskbar, string colorMode, int custom,
        string backdrop, BarPreviewState s, Color taskbarTint, TaskbarStyle style)
    {
        // Background of the strip.
        Color? fill = colorMode switch
        {
            "lightblue" => Color.FromArgb(176, 196, 214),
            "accent" => TaskbarStyle.AccentColor(),
            "custom" => Color.FromArgb(255, Color.FromArgb(custom)),
            "theme" => s.Dark ? Color.FromArgb(24, 24, 28) : Color.FromArgb(245, 245, 248),
            _ => null // "taskbar": completely clear, the taskbar shows through
        };
        var alpha = (int)Math.Round(255 * Math.Clamp(s.OpacityPercent / 100.0, 0.3, 1.0));
        Color textColor;
        if (fill is { } f)
        {
            using var path = Round(r, inTaskbar ? 6 : 0);
            if (backdrop == "gradient")
            {
                using var gb = new LinearGradientBrush(r, Color.FromArgb(alpha, f), Color.FromArgb(Math.Max(40, alpha - 90), f), 90f);
                g.FillPath(gb, path);
            }
            else
            {
                using var b = new SolidBrush(Color.FromArgb(backdrop == "blur" ? Math.Min(255, alpha) : Math.Min(255, alpha + 10), f));
                g.FillPath(b, path);
            }
            textColor = Luminance(f) > 0.6 ? Color.FromArgb(24, 26, 30) : Color.White;
        }
        else
        {
            textColor = style.Text;
            if (s.Dark != !style.Light) textColor = s.Dark ? Color.White : Color.FromArgb(27, 27, 27);
        }

        // The text.
        using var font = new Font("Segoe UI", 8f);
        using var bold = new Font("Segoe UI Semibold", 8f);
        using var textBrush = new SolidBrush(textColor);
        using var muted = new SolidBrush(Color.FromArgb(170, textColor));
        var green = new SolidBrush(Color.FromArgb(46, 190, 100));
        var red = new SolidBrush(Color.FromArgb(235, 80, 80));
        var sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        void News(Rectangle area)
        {
            g.SetClip(area, CombineMode.Intersect);
            var xx = area.Left + 6f;
            var sample = Loc.T("preview.sampleNews");
            for (var i = 0; i < 4 && xx < area.Right; i++)
            {
                g.FillEllipse(muted, xx, area.Top + area.Height / 2f - 3, 6, 6);
                xx += 10;
                g.DrawString(sample, font, textBrush, new RectangleF(xx, area.Top, 400, area.Height), sf);
                xx += g.MeasureString(sample, font).Width + 18;
            }
            g.ResetClip();
        }

        void Quotes(Rectangle area)
        {
            g.SetClip(area, CombineMode.Intersect);
            var xx = area.Left + 6f;
            var rows = new (string Name, string Price, string Change, bool Up)[]
            {
                ("ALPHA", "4,29 €", "+1,7%", true), ("ETE", "11,02 €", "-0,4%", false), ("Gold", "2.318 $", "+0,3%", true),
                ("Brent", "82,4 $", "-1,1%", false), ("OPAP", "17,5 €", "+0,9%", true), ("PPC", "13,9 €", "+2,1%", true)
            };
            foreach (var row in rows)
            {
                if (xx >= area.Right) break;
                g.DrawString(row.Name, bold, textBrush, new RectangleF(xx, area.Top, 200, area.Height), sf);
                xx += g.MeasureString(row.Name, bold).Width + 3;
                g.DrawString(row.Price, font, textBrush, new RectangleF(xx, area.Top, 200, area.Height), sf);
                xx += g.MeasureString(row.Price, font).Width + 3;
                g.DrawString(row.Change, bold, row.Up ? green : red, new RectangleF(xx, area.Top, 200, area.Height), sf);
                xx += g.MeasureString(row.Change, bold).Width + 16;
            }
            g.ResetClip();
        }

        void Weather(Rectangle area)
        {
            g.SetClip(area, CombineMode.Intersect);
            var xx = area.Left + 6f;
            var samples = new[] { (City: "athens", Code: 1, Temp: 16.6, Wind: 15.0, Dir: 0, Hum: 68), (City: "thessaloniki", Code: 61, Temp: 14.2, Wind: 23.0, Dir: 45, Hum: 81), (City: "heraklion", Code: 0, Temp: 19.0, Wind: 9.0, Dir: 315, Hum: 62) };
            foreach (var s in samples)
            {
                if (xx >= area.Right) break;
                var city = GreekCities.Find(s.City)!;
                var item = WeatherInfo.ToItem(city, new WeatherReading(s.City, s.Temp, s.Hum, s.Wind, s.Dir, s.Code, true, DateTimeOffset.Now), Loc.IsGreek);
                var size = Math.Min(area.Height - 4, 20);
                WeatherIcons.Draw(g, new Rectangle((int)xx, area.Top + (area.Height - size) / 2, size, size), item.Icon, textColor.GetBrightness() > 0.5f);
                xx += size + 5;
                g.DrawString(item.Source, bold, textBrush, new RectangleF(xx, area.Top, 300, area.Height), sf);
                xx += g.MeasureString(item.Source, bold).Width + 3;
                g.DrawString(item.Title, font, textBrush, new RectangleF(xx, area.Top, 600, area.Height), sf);
                xx += g.MeasureString(item.Title, font).Width + 24;
            }
            g.ResetClip();
        }

        switch (content)
        {
            case BarContent.Weather: Weather(r); break;
            case BarContent.News: News(r); break;
            case BarContent.Stocks: Quotes(r); break;
            default:
                var half = r.Width / 2;
                var left = new Rectangle(r.Left, r.Top, half, r.Height);
                var right = new Rectangle(r.Left + half, r.Top, r.Width - half, r.Height);
                if (stocksLeft) { Quotes(left); News(right); } else { News(left); Quotes(right); }
                using (var divider = new Pen(Color.FromArgb(120, textColor))) g.DrawLine(divider, r.Left + half, r.Top + 4, r.Left + half, r.Bottom - 4);
                break;
        }

        // Faded ends: the same colour as the bar (or the taskbar) painted over the text.
        if (s.EdgeFade)
        {
            var edge = fill is { } ff ? Color.FromArgb(alpha, ff) : Color.FromArgb(225, taskbarTint);
            var w = Math.Min(36, r.Width / 5);
            using var l = new LinearGradientBrush(new Rectangle(r.Left, r.Top, w + 1, r.Height), edge, Color.FromArgb(0, edge), 0f);
            using var rr = new LinearGradientBrush(new Rectangle(r.Right - w, r.Top, w + 1, r.Height), Color.FromArgb(0, edge), edge, 0f);
            g.FillRectangle(l, r.Left, r.Top, w, r.Height);
            g.FillRectangle(rr, r.Right - w, r.Top, w, r.Height);
        }
        green.Dispose();
        red.Dispose();
        sf.Dispose();
    }

    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;

    private static GraphicsPath Round(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0) { path.AddRectangle(r); return path; }
        var d = radius * 2;
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
