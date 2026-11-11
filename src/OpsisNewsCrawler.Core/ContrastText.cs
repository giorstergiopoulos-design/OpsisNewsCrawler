namespace OpsisNewsCrawler.Core;

/// <summary>
/// Which text colour (light or dark) reads on the background behind a transparent bar. WCAG relative luminance and contrast ratio; the decision has a dead band
/// and needs two agreeing samples before it changes, so a background that hovers around the limit does not make the text blink.
/// </summary>
public sealed class ContrastChooser
{
    /// <summary>Luminance of the two text colours that are used (white and a near-black).</summary>
    public const double LightTextLuminance = 1.0;
    public const double DarkTextLuminance = 0.0;   // black: the only dark text that reaches WCAG 4.5:1 on every background

    // Both texts have the same contrast on a background of luminance about 0.18; the band around it is where the decision is kept as it is.
    private const double DarkBelow = 0.14;     // below this a light text is clearly better
    private const double LightAbove = 0.23;    // above this a dark text is clearly better
    private const int AgreeingSamples = 2;

    private bool? useDarkText;
    private bool pendingDark;
    private int pendingCount;

    /// <summary>true = dark text, false = light text, null = nothing sampled yet.</summary>
    public bool? UseDarkText => useDarkText;

    /// <summary>WCAG relative luminance of an sRGB colour (0 black ... 1 white).</summary>
    public static double RelativeLuminance(byte r, byte g, byte b)
    {
        static double Channel(byte v)
        {
            var c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }

    /// <summary>WCAG contrast ratio of two luminances (1 to 21).</summary>
    public static double ContrastRatio(double a, double b)
    {
        var hi = Math.Max(a, b); var lo = Math.Min(a, b);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>The contrast the chosen text has on a background of this luminance.</summary>
    public static double Contrast(double backgroundLuminance, bool darkText) =>
        ContrastRatio(backgroundLuminance, darkText ? DarkTextLuminance : LightTextLuminance);

    /// <summary>The middle value of the samples (a few bright or dark pixels of text and icons do not matter); 0.5 for no samples.</summary>
    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0.5;
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <summary>Takes one sample of the background luminance; returns true when the choice changed.</summary>
    public bool Update(double backgroundLuminance)
    {
        bool? wanted = backgroundLuminance < DarkBelow ? false : backgroundLuminance > LightAbove ? true : null;
        if (useDarkText is null)
        {
            // the first sample decides at once; inside the dead band the one with the better contrast
            useDarkText = wanted ?? (Contrast(backgroundLuminance, true) >= Contrast(backgroundLuminance, false));
            return true;
        }
        if (wanted is null || wanted == useDarkText) { pendingCount = 0; return false; }
        if (pendingCount > 0 && pendingDark == wanted) pendingCount++;
        else { pendingDark = wanted.Value; pendingCount = 1; }
        if (pendingCount < AgreeingSamples) return false;
        useDarkText = wanted;
        pendingCount = 0;
        return true;
    }

    public void Reset()
    {
        useDarkText = null;
        pendingCount = 0;
    }
}
