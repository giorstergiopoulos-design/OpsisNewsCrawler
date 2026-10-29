using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// All colours of the crawler UI in one place. Standalone it follows the Windows light/dark setting (or the user's choice);
/// hosted by Opsis the host pushes its own tokens with <see cref="SetColors"/> so both look identical.
/// </summary>
public static class Theme
{
    public static event Action? Changed;

    static Theme() => OpsisTheme.Use(new SharedTokens());

    /// <summary>What the shared controls of Opsis.UI read: always the current values of this class (no copy).</summary>
    private sealed class SharedTokens : IOpsisTheme
    {
        public bool IsDark => Theme.IsDark;
        public Color Background => Theme.Background;
        public Color Surface => Theme.Surface;
        public Color Border => Theme.Border;
        public Color Text => Theme.Text;
        public Color TextMuted => Theme.TextMuted;
        public Color Accent => Theme.Accent;
    }

    public static bool IsDark { get; private set; } = true;
    public static Color Background { get; private set; } = Color.FromArgb(24, 24, 28);
    public static Color Surface { get; private set; } = Color.FromArgb(36, 36, 42);
    public static Color Border { get; private set; } = Color.FromArgb(62, 62, 72);
    public static Color Text { get; private set; } = Color.FromArgb(235, 235, 240);
    public static Color TextMuted { get; private set; } = Color.FromArgb(160, 160, 172);
    public static Color Accent { get; private set; } = Color.FromArgb(0, 188, 212);

    /// <summary>Window opacity 0.3 - 1.0 for the ticker strip.</summary>
    public static double Opacity { get; private set; } = .94;

    // The colours the host (Opsis) pushed. They are used ONLY when the user chose "host" (Follow Opsis): the crawler's own themes
    // (Windows / light / dark / Midnight / Forest / Dawn / Contrast) are independent of Opsis.
    private static Palette? hostPalette;
    private static string currentMode = "system";

    private static bool followAccent;

    /// <summary>The theme names that are the same as in Opsis (full palettes with their own accent): the shared theme file of Opsis.UI.</summary>
    public static readonly string[] NamedThemes = OpsisThemes.Named.ToArray();

    private sealed record Palette(bool Dark, Color Background, Color Surface, Color Border, Color Text, Color Muted, Color Accent);

    // The palettes live in Opsis.UI (OpsisThemes), the same file for Opsis, the Crawler and Appearance.
    private static Palette? Named(string name) =>
        Array.Exists(NamedThemes, n => n == name) && OpsisThemes.TryGet(name) is { } p
            ? new Palette(p.IsDark, p.Background, p.Surface, p.Border, p.Text, p.TextMuted, p.Accent)
            : null;

    /// <summary>
    /// "system", "light", "dark" or one of <see cref="NamedThemes"/>. <paramref name="useWindowsAccent"/> takes the accent colour from Windows
    /// (like Opsis' "follow Windows accent"). Ignored for the colours once a host (Opsis) has supplied its own; only the transparency is kept.
    /// </summary>
    public static void SetMode(string mode, double opacity, bool? useWindowsAccent = null)
    {
        Opacity = Math.Clamp(opacity, .3, 1.0);
        if (useWindowsAccent is { } flag) followAccent = flag;
        currentMode = (mode ?? "system");
        if (currentMode == "host" && hostPalette is { } hp) { ApplyHost(hp); return; }
        if (Named(currentMode) is { } p)
        {
            IsDark = p.Dark;
            Background = p.Background; Surface = p.Surface; Border = p.Border; Text = p.Text; TextMuted = p.Muted;
            Accent = followAccent ? WindowsAccent() ?? p.Accent : p.Accent;
            Changed?.Invoke();
            return;
        }
        var dark = currentMode.ToLowerInvariant() switch
        {
            "dark" => true,
            "light" => false,
            _ => !SystemUsesLightTheme()
        };
        ApplyPalette(dark);
    }

    [DllImport("dwmapi.dll")] private static extern int DwmGetColorizationColor(out uint colorizationColor, out bool opaqueBlend);

    private static Color? WindowsAccent()
    {
        try { return DwmGetColorizationColor(out var argb, out _) == 0 ? Color.FromArgb(255, (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb) : null; }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { return null; }
    }
    /// <summary>Host (Opsis) supplies its own colours.</summary>
    public static void SetColors(bool dark, Color background, Color surface, Color border, Color text, Color textMuted, Color accent, double opacity)
    {
        hostPalette = new Palette(dark, background, surface, border, text, textMuted, accent);
        Opacity = Math.Clamp(opacity, .3, 1.0);
        if (currentMode == "host") ApplyHost(hostPalette);     // otherwise only remembered: the user's own theme stays
    }

    private static void ApplyHost(Palette p)
    {
        IsDark = p.Dark;
        Background = p.Background; Surface = p.Surface; Border = p.Border; Text = p.Text; TextMuted = p.Muted; Accent = p.Accent;
        Changed?.Invoke();
    }

    private static void ApplyPalette(bool dark)
    {
        IsDark = dark;
        Accent = followAccent ? WindowsAccent() ?? Color.FromArgb(0, 188, 212) : Color.FromArgb(0, 188, 212);
        if (dark)
        {
            Background = Color.FromArgb(24, 24, 28);
            Surface = Color.FromArgb(36, 36, 42);
            Border = Color.FromArgb(62, 62, 72);
            Text = Color.FromArgb(235, 235, 240);
            TextMuted = Color.FromArgb(160, 160, 172);
        }
        else
        {
            Background = Color.FromArgb(243, 243, 247);
            Surface = Color.FromArgb(255, 255, 255);
            Border = Color.FromArgb(214, 214, 222);
            Text = Color.FromArgb(28, 28, 32);
            TextMuted = Color.FromArgb(96, 96, 108);
        }
        Changed?.Invoke();
    }

    public static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Colours a control tree with the current theme.</summary>
    public static void Apply(Control root) => ApplyCore(root, Background);

    private static void ApplyCore(Control c, Color back)
    {
        switch (c)
        {
            case FlatCard card:
                card.BackColor = back;
                card.ForeColor = Text;
                foreach (Control child in card.Controls) ApplyCore(child, Surface);
                return;
            case FlatNumber or FlatText or FlatButton or FlatCheckBox or FlatComboBox or FlatSlider or FlatSwatch:
                c.BackColor = back; // drawn by themselves; they use the parent's colour for their corners
                c.ForeColor = Text;
                return;
            case FlatList list:
                StyleScrollbars(list);
                list.BackColor = Surface;
                list.ForeColor = Text;
                list.Invalidate();
                return;
            case TextBox tb when Equals(tb.Tag, "surface"):
                tb.BackColor = Surface;
                tb.ForeColor = Text;
                return;
        }

        if (c is ScrollableControl { AutoScroll: true } or TextBox { Multiline: true }) StyleScrollbars(c);
        var surface = Equals(c.Tag, "surface");
        c.BackColor = surface ? Surface : back;
        c.ForeColor = Equals(c.Tag, "muted") ? TextMuted : Text;
        var childBack = surface ? Surface : back;
        foreach (Control child in c.Controls) ApplyCore(child, childBack);
        c.Invalidate();
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> ScrollHooked = new();

    /// <summary>Dark (or light) scroll bars, like Opsis. Safe to call repeatedly.</summary>
    public static void StyleScrollbars(Control c)
    {
        void Set()
        {
            try { SetWindowTheme(c.Handle, IsDark ? "DarkMode_Explorer" : "Explorer", null); }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        }
        if (c.IsHandleCreated) Set();
        if (ScrollHooked.TryGetValue(c, out _)) return;
        ScrollHooked.Add(c, new object());
        c.HandleCreated += (_, _) => Set();
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Dark/light title bar for a top-level window (Windows 10 2004+/11).</summary>
    public static void ApplyTitleBar(Form form)
    {
        if (!form.IsHandleCreated) return;
        var value = IsDark ? 1 : 0;
        try { DwmSetWindowAttribute(form.Handle, 20, ref value, sizeof(int)); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }
}
