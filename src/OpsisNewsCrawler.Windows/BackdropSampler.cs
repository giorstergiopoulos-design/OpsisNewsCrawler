using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Measures how light the background of a bar is: the screen area of the bar (what the user sees, including the tint and blur of the bar itself) is copied once and
/// a sparse grid of its pixels gives the middle luminance; the few pixels of text and icons do not change a median. Called only when the background may have changed,
/// never once per frame.
/// </summary>
internal static class BackdropSampler
{
    private const int Step = 7;
    private const int MaxPixelsPerSide = 4000;

    /// <summary>The middle luminance (0..1) of the screen area, or null when it cannot be read (locked screen, secure desktop, remote session without a screen).</summary>
    public static double? Sample(Rectangle screenArea)
    {
        var area = Rectangle.Intersect(screenArea, SystemInformation.VirtualScreen);
        if (area.Width < 8 || area.Height < 4 || area.Width > MaxPixelsPerSide || area.Height > MaxPixelsPerSide) return null;
        try
        {
            using var bmp = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(area.Location, Point.Empty, area.Size);
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var samples = new List<double>((bmp.Width / Step + 1) * (bmp.Height / Step + 1));
                var row = new byte[bmp.Width * 4];
                for (var y = Step / 2; y < bmp.Height; y += Step)
                {
                    System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (var x = Step / 2; x < bmp.Width; x += Step)
                        samples.Add(ContrastChooser.RelativeLuminance(row[x * 4 + 2], row[x * 4 + 1], row[x * 4]));
                }
                return ContrastChooser.Median(samples);
            }
            finally { bmp.UnlockBits(data); }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or ArgumentException or ExternalException or InvalidOperationException)
        {
            return null;
        }
    }
}
