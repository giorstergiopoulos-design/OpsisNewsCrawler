using System.Drawing.Drawing2D;
using System.Globalization;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

internal static class ChartColors
{
    public static readonly Color Up = Color.FromArgb(46, 170, 98);
    public static readonly Color Down = Color.FromArgb(224, 76, 76);
}

/// <summary>Candlestick chart of daily candles with the 20- and 50-day averages. Hover shows the day's open, high, low, close.</summary>
internal sealed class CandleChart : Control
{
    private IReadOnlyList<Candle> candles = Array.Empty<Candle>();
    private int hover = -1;
    private string emptyText = "";

    public CandleChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9f);
        MinimumSize = new Size(300, 240);
    }

    /// <summary>All stored candles; <paramref name="show"/> says how many of the newest ones are drawn (the averages use all).</summary>
    public void SetData(IReadOnlyList<Candle> all, int show, string empty)
    {
        candles = all;
        Shown = Math.Max(5, show);
        emptyText = empty;
        hover = -1;
        Invalidate();
    }

    private int Shown { get; set; } = 60;

    /// <summary>Bollinger bands: the 20-day average plus and minus two standard deviations.</summary>
    public bool ShowBands { get; set; }

    private (double Upper, double Lower)? BandAt(int index)
    {
        if (index + 1 < 20) return null;
        double sum = 0;
        for (var i = index - 19; i <= index; i++) sum += (double)candles[i].Close;
        var mean = sum / 20;
        double sq = 0;
        for (var i = index - 19; i <= index; i++) { var d = (double)candles[i].Close - mean; sq += d * d; }
        var sd = Math.Sqrt(sq / 20);
        return (mean + 2 * sd, mean - 2 * sd);
    }

    private static double? SmaAt(IReadOnlyList<Candle> c, int index, int period)
    {
        if (index + 1 < period) return null;
        double sum = 0;
        for (var i = index - period + 1; i <= index; i++) sum += (double)c[i].Close;
        return sum / period;
    }

    /// <summary>A second panel under the price with the MACD lines and histogram.</summary>
    public bool ShowMacd { get; set; }

    private Rectangle Plot
    {
        get
        {
            var h = Math.Max(10, Height - 14 - 30);
            return new(70, 14, Math.Max(10, Width - 70 - 14), ShowMacd ? (int)(h * 0.68) : h);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var count = Math.Min(Shown, candles.Count);
        if (count == 0) return;
        var plot = Plot;
        var slot = plot.Width / (double)count;
        var i = (int)((e.X - plot.Left) / slot);
        var h = i >= 0 && i < count ? candles.Count - count + i : -1;
        if (h == hover) return;
        hover = h;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = -1;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Surface);
        using var border = new Pen(Theme.Border);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        var count = Math.Min(Shown, candles.Count);
        if (count < 1)
        {
            TextRenderer.DrawText(g, emptyText, Font, new Rectangle(20, 20, Width - 40, Height - 40), Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return;
        }
        var first = candles.Count - count;
        var plot = Plot;
        double lo = double.MaxValue, hi = double.MinValue;
        for (var i = first; i < candles.Count; i++)
        {
            lo = Math.Min(lo, (double)candles[i].Low);
            hi = Math.Max(hi, (double)candles[i].High);
            foreach (var p in new[] { 20, 50 }) if (SmaAt(candles, i, p) is { } v) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            if (ShowBands && BandAt(i) is { } band) { lo = Math.Min(lo, band.Lower); hi = Math.Max(hi, band.Upper); }
        }
        if (hi <= lo) { hi = lo + Math.Max(0.01, Math.Abs(lo) * 0.01); lo -= (hi - lo) * 0.5; }
        var pad = (hi - lo) * 0.06; lo -= pad; hi += pad;
        float Y(double v) => plot.Bottom - (float)((v - lo) / (hi - lo) * plot.Height);

        // grid and price labels
        var decimals = hi < 10 ? 3 : hi < 1000 ? 2 : 0;
        using (var grid = new Pen(Color.FromArgb(60, Theme.Border), 1) { DashStyle = DashStyle.Dot })
            for (var k = 0; k <= 4; k++)
            {
                var v = lo + (hi - lo) * k / 4;
                var y = Y(v);
                g.DrawLine(grid, plot.Left, y, plot.Right, y);
                TextRenderer.DrawText(g, v.ToString("N" + decimals, CultureInfo.CurrentCulture), Font, new Rectangle(2, (int)y - 9, plot.Left - 6, 18), Theme.TextMuted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

        // Bollinger bands behind everything else
        if (ShowBands)
        {
            var up = new List<PointF>(); var down = new List<PointF>();
            for (var i = first; i < candles.Count; i++)
                if (BandAt(i) is { } band)
                {
                    var bx = plot.Left + (float)((i - first + 0.5) * plot.Width / count);
                    up.Add(new PointF(bx, Y(band.Upper))); down.Add(new PointF(bx, Y(band.Lower)));
                }
            if (up.Count > 1)
            {
                var poly = up.Concat(Enumerable.Reverse(down)).ToArray();
                using (var fillBrush = new SolidBrush(Color.FromArgb(34, 160, 120, 230))) g.FillPolygon(fillBrush, poly);
                using var bandPen = new Pen(Color.FromArgb(150, 160, 120, 230), 1.2f);
                g.DrawLines(bandPen, up.ToArray());
                g.DrawLines(bandPen, down.ToArray());
            }
        }

        // averages
        foreach (var (period, color) in new[] { (20, Color.FromArgb(230, 170, 40)), (50, Color.FromArgb(120, 150, 240)) })
        {
            var pts = new List<PointF>();
            for (var i = first; i < candles.Count; i++)
                if (SmaAt(candles, i, period) is { } v) pts.Add(new PointF(plot.Left + (float)((i - first + 0.5) * plot.Width / count), Y(v)));
            if (pts.Count > 1) using (var pen = new Pen(color, 1.6f)) g.DrawLines(pen, pts.ToArray());
        }

        // candles
        var slot = plot.Width / (double)count;
        var body = (float)Math.Clamp(slot * 0.64, 2, 24);
        for (var i = first; i < candles.Count; i++)
        {
            var c = candles[i];
            var x = plot.Left + (float)((i - first + 0.5) * slot);
            var color = c.Close >= c.Open ? ChartColors.Up : ChartColors.Down;
            using var pen = new Pen(c.Exact ? color : Color.FromArgb(150, color), 1.4f);
            g.DrawLine(pen, x, Y((double)c.High), x, Y((double)c.Low));
            var top = Y((double)Math.Max(c.Open, c.Close)); var bottom = Y((double)Math.Min(c.Open, c.Close));
            var rect = new RectangleF(x - body / 2, top, body, Math.Max(1.5f, bottom - top));
            if (c.Exact) { using var brush = new SolidBrush(color); g.FillRectangle(brush, rect); }
            else g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);   // hollow: only sampled, not the exact open/high/low
            if (i == hover) { using var hl = new Pen(Color.FromArgb(120, Theme.Text), 1) { DashStyle = DashStyle.Dash }; g.DrawLine(hl, x, plot.Top, x, plot.Bottom); }
        }

        // MACD panel
        if (ShowMacd && candles.Count > 0)
        {
            var macdPlot = new Rectangle(plot.Left, plot.Bottom + 22, plot.Width, Math.Max(10, Height - 14 - 30 - plot.Height - 22 - 8));
            var m = Technical.Macd(candles.Select(c => c.Close).ToList());
            double mlo = double.MaxValue, mhi = double.MinValue;
            for (var i = first; i < candles.Count; i++)
                foreach (var v in new[] { m.Macd[i], m.Signal[i], m.Histogram[i], 0 })
                    if (!double.IsNaN(v)) { mlo = Math.Min(mlo, v); mhi = Math.Max(mhi, v); }
            if (mhi <= mlo) { mhi = mlo + 1; }
            var mpad = (mhi - mlo) * 0.1; mlo -= mpad; mhi += mpad;
            float MY(double v) => macdPlot.Bottom - (float)((v - mlo) / (mhi - mlo) * macdPlot.Height);
            using (var frame = new Pen(Color.FromArgb(70, Theme.Border))) g.DrawRectangle(frame, macdPlot);
            using (var zero = new Pen(Color.FromArgb(90, Theme.TextMuted)) { DashStyle = DashStyle.Dot }) g.DrawLine(zero, macdPlot.Left, MY(0), macdPlot.Right, MY(0));
            var barW = (float)Math.Clamp(slot * 0.6, 1.5, 14);
            for (var i = first; i < candles.Count; i++)
            {
                if (double.IsNaN(m.Histogram[i])) continue;
                var x = macdPlot.Left + (float)((i - first + 0.5) * slot);
                var y0 = MY(0); var y1 = MY(m.Histogram[i]);
                using var hb = new SolidBrush(Color.FromArgb(150, m.Histogram[i] >= 0 ? ChartColors.Up : ChartColors.Down));
                g.FillRectangle(hb, x - barW / 2, Math.Min(y0, y1), barW, Math.Max(1f, Math.Abs(y1 - y0)));
            }
            foreach (var (series, color) in new[] { (m.Macd, Theme.Accent), (m.Signal, Color.FromArgb(230, 170, 40)) })
            {
                var pts = new List<PointF>();
                for (var i = first; i < candles.Count; i++)
                    if (!double.IsNaN(series[i])) pts.Add(new PointF(macdPlot.Left + (float)((i - first + 0.5) * slot), MY(series[i])));
                if (pts.Count > 1) { using var pen = new Pen(color, 1.5f); g.DrawLines(pen, pts.ToArray()); }
            }
            TextRenderer.DrawText(g, "MACD 12·26·9", Font, new Rectangle(macdPlot.Left + 4, macdPlot.Top + 1, 160, 16), Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }

        // day labels
        var step = Math.Max(1, count / 6);
        for (var i = first; i < candles.Count; i += step)
        {
            var x = plot.Left + (float)((i - first + 0.5) * slot);
            TextRenderer.DrawText(g, DayText(candles[i].Day), Font, new Rectangle((int)x - 30, ShowMacd ? Height - 24 : plot.Bottom + 4, 60, 18), Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
        }

        if (hover >= 0 && hover < candles.Count)
        {
            var c = candles[hover];
            var text = $"{DayText(c.Day, true)}   O {c.Open.ToString("N" + decimals, CultureInfo.CurrentCulture)}   H {c.High.ToString("N" + decimals, CultureInfo.CurrentCulture)}   L {c.Low.ToString("N" + decimals, CultureInfo.CurrentCulture)}   C {c.Close.ToString("N" + decimals, CultureInfo.CurrentCulture)}"
                       + (c.Exact ? "" : "   ~");
            var size = TextRenderer.MeasureText(text, Font);
            var box = new Rectangle(plot.Left + 8, plot.Top + 6, size.Width + 14, size.Height + 8);
            using (var brush = new SolidBrush(Color.FromArgb(235, Theme.Background))) g.FillRectangle(brush, box);
            g.DrawRectangle(border, box);
            TextRenderer.DrawText(g, text, Font, box, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    private static string DayText(string day, bool year = false) =>
        DateTime.TryParseExact(day, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString(year ? "dd/MM/yyyy" : "dd/MM") : day;
}

/// <summary>Two lines on one scale (the strategy and buy &amp; hold), both starting at 100.</summary>
internal sealed class LineChart : Control
{
    private IReadOnlyList<double> a = Array.Empty<double>(), b = Array.Empty<double>();

    public LineChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = new Font("Segoe UI", 9f);
    }

    public string NameA { get; set; } = "";
    public string NameB { get; set; } = "";

    public void SetData(IReadOnlyList<double> strategy, IReadOnlyList<double> buyHold) { a = strategy; b = buyHold; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Surface);
        using var border = new Pen(Theme.Border);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        if (a.Count < 2) return;
        var plot = new Rectangle(56, 12, Math.Max(10, Width - 56 - 12), Math.Max(10, Height - 12 - 34));
        var lo = Math.Min(a.Min(), b.Count > 0 ? b.Min() : a.Min()); var hi = Math.Max(a.Max(), b.Count > 0 ? b.Max() : a.Max());
        if (hi - lo < 1) { hi += 1; lo -= 1; }
        float Y(double v) => plot.Bottom - (float)((v - lo) / (hi - lo) * plot.Height);
        using (var grid = new Pen(Color.FromArgb(60, Theme.Border)) { DashStyle = DashStyle.Dot })
            for (var k = 0; k <= 3; k++)
            {
                var v = lo + (hi - lo) * k / 3; var y = Y(v);
                g.DrawLine(grid, plot.Left, y, plot.Right, y);
                TextRenderer.DrawText(g, v.ToString("0"), Font, new Rectangle(2, (int)y - 9, plot.Left - 6, 18), Theme.TextMuted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        void Draw(IReadOnlyList<double> s, Color c, float w)
        {
            if (s.Count < 2) return;
            var pts = s.Select((v, i) => new PointF(plot.Left + (float)(i * (double)plot.Width / (s.Count - 1)), Y(v))).ToArray();
            using var pen = new Pen(c, w);
            g.DrawLines(pen, pts);
        }
        Draw(b, Theme.TextMuted, 1.4f);
        Draw(a, Theme.Accent, 2.2f);
        var x = plot.Left;
        foreach (var (name, color) in new[] { (NameA, Theme.Accent), (NameB, Theme.TextMuted) })
        {
            using (var brush = new SolidBrush(color)) g.FillRectangle(brush, x, plot.Bottom + 14, 14, 4);
            var w = TextRenderer.MeasureText(name, Font).Width;
            TextRenderer.DrawText(g, name, Font, new Rectangle(x + 20, plot.Bottom + 7, w + 8, 18), Theme.Text, TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            x += w + 52;
        }
    }
}

/// <summary>A half-circle gauge from fear (red) to greed (green) with a needle and the score in the middle.</summary>
internal sealed class FearGreedGauge : Control
{
    private double? score;
    private string label = "";

    public FearGreedGauge()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(380, 300);
    }

    public void SetValue(double? value, string text) { score = value; label = text; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Surface);
        var radius = Math.Max(40, Math.Min((Width - 40) / 2, Height - 112));
        var size = radius * 2;
        var arc = new Rectangle((Width - size) / 2, 20, size, size);
        var colors = new[] { Color.FromArgb(214, 66, 66), Color.FromArgb(232, 140, 60), Color.FromArgb(220, 200, 70), Color.FromArgb(140, 200, 80), Color.FromArgb(46, 170, 98) };
        for (var i = 0; i < 5; i++)
        {
            using var pen = new Pen(colors[i], 22) { StartCap = LineCap.Flat, EndCap = LineCap.Flat };
            g.DrawArc(pen, arc, 180 + i * 36 + 1, 34);
        }
        var cx = arc.Left + arc.Width / 2f; var cy = arc.Top + arc.Height / 2f;
        if (score is { } s)
        {
            var angle = Math.PI * (1 - s / 100.0);
            var r = arc.Width / 2f - 34;
            using var needle = new Pen(Theme.Text, 3f) { EndCap = LineCap.Round };
            g.DrawLine(needle, cx, cy, cx + (float)(Math.Cos(angle) * r), cy - (float)(Math.Sin(angle) * r));
            using var hub = new SolidBrush(Theme.Text);
            g.FillEllipse(hub, cx - 7, cy - 7, 14, 14);
            TextRenderer.DrawText(g, s.ToString("0"), new Font("Segoe UI Semibold", 26f), new Rectangle((int)cx - 70, (int)cy + 8, 140, 46), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
        }
        TextRenderer.DrawText(g, label, new Font("Segoe UI Semibold", 11f), new Rectangle(0, (int)cy + (score is null ? 10 : 54), Width, 28), score is null ? Theme.TextMuted : Theme.Accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
    }
}
