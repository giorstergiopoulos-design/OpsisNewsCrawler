using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OpsisNewsCrawler.Core;
using System.Drawing.Drawing2D;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Preview of a live video source (a YouTube live stream or a web TV page) in a rectangular window above the bar.
/// It is opened by the user from the pulsing "live" button (never automatically) and has play/pause, maximize/restore and close.
/// </summary>
internal sealed class LivePreviewForm : Form
{
    private const int Skew = 0; // a plain rectangle
    private const int ToolbarHeight = 44;

    private readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
    private readonly Panel toolbar = new() { Dock = DockStyle.Bottom, Height = ToolbarHeight };
    private readonly FlatButton playPause = new();
    private readonly FlatButton maximize = new();
    private readonly FlatButton close = new();
    private readonly Label title = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label message = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
    private readonly string url;
    private readonly string name;
    private Rectangle normalBounds;
    private bool maximized;
    private bool playing = true;

    public LivePreviewForm(string name, string url, Rectangle anchorBar)
    {
        this.name = name;
        this.url = url;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Text = Loc.T("app.name") + " — " + name;
        Icon = AppIcon.Get();
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5f);
        KeyPreview = true;

        // 16:9 picture + toolbar, above the bar and towards its right end.
        var width = 520;
        var height = (int)((width - Skew) * 9 / 16.0) + ToolbarHeight;
        var screen = Screen.FromRectangle(anchorBar).WorkingArea;
        var x = Math.Max(screen.Left, Math.Min(anchorBar.Right - width - 16, screen.Right - width));
        var y = Math.Max(screen.Top, anchorBar.Top - height - 10);
        normalBounds = new Rectangle(x, y, width, height);
        Bounds = normalBounds;

        playPause.Text = "⏸";
        maximize.Text = "⤢";
        close.Text = "✕";
        foreach (var b in new[] { playPause, maximize, close })
        {
            b.Padding = new Padding(10, 3, 10, 3);
            b.Font = new Font("Segoe UI Symbol", 11f);
            b.Margin = new Padding(0, 6, 6, 6);
        }
        playPause.Click += async (_, _) => await TogglePlayAsync();
        maximize.Click += (_, _) => ToggleMaximize();
        close.Click += (_, _) => Close();

        title.Text = name;
        title.ForeColor = Theme.TextMuted;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 0, 8, 0) };
        buttons.Controls.AddRange(new Control[] { playPause, maximize, close });
        toolbar.Controls.Add(buttons);
        title.Dock = DockStyle.Fill;
        title.Padding = new Padding(14, 0, 0, 0);
        toolbar.Controls.Add(title);
        toolbar.BackColor = Theme.Surface;
        buttons.BackColor = Theme.Surface;
        title.BackColor = Theme.Surface;

        message.ForeColor = Theme.TextMuted;
        Controls.Add(message);
        Controls.Add(web);
        Controls.Add(toolbar);
        web.BringToFront();

        Resize += (_, _) => ApplyShape();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (maximized) ToggleMaximize();
                else Close();
            }
        };
        Load += async (_, _) => await StartAsync();
        ApplyShape();
    }

    /// <summary>The rectangle (or the whole screen when maximized).</summary>
    private void ApplyShape()
    {
        if (maximized)
        {
            Region = null;
            return;
        }
        Region = null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (maximized) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Theme.Accent, 3f);
        e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
    }

    private async Task StartAsync()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpsisNewsCrawler", "webview2");
            var env = await CoreWebView2Environment.CreateAsync(null, folder);
            await web.EnsureCoreWebView2Async(env);
            var settings = web.CoreWebView2.Settings;
            settings.AreDevToolsEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;
            web.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true; // links stay in the preview
            // A web page of the user's choice runs here: no camera/microphone/location, no downloads, only http(s).
            web.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            web.CoreWebView2.DownloadStarting += (_, e) => e.Cancel = true;
            web.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) || target.Scheme is not ("http" or "https" or "about")) e.Cancel = true;
            };
            settings.AreHostObjectsAllowed = false;
            settings.IsWebMessageEnabled = false;
            web.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            web.Visible = false;
            message.Text = ex is WebView2RuntimeNotFoundException ? Loc.T("live.noRuntime") : Loc.T("live.error", ex.GetType().Name + ": " + ex.Message);
            message.Visible = true;
            message.BringToFront();
        }
    }

    private async Task TogglePlayAsync()
    {
        if (web.CoreWebView2 is null) return;
        try
        {
            // Works for the YouTube player page and for any page with a <video> element.
            var script = "(function(){var v=document.querySelector('video');if(!v)return 'none';if(v.paused){v.play();return 'playing';}v.pause();return 'paused';})()";
            var result = await web.CoreWebView2.ExecuteScriptAsync(script);
            playing = result.Contains("playing") ? true : result.Contains("paused") ? false : playing;
            playPause.Text = playing ? "⏸" : "▶";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
        }
    }

    private void ToggleMaximize()
    {
        if (!maximized)
        {
            normalBounds = Bounds;
            maximized = true;
            Bounds = Screen.FromControl(this).Bounds;
            maximize.Text = "⤡";
            title.Padding = new Padding(16, 0, 0, 0);
        }
        else
        {
            maximized = false;
            Bounds = normalBounds;
            maximize.Text = "⤢";
            title.Padding = new Padding(14, 0, 0, 0);
        }
        ApplyShape();
        Invalidate();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW: not in Alt+Tab
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.MarkNoSnap(Handle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            web.Dispose();
            foreach (var b in new[] { playPause, maximize, close }) b.Font.Dispose();
            Font.Dispose();
        }
        base.Dispose(disposing);
    }
}
