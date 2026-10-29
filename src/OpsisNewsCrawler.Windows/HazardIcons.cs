using System.Drawing.Drawing2D;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The pictures of the disaster feed, drawn in code (no image files): a rounded tile in the colour of the alert level (yellow, orange, red)
/// with a white symbol of the kind of event, and the tile of a continent with its outline. Used by the bar, the detail card and the settings.
/// </summary>
internal static class HazardIcons
{
    public static (Color Top, Color Bottom) LevelColors(int level) => level switch
    {
        >= 3 => (Color.FromArgb(255, 110, 90), Color.FromArgb(205, 40, 40)),
        2 => (Color.FromArgb(255, 170, 70), Color.FromArgb(230, 105, 35)),
        _ => (Color.FromArgb(255, 205, 80), Color.FromArgb(222, 150, 30)),
    };

    public static void Draw(Graphics g, Rectangle rect, HazardKind kind, int level)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = Rectangle.Inflate(rect, -1, -1);
        var (top, bottom) = LevelColors(level);
        using (var tile = AppIconArt.RoundedBox(r, Math.Max(2f, r.Width * 0.22f)))
        {
            using var fill = new LinearGradientBrush(r, top, bottom, 90f);
            g.FillPath(fill, tile);
        }
        DrawGlyph(g, r, kind, Color.White);
        g.Restore(state);
    }

    /// <summary>The white symbol alone, inside the unit square of <paramref name="r"/> (with a margin of about a sixth).</summary>
    public static void DrawGlyph(Graphics g, Rectangle r, HazardKind kind, Color color)
    {
        var w = Math.Max(1.4f, r.Width / 9f);
        PointF P(float x, float y) => new(r.Left + r.Width * x, r.Top + r.Height * y);
        using var pen = new Pen(color, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);
        switch (kind)
        {
            case HazardKind.Earthquake:      // a seismograph trace
                g.DrawLines(pen, new[] { P(.14f, .52f), P(.30f, .52f), P(.38f, .24f), P(.50f, .78f), P(.61f, .34f), P(.69f, .52f), P(.86f, .52f) });
                break;
            case HazardKind.Wildfire:        // a flame
            {
                using var flame = new GraphicsPath();
                flame.AddBezier(P(.50f, .12f), P(.78f, .38f), P(.82f, .62f), P(.66f, .80f));
                flame.AddBezier(P(.66f, .80f), P(.56f, .90f), P(.42f, .90f), P(.32f, .80f));
                flame.AddBezier(P(.32f, .80f), P(.18f, .64f), P(.28f, .44f), P(.40f, .34f));
                flame.AddBezier(P(.40f, .34f), P(.42f, .46f), P(.46f, .50f), P(.50f, .12f));
                flame.CloseFigure();
                g.FillPath(brush, flame);
                break;
            }
            case HazardKind.Flood:           // two waves and a drop
            {
                for (var row = 0; row < 2; row++)
                {
                    var y = .60f + row * .20f;
                    var pts = new List<PointF>();
                    for (var i = 0; i <= 16; i++) pts.Add(P(.14f + .72f * i / 16f, y + .05f * MathF.Sin(i / 16f * MathF.PI * 3)));
                    g.DrawLines(pen, pts.ToArray());
                }
                using var drop = new GraphicsPath();
                drop.AddBezier(P(.50f, .12f), P(.62f, .30f), P(.64f, .38f), P(.50f, .46f));
                drop.AddBezier(P(.50f, .46f), P(.36f, .38f), P(.38f, .30f), P(.50f, .12f));
                g.FillPath(brush, drop);
                break;
            }
            case HazardKind.Storm:           // a cloud with a lightning bolt
                g.FillEllipse(brush, P(.16f, .30f).X, P(.16f, .30f).Y, r.Width * .36f, r.Height * .34f);
                g.FillEllipse(brush, P(.34f, .20f).X, P(.34f, .20f).Y, r.Width * .38f, r.Height * .40f);
                g.FillEllipse(brush, P(.54f, .32f).X, P(.54f, .32f).Y, r.Width * .30f, r.Height * .30f);
                g.FillRectangle(brush, P(.28f, .46f).X, P(.28f, .46f).Y, r.Width * .46f, r.Height * .17f);
                using (var bolt = new SolidBrush(Color.FromArgb(255, 255, 214, 64)))
                    g.FillPolygon(bolt, new[] { P(.54f, .50f), P(.40f, .72f), P(.50f, .72f), P(.44f, .92f), P(.66f, .64f), P(.54f, .64f), P(.62f, .50f) });
                break;
            case HazardKind.Volcano:         // a cone with an eruption
                g.FillPolygon(brush, new[] { P(.16f, .88f), P(.40f, .50f), P(.60f, .50f), P(.84f, .88f) });
                g.DrawLine(pen, P(.50f, .40f), P(.50f, .14f));
                g.DrawLine(pen, P(.38f, .42f), P(.28f, .22f));
                g.DrawLine(pen, P(.62f, .42f), P(.72f, .22f));
                break;
            case HazardKind.Drought:         // a sun above cracked ground
                g.FillEllipse(brush, P(.39f, .20f).X, P(.39f, .20f).Y, r.Width * .22f, r.Height * .22f);
                for (var i = 0; i < 8; i++)
                {
                    var a = i * MathF.PI / 4;
                    var c = P(.50f, .30f);
                    g.DrawLine(pen, c.X + MathF.Cos(a) * r.Width * .17f, c.Y + MathF.Sin(a) * r.Height * .17f, c.X + MathF.Cos(a) * r.Width * .25f, c.Y + MathF.Sin(a) * r.Height * .25f);
                }
                g.DrawLines(pen, new[] { P(.14f, .66f), P(.34f, .72f), P(.46f, .64f), P(.60f, .76f), P(.86f, .68f) });
                g.DrawLines(pen, new[] { P(.34f, .72f), P(.30f, .88f) });
                g.DrawLines(pen, new[] { P(.60f, .76f), P(.66f, .90f) });
                break;
            case HazardKind.Tsunami:         // a breaking wave
            {
                using var wave = new GraphicsPath();
                wave.AddBezier(P(.12f, .84f), P(.20f, .30f), P(.52f, .12f), P(.78f, .24f));
                wave.AddBezier(P(.78f, .24f), P(.64f, .26f), P(.60f, .44f), P(.70f, .56f));
                wave.AddBezier(P(.70f, .56f), P(.58f, .50f), P(.44f, .52f), P(.40f, .66f));
                wave.AddLine(P(.40f, .66f), P(.88f, .84f));
                wave.CloseFigure();
                g.FillPath(brush, wave);
                break;
            }
            default:                         // an exclamation mark
                g.DrawLine(pen, P(.50f, .20f), P(.50f, .60f));
                g.FillEllipse(brush, P(.50f, .78f).X - w * .7f, P(.50f, .78f).Y - w * .7f, w * 1.4f, w * 1.4f);
                break;
        }
    }

    /// <summary>The picture of a continent: a dark blue tile with the outline in a light colour.</summary>
    public static void DrawContinent(Graphics g, Rectangle rect, Continent continent)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = Rectangle.Inflate(rect, -1, -1);
        using (var tile = AppIconArt.RoundedBox(r, Math.Max(2f, r.Width * 0.22f)))
        {
            using var fill = new LinearGradientBrush(r, Color.FromArgb(52, 118, 178), Color.FromArgb(22, 64, 118), 90f);
            g.FillPath(fill, tile);
        }
        var (minLon, minLat, maxLon, maxLat) = Continents.Bounds(continent);
        var inner = Rectangle.Inflate(r, -(int)Math.Max(2, r.Width * .12f), -(int)Math.Max(2, r.Height * .12f));
        var scale = Math.Min(inner.Width / (maxLon - minLon), inner.Height / (maxLat - minLat));
        var offX = inner.Left + (inner.Width - (maxLon - minLon) * scale) / 2;
        var offY = inner.Top + (inner.Height - (maxLat - minLat) * scale) / 2;
        using var land = new SolidBrush(Color.FromArgb(236, 244, 236));
        foreach (var ring in Continents.Outline(continent))
        {
            var pts = ring.Select(p => new PointF((float)(offX + (p.Lon - minLon) * scale), (float)(offY + (maxLat - p.Lat) * scale))).ToArray();
            g.FillPolygon(land, pts);
        }
        g.Restore(state);
    }
}
