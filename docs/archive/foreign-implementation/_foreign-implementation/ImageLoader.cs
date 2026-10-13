using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DeskTicker.Windows;

/// <summary>
/// Downloads and caches site icons and preview pictures. Call it from the UI thread.
/// Icons are also cached on disk in %AppData%\DeskTicker\icons.
/// </summary>
internal sealed class ImageLoader : IDisposable
{
    private const int MaxBytes = 3 * 1024 * 1024;
    private const int MaxPictureWidth = 800;
    private const int MaxPictureCacheEntries = 40;

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly Dictionary<string, Image?> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<Image?>> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly string iconDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskTicker", "icons");

    public ImageLoader()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; MotionDeskTicker/0.6)");
    }

    public Task<Image?> GetPictureAsync(string url)
    {
        TrimPictureCache();
        return GetCachedAsync("img:" + url, () => DownloadAsync(url));
    }

    public Task<Image?> GetFaviconAsync(string articleUrl)
    {
        if (!Uri.TryCreate(articleUrl, UriKind.Absolute, out var uri) || !IsSafe(uri))
            return Task.FromResult<Image?>(null);
        var host = uri.Host.ToLowerInvariant();
        return GetCachedAsync("ico:" + host, () => LoadFaviconAsync(host));
    }

    private async Task<Image?> GetCachedAsync(string key, Func<Task<Image?>> factory)
    {
        if (cache.TryGetValue(key, out var hit)) return hit;
        if (!pending.TryGetValue(key, out var task))
        {
            task = factory();
            pending[key] = task;
        }

        var image = await task; // resumes on the UI thread
        cache[key] = image;
        pending.Remove(key);
        return image;
    }

    private void TrimPictureCache()
    {
        var keys = cache.Keys.Where(k => k.StartsWith("img:", StringComparison.Ordinal)).ToList();
        if (keys.Count <= MaxPictureCacheEntries) return;
        // Images may still be on screen, so they are only forgotten, never disposed.
        foreach (var k in keys) cache.Remove(k);
    }

    private async Task<Image?> LoadFaviconAsync(string host)
    {
        var file = Path.Combine(iconDir, SafeFileName(host) + ".png");
        var fromDisk = LoadFile(file);
        if (fromDisk is not null) return fromDisk;

        var image = await DownloadAsync($"https://{host}/favicon.ico").ConfigureAwait(false);
        if (image is not null)
        {
            try
            {
                Directory.CreateDirectory(iconDir);
                image.Save(file, ImageFormat.Png);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException)
            {
                // The icon is simply not cached on disk.
            }
        }
        return image;
    }

    private static Image? LoadFile(string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            using var stream = File.OpenRead(file);
            using var source = Image.FromStream(stream);
            return new Bitmap(source);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or ExternalException or OutOfMemoryException)
        {
            return null;
        }
    }

    private async Task<Image?> DownloadAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsSafe(uri)) return null;
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength is > MaxBytes) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return bytes.Length is 0 or > MaxBytes ? null : Decode(bytes);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    private static Image? Decode(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            // ICO files start with 00 00 01 00.
            if (bytes.Length > 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 1 && bytes[3] == 0)
            {
                using var icon = new Icon(stream, 32, 32);
                return icon.ToBitmap();
            }

            using var source = Image.FromStream(stream);
            var scale = Math.Min(1.0, (double)MaxPictureWidth / source.Width);
            return new Bitmap(source, new Size(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale))));
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
        {
            return null;
        }
    }

    // Only public web hosts: no localhost and no IP addresses.
    private static bool IsSafe(Uri uri) =>
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.HostNameType == UriHostNameType.Dns
        && uri.Host.Contains('.')
        && !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private static string SafeFileName(string host)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(host.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }

    public void Dispose() => http.Dispose();
}
