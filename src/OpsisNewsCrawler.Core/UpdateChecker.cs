using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// A newer release found on GitHub. <paramref name="Sha256"/> is the digest GitHub itself publishes for the setup file (preferred);
/// <paramref name="ChecksumUrl"/> is the .sha256 file next to it (fallback). Without one of them the setup is never run automatically.
/// </summary>
public sealed record UpdateInfo(Version Version, string PageUrl, string? SetupUrl, string? ChecksumUrl, string? Sha256 = null, string Tag = "")
{
    public bool CanInstallAutomatically => SetupUrl is not null && (Sha256 is not null || ChecksumUrl is not null);
}

/// <summary>
/// Asks GitHub whether a newer release exists (same logic as GearWin's self-update): only the public releases API is used, only a setup of
/// this repository's own releases (https, github.com, exact path, exact file name) is accepted, and it is verified with SHA-256 before it runs.
/// </summary>
public static class UpdateChecker
{
    public const string Repository = "giorstergiopoulos-design/OpsisNewsCrawler";
    private const string SetupName = "OpsisNewsCrawler-Setup.exe";
    private static readonly string DownloadPrefix = $"https://github.com/{Repository}/releases/download/";

    private static readonly object CacheLock = new();
    private static string? etag;
    private static string? lastJson;

    /// <summary>Forgets the remembered ETag (tests).</summary>
    internal static void ResetCache() { lock (CacheLock) { etag = null; lastJson = null; } }

    /// <summary>The newer release, or null when this version is the latest. Throws only <see cref="HttpRequestException"/>/<see cref="TaskCanceledException"/>.</summary>
    public static async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken ct = default, HttpMessageHandler? handler = null)
    {
        using var http = handler is null
            ? new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 }
            : new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler-UpdateCheck");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        // ETag / If-None-Match: an unchanged answer (HTTP 304) is free and does not count against GitHub's rate limit.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
        string? cachedEtag, cachedJson;
        lock (CacheLock) { cachedEtag = etag; cachedJson = lastJson; }
        if (cachedEtag is not null && cachedJson is not null) request.Headers.TryAddWithoutValidation("If-None-Match", cachedEtag);
        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        string json;
        if (response.StatusCode == System.Net.HttpStatusCode.NotModified && cachedJson is not null) json = cachedJson;
        else
        {
            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            lock (CacheLock) { etag = response.Headers.ETag?.ToString(); lastJson = json; }
        }
        try
        {
            return Parse(json, current);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new HttpRequestException("unexpected answer from GitHub", ex);
        }
    }

    public static UpdateInfo? Parse(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!TryParseVersion(tag, out var latest) || latest <= current) return null;

        var page = root.GetProperty("html_url").GetString() ?? $"https://github.com/{Repository}/releases";
        // Only addresses of this repository's releases are ever followed.
        if (!page.StartsWith($"https://github.com/{Repository}/", StringComparison.Ordinal)) return null;
        string? setup = null, sum = null, digest = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString();
                var url = a.GetProperty("browser_download_url").GetString();
                if (!IsTrustedDownloadUrl(url)) continue;
                if (name == SetupName)
                {
                    setup = url;
                    digest = ParseDigest(a.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String ? dg.GetString() : null);
                }
                else if (name == SetupName + ".sha256") sum = url;
            }
        }
        return new UpdateInfo(latest, page, setup, sum, digest, tag);
    }

    /// <summary>True for https://github.com/&lt;this repository&gt;/releases/download/... and nothing else.</summary>
    public static bool IsTrustedDownloadUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == "github.com"
        && string.IsNullOrEmpty(u.UserInfo) && url!.StartsWith(DownloadPrefix, StringComparison.Ordinal);

    /// <summary>"sha256:ABC..." gives "abc..." (64 hex digits), anything else null.</summary>
    public static string? ParseDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return null;
        var hex = digest[7..].Trim().ToLowerInvariant();
        return Regex.IsMatch(hex, "^[0-9a-f]{64}$") ? hex : null;
    }

    public static bool TryParseVersion(string text, out Version version)
    {
        text = text.Trim().TrimStart('v', 'V');
        var dash = text.IndexOfAny(new[] { '-', '+' });
        if (dash >= 0) text = text[..dash];
        return Version.TryParse(text, out version!);
    }
}

/// <summary>"Once per version": a release is announced when it is not the one the user was already told about.</summary>
public static class UpdateAnnouncePolicy
{
    public static bool ShouldAnnounce(string? lastNotifiedTag, string? foundTag) =>
        !string.IsNullOrWhiteSpace(foundTag) && !string.Equals(lastNotifiedTag, foundTag, StringComparison.Ordinal);
}
