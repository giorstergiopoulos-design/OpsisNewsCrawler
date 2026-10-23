namespace OpsisNewsCrawler.Core;

/// <summary>
/// Proportions of the rectangular app icon and of the small icons next to news items. Pure arithmetic, kept here so it can be tested;
/// the drawing itself is in the Windows project. Nothing in this app is cropped to a circle.
/// </summary>
public static class IconGeometry
{
    /// <summary>The wide app icon is four square symbol cells side by side.</summary>
    public const double WideAspect = 4.0;

    /// <summary>Width of the wide icon for a given height.</summary>
    public static int WideWidthFor(int height) => Math.Max(height, (int)Math.Round(height * WideAspect));

    /// <summary>Corner radius: a fifth of the shorter side, never less than 2 px (so never a circle or a pill).</summary>
    public static float CornerRadius(float width, float height) => Math.Max(2f, Math.Min(width, height) * 0.2f);

    /// <summary>
    /// The wide icon as the breaking-news intro shows it: the full bar height minus a 2 px margin on each side, as wide as
    /// <see cref="WideAspect"/> says, but never wider than the bar.
    /// </summary>
    public static (int Width, int Height) IntroSize(int barWidth, int barHeight)
    {
        var h = Math.Max(8, barHeight - 4);
        var w = Math.Min(WideWidthFor(h), Math.Max(h, barWidth - 8));
        return (w, h);
    }

    /// <summary>Largest size with the aspect ratio of the source that fits in a square box (a favicon is never cropped).</summary>
    public static (float Width, float Height) FitInside(float sourceWidth, float sourceHeight, float box)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0) return (box, box);
        var scale = Math.Min(box / sourceWidth, box / sourceHeight);
        return (sourceWidth * scale, sourceHeight * scale);
    }

    /// <summary>Size of the icon next to a news item: 20 px at the normal 42 px bar, growing with the bar, at least 16.</summary>
    public static int ItemIconSize(float fontScale) => Math.Max(16, (int)Math.Round(20 * fontScale));
}
