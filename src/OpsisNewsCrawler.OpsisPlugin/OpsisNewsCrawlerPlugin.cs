using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;
using OpsisNewsCrawler.Windows;

namespace OpsisNewsCrawler.OpsisPlugin;

/// <summary>
/// Plugin entry point for Opsis - Desktop Customization. The host must create it and call it on its UI thread.
/// Hosted, the crawler never creates its own tray icon: its commands are offered through <see cref="GetTrayMenuItems"/>
/// and its settings are a tab of the host.
/// </summary>
public sealed class OpsisNewsCrawlerPlugin : IOpsisNewsCrawlerPlugin, IPluginProfileAware
{
    public const string NavigationKey = "OpsisNewsCrawler";

    private readonly TickerController _controller = new();

    public OpsisNewsCrawlerPlugin()
    {
        // Diagnostics: when Opsis cannot show the plugin, crawler.log says which step failed.
        Log.Write($"Plugin {AboutTexts.Version} created inside {AppDomain.CurrentDomain.FriendlyName} (runtime {Environment.Version}).");
        _controller.SettingsRequested += OnSettingsRequested;
    }

    /// <inheritdoc />
    public event Action<string>? NavigationRequested;

    private void OnSettingsRequested()
    {
        // Preferred: the host shows the plugin's own tab. If nobody subscribed, open a settings window instead.
        if (NavigationRequested is { } navigate) navigate(NavigationKey);
        else _controller.ShowFallbackSettings();
    }

    public string Id => "giorstergiopoulos.opsisnewscrawler";
    public string Name => "Opsis News Crawler";
    public string Version => AboutTexts.Version;

    public void Start()
    {
        try { _controller.Start(); Log.Write("Plugin started."); }
        catch (Exception ex) { Log.Write($"Plugin Start failed: {ex}"); throw; }
    }
    public void Stop() => _controller.Stop();

    /// <summary>Tray "Close": the bars go away and nothing is fetched; "Open" brings them back. The plugin itself stays loaded.</summary>
    private void CloseCrawler()
    {
        _controller.Stop();
        Log.Write("Crawler closed from the tray menu.");
    }

    private void OpenCrawler()
    {
        _controller.Start();
        Log.Write("Crawler opened from the tray menu.");
    }
    public void Pause() => _controller.Pause();
    public void Resume() => _controller.Resume();
    public void Refresh() => _controller.Refresh();
    public void SetMode(string mode) => _controller.SetMode(TickerParsing.ParseMode(mode));
    public void SetPosition(string position) => _controller.SetPosition(TickerParsing.ParsePosition(position));

    public void SetTheme(bool dark, int background, int surface, int border, int text, int textMuted, int accent, double opacity) =>
        Theme.SetColors(dark, Color.FromArgb(background), Color.FromArgb(surface), Color.FromArgb(border),
            Color.FromArgb(text), Color.FromArgb(textMuted), Color.FromArgb(accent), Theme.Opacity); // the strip keeps its own transparency setting

    public void SetHardwareProfile(string tier) => TickerWindow.LowPower = string.Equals(tier, "Low", StringComparison.OrdinalIgnoreCase);

    public void SetLanguage(string code)
    {
        _controller.LanguageFromHost = true;
        Loc.SetLanguage(code);
    }

    public PluginAboutInfo GetAboutInfo() => new(
        Name,
        Version,
        "Μπάρα ειδήσεων και τιμών για την επιφάνεια εργασίας: RSS ανά κατηγορία, τιμές Euronext Athens, μετάλλων και πετρελαίου, δύο μπάρες.",
        "News and price bar for the desktop: RSS per category, Euronext Athens, metals and oil prices, two bars.",
        "Opsis Team",
        "MIT",
        new[]
        {
            new PluginAboutSection("Βοήθεια", "Help", AboutTexts.Help),
            new PluginAboutSection("Οδηγίες χρήσης", "Instructions", AboutTexts.Instructions),
            new PluginAboutSection("Ιστορικό εκδόσεων", "Version history", AboutTexts.History),
            new PluginAboutSection("Άδεια χρήσης", "License", AboutTexts.License),
        });

    public IReadOnlyList<PluginMenuItem> GetTrayMenuItems() => new[]
    {
        new PluginMenuItem("news.separator", "", "", () => { }, IsSeparator: true),
        new PluginMenuItem("news.settings", "News Crawler — Ρυθμίσεις", "News Crawler — Settings", () => NavigationRequested?.Invoke(NavigationKey)),
        new PluginMenuItem("news.refresh", "News Crawler — Ανανέωση", "News Crawler — Refresh", Refresh),
        new PluginMenuItem("news.pause", "News Crawler — Παύση / Συνέχεια", "News Crawler — Pause / Resume", () =>
        {
            if (_controller.Host.Paused) Resume(); else Pause();
        }),
        // The guide and the family window are offered here; inside Opsis they are never shown by themselves (Opsis has its own).
        new PluginMenuItem("news.welcome", "News Crawler β€” ΞΞ΄Ξ·Ξ³ΟΟ‚ ΞΊΞ±Ξ»Ο‰ΟƒΞΏΟΞ―ΟƒΞΌΞ±Ο„ΞΏΟ‚", "News Crawler β€” Welcome guide", () => _controller.Host.ShowWelcome()),
        new PluginMenuItem("news.suite", "News Crawler β€” Ξ— ΞΏΞΉΞΊΞΏΞ³Ξ­Ξ½ΞµΞΉΞ± Opsis", "News Crawler β€” The Opsis family", () => _controller.Host.ShowSuite()),
        new PluginMenuItem("news.close", "News Crawler — Κλείσιμο", "News Crawler — Close", CloseCrawler),
        new PluginMenuItem("news.open", "News Crawler — Άνοιγμα", "News Crawler — Open", OpenCrawler),
    };

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems() =>
        new[]
        {
            new PluginNavigationItem(
                NavigationKey,
                "News Crawler",
                "News Crawler",
                () =>
                {
                    try { return new SettingsPanel(_controller.Host, showCloseButton: false, showAboutTabs: false) { HostThemeLocked = true }; }
                    catch (Exception ex) { Log.Write($"Plugin settings page failed: {ex}"); throw; }
                },
                90)
        };
}

/// <summary>Bridges the plugin API to the real ticker (the same TickerHost the standalone app uses).</summary>
internal sealed class TickerController
{
    private TickerHost? _host;
    private SettingsForm? _fallbackSettings;

    /// <summary>The host (Opsis) told us its language: the crawler's own language setting is then ignored.</summary>
    public bool LanguageFromHost { get; set; }

    public event Action? SettingsRequested;

    public TickerHost Host
    {
        get
        {
            if (_host is null)
            {
                _host = new TickerHost { HostedByOpsis = true, LanguageFollowsHost = LanguageFromHost };
                if (!_host.Config.ThemeIndependentMigrated)
                {
                    _host.Config.ThemeIndependentMigrated = true;
                    _host.Config.ThemeMode = "host";
                    try { _host.Config.Save(); } catch (Exception) { }
                }
                _host.ApplyConfig(); // HostedByOpsis changes the default for reserving screen space
                _host.SettingsRequested += () => SettingsRequested?.Invoke();
            }
            return _host;
        }
    }

    public void ShowFallbackSettings()
    {
        _fallbackSettings ??= new SettingsForm(Host);
        _fallbackSettings.ShowSettings();
    }

    private readonly InstanceGuard _guard = new();

    /// <summary>True when the stand-alone program is running: the plugin then starts no second bar (one crawler, one settings file).</summary>
    public bool StandaloneRunning { get; private set; }

    public void Start()
    {
        if (!_guard.TryAcquire())
        {
            StandaloneRunning = true;
            Log.Write("Plugin: the stand-alone Opsis News Crawler is running; the plugin does not start a second bar.");
            return;
        }
        StandaloneRunning = false;
        Host.Start();
    }

    public void Stop()
    {
        _host?.Stop();
        _guard.Release();
    }
    public void Pause() => _host?.Pause();
    public void Resume() => _host?.Resume();
    public void Refresh() => _host?.Refresh();
    public void SetMode(TickerMode mode) => Host.SetMode(mode);
    public void SetPosition(TickerPosition position) => Host.SetPosition(position);
}
