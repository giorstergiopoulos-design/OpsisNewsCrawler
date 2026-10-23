using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The artwork of the application icon, drawn in code: a rounded tile with a news bar across it; on the bar the four kinds of news
/// pass from right to left — newspaper (news), ball (sports), shield with olive leaf (defence and diplomacy), chart with coins (economy).
/// The same drawing makes the static .ico of the program and the frames of the animated tray icon.
/// </summary>
public static class AppIconArt
{
    private const int FramesPerLoop = 64;   // with the 60 ms tray timer: a ~3.8 s loop that moves smoothly

    /// <summary>One picture of the icon. <paramref name="phase"/> 0..1 is the position in the loop of the passing symbols.</summary>
    public static Bitmap Render(int size, double phase, bool showAllFour = false)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
        var s = size / 100f;

        // The tile.
        using (var tile = RoundRect(new RectangleF(2 * s, 2 * s, 96 * s, 96 * s), 22 * s))
        using (var back = new LinearGradientBrush(new RectangleF(0, 0, size, size), Color.FromArgb(24, 40, 78), Color.FromArgb(14, 120, 140), 50f))
        {
            g.FillPath(back, tile);
            using var rim = new Pen(Color.FromArgb(90, 255, 255, 255), Math.Max(1f, 1.5f * s));
            g.DrawPath(rim, tile);
        }

        // The bar.
        var barRect = new RectangleF(7 * s, 33 * s, 86 * s, 36 * s);
        using (var bar = RoundRect(barRect, 10 * s))
        {
            using var barBrush = new SolidBrush(Color.FromArgb(235, 12, 18, 32));
            g.FillPath(barBrush, bar);
            using var barEdge = new Pen(Color.FromArgb(170, 110, 190, 255), Math.Max(1f, 1.6f * s));
            g.DrawPath(barEdge, bar);

            // The symbols, clipped to the bar.
            g.SetClip(bar, CombineMode.Intersect);
            var cell = (showAllFour ? barRect.Width / 4.0 : Math.Max(barRect.Height * 0.95, barRect.Width / 2.6)); // pixels per symbol cell
            var loop = cell * 4;
            var shift = (float)(phase * loop);
            // Moving right to left: the strip is drawn twice so there is never a gap.
            for (var repeat = 0; repeat < 2; repeat++)
            {
                for (var i = 0; i < 4; i++)
                {
                    var x = barRect.Left + (float)(i * cell + repeat * loop) - shift;
                    var box = new RectangleF(x + (float)cell * 0.06f, barRect.Top + barRect.Height * 0.08f, (float)cell * 0.88f, barRect.Height * 0.84f);
                    if (box.Right < barRect.Left || box.Left > barRect.Right) continue;
                    DrawSymbol(g, i, box);
                }
            }
            g.ResetClip();

            // The ends of the bar fade out (like the bar itself does on the desktop).
            var fade = barRect.Width * 0.12f;
            using var left = new LinearGradientBrush(new RectangleF(barRect.Left, 0, fade + 1, 1), Color.FromArgb(235, 12, 18, 32), Color.FromArgb(0, 12, 18, 32), 0f);
            using var right = new LinearGradientBrush(new RectangleF(barRect.Right - fade, 0, fade + 1, 1), Color.FromArgb(0, 12, 18, 32), Color.FromArgb(235, 12, 18, 32), 0f);
            g.SetClip(bar, CombineMode.Intersect);
            g.FillRectangle(left, barRect.Left, barRect.Top, fade, barRect.Height);
            g.FillRectangle(right, barRect.Right - fade, barRect.Top, fade, barRect.Height);
            g.ResetClip();
        }

        // Two small lines above and below the bar: more news waiting.
        using (var line = new Pen(Color.FromArgb(120, 255, 255, 255), Math.Max(1f, 2.2f * s)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(line, 20 * s, 22 * s, 62 * s, 22 * s);
            g.DrawLine(line, 38 * s, 80 * s, 80 * s, 80 * s);
        }
        return bmp;
    }

    /// <summary>Width of the rectangular icon for a given height: four square symbol cells side by side.</summary>
    public static int WideWidthFor(int height) => OpsisNewsCrawler.Core.IconGeometry.WideWidthFor(height);

    public const double WideAspect = OpsisNewsCrawler.Core.IconGeometry.WideAspect;

    /// <summary>
    /// The rectangular version of the icon: the whole artwork (tile with the four news symbols) uses the full width and height of the
    /// given box, with a small corner radius and no round mask. Drawn at exactly the pixel size asked for, so it is crisp at any DPI.
    /// <paramref name="phase"/> 0..1 slides the symbols like the tray icon does; <paramref name="showAllFour"/> is the still picture.
    /// </summary>
    public static Bitmap RenderWide(int width, int height, double phase = 0, bool showAllFour = true)
    {
        width = Math.Max(8, width);
        height = Math.Max(8, height);
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        var radius = WideCornerRadius(width, height);
        var tileRect = new RectangleF(0.5f, 0.5f, width - 1f, height - 1f);
        using (var tile = RoundRect(tileRect, radius))
        {
            using (var back = new LinearGradientBrush(new RectangleF(0, 0, width, height), Color.FromArgb(24, 40, 78), Color.FromArgb(14, 120, 140), 20f))
                g.FillPath(back, tile);

            // Symbols clipped to the tile; the strip slides right to left, drawn twice so there is no gap.
            g.SetClip(tile, CombineMode.Intersect);
            var pad = height * 0.10f;
            var cell = showAllFour ? (width - pad) / 4.0 : Math.Max(height * 0.95, width / 2.6);
            var loop = cell * 4;
            var shift = (float)(phase * loop);
            for (var repeat = 0; repeat < (showAllFour ? 1 : 2); repeat++)
            {
                for (var i = 0; i < 4; i++)
                {
                    var x = pad / 2 + (float)(i * cell + repeat * loop) - shift;
                    var box = new RectangleF(x + (float)cell * 0.04f, height * 0.10f, (float)cell * 0.92f, height * 0.80f);
                    if (box.Right < 0 || box.Left > width) continue;
                    DrawSymbol(g, i, box);
                }
            }
            g.ResetClip();

            using var rim = new Pen(Color.FromArgb(110, 255, 255, 255), Math.Max(1f, height / 40f));
            g.DrawPath(rim, tile);
        }
        return bmp;
    }

    /// <summary>Small corner radius shared by the wide icon and the letter badges (about a fifth of the height, never a circle).</summary>
    public static float WideCornerRadius(int width, int height) => OpsisNewsCrawler.Core.IconGeometry.CornerRadius(width, height);

    /// <summary>A rounded rectangle for callers that draw next to the icon (letter badges, favicon plates).</summary>
    public static GraphicsPath RoundedBox(RectangleF r, float radius) => RoundRect(r, Math.Min(radius, Math.Min(r.Width, r.Height) / 2f));

    /// <summary>0 newspaper, 1 ball, 2 shield (defence and diplomacy), 3 chart with coins (economy).</summary>
    private static void DrawSymbol(Graphics g, int kind, RectangleF box)
    {
        var side = Math.Min(box.Width, box.Height);
        var rect = new RectangleF(box.Left + (box.Width - side) / 2, box.Top + (box.Height - side) / 2, side, side);
        var state = g.Save();
        g.TranslateTransform(rect.Left, rect.Top);
        g.ScaleTransform(side / 100f, side / 100f);
        switch (kind)
        {
            case 0: Newspaper(g); break;
            case 1: Ball(g); break;
            case 2: Shield(g); break;
            default: Economy(g); break;
        }
        g.Restore(state);
    }

    private static void Newspaper(Graphics g)
    {
        using (var paper = RoundRect(new RectangleF(10, 12, 80, 76), 8)) using (var white = new SolidBrush(Color.FromArgb(245, 246, 250))) g.FillPath(white, paper);
        using (var head = new SolidBrush(Color.FromArgb(40, 80, 150))) g.FillRectangle(head, 18, 20, 64, 16);
        using (var photo = new SolidBrush(Color.FromArgb(150, 170, 205))) g.FillRectangle(photo, 18, 44, 26, 30);
        using var ink = new Pen(Color.FromArgb(90, 100, 120), 5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(ink, 52, 49, 82, 49);
        g.DrawLine(ink, 52, 60, 82, 60);
        g.DrawLine(ink, 52, 71, 82, 71);
    }

    private static void Ball(Graphics g)
    {
        using (var ball = new SolidBrush(Color.FromArgb(250, 250, 250))) g.FillEllipse(ball, 12, 12, 76, 76);
        using (var rim = new Pen(Color.FromArgb(40, 40, 50), 4f)) g.DrawEllipse(rim, 12, 12, 76, 76);
        using var dark = new SolidBrush(Color.FromArgb(35, 35, 45));
        g.FillPolygon(dark, Pentagon(50, 50, 15, -90));
        using var seam = new Pen(Color.FromArgb(35, 35, 45), 3.5f);
        for (var i = 0; i < 5; i++)
        {
            var a = (-90 + i * 72) * Math.PI / 180;
            g.DrawLine(seam, (float)(50 + Math.Cos(a) * 15), (float)(50 + Math.Sin(a) * 15), (float)(50 + Math.Cos(a) * 36), (float)(50 + Math.Sin(a) * 36));
        }
    }

    private static void Shield(Graphics g)
    {
        using var path = new GraphicsPath();
        path.AddLine(50, 8, 86, 20);
        path.AddBezier(86, 20, 86, 58, 74, 76, 50, 92);
        path.AddBezier(50, 92, 26, 76, 14, 58, 14, 20);
        path.CloseFigure();
        using (var fill = new LinearGradientBrush(new RectangleF(10, 8, 80, 84), Color.FromArgb(90, 150, 235), Color.FromArgb(40, 80, 170), 90f)) g.FillPath(fill, path);
        using (var edge = new Pen(Color.FromArgb(235, 240, 255), 4f)) g.DrawPath(edge, path);
        using (var star = new SolidBrush(Color.FromArgb(255, 255, 255))) g.FillPolygon(star, Star(50, 42, 19, 8));
        // the olive leaf: diplomacy
        using var leaf = new SolidBrush(Color.FromArgb(150, 205, 110));
        g.FillEllipse(leaf, 44, 64, 24, 11);
        using var stem = new Pen(Color.FromArgb(150, 205, 110), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(stem, 34, 74, 56, 68);
    }

    private static void Economy(Graphics g)
    {
        using (var green = new SolidBrush(Color.FromArgb(70, 205, 120)))
        {
            g.FillRectangle(green, 14, 62, 16, 26);
            g.FillRectangle(green, 36, 46, 16, 42);
            g.FillRectangle(green, 58, 30, 16, 58);
        }
        using var axis = new Pen(Color.FromArgb(220, 230, 240), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(axis, 10, 92, 90, 92);
        using (var coin = new SolidBrush(Color.FromArgb(255, 200, 45))) g.FillEllipse(coin, 56, 6, 36, 36);
        using (var coinEdge = new Pen(Color.FromArgb(200, 140, 20), 3f)) g.DrawEllipse(coinEdge, 56, 6, 36, 36);
        using var mark = new Pen(Color.FromArgb(170, 110, 10), 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(mark, 74, 15, 74, 33);
        g.DrawLine(mark, 67, 20, 81, 20);
        g.DrawLine(mark, 67, 28, 81, 28);
    }

    private static PointF[] Pentagon(float cx, float cy, float r, double startDeg) =>
        Enumerable.Range(0, 5).Select(i => { var a = (startDeg + i * 72) * Math.PI / 180; return new PointF((float)(cx + Math.Cos(a) * r), (float)(cy + Math.Sin(a) * r)); }).ToArray();

    private static PointF[] Star(float cx, float cy, float outer, float inner) =>
        Enumerable.Range(0, 10).Select(i =>
        {
            var a = (-90 + i * 36) * Math.PI / 180;
            var r = i % 2 == 0 ? outer : inner;
            return new PointF((float)(cx + Math.Cos(a) * r), (float)(cy + Math.Sin(a) * r));
        }).ToArray();

    private static GraphicsPath RoundRect(RectangleF r, float radius)
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

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

    /// <summary>A bitmap as an <see cref="Icon"/> that owns its own copy (the temporary Win32 handle is released at once).</summary>
    public static Icon ToIcon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    /// <summary>All the frames of one loop of the animated tray icon, made once.</summary>
    public static Icon[] BuildTrayFrames(int size)
    {
        var frames = new Icon[FramesPerLoop];
        for (var i = 0; i < FramesPerLoop; i++)
        {
            using var bmp = Render(size, (double)i / FramesPerLoop);
            frames[i] = ToIcon(bmp);
        }
        return frames;
    }

    /// <summary>Writes the program icon (several sizes, PNG inside) to a .ico file. Used to make Assets\app.ico.</summary>
    public static void WriteIcoFile(string path)
    {
        var sizes = new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var images = new List<byte[]>();
        foreach (var size in sizes)
        {
            using var bmp = Render(size, 0.0, showAllFour: size >= 40);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            images.Add(ms.ToArray());
        }
        using var file = File.Create(path);
        using var w = new BinaryWriter(file);
        w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
        var offset = 6 + 16 * sizes.Length;
        for (var i = 0; i < sizes.Length; i++)
        {
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32);
            w.Write(images[i].Length);
            w.Write(offset);
            offset += images[i].Length;
        }
        foreach (var image in images) w.Write(image);
    }
}
