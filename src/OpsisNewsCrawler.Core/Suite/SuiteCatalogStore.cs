using System.Reflection;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Gives the program the catalogue: the copy fetched from GitHub at most once a day (kept in the program's data folder), else the last good copy,
/// else the one embedded in the program. A failure of any kind is silent; the only request is one GET of the catalogue file.
/// </summary>
public sealed class SuiteCatalogStore
{
    private const string FileName = "suite-apps.json";
    private const string StampName = "suite-apps.stamp";
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private readonly string dataDir;
    private readonly HttpMessageHandler? handler;
    private readonly Func<DateTimeOffset> now;

    public SuiteCatalogStore(string dataDir, HttpMessageHandler? handler = null, Func<DateTimeOffset>? now = null)
    {
        this.dataDir = dataDir;
        this.handler = handler;
        this.now = now ?? (() => DateTimeOffset.Now);
    }

    public static SuiteCatalog Embedded()
    {
        var asm = typeof(SuiteCatalogStore).Assembly;
        using var stream = asm.GetManifestResourceStream("OpsisNewsCrawler.Suite.suite-apps.json")
                           ?? throw new InvalidOperationException("embedded catalogue missing");
        using var reader = new StreamReader(stream);
        if (!SuiteCatalog.TryParse(reader.ReadToEnd(), out var catalog, out var error) || catalog is null)
            throw new InvalidOperationException("embedded catalogue is invalid: " + error);
        return catalog;
    }

    /// <summary>The cached catalogue if it is valid (stale or not), else null.</summary>
    public SuiteCatalog? LoadCached()
    {
        try
        {
            var path = Path.Combine(dataDir, FileName);
            if (!File.Exists(path) || new FileInfo(path).Length > SuiteCatalog.MaxBytes) return null;
            return SuiteCatalog.TryParse(File.ReadAllText(path), out var c, out _) ? c : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private bool CacheIsFresh()
    {
        try
        {
            var stamp = Path.Combine(dataDir, StampName);
            return File.Exists(stamp) && DateTimeOffset.TryParse(File.ReadAllText(stamp), out var at) && now() - at < MaxAge;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Never throws (except for cancellation): the worst answer is the embedded copy.</summary>
    public async Task<SuiteCatalog> GetAsync(CancellationToken ct = default)
    {
        var cached = LoadCached();
        if (cached is not null && CacheIsFresh()) return cached;

        try
        {
            var fetched = await FetchAsync(ct).ConfigureAwait(false);
            if (fetched is not null) return fetched;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
        {
            if (ct.IsCancellationRequested) throw;
        }
        return cached ?? Embedded();
    }

    private async Task<SuiteCatalog?> FetchAsync(CancellationToken ct)
    {
        if (!SuiteCatalog.IsAllowedSource(SuiteCatalog.LiveUrl)) return null;
        using var http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(5);
        http.MaxResponseContentBufferSize = SuiteCatalog.MaxBytes + 1;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler-Suite");
        using var response = await http.GetAsync(SuiteCatalog.LiveUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        if (response.Content.Headers.ContentLength is > SuiteCatalog.MaxBytes) return null;
        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (bytes.Length > SuiteCatalog.MaxBytes) return null;
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        if (!SuiteCatalog.TryParse(text, out var catalog, out _) || catalog is null) return null;
        try
        {
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, FileName), text);
            File.WriteAllText(Path.Combine(dataDir, StampName), now().ToString("o"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // not cached: it is fetched again next time
        }
        return catalog;
    }
}
