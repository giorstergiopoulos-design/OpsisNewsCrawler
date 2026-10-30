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

        if (Array.IndexOf(args, "--snapshot-bar") is >= 0 and var barAt && barAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            BarSnapshot.Save(args[barAt + 1]);
            return;
        }

        if (Array.IndexOf(args, "--snapshot-welcome") is >= 0 and var wAt && wAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgW = Array.IndexOf(args, "--config");
            using var wHost = new TickerHost(cfgW >= 0 && cfgW + 1 < args.Length ? args[cfgW + 1] : null);
            SuiteSnapshots.Welcome(args[wAt + 1]);
            return;
        }

        if (Array.IndexOf(args, "--snapshot-suite") is >= 0 and var sAt && sAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgS = Array.IndexOf(args, "--config");
            using var sHost = new TickerHost(cfgS >= 0 && cfgS + 1 < args.Length ? args[cfgS + 1] : null);
            SuiteSnapshots.Suite(args[sAt + 1], sHost.DataDirectory);
            return;
        }

        if (Array.IndexOf(args, "--snapshot-minimise") is >= 0 and var mAt && mAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgM = Array.IndexOf(args, "--config");
            using var mHost = new TickerHost(cfgM >= 0 && cfgM + 1 < args.Length ? args[cfgM + 1] : null);
            SuiteSnapshots.Minimise(args[mAt + 1]);
            return;
        }
        if (Array.IndexOf(args, "--audit-settings") is >= 0 and var auAt && auAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgAu = Array.IndexOf(args, "--config");
            using var auHost = new TickerHost(cfgAu >= 0 && cfgAu + 1 < args.Length ? args[cfgAu + 1] : null);
            Environment.ExitCode = SettingsAudit.Run(auHost, args[auAt + 1]) ? 0 : 1;   // CI fails on a finding
            return;
        }
        if (Array.IndexOf(args, "--make-brand-art") is >= 0 and var baAt && baAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            BrandArt.SaveAll(args[baAt + 1]);
            return;
        }
        if (Array.IndexOf(args, "--snapshot-geo") is >= 0 and var geAt && geAt + 2 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgGe = Array.IndexOf(args, "--config");
            using var geHost = new TickerHost(cfgGe >= 0 && cfgGe + 1 < args.Length ? args[cfgGe + 1] : null);
            GeoSnapshot.Run(geHost, args[geAt + 1], args[geAt + 2]);
            return;
        }
        if (Array.IndexOf(args, "--snapshot-live-icon") is >= 0 and var liAt && liAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            LiveIconSnapshot.Save(args[liAt + 1]);
            return;
        }

        if (Array.IndexOf(args, "--snapshot-hazard-card") is >= 0 and var hcAt && hcAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var result = HazardCardSnapshot.Save(args[hcAt + 1]);
            File.WriteAllText(args[hcAt + 1] + ".txt", result);
            return;
        }

        if (Array.IndexOf(args, "--snapshot-hazard-bar") is >= 0 and var hbAt && hbAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            HazardBarSnapshot.Save(args[hbAt + 1]);
            return;
        }

        if (Array.IndexOf(args, "--snapshot-hazard-icons") is >= 0 and var hiAt && hiAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            HazardIconSheet.Save(args[hiAt + 1]);
            return;
        }

        if (Array.IndexOf(args, "--snapshot-tall") is >= 0 and var taAt && taAt + 2 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgTa = Array.IndexOf(args, "--config");
            using var taHost = new TickerHost(cfgTa >= 0 && cfgTa + 1 < args.Length ? args[cfgTa + 1] : null);
            TallSnapshot.Run(taHost, args[taAt + 1], int.Parse(args[taAt + 2]), taAt + 3 < args.Length && int.TryParse(args[taAt + 3], out var tallHeight) ? tallHeight : 2200);
            return;
        }
        if (Array.IndexOf(args, "--audit-layout") is >= 0 and var lyAt && lyAt + 1 < args.Length)
        {
            ApplicationConfiguration.Initialize();
            var cfgLy = Array.IndexOf(args, "--config");
            using var lyHost = new TickerHost(cfgLy >= 0 && cfgLy + 1 < args.Length ? args[cfgLy + 1] : null);
            Environment.ExitCode = LayoutAudit.Run(lyHost, args[lyAt + 1]) ? 0 : 1;
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

        // A probe (own --config, snapshot, audit, test switch) never touches the real registry (start-up entry, uninstall entries).
        WinRegistry.Configure(args);

        // Only one ticker at a time.
        // A run with its own --config (tests, a second profile) does not collide with the normal instance.
        var configArgAt = Array.IndexOf(args, "--config");
        var mutexName = configArgAt >= 0 && configArgAt + 1 < args.Length
            ? "OpsisNewsCrawler.SingleInstance." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(args[configArgAt + 1]).ToLowerInvariant())))[..16]
            : "OpsisNewsCrawler.SingleInstance";
        using var singleInstance = new Mutex(true, mutexName, out var createdNew);
        if (!createdNew)
        {
            // The default instance may be the plugin inside Opsis or an earlier start: say so instead of doing nothing.
            if (configArgAt < 0 && !Environment.GetCommandLineArgs().Skip(1).Any(a => a.StartsWith("--", StringComparison.Ordinal)))
            {
                ApplicationConfiguration.Initialize();
                MessageBox.Show(Loc.T("app.alreadyRunning"), Loc.T("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return;
        }

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
        var trayTimer = new System.Windows.Forms.Timer { Interval = 60 };
        // The weather and alert symbols follow the live state: the frames are drawn again when it changes (checked on the UI thread by this timer).
        var liveDirty = 0;
        LiveIconState.Changed += () => Interlocked.Exchange(ref liveDirty, 1);
        trayTimer.Tick += (_, _) =>
        {
            if (Interlocked.Exchange(ref liveDirty, 0) == 1)
            {
                trayFrames = AppIconArt.BuildTrayFrames(Math.Max(16, SystemInformation.SmallIconSize.Width));
                var tip = LiveIconState.Tip;
                tray.Text = tip.Length == 0 ? "Opsis News Crawler" : ("Opsis News Crawler\n" + tip).Substring(0, Math.Min(("Opsis News Crawler\n" + tip).Length, 127));
            }
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
        var menuWelcome = menu.Items.Add(Loc.T("tray.welcome"), null, (_, _) => host.ShowWelcome());
        var menuSuite = menu.Items.Add(Loc.T("tray.suite"), null, (_, _) => host.ShowSuite());
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
            menuWelcome.Text = Loc.T("tray.welcome");
            menuSuite.Text = Loc.T("tray.suite");
            UpdatePauseItem();
            snoozeMenu.Text = Loc.T("pk.snooze.bars");
            snooze15.Text = Loc.T("pk.for.15.minutes");
            snooze60.Text = Loc.T("pk.for.60.minutes");
            cancelSnooze.Text = Loc.T("pk.cancel.snooze");
            menuExit.Text = Loc.T("tray.exit");
        };
        tray.DoubleClick += (_, _) => settings.ShowSettings();

        host.SettingsRequested += settings.ShowSettings; // right-click on the strip

        // A closing splash shrinks into the header icon of the settings window when that is open, otherwise into the tray area.
        host.MinimiseTargetProvider = () =>
        {
            var header = settings.HeaderIconScreenRect();
            return header.IsEmpty ? MinimiseTarget.Tray() : header;
        };
        InstallChecks.Run(host);
        host.Start();

        if (args.Contains("--portfolio")) host.ShowPortfolio(); // handy for testing
        if (args.Contains("--welcome")) host.ShowWelcome();     // test switches: the guide / the family window at once
        if (args.Contains("--suite")) host.ShowSuite();
        if (args.Contains("--test-welcome-close"))
        {
            var closer = new System.Windows.Forms.Timer { Interval = 5000 };
            closer.Tick += (_, _) => { closer.Stop(); host.CloseWelcome(); };
            closer.Start();
        }

        // First start of a new major/minor version: the welcome guide, a moment after the bar is up (nothing waits for it).
        if (!args.Contains("--welcome") && host.ShouldShowWelcome())
        {
            var first = new System.Windows.Forms.Timer { Interval = 2500 };
            first.Tick += (_, _) => { first.Stop(); first.Dispose(); host.ShowWelcome(); };
            first.Start();
        }

        if (args.Contains("--test-weather-alert"))
        {
            var tw = new System.Windows.Forms.Timer { Interval = 6000 };
            tw.Tick += (_, _) => { tw.Stop(); host.SimulateWeatherAlert(); };
            tw.Start();
        }

        if (args.Contains("--test-taskbar-hidden"))
        {
            // Behaves as if the auto-hiding taskbar were hidden, to see the desktop bar that stands in for it.
            var th = new System.Windows.Forms.Timer { Interval = 6000 };
            th.Tick += (_, _) => { th.Stop(); host.SimulateTaskbarHidden(true); };
            th.Start();
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
