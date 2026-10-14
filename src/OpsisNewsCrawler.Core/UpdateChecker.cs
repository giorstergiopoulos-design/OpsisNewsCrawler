using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>A newer release found on GitHub.</summary>
public sealed record UpdateInfo(Version Version, string PageUrl, string? SetupUrl, string? ChecksumUrl);

/// <summary>
/// Asks GitHub whether a newer release exists. Only the public releases API is used (no account, no data sent except the usual
/// request headers); nothing is downloaded or installed here.
/// </summary>
public static class UpdateChecker
{
    public const string Repository = "giorstergiopoulos-design/OpsisNewsCrawler";
    private const string SetupName = "OpsisNewsCrawler-Setup.exe";

    /// <summary>The newer release, or null when this version is the latest. Throws only <see cref="HttpRequestException"/>/<see cref="TaskCanceledException"/>.</summary>
    public static async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler-UpdateCheck");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        var json = await http.GetStringAsync($"https://api.github.com/repos/{Repository}/releases/latest", ct).ConfigureAwait(false);
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
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!TryParseVersion(tag, out var latest) || latest <= current) return null;

        var page = root.GetProperty("html_url").GetString() ?? $"https://github.com/{Repository}/releases";
        // Only addresses of this repository's releases are ever followed.
        if (!page.StartsWith($"https://github.com/{Repository}/", StringComparison.Ordinal)) return null;
        string? setup = null, sum = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString();
                var url = a.GetProperty("browser_download_url").GetString();
                if (url is null || !url.StartsWith($"https://github.com/{Repository}/releases/download/", StringComparison.Ordinal)) continue;
                if (name == SetupName) setup = url;
                else if (name == SetupName + ".sha256") sum = url;
            }
        }
        return new UpdateInfo(latest, page, setup, sum);
    }

    public static bool TryParseVersion(string text, out Version version)
    {
        text = text.Trim().TrimStart('v', 'V');
        var dash = text.IndexOfAny(new[] { '-', '+' });
        if (dash >= 0) text = text[..dash];
        return Version.TryParse(text, out version!);
    }
}
