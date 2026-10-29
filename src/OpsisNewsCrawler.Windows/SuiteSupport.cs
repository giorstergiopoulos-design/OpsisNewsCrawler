using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>Reads (never writes) what the machine has: files, file versions and the uninstall list of Windows.</summary>
internal sealed class WindowsInstallProbe : IInstallProbe
{
    public bool FileExists(string path)
    {
        try { return File.Exists(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }

    public string? FileVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var v = info.ProductVersion ?? info.FileVersion;
            if (string.IsNullOrWhiteSpace(v)) return null;
            var cut = v.IndexOf('+');
            return cut > 0 ? v[..cut] : v;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    public string Expand(string text) => Environment.ExpandEnvironmentVariables(text);

    public IReadOnlyList<UninstallEntry> UninstallEntries()
    {
        var list = new List<UninstallEntry>();
        void Read(RegistryKey root, string path)
        {
            try
            {
                using var key = root.OpenSubKey(path);
                if (key is null) return;
                foreach (var name in key.GetSubKeyNames())
                {
                    using var sub = key.OpenSubKey(name);
                    if (sub?.GetValue("DisplayName") is not string display || display.Length == 0) continue;
                    list.Add(new UninstallEntry(display, sub.GetValue("DisplayVersion") as string, sub.GetValue("InstallLocation") as string));
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException) { }
        }
        const string Uninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
        Read(Registry.CurrentUser, Uninstall);
        Read(Registry.LocalMachine, Uninstall);
        Read(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        return list;
    }
}

/// <summary>
/// Downloads the setup of an app of the family: only after the user confirmed file name, size and source; only from the app's own GitHub releases;
/// the SHA-256 must match (GitHub's digest or the .sha256 file) or the file is deleted; it is NEVER started.
/// </summary>
internal static class SuiteDownloader
{
    private const long MaxBytes = 600L * 1024 * 1024;

    private static HttpClient NewClient(int seconds)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(seconds) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler-Suite");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>The latest release's tag and setup (null setup when there is none usable). Network problems give (null, null).</summary>
    public static async Task<(string? Tag, SuiteDownload? Download)> LatestAsync(SuiteCatalog catalog, SuiteApp app, CancellationToken ct)
    {
        try
        {
            using var http = NewClient(8);
            var json = await http.GetStringAsync(SuiteReleases.LatestApiUrl(catalog, app), ct).ConfigureAwait(false);
            var pick = SuiteReleases.PickDownload(json, catalog, app, out var tag);
            return (tag, pick);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            if (ct.IsCancellationRequested) throw;
            return (null, null);
        }
    }

    public static string DownloadsFolder()
    {
        try
        {
            var path = SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out var ptr) == 0 ? Marshal.PtrToStringUni(ptr) : null;
            if (ptr != IntPtr.Zero) Marshal.FreeCoTaskMem(ptr);
            if (!string.IsNullOrEmpty(path)) return path;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);

    private static string FreeName(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        for (var i = 1; File.Exists(path) && i < 1000; i++)
            path = Path.Combine(folder, Path.GetFileNameWithoutExtension(fileName) + $" ({i})" + Path.GetExtension(fileName));
        return path;
    }

    /// <summary>Returns the saved path, or throws <see cref="SuiteDownloadException"/> with the reason (mismatch, size, network).</summary>
    public static async Task<string> DownloadAsync(SuiteDownload d, IProgress<int> progress, CancellationToken ct)
    {
        if (!SuiteCatalog.IsAllowedSource(d.Url) || !DownloadPolicy.IsSafeFileName(d.FileName)) throw new SuiteDownloadException("address not allowed", mismatch: false);
        var expected = d.Sha256;
        using var http = NewClient(600);
        try
        {
            if (expected is null && d.ChecksumUrl is not null && SuiteCatalog.IsAllowedSource(d.ChecksumUrl))
            {
                var text = await DownloadPolicy.ReadLimitedTextAsync(http, d.ChecksumUrl, 4096, ct).ConfigureAwait(false);
                if (text is not null) expected = Sha256Check.ParseChecksumFile(text);
            }
            if (expected is null) throw new SuiteDownloadException("no checksum", mismatch: false);

            var folder = DownloadsFolder();
            Directory.CreateDirectory(folder);
            var target = FreeName(folder, d.FileName);
            var temp = target + ".part";
            using (var response = await http.GetAsync(d.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (!DownloadPolicy.IsTrustedRedirectTarget(response.RequestMessage?.RequestUri)) throw new SuiteDownloadException("address not allowed", mismatch: false);
                var total = response.Content.Headers.ContentLength ?? d.Size;
                if (total > MaxBytes) throw new SuiteDownloadException("file too large", mismatch: false);
                await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var output = File.Create(temp);
                var buffer = new byte[81920];
                long done = 0;
                int read;
                var last = -1;
                while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    done += read;
                    if (done > MaxBytes) throw new SuiteDownloadException("file too large", mismatch: false);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    var pct = total > 0 ? (int)Math.Min(100, done * 100 / total) : 0;
                    if (pct != last) { last = pct; progress.Report(pct); }
                }
            }

            if (!Sha256Check.Matches(temp, expected))
            {
                try { File.Delete(temp); } catch (IOException) { }
                throw new SuiteDownloadException("sha256 mismatch", mismatch: true);
            }
            File.Move(temp, target);
            return target;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            if (ct.IsCancellationRequested) throw;
            throw new SuiteDownloadException(ex.Message, mismatch: false);
        }
    }

    public static string FormatSize(long bytes) => bytes <= 0 ? "?" : bytes >= 1024 * 1024 ? $"{bytes / 1048576.0:0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}

internal sealed class SuiteDownloadException : Exception
{
    public SuiteDownloadException(string message, bool mismatch) : base(message) => Mismatch = mismatch;
    public bool Mismatch { get; }
}

/// <summary>Where a closing splash goes: the header icon of the settings window if it is open, else the notification area (tray icon).</summary>
public static class MinimiseTarget
{
    public static Rectangle Tray()
    {
        var screen = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).Bounds;
        var notify = Native.GetTrayNotifyRect();
        // With an auto-hiding taskbar the notification area is off the screen: aim at the corner where it will come back.
        if (!notify.IsEmpty && notify.Top < screen.Bottom - 6 && notify.Width > 0)
            return new Rectangle(notify.Left + 6, notify.Top + Math.Max(0, (notify.Height - 24) / 2), 24, 24);
        return new Rectangle(screen.Right - 90, screen.Bottom - 34, 24, 24);
    }
}

/// <summary>The shrink-and-fade of a closing splash (scale + move + fade, about 400 ms; just a fade when animations are off or the PC is low-end).</summary>
internal static class MinimiseAnimation
{
    public const int DurationMs = 400;

    public static bool Simple => TickerWindow.LowPower || !SystemInformation.UIEffectsEnabled;

    /// <summary>Rectangle and opacity at time <paramref name="t"/> (0..1); the pure part of the animation, drawn by <see cref="SplashGhost"/> and by the snapshots.</summary>
    public static (RectangleF Bounds, double Opacity) At(Rectangle from, Rectangle to, double t, bool simple)
    {
        t = Math.Clamp(t, 0, 1);
        if (simple) return (from, 1 - t);
        var move = t * t * (3 - 2 * t);                  // smooth in and out
        var x = from.X + (to.X - from.X) * move;
        var y = from.Y + (to.Y - from.Y) * move;
        var w = from.Width + (to.Width - from.Width) * move;
        var h = from.Height + (to.Height - from.Height) * move;
        var fade = t < 0.55 ? 1.0 : 1 - (t - 0.55) / 0.45;
        return (new RectangleF((float)x, (float)y, (float)w, (float)h), Math.Clamp(fade, 0, 1));
    }

    /// <summary>Hides <paramref name="form"/>, runs the animation with a picture of it, then calls <paramref name="done"/>.</summary>
    public static void Run(Form form, Rectangle target, Action done)
    {
        Bitmap picture;
        try
        {
            picture = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height));
            form.DrawToBitmap(picture, new Rectangle(Point.Empty, picture.Size));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ExternalException)
        {
            done();
            return;
        }

        var from = form.Bounds;
        var ghost = new SplashGhost(picture) { Bounds = from };
        var simple = Simple;
        ghost.Show();
        form.Hide();
        var clock = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            var t = clock.ElapsedMilliseconds / (double)(simple ? DurationMs / 2 : DurationMs);
            var (bounds, opacity) = At(from, target, t, simple);
            ghost.Bounds = Rectangle.Round(bounds);
            ghost.Opacity = opacity;
            if (t < 1) return;
            timer.Stop();
            timer.Dispose();
            ghost.Close();
            ghost.Dispose();
            done();
        };
        timer.Start();
    }
}

/// <summary>A borderless picture of a splash that shrinks towards its target while the real window is already hidden.</summary>
internal sealed class SplashGhost : Form
{
    private readonly Bitmap picture;

    public SplashGhost(Bitmap picture)
    {
        this.picture = picture;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.Black;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x80 | 0x20;   // WS_EX_NOACTIVATE, TOOLWINDOW, TRANSPARENT: never takes focus or clicks
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(picture, ClientRectangle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) picture.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>The pictures of the apps of the family, embedded in the program (one square PNG per catalogue id), so every card shows its real icon even when the app is not installed.</summary>
internal static class SuiteIcons
{
    public static Bitmap? Load(string id)
    {
        try
        {
            using var stream = typeof(OpsisNewsCrawler.Core.SuiteCatalog).Assembly.GetManifestResourceStream($"OpsisNewsCrawler.Suite.icons.{id}.png");
            return stream is null ? null : new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException) { return null; }
    }

    public static bool Has(string id) => typeof(OpsisNewsCrawler.Core.SuiteCatalog).Assembly.GetManifestResourceNames().Contains($"OpsisNewsCrawler.Suite.icons.{id}.png");
}
