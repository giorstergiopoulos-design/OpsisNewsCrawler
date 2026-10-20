using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OpsisNewsCrawler.Core;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Opened by a click on a city of the weather line: the current weather of the city on top of a live OpenStreetMap map (Leaflet),
/// in a rectangular window above the bar with maximize / restore and close. The map needs internet; the weather box does not.
/// The tiles come from the public OpenStreetMap servers (light use, attribution shown, the app identifies itself in the Referer).
/// </summary>
internal sealed class WeatherMapForm : Form
{
    private const int ToolbarHeight = 44;
    private const string TileReferer = "https://github.com/giorstergiopoulos-design/OpsisNewsCrawler";

    private readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(30, 30, 30) };
    private readonly Panel toolbar = new() { Dock = DockStyle.Bottom, Height = ToolbarHeight };
    private readonly FlatButton maximize = new();
    private readonly FlatButton close = new();
    private readonly Label title = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label message = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
    private readonly string html;
    private Rectangle normalBounds;
    private bool maximized;

    public WeatherMapForm(GreekCity city, WeatherReading? reading, bool stored, Rectangle anchorBar)
    {
        var name = Loc.IsGreek ? city.NameEl : city.NameEn;
        html = BuildHtml(city, name, reading, stored);

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

        var width = 560;
        var height = 380 + ToolbarHeight;
        var screen = Screen.FromRectangle(anchorBar).WorkingArea;
        var x = Math.Max(screen.Left, Math.Min(anchorBar.Left + (anchorBar.Width - width) / 2, screen.Right - width));
        var y = anchorBar.Top - height - 10;
        if (y < screen.Top) y = Math.Min(screen.Bottom - height, anchorBar.Bottom + 10); // a bar at the top: open below it
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

        title.Text = name + "  ·  " + Loc.Pick("Καιρός και χάρτης", "Weather and map");
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
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.NavigationStarting += (_, e) =>
            {
                // Only the page we generate (a data: / about: document). Clicks on links inside the map go nowhere.
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) || target.Scheme is not ("data" or "about")) e.Cancel = true;
            };
            // The public OpenStreetMap tile servers ask for an identifying Referer; a generated page has none of its own.
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
            message.Text = ex is WebView2RuntimeNotFoundException
                ? Loc.Pick("Λείπει το Microsoft Edge WebView2 Runtime· χρειάζεται για τον χάρτη.", "Microsoft Edge WebView2 Runtime was not found; it is needed for the map.")
                : Loc.Pick("Ο χάρτης δεν άνοιξε: ", "The map could not be opened: ") + ex.GetType().Name + ": " + ex.Message;
            message.Visible = true;
            message.BringToFront();
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
        }
        else
        {
            maximized = false;
            Bounds = normalBounds;
            maximize.Text = "⤢";
        }
        Invalidate();
    }

    /// <summary>The page: a Leaflet map centred on the city with a box of the current weather. Every text is HTML-encoded.</summary>
    internal static string BuildHtml(GreekCity city, string name, WeatherReading? r, bool stored)
    {
        var greek = Loc.IsGreek;
        var culture = CultureInfo.GetCultureInfo("el-GR");
        string H(string s) => WebUtility.HtmlEncode(s);
        var rows = new List<string>();
        if (r is null)
        {
            rows.Add(H(Loc.Pick("Δεν υπάρχουν ακόμα δεδομένα καιρού.", "No weather data yet.")));
        }
        else
        {
            var kind = WeatherInfo.KindOf(r.Code);
            rows.Add("<b>" + H(WeatherInfo.KindName(kind, greek)) + "</b>  " + r.TemperatureC.ToString("0.0", culture) + " °C");
            if (r.FeelsLikeC is { } feels) rows.Add(H(Loc.T("weather.feels")) + ": " + feels.ToString("0.0", culture) + " °C");
            rows.Add(H(Loc.T("weather.wind")) + ": " + r.WindKmh.ToString("0", culture) + " km/h " + H(Loc.T(new[] { "wind.n", "wind.ne", "wind.e", "wind.se", "wind.s", "wind.sw", "wind.w", "wind.nw" }[WeatherInfo.CompassIndex(r.WindFromDegrees)])));
            if (r.GustsKmh is { } gusts) rows.Add(H(Loc.T("weather.gusts")) + ": " + gusts.ToString("0", culture) + " km/h");
            rows.Add(H(Loc.T("weather.humidity")) + ": " + r.HumidityPercent + "%");
            if (r.PrecipitationMm is { } rain) rows.Add(H(Loc.T("weather.rain")) + ": " + rain.ToString("0.0", culture) + " mm");
            if (r.CloudPercent is { } cloud) rows.Add(H(Loc.T("weather.cloud")) + ": " + cloud + "%");
            if (r.PressureHpa is { } pressure) rows.Add(H(Loc.T("weather.pressure")) + ": " + pressure.ToString("0", culture) + " hPa");
            rows.Add("<span class='muted'>" + H(Loc.Pick("Ώρα δεδομένων", "Data time")) + ": " + r.At.ToLocalTime().ToString("dd/MM HH:mm", culture) + (stored ? " · " + H(Loc.Pick("αποθηκευμένα δεδομένα", "stored data")) : "") + "</span>");
        }
        var lat = city.Lat.ToString("0.####", CultureInfo.InvariantCulture);
        var lon = city.Lon.ToString("0.####", CultureInfo.InvariantCulture);
        var dark = Theme.IsDark;
        var offline = Loc.Pick("Ο χάρτης χρειάζεται σύνδεση στο διαδίκτυο.", "The map needs an internet connection.");
        return @"<!DOCTYPE html><html><head><meta charset='utf-8'><meta name='referrer' content='origin-when-cross-origin'>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>
html,body{margin:0;height:100%;font-family:'Segoe UI',sans-serif;background:#1e1e1e}
#map{position:absolute;inset:0;color:#ddd;display:flex;align-items:center;justify-content:center}
.box{position:absolute;top:12px;left:12px;z-index:1000;padding:10px 14px;border-radius:8px;font-size:13px;line-height:1.5;max-width:60%;
background:" + (dark ? "rgba(30,30,34,.92)" : "rgba(255,255,255,.95)") + ";color:" + (dark ? "#eee" : "#222") + @";box-shadow:0 3px 10px rgba(0,0,0,.35)}
.box h3{margin:0 0 4px 0;font-size:15px}.muted{opacity:.65;font-size:11px}
</style></head><body>
<div id='map'>" + H(offline) + @"</div>
<div class='box'><h3>" + H(name) + "</h3>" + string.Join("<br>", rows) + @"</div>
<script>
if (typeof L !== 'undefined') {
  document.getElementById('map').textContent = '';
  var map = L.map('map').setView([" + lat + ", " + lon + @"], 11);
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {maxZoom: 18, attribution: '&copy; OpenStreetMap contributors'}).addTo(map);
  L.marker([" + lat + ", " + lon + @"]).addTo(map);
}
</script></body></html>";
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
            foreach (var b in new[] { maximize, close }) b.Font.Dispose();
            Font.Dispose();
        }
        base.Dispose(disposing);
    }
}
