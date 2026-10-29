using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OpsisNewsCrawler.Core;
using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Opened by a click on an event of the disaster line: kind, place, magnitude (with the spread between the organisations), depth, time, alert
/// level, the organisations that report it with a link to each, and the official page, on top of a live OpenStreetMap map. Same window
/// style as the weather map (rectangular, above the bar, maximize / close). Links open in the default browser, never inside the window.
/// </summary>
internal sealed class HazardDetailForm : Form
{
    private const int ToolbarHeight = 44;
    private const string TileReferer = "https://github.com/giorstergiopoulos-design/OpsisNewsCrawler";

    private readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(30, 30, 30) };
    private readonly Panel toolbar = new() { Dock = DockStyle.Bottom, Height = ToolbarHeight };
    private readonly FlatButton maximize = new();
    private readonly FlatButton close = new();
    private readonly Label title = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label message = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
    private readonly HashSet<string> allowedLinks;
    private readonly string html;
    private Rectangle normalBounds;
    private bool maximized;

    public HazardDetailForm(HazardEvent hazard, Rectangle anchorBar)
    {
        allowedLinks = Links(hazard).Select(l => l.Url).ToHashSet(StringComparer.Ordinal);
        html = BuildHtml(hazard, DateTimeOffset.Now);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Text = Loc.T("app.name") + " — " + HazardText.KindName(hazard.Kind);
        Icon = AppIcon.Get();
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5f);
        KeyPreview = true;

        var width = 600;
        var height = 420 + ToolbarHeight;
        var screen = Screen.FromRectangle(anchorBar).Bounds;
        var x = screen.Right - width;
        var y = anchorBar.Top - height;
        if (y < screen.Top) y = Math.Min(screen.Bottom - height, anchorBar.Bottom);
        normalBounds = new Rectangle(x, Math.Max(screen.Top, y), width, height);
        Bounds = normalBounds;

        maximize.Text = "⤢";
        close.Text = "✕";
        foreach (var b in new[] { maximize, close })
        {
            b.Padding = new Padding(10, 3, 10, 3);
            b.Font = new Font("Segoe UI Symbol", 11f);
            b.Margin = new Padding(0, 6, 6, 6);
        }
        maximize.Click += (_, _) => ToggleMaximize();
        close.Click += (_, _) => Close();

        title.Text = HazardText.Tip(hazard);
        title.ForeColor = Theme.TextMuted;
        title.BackColor = Theme.Surface;
        title.Dock = DockStyle.Fill;
        title.Padding = new Padding(14, 0, 0, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 0, 8, 0), BackColor = Theme.Surface };
        buttons.Controls.AddRange(new Control[] { maximize, close });
        toolbar.BackColor = Theme.Surface;
        toolbar.Controls.Add(buttons);
        toolbar.Controls.Add(title);

        message.ForeColor = Theme.TextMuted;
        Controls.Add(message);
        Controls.Add(web);
        Controls.Add(toolbar);
        web.BringToFront();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Escape) return;
            if (maximized) ToggleMaximize(); else Close();
        };
        Load += async (_, _) => await StartAsync();
    }

    /// <summary>The links of an event: one per organisation, then the official page of the first one when it is not among them.</summary>
    internal static IReadOnlyList<(string Source, string Url)> Links(HazardEvent e)
    {
        var list = new List<(string, string)>();
        if (e.SourceUrls is not null)
            foreach (var (source, url) in e.SourceUrls)
                if (IsWebLink(url)) list.Add((source, url));
        if (list.Count == 0 && IsWebLink(e.Url)) list.Add((e.Sources.FirstOrDefault() ?? "", e.Url));
        return list;
    }

    internal static bool IsWebLink(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(u.UserInfo);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (maximized) return;
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
            var core = web.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsWebMessageEnabled = false;
            core.NewWindowRequested += (_, e) => { e.Handled = true; OpenLink(e.Uri); };
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.NavigationStarting += (_, e) =>
            {
                // The page we generate stays; a click on one of the listed links goes to the default browser, anything else goes nowhere.
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) && target.Scheme is "data" or "about") return;
                e.Cancel = true;
                OpenLink(e.Uri);
            };
            core.AddWebResourceRequestedFilter("https://tile.openstreetmap.org/*", CoreWebView2WebResourceContext.Image);
            core.WebResourceRequested += (_, e) =>
            {
                try { e.Request.Headers.SetHeader("Referer", TileReferer); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { }
            };
            core.NavigateToString(html);
        }
        catch (Exception ex)
        {
            web.Visible = false;
            message.Text = ex is WebView2RuntimeNotFoundException ? Loc.T("live.noRuntime") : Loc.T("live.error", ex.GetType().Name + ": " + ex.Message);
            message.Visible = true;
            message.BringToFront();
        }
    }

    /// <summary>Opens one of the event's own links in the default browser (https only, and only an address that is listed on the card).</summary>
    private void OpenLink(string? url)
    {
        if (url is null || !allowedLinks.Contains(url) || !IsWebLink(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    private void ToggleMaximize()
    {
        if (!maximized)
        {
            normalBounds = Bounds;
            maximized = true;
            Bounds = Screen.FromControl(this).Bounds;
            maximize.Text = "⤡";
        }
        else
        {
            maximized = false;
            Bounds = normalBounds;
            maximize.Text = "⤢";
        }
        Invalidate();
    }

    /// <summary>The page: the map centred on the event with a box of the details. Every text is HTML-encoded, every link is one of the event's own https addresses.</summary>
    internal static string BuildHtml(HazardEvent e, DateTimeOffset now)
    {
        string H(string s) => WebUtility.HtmlEncode(s);
        var culture = CultureInfo.GetCultureInfo(Loc.IsGreek ? "el-GR" : "en-GB");
        var rows = new List<string>();
        var heading = HazardText.KindName(e.Kind) + (string.IsNullOrWhiteSpace(e.Place) ? "" : " — " + e.Place);
        if (e.Magnitude is { } m)
        {
            var text = "M" + m.ToString("0.0", culture);
            if (e.SourceCount > 1 && e.MagnitudeMin is { } lo && e.MagnitudeMax is { } hi && hi - lo >= 0.15)
                text += "  (" + H(Loc.T("hazard.range", lo.ToString("0.0", culture), hi.ToString("0.0", culture), e.SourceCount)) + ")";
            rows.Add("<b>" + H(Loc.T("hazard.detail.magnitude")) + ":</b> " + text);
        }
        if (e.DepthKm is { } d && d > 0) rows.Add("<b>" + H(Loc.T("hazard.detail.depth")) + ":</b> " + H(Loc.T("hazard.depth", Math.Round(d).ToString("0", culture))));
        rows.Add("<b>" + H(Loc.T("hazard.detail.time")) + ":</b> " + e.Time.ToLocalTime().ToString("dd/MM/yyyy HH:mm", culture));
        rows.Add("<b>" + H(Loc.T("hazard.detail.level")) + ":</b> <span class='lv" + Math.Clamp(e.Level, 1, 3) + "'>" + H(Loc.T("hazard.level." + Math.Clamp(e.Level, 1, 3))) + "</span>");
        rows.Add("<b>" + H(Loc.T("hazard.detail.position")) + ":</b> " + e.Lat.ToString("0.00", CultureInfo.InvariantCulture) + ", " + e.Lon.ToString("0.00", CultureInfo.InvariantCulture)
                 + " · " + H(Continents.Name(Continents.Of(e.Lat, e.Lon))));
        var links = Links(e);
        var reported = string.Join(", ", e.Sources.Select(s => links.FirstOrDefault(l => l.Source == s) is { Url: { Length: > 0 } url } ? "<a href='" + H(url) + "'>" + H(s) + "</a>" : H(s)));
        rows.Add("<b>" + H(Loc.T("hazard.detail.reported")) + ":</b> " + reported);
        if (links.Count > 0) rows.Add("<a href='" + H(links[0].Url) + "'>" + H(Loc.T("hazard.detail.official")) + " ↗</a>");

        var lat = e.Lat.ToString("0.####", CultureInfo.InvariantCulture);
        var lon = e.Lon.ToString("0.####", CultureInfo.InvariantCulture);
        var dark = Theme.IsDark;
        var color = e.Level >= 3 ? "#d62828" : e.Level == 2 ? "#e67e22" : "#e0a020";
        var offline = Loc.T("pk.the.map.needs.an");
        return @"<!DOCTYPE html><html><head><meta charset='utf-8'><meta name='referrer' content='origin-when-cross-origin'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
html,body{margin:0;height:100%;font-family:'Segoe UI',sans-serif;background:#1e1e1e}
#map{position:absolute;inset:0;color:#ddd;display:flex;align-items:center;justify-content:center}
.box{position:absolute;top:12px;left:12px;z-index:1000;padding:10px 14px;border-radius:8px;font-size:13px;line-height:1.55;max-width:70%;
background:" + (dark ? "rgba(30,30,34,.93)" : "rgba(255,255,255,.96)") + ";color:" + (dark ? "#eee" : "#222") + @";box-shadow:0 3px 10px rgba(0,0,0,.35)}
.box h3{margin:0 0 6px 0;font-size:15px}a{color:" + (dark ? "#5ec8e5" : "#0b6e99") + @"}
.lv1{color:#e0a020;font-weight:600}.lv2{color:#e67e22;font-weight:600}.lv3{color:#d62828;font-weight:600}
</style></head><body>
<div id='map'>" + H(offline) + @"</div>
<div class='box'><h3>" + H(heading) + "</h3>" + string.Join("<br>", rows) + @"</div>
<script>
if (typeof L !== 'undefined') {
  document.getElementById('map').textContent = '';
  var map = L.map('map').setView([" + lat + ", " + lon + @"], 7);
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {maxZoom: 18, attribution: '&copy; OpenStreetMap contributors'}).addTo(map);
  L.circleMarker([" + lat + ", " + lon + "], {radius: 11, color: '" + color + @"', fillColor: '" + color + @"', fillOpacity: .55, weight: 3}).addTo(map);
}
</script></body></html>";
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080;
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
            foreach (var b in new[] { maximize, close }) b.Font.Dispose();
            Font.Dispose();
        }
        base.Dispose(disposing);
    }
}
