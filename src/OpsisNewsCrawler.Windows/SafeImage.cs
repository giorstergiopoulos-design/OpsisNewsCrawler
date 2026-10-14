using System.Drawing.Drawing2D;

namespace OpsisNewsCrawler.Windows;

/// <summary>Decodes images that come from the internet without letting a huge picture eat the memory.</summary>
internal static class SafeImage
{
    /// <summary>Pictures larger than this many pixels are refused (the file size limit alone does not bound the decoded size).</summary>
    public const long MaxPixels = 24_000_000;

    /// <summary>A copy that fits in <paramref name="maxWidth"/> x <paramref name="maxHeight"/>, or null when the picture is too big to decode.</summary>
    public static Bitmap? Load(Image source, int maxWidth, int maxHeight)
    {
        if (source.Width <= 0 || source.Height <= 0 || (long)source.Width * source.Height > MaxPixels) return null;
        var scale = Math.Min(1.0, Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height));
        var w = Math.Max(1, (int)(source.Width * scale));
        var h = Math.Max(1, (int)(source.Height * scale));
        var result = new Bitmap(w, h);
        using var g = Graphics.FromImage(result);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(source, 0, 0, w, h);
        return result;
    }
}
