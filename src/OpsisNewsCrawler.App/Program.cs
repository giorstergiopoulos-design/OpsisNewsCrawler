using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;

namespace OpsisNewsCrawler.App;

internal static class Program
{
    // WinForms, the clipboard, drag and drop and WebView2 (live preview) all need a single-threaded apartment.
    [STAThread]
    private static void Main(string[] args)
    {
        if (Array.IndexOf(args, "--make-icon") is >= 0 and var iconAt && iconAt + 1 < args.Length)
        {
            // Writes the program icon (Assets\app.ico) from the drawing in AppIconArt; also a strip of animation frames next to it.
            var target = args[iconAt + 1];
            AppIconArt.WriteIcoFile(target);
            using (var sheet = new Bitmap(256 * 4, 256 * 2))
            using (var g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(30, 30, 34));
                for (var i = 0; i < 8; i++)
                {
                    using var frame = AppIconArt.Render(256, i / 8.0);
                    g.DrawImage(frame, (i % 4) * 256, (i / 4) * 256);
                }
                sheet.Save(Path.ChangeExtension(target, ".frames.png"));
            }
            using (var small = new Bitmap(900, 260))
            using (var g2 = Graphics.FromImage(small))
            {
                g2.Clear(Color.FromArgb(32, 32, 36));
                g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g2.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                var x = 10;
                foreach (var size in new[] { 16, 24, 32, 48, 64 })
                {
                    using var one = AppIconArt.Render(size, 0.1);
                    g2.DrawImage(one, x, 10, size * 3, size * 3);
                    x += size * 3 + 16;
                }
                small.Save(Path.ChangeExtension(target, ".sizes.png"));
            }
            return;
        }

        if (Array.IndexOf(args, "--snapshot") is >= 0 and var at && at + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgAt = Array.IndexOf(args, "--config");
            using var snapHost = new TickerHost(cfgAt >= 0 && cfgAt + 1 < args.Length ? args[cfgAt + 1] : null);
            using var snapForm = new SettingsForm(snapHost);
            snapForm.SaveSnapshots(args[at + 1]);
            return;
        }

        if (Array.IndexOf(args, "--snapshot-portfolio") is >= 0 and var pfAt && pfAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgPf = Array.IndexOf(args, "--config");
            using var pfHost = new TickerHost(cfgPf >= 0 && cfgPf + 1 < args.Length ? args[cfgPf + 1] : null);
            pfHost.SnapshotPortfolio(args[pfAt + 1], 14);
            return;
        }

        if (Array.IndexOf(args, "--snapshot-analysis") is >= 0 and var anAt && anAt + 2 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgAn = Array.IndexOf(args, "--config");
            using var anHost = new TickerHost(cfgAn >= 0 && cfgAn + 1 < args.Length ? args[cfgAn + 1] : null);
            anHost.SnapshotAnalysis(args[anAt + 1], int.Parse(args[anAt + 2]), 10);
            return;
        }

        // Only one ticker at a time.
        using var singleInstance = new Mutex(true, "OpsisNewsCrawler.SingleInstance", out var createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();

        var configAt = Array.IndexOf(args, "--config");
        using var host = new TickerHost(configAt >= 0 && configAt + 1 < args.Length ? args[configAt + 1] : null);
        var settings = new SettingsForm(host);

        using var tray = new NotifyIcon
        {
            Icon = AppIcon.Get(),
            Visible = true,
            Text = "Opsis News Crawler"
        };

        // The icon of the notification area: a little bar with the news passing by (right to left). The frames are drawn once;
        // the timer only swaps them, and stops swapping when the animation is off or the bars are switched off.
        var staticIcon = AppIcon.Get();
        var trayFrames = AppIconArt.BuildTrayFrames(Math.Max(16, SystemInformation.SmallIconSize.Width));
        var trayFrame = 0;
        var trayAnimating = false;
        var trayTimer = new System.Windows.Forms.Timer { Interval = 140 };
        trayTimer.Tick += (_, _) =>
        {
            var animate = host.Config.TrayIconAnimated && host.Config.BarsEnabled;
            if (animate)
            {
                trayFrame = (trayFrame + 1) % trayFrames.Length;
                tray.Icon = trayFrames[trayFrame];
            }
            else if (trayAnimating)
            {
                tray.Icon = staticIcon;
            }
            trayAnimating = animate;
        };
        trayTimer.Start();

        var menu = new ContextMenuStrip();
        var menuBars = new ToolStripMenuItem(Loc.T("chk.barsEnabled")) { Checked = host.Config.BarsEnabled };
        menuBars.Click += (_, _) => host.SetBarsEnabled(!host.Config.BarsEnabled);
        host.BarsEnabledChanged += () => menuBars.Checked = host.Config.BarsEnabled;
        menu.Items.Add(menuBars);
        var snoozeMenu = new ToolStripMenuItem(Loc.T("pk.snooze.bars"));
        var snooze15 = new ToolStripMenuItem(Loc.T("pk.for.15.minutes"));
        var snooze60 = new ToolStripMenuItem(Loc.T("pk.for.60.minutes"));
        var cancelSnooze = new ToolStripMenuItem(Loc.T("pk.cancel.snooze")) { Enabled = false };
        snooze15.Click += (_, _) => host.SnoozeBars(TimeSpan.FromMinutes(15));
        snooze60.Click += (_, _) => host.SnoozeBars(TimeSpan.FromMinutes(60));
        cancelSnooze.Click += (_, _) => host.CancelSnooze();
        snoozeMenu.DropDownItems.AddRange(new ToolStripItem[] { snooze15, snooze60, cancelSnooze });
        host.SnoozeChanged += active => cancelSnooze.Enabled = active;
        menu.Items.Add(snoozeMenu);
        var menuPortfolio = menu.Items.Add(Loc.T("tray.portfolio"), null, (_, _) => host.ShowPortfolio());
        var menuAnalysis = menu.Items.Add(Loc.T("tray.analysis"), null, (_, _) => host.ShowAnalysis());
        var menuSettings = menu.Items.Add(Loc.T("tray.settings"), null, (_, _) => settings.ShowSettings());
        var menuRefresh = menu.Items.Add(Loc.T("tray.refresh"), null, (_, _) => host.Refresh());
        // One item that switches between Pause and Resume.
        var menuPause = menu.Items.Add(Loc.T("tray.pause"), null, (_, _) => { if (host.Paused) host.Resume(); else host.Pause(); });
        void UpdatePauseItem() => menuPause.Text = Loc.T(host.Paused ? "tray.resume" : "tray.pause");
        host.PausedChanged += UpdatePauseItem;
        menu.Items.Add(new ToolStripSeparator());
        var menuExit = menu.Items.Add(Loc.T("tray.exit"), null, (_, _) =>
        {
            host.Stop();
            tray.Visible = false;
            Application.Exit();
        });
        tray.ContextMenuStrip = menu;
        // A new version: one notification per version; a click on it opens the settings (Information > Updates).
        host.UpdateAnnounce += info =>
        {
            tray.BalloonTipTitle = Loc.T("upd.notifyTitle");
            tray.BalloonTipText = Loc.T("upd.notifyBody", info.Version);
            tray.BalloonTipIcon = ToolTipIcon.Info;
            tray.ShowBalloonTip(10000);
        };
        tray.BalloonTipClicked += (_, _) => settings.ShowSettings();
        // After an update setup was handed over, the program ends so the installer can replace its files and open it again.
        host.ExitRequested += () =>
        {
            host.Stop();
            tray.Visible = false;
            Application.Exit();
        };
        Loc.Changed += () =>
        {
            menuBars.Text = Loc.T("chk.barsEnabled");
            menuPortfolio.Text = Loc.T("tray.portfolio");
            menuAnalysis.Text = Loc.T("tray.analysis");
            menuSettings.Text = Loc.T("tray.settings");
            menuRefresh.Text = Loc.T("tray.refresh");
            UpdatePauseItem();
            snoozeMenu.Text = Loc.T("pk.snooze.bars");
            snooze15.Text = Loc.T("pk.for.15.minutes");
            snooze60.Text = Loc.T("pk.for.60.minutes");
            cancelSnooze.Text = Loc.T("pk.cancel.snooze");
            menuExit.Text = Loc.T("tray.exit");
        };
        tray.DoubleClick += (_, _) => settings.ShowSettings();

        host.SettingsRequested += settings.ShowSettings; // right-click on the strip
        host.Start();

        if (args.Contains("--portfolio")) host.ShowPortfolio(); // handy for testing

        if (args.Contains("--test-weather-alert"))
        {
            var tw = new System.Windows.Forms.Timer { Interval = 6000 };
            tw.Tick += (_, _) => { tw.Stop(); host.SimulateWeatherAlert(); };
            tw.Start();
        }

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

        if (host.Config.IsFirstRun && !args.Contains("--settings"))
        {
            // First start ever: show the settings once so the owner can pick language, content and position.
            settings.SelectTab(0);
            settings.ShowSettings();
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
