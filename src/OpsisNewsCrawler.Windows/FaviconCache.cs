using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Downloads the small site icon (favicon.ico) of each news source once and keeps it in memory.
/// While it is not available (or the site has none) the ticker draws a coloured letter badge.
/// </summary>
internal static class FaviconCache
{
    private const int MaxIconBytes = 256 * 1024;
    private const int IconSize = 16;

    private static readonly HttpClient Http = CreateClient();
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> Pending = new(StringComparer.OrdinalIgnoreCase);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; OpsisNewsCrawler/0.6)");
        return client;
    }

    private static readonly ConcurrentDictionary<(string Host, bool DarkSurface), Bitmap?> Styled = new();

    public static Bitmap? TryGet(string host) => Cache.TryGetValue(host, out var bmp) ? bmp : null;

    /// <summary>
    /// The icon prepared for the surface it is drawn on. Dark or single-colour icons that would disappear on the strip are
    /// inverted (monochrome) or placed on a contrasting round plate (coloured).
    /// </summary>
    public static Bitmap? TryGetFor(string host, bool darkSurface)
    {
        var raw = TryGet(host);
        if (raw is null) return null;
        return Styled.GetOrAdd((host, darkSurface), _ => MakeVisible(raw, darkSurface));
    }

    private static Bitmap MakeVisible(Bitmap src, bool darkSurface)
    {
        // Mean luminance (alpha weighted) and mean saturation of the visible pixels.
        double lum = 0, sat = 0, weight = 0;
        for (var y = 0; y < src.Height; y++)
        {
            for (var x = 0; x < src.Width; x++)
            {
                var c = src.GetPixel(x, y);
                if (c.A < 20) continue;
                var w = c.A / 255.0;
                lum += (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0 * w;
                sat += c.GetSaturation() * w;
                weight += w;
            }
        }
        if (weight < 1) return src;
        lum /= weight;
        sat /= weight;

        var tooDark = darkSurface && lum < 0.38;
        var tooLight = !darkSurface && lum > 0.72;
        if (!tooDark && !tooLight) return src;

        var result = new Bitmap(src.Width, src.Height);
        using var g = Graphics.FromImage(result);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (sat < 0.25)
        {
            // Monochrome logo: invert the colours, keep the shape (alpha).
            var inverted = new System.Drawing.Imaging.ColorMatrix(new[]
            {
                new float[] { -1, 0, 0, 0, 0 },
                new float[] { 0, -1, 0, 0, 0 },
                new float[] { 0, 0, -1, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 1, 1, 1, 0, 1 },
            });
            using var attrs = new System.Drawing.Imaging.ImageAttributes();
            attrs.SetColorMatrix(inverted);
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        }
        else
        {
            // Coloured but low contrast: draw it on a plate that stands out from the strip.
            using var plate = new SolidBrush(darkSurface ? Color.FromArgb(235, 255, 255, 255) : Color.FromArgb(235, 40, 40, 46));
            g.FillEllipse(plate, 0, 0, src.Width - 1, src.Height - 1);
            var inset = Math.Max(2, src.Width / 6);
            g.DrawImage(src, new Rectangle(inset, inset, src.Width - 2 * inset, src.Height - 2 * inset));
        }
        return result;
    }

    /// <summary>Starts a background download if the host is unknown. <paramref name="onLoaded"/> runs on a thread-pool thread.</summary>
    public static void Request(string host, Action onLoaded)
    {
        if (string.IsNullOrWhiteSpace(host) || Cache.ContainsKey(host) || !Pending.TryAdd(host, 0)) return;

        _ = Task.Run(async () =>
        {
            Bitmap? bmp = null;
            try
            {
                bmp = await DownloadAsync(host).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                // No icon: the badge is used instead.
            }

            Cache[host] = bmp;
            Pending.TryRemove(host, out _);
            if (bmp is not null) onLoaded();
        });
    }

    private static async Task<Bitmap?> DownloadAsync(string host)
    {
        var direct = await FetchIconAsync($"https://{host}/favicon.ico").ConfigureAwait(false);
        if (direct is not null) return direct;

        // No /favicon.ico: use the icon the home page declares (<link rel="icon"> / apple-touch-icon).
        foreach (var url in await FindDeclaredIconsAsync(host).ConfigureAwait(false))
        {
            var bmp = await FetchIconAsync(url).ConfigureAwait(false);
            if (bmp is not null) return bmp;
        }
        return null;
    }

    private static async Task<Bitmap?> FetchIconAsync(string url)
    {
        try
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength is > MaxIconBytes) return null;
            var data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return data.Length is > 0 and <= MaxIconBytes ? Decode(data) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UriFormatException)
        {
            return null;
        }
    }

    private static readonly System.Text.RegularExpressions.Regex LinkTag =
        new(@"<link[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex RelAttr =
        new(@"rels*=s*[""']([^""']*)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex HrefAttr =
        new(@"hrefs*=s*[""']([^""']*)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static async Task<List<string>> FindDeclaredIconsAsync(string host)
    {
        var found = new List<string>();
        try
        {
            var baseUri = new Uri($"https://{host}/");
            var html = await Http.GetStringAsync(baseUri).ConfigureAwait(false);
            if (html.Length > 200_000) html = html[..200_000];
            foreach (System.Text.RegularExpressions.Match tag in LinkTag.Matches(html))
            {
                var rel = RelAttr.Match(tag.Value);
                var href = HrefAttr.Match(tag.Value);
                if (!rel.Success || !href.Success) continue;
                if (!rel.Groups[1].Value.Contains("icon", StringComparison.OrdinalIgnoreCase)) continue;
                if (Uri.TryCreate(baseUri, System.Net.WebUtility.HtmlDecode(href.Groups[1].Value), out var abs) &&
                    abs.Scheme is "http" or "https")
                    found.Add(abs.AbsoluteUri);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UriFormatException)
        {
        }
        return found.Distinct().Take(4).ToList();
    }

    private static Bitmap? Decode(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data);
            using var icon = new Icon(ms, new Size(IconSize, IconSize));
            return icon.ToBitmap();
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or InvalidOperationException)
        {
            // Not a classic .ico: try a regular image format (PNG, GIF, JPEG).
        }

        try
        {
            using var ms = new MemoryStream(data);
            using var image = Image.FromStream(ms);
            return new Bitmap(image, IconSize, IconSize);
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
        {
            return null;
        }
    }
}
