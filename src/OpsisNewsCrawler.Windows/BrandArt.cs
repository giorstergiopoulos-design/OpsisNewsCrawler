using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Pictures made from the app's own artwork: the installer's wizard images (<c>--make-brand-art &lt;folder&gt;</c>), the README icon and the
/// social-preview image of the GitHub repository (1280x640). Nothing here is drawn by hand elsewhere; it all comes from <see cref="AppIconArt"/>.
/// </summary>
public static class BrandArt
{
    private static readonly Color Top = Color.FromArgb(18, 30, 62);
    private static readonly Color Bottom = Color.FromArgb(14, 110, 130);

    public static void SaveAll(string folder)
    {
        Directory.CreateDirectory(folder);

        // Inno Setup wizard images for 100 / 125 / 150 / 200 % (the sizes Inno asks for), 24-bit BMP.
        var large = new (string Name, int W, int H)[] { ("large-100", 164, 314), ("large-125", 192, 386), ("large-150", 246, 459), ("large-200", 328, 604) };
        var small = new (string Name, int W, int H)[] { ("small-100", 55, 58), ("small-125", 64, 68), ("small-150", 83, 80), ("small-200", 110, 106) };
        foreach (var (name, w, h) in large)
        {
            using var bmp = WizardLarge(w, h);
            bmp.Save(Path.Combine(folder, name + ".bmp"), ImageFormat.Bmp);
        }
        foreach (var (name, w, h) in small)
        {
            using var bmp = WizardSmall(w, h);
            bmp.Save(Path.Combine(folder, name + ".bmp"), ImageFormat.Bmp);
        }

        using (var icon = AppIconArt.Render(512, 0, showAllFour: true)) icon.Save(Path.Combine(folder, "icon.png"), ImageFormat.Png);
        using (var wide = AppIconArt.RenderWide(1024, 256)) wide.Save(Path.Combine(folder, "icon-wide.png"), ImageFormat.Png);
        using (var social = SocialPreview()) social.Save(Path.Combine(folder, "social-preview.png"), ImageFormat.Png);
    }

    private static Bitmap NewCanvas(int w, int h, out Graphics g)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var back = new LinearGradientBrush(new Rectangle(0, 0, w, h), Top, Bottom, LinearGradientMode.Vertical);
        g.FillRectangle(back, 0, 0, w, h);
        return bmp;
    }

    /// <summary>The tall picture on the left of the first and last wizard pages: the wide icon, a few "headline" bars, the name.</summary>
    private static Bitmap WizardLarge(int w, int h)
    {
        var bmp = NewCanvas(w, h, out var g);
        using (g)
        {
            var pad = w * 0.09f;
            var iconW = (int)(w - 2 * pad);
            using (var wide = AppIconArt.RenderWide(iconW, iconW / 4))
                g.DrawImage(wide, pad, h * 0.07f, iconW, iconW / 4f);

            using (var line = new Pen(Color.FromArgb(70, 255, 255, 255), Math.Max(2f, w / 50f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                var y = h * 0.30f;
                foreach (var f in new[] { 0.9f, 0.62f, 0.8f, 0.5f, 0.72f, 0.4f })
                {
                    g.DrawLine(line, pad, y, pad + (w - 2 * pad) * f, y);
                    y += h * 0.045f;
                }
            }

            using var font = new Font("Segoe UI Semibold", w * 0.115f, FontStyle.Regular, GraphicsUnit.Pixel);
            var fmt = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Far };
            g.DrawString("Opsis", font, Brushes.White, new RectangleF(pad, h * 0.60f, w - 2 * pad, h * 0.12f), fmt);
            g.DrawString("News Crawler", font, Brushes.White, new RectangleF(pad, h * 0.71f, w - 2 * pad, h * 0.12f), fmt);
        }
        return bmp;
    }

    /// <summary>The small picture at the top right of the inner wizard pages: just the icon tile.</summary>
    private static Bitmap WizardSmall(int w, int h)
    {
        var bmp = NewCanvas(w, h, out var g);
        using (g)
        {
            g.Clear(Color.White);
            var size = Math.Min(w, h) - 6;
            using var icon = AppIconArt.Render(size * 2, 0, showAllFour: true);
            g.DrawImage(icon, (w - size) / 2f, (h - size) / 2f, size, size);
        }
        return bmp;
    }

    /// <summary>1280x640 image for the repository's social preview (link cards): wide icon, name, one line, a ticker strip.</summary>
    private static Bitmap SocialPreview()
    {
        const int w = 1280, h = 640;
        var bmp = NewCanvas(w, h, out var g);
        using (g)
        {
            using (var wide = AppIconArt.RenderWide(960, 240)) g.DrawImage(wide, 160, 70, 960, 240);
            using var title = new Font("Segoe UI Semibold", 64f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var line = new Font("Segoe UI", 30f, FontStyle.Regular, GraphicsUnit.Pixel);
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Opsis News Crawler", title, Brushes.White, new RectangleF(0, 330, w, 90), center);
            using var muted = new SolidBrush(Color.FromArgb(215, 235, 245));
            g.DrawString("News, markets and weather in your Windows taskbar", line, muted, new RectangleF(0, 425, w, 50), center);

            // a taskbar-like strip at the bottom with headlines passing
            using (var strip = new SolidBrush(Color.FromArgb(150, 6, 12, 26))) g.FillRectangle(strip, 0, 540, w, 70);
            using var small = new Font("Segoe UI", 24f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var bold = new Font("Segoe UI Semibold", 24f, FontStyle.Regular, GraphicsUnit.Pixel);
            var x = 40f;
            foreach (var (src, text) in new[] { ("ΕΤΕ", "+1,2 %"), ("Gold", "4.025 $/oz"), ("Athens", "24 °C"), ("News", "Headline after headline") })
            {
                g.DrawString(src + ":", bold, new SolidBrush(Color.FromArgb(125, 190, 255)), x, 558);
                x += g.MeasureString(src + ":", bold).Width + 8;
                g.DrawString(text, small, Brushes.White, x, 558);
                x += g.MeasureString(text, small).Width + 60;
            }
        }
        return bmp;
    }
}
