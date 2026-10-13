using Microsoft.Win32;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// How the Windows taskbar looks right now (light/dark, transparency, accent colour), so a strip drawn on top of it
/// can use exactly the same material and colours.
/// </summary>
internal sealed record TaskbarStyle(bool Light, bool Transparency, bool AccentOnTaskbar, Color Tint, Color Text, Color SourceText, Color HoverOverlay)
{
    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static TaskbarStyle Read()
    {
        var light = ReadInt(Registry.CurrentUser, Personalize, "SystemUsesLightTheme", 0) == 1;
        var transparency = ReadInt(Registry.CurrentUser, Personalize, "EnableTransparency", 1) == 1;
        var accentOn = ReadInt(Registry.CurrentUser, Personalize, "ColorPrevalence", 0) == 1;

        Color tint;
        if (accentOn)
        {
            // Accent colour chosen by the user (stored as ABGR).
            var abgr = ReadInt(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent", "AccentColorMenu", 0);
            tint = abgr == 0 ? Color.FromArgb(0, 120, 212) : Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
        }
        else
        {
            tint = light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
        }

        // Text is white on dark / accent surfaces, near-black on the light taskbar.
        var darkSurface = !light || accentOn;
        var text = darkSurface ? Color.White : Color.FromArgb(27, 27, 27);
        var source = darkSurface ? Color.FromArgb(205, 255, 255, 255) : Color.FromArgb(200, 27, 27, 27);
        var hover = darkSurface ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(24, 0, 0, 0);
        return new TaskbarStyle(light, transparency, accentOn, tint, text, source, hover);
    }

    /// <summary>The material behind the strip: acrylic tint when the taskbar is translucent, solid otherwise.</summary>
    public Color AcrylicTint(double opacity)
    {
        // The Windows 11 taskbar tint is ~85 % opaque; the user's transparency setting scales it.
        var alpha = (int)Math.Round(255 * Math.Clamp(0.85 * opacity / 0.94, 0.2, 1.0));
        return Color.FromArgb(alpha, Tint);
    }

    /// <summary>The colour the user picked as the Windows accent colour (Settings → Personalisation → Colours).</summary>
    public static Color AccentColor()
    {
        var abgr = ReadInt(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent", "AccentColorMenu", 0);
        return abgr == 0 ? Color.FromArgb(0, 120, 212) : Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
    }

    private static int ReadInt(RegistryKey root, string path, string name, int fallback)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            return key?.GetValue(name) is int v ? v : fallback;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return fallback;
        }
    }
}
