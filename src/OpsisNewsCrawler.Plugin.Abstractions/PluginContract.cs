using System.Windows.Forms;

namespace OpsisNewsCrawler.Plugin.Abstractions;

public sealed record PluginNavigationItem(
    string Key,
    string TitleEl,
    string TitleEn,
    Func<Control> CreatePage,
    int Order = 100);

/// <summary>An entry the plugin wants in Opsis' own tray menu (the crawler never creates a tray icon of its own when hosted).</summary>
public sealed record PluginMenuItem(
    string Key,
    string TitleEl,
    string TitleEn,
    Action Invoke,
    bool IsSeparator = false);

/// <summary>One block of text of a plugin's "About" information (help, instructions, version history, licence...).</summary>
public sealed record PluginAboutSection(string TitleEl, string TitleEn, string Text);

/// <summary>What the host shows for the plugin in its own "About" page (the plugin has no About tab of its own when hosted).</summary>
public sealed record PluginAboutInfo(
    string Name,
    string Version,
    string DescriptionEl,
    string DescriptionEn,
    string Author,
    string License,
    IReadOnlyList<PluginAboutSection> Sections);

/// <summary>
/// Contract between Opsis - Desktop Customization (the host) and the News Crawler plugin.
/// The host loads the plugin DLL from its "plugins" folder, creates the class that implements this interface,
/// and calls everything on its UI thread.
/// </summary>
public interface IOpsisNewsCrawlerPlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }

    void Start();
    void Stop();
    void Pause();
    void Resume();
    void Refresh();

    void SetMode(string mode);
    void SetPosition(string position);

    /// <summary>
    /// Pushes the host's theme so the strip, the settings tab and the dialogs look like the host.
    /// Colours are ARGB integers (<c>Color.ToArgb()</c>); opacity is 0.3 - 1.0.
    /// </summary>
    void SetTheme(bool dark, int background, int surface, int border, int text, int textMuted, int accent, double opacity);

    /// <summary>The host's UI language ("el", "en", ...). The plugin shows every text in that language and stops offering its own language choice.</summary>
    void SetLanguage(string code);

    /// <summary>Properties, help, instructions, version history and licence, for the host's "About" page.</summary>
    PluginAboutInfo GetAboutInfo();

    /// <summary>Items to append to the host's tray menu (settings, refresh, pause/resume, ...).</summary>
    IReadOnlyList<PluginMenuItem> GetTrayMenuItems();

    IReadOnlyList<PluginNavigationItem> GetNavigationItems();

    /// <summary>
    /// Raised when the plugin wants the host to show one of its pages (e.g. the user right-clicked the ticker strip).
    /// The argument is the <see cref="PluginNavigationItem.Key"/> of the page.
    /// </summary>
    event Action<string>? NavigationRequested;
}
