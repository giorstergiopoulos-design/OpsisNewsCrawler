using System.Drawing.Drawing2D;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>Weather icons drawn with shapes (no fonts, no pictures), so they stay sharp at any size and work on light and dark bars.</summary>
internal static class WeatherIcons
{
    private static readonly Color SunColor = Color.FromArgb(255, 193, 7);
    private static readonly Color MoonColor = Color.FromArgb(235, 232, 200);
    private static readonly Color RainColor = Color.FromArgb(70, 160, 240);
    private static readonly Color BoltColor = Color.FromArgb(255, 214, 10);

    /// <param name="icon">WeatherKind, plus 10 at night.</param>
    public static void Draw(Graphics g, Rectangle r, int icon, bool darkSurface)
    {
        var night = icon >= 10;
        var kind = (WeatherKind)(icon % 10);
        var oldMode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var state = g.Save();
        g.TranslateTransform(r.Left, r.Top);
        g.ScaleTransform(r.Width / 100f, r.Height / 100f); // draw in a 100 x 100 box

        var cloud = darkSurface ? Color.FromArgb(208, 214, 224) : Color.FromArgb(120, 132, 150);
        var darkCloud = darkSurface ? Color.FromArgb(150, 158, 172) : Color.FromArgb(88, 98, 114);
        switch (kind)
        {
            case WeatherKind.Clear:
                if (night) Moon(g, 50, 50, 30); else Sun(g, 50, 50, 21);
                break;
            case WeatherKind.PartlyCloudy:
                if (night) Moon(g, 36, 36, 22); else Sun(g, 36, 36, 16);
                Cloud(g, 12, 38, 80, 52, cloud);
                break;
            case WeatherKind.Cloudy:
                Cloud(g, 28, 14, 66, 36, darkCloud);
                Cloud(g, 6, 34, 84, 58, cloud);
                break;
            case WeatherKind.Fog:
                Cloud(g, 10, 6, 80, 48, cloud);
                using (var pen = new Pen(cloud, 7f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(pen, 14, 66, 86, 66);
                    g.DrawLine(pen, 24, 82, 76, 82);
                }
                break;
            case WeatherKind.Drizzle:
                Cloud(g, 8, 8, 84, 52, cloud);
                Drops(g, 2, 7f);
                break;
            case WeatherKind.Rain:
                Cloud(g, 8, 6, 84, 50, darkCloud);
                Drops(g, 3, 8f);
                break;
            case WeatherKind.Snow:
                Cloud(g, 8, 6, 84, 50, cloud);
                using (var flake = new SolidBrush(darkSurface ? Color.White : Color.FromArgb(110, 165, 215)))
                {
                    foreach (var x in new[] { 28f, 50f, 72f }) g.FillEllipse(flake, x - 6, 68, 12, 12);
                    g.FillEllipse(flake, 39 - 5, 86, 10, 10);
                    g.FillEllipse(flake, 61 - 5, 86, 10, 10);
                }
                break;
            case WeatherKind.Thunder:
                Cloud(g, 8, 4, 84, 48, darkCloud);
                using (var bolt = new SolidBrush(BoltColor))
                    g.FillPolygon(bolt, new[] { new PointF(54, 50), new PointF(34, 76), new PointF(48, 76), new PointF(40, 98), new PointF(68, 66), new PointF(53, 66), new PointF(62, 50) });
                break;
        }
        g.Restore(state);
        g.SmoothingMode = oldMode;
    }

    private static void Sun(Graphics g, float cx, float cy, float radius)
    {
        using var brush = new SolidBrush(SunColor);
        g.FillEllipse(brush, cx - radius, cy - radius, radius * 2, radius * 2);
        using var pen = new Pen(SunColor, 6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (var i = 0; i < 8; i++)
        {
            var a = i * Math.PI / 4;
            var (c, s) = ((float)Math.Cos(a), (float)Math.Sin(a));
            g.DrawLine(pen, cx + c * (radius + 7), cy + s * (radius + 7), cx + c * (radius + 15), cy + s * (radius + 15));
        }
    }

    private static void Moon(Graphics g, float cx, float cy, float radius)
    {
        using var moon = new GraphicsPath();
        moon.AddEllipse(cx - radius, cy - radius, radius * 2, radius * 2);
        using var bite = new GraphicsPath();
        bite.AddEllipse(cx - radius * 0.35f, cy - radius * 1.05f, radius * 1.8f, radius * 1.8f);
        using var region = new Region(moon);
        region.Exclude(bite);
        using var brush = new SolidBrush(MoonColor);
        g.FillRegion(brush, region);
    }

    private static void Cloud(Graphics g, float x, float y, float w, float h, Color color)
    {
        using var brush = new SolidBrush(color);
        var baseTop = y + h * 0.42f;
        g.FillEllipse(brush, x, baseTop, h * 0.58f, h * 0.58f);                          // left lobe
        g.FillEllipse(brush, x + w - h * 0.58f, baseTop, h * 0.58f, h * 0.58f);          // right lobe
        g.FillEllipse(brush, x + w * 0.18f, y, h * 0.78f, h * 0.78f);                    // big puff
        g.FillEllipse(brush, x + w * 0.46f, y + h * 0.16f, h * 0.62f, h * 0.62f);        // second puff
        g.FillRectangle(brush, x + h * 0.29f, baseTop + h * 0.2f, w - h * 0.58f, h * 0.38f);
    }

    private static void Drops(Graphics g, int rows, float width)
    {
        using var pen = new Pen(RainColor, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, 30, 68, 24, 86);
        g.DrawLine(pen, 52, 68, 46, 86);
        g.DrawLine(pen, 74, 68, 68, 86);
        if (rows > 2)
        {
            g.DrawLine(pen, 41, 80, 36, 94);
            g.DrawLine(pen, 63, 80, 58, 94);
        }
    }
}
