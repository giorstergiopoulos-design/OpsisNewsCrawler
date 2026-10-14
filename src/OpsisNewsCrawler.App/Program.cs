using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;

namespace OpsisNewsCrawler.App;

internal static class Program
{
    // WinForms, the clipboard, drag and drop and WebView2 (live preview) all need a single-threaded apartment.
    [STAThread]
    private static void Main(string[] args)
    {
        if (Array.IndexOf(args, "--snapshot") is >= 0 and var at && at + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgAt = Array.IndexOf(args, "--config");
            using var snapHost = new TickerHost(cfgAt >= 0 && cfgAt + 1 < args.Length ? args[cfgAt + 1] : null);
            using var snapForm = new SettingsForm(snapHost);
            snapForm.SaveSnapshots(args[at + 1]);
            return;
        }

        // Only one ticker at a time.
        using var singleInstance = new Mutex(true, "OpsisNewsCrawler.SingleInstance", out var createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();

        using var host = new TickerHost();
        var settings = new SettingsForm(host);

        using var tray = new NotifyIcon
        {
            Icon = AppIcon.Get(),
            Visible = true,
            Text = "Opsis News Crawler"
        };

        var menu = new ContextMenuStrip();
        var menuSettings = menu.Items.Add(Loc.T("tray.settings"), null, (_, _) => settings.ShowSettings());
        var menuRefresh = menu.Items.Add(Loc.T("tray.refresh"), null, (_, _) => host.Refresh());
        menu.Items.Add(new ToolStripSeparator());
        var menuExit = menu.Items.Add(Loc.T("tray.exit"), null, (_, _) =>
        {
            host.Stop();
            tray.Visible = false;
            Application.Exit();
        });
        tray.ContextMenuStrip = menu;
        Loc.Changed += () =>
        {
            menuSettings.Text = Loc.T("tray.settings");
            menuRefresh.Text = Loc.T("tray.refresh");
            menuExit.Text = Loc.T("tray.exit");
        };
        tray.DoubleClick += (_, _) => settings.ShowSettings();

        host.SettingsRequested += settings.ShowSettings; // right-click on the strip
        host.Start();

        if (args.Contains("--test-breaking"))
        {
            // Simulates an urgent headline a few seconds after the start (for testing the red bar and the live button).
            var t = new System.Windows.Forms.Timer { Interval = 6000 };
            t.Tick += (_, _) =>
            {
                t.Stop();
                host.SimulateBreaking("Έκτακτο: δοκιμαστική είδηση για τον έλεγχο της μπάρας");
            };
            t.Start();
        }

        if (args.Contains("--settings"))
        {
            var tabArg = Array.IndexOf(args, "--tab");
            if (tabArg >= 0 && tabArg + 1 < args.Length && int.TryParse(args[tabArg + 1], out var tab)) settings.SelectTab(tab);
            settings.ShowSettings(); // handy for testing and shortcuts
        }

        Application.Run(host.Window);
    }
}
