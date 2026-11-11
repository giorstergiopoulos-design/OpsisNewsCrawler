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

/// <summary>A widget a plugin offers to the host's desktop surface; <see cref="ReplacesBuiltIn"/> names a built-in widget (e.g. "calendar") that it replaces.</summary>
public sealed record PluginWidget(string Key, string TitleEl, string TitleEn, Func<Control> CreateContent, string? ReplacesBuiltIn = null);

/// <summary>Optional capability (the host checks with <c>is</c>): the plugin offers widgets.</summary>
public interface IPluginWidgetProvider
{
    IReadOnlyList<PluginWidget> GetWidgets();
}

/// <summary>One line of a plugin feed (text, with an optional link opened on click).</summary>
public sealed record PluginFeedItem(string Text, string? Url = null);

/// <summary>
/// One line of a RICH feed: the text and optional details (a stable id, a kind such as "holiday" or "history", a summary for the preview card, an image id, the country and language the
/// line is for, the year of a historical event, an id of the detail page, a priority). Only additions to the contract: <see cref="PluginFeedItem"/> and <see cref="PluginFeed"/> are unchanged, so
/// plugins built before this type keep working. A plugin that wants to use it calls <see cref="PluginFeeds.RegisterRich"/> inside a try / catch (MissingMethodException, TypeLoadException) and falls
/// back to a text feed when the host has an older contract.
/// </summary>
public sealed record PluginRichFeedItem(
    string EntryId, string Text, string? Url = null, string? Kind = null, string? Summary = null, string? ImageId = null,
    string? CountryCode = null, string? LanguageCode = null, int? HistoricalYear = null, string? DetailId = null, int Priority = 0);

/// <summary>A rich feed a plugin registers; <see cref="GetItems"/> is called on the UI thread and must only return what the plugin already has in memory.</summary>
public sealed record PluginRichFeed(string Key, string TitleEl, string TitleEn, Func<IReadOnlyList<PluginRichFeedItem>> GetItems);

/// <summary>A text feed a plugin registers; the user can choose it as the content of a bar of the News Crawler. <see cref="GetItems"/> is called on the UI thread.</summary>
public sealed record PluginFeed(string Key, string TitleEl, string TitleEn, Func<IReadOnlyList<PluginFeedItem>> GetItems);

/// <summary>The registry of plugin feeds, shared by the plugin and the News Crawler through this contract DLL.</summary>
public static class PluginFeeds
{
    private static readonly object Gate = new(); private static readonly List<PluginFeed> Feeds = new();
    /// <summary>Raised after a feed was added, replaced or removed.</summary>
    public static event Action? Changed;
    public static void Register(PluginFeed feed) { lock (Gate) { Feeds.RemoveAll(f => string.Equals(f.Key, feed.Key, StringComparison.OrdinalIgnoreCase)); Feeds.Add(feed); } Changed?.Invoke(); }
    public static void Unregister(string key) { int removed; lock (Gate) removed = Feeds.RemoveAll(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase)) + RichFeeds.RemoveAll(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase)); if (removed > 0) Changed?.Invoke(); }
    public static IReadOnlyList<PluginFeed> All() { lock (Gate) return Feeds.ToList(); }

    private static readonly List<PluginRichFeed> RichFeeds = new();

    /// <summary>Registers a rich feed (a text feed with details). When a text feed of the same key exists, hosts that understand rich feeds prefer the rich one.</summary>
    public static void RegisterRich(PluginRichFeed feed) { lock (Gate) { RichFeeds.RemoveAll(f => string.Equals(f.Key, feed.Key, StringComparison.OrdinalIgnoreCase)); RichFeeds.Add(feed); } Changed?.Invoke(); }
    public static IReadOnlyList<PluginRichFeed> AllRich() { lock (Gate) return RichFeeds.ToList(); }
    public static PluginRichFeed? FindRich(string? key) { if (string.IsNullOrWhiteSpace(key)) return null; lock (Gate) return RichFeeds.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase)); }
    public static PluginFeed? Find(string? key) { if (string.IsNullOrWhiteSpace(key)) return null; lock (Gate) return Feeds.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase)); }
}

/// <summary>
/// Optional capability (the host checks with <c>is</c>, so plugins that do not implement it keep working): the host's
/// hardware tier ("Low", "Balanced" or "High"). A plugin should lighten its animations and polling on "Low".
/// </summary>
public interface IPluginProfileAware
{
    void SetHardwareProfile(string tier);
}
