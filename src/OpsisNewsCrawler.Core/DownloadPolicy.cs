using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// The rules every download and every launched file of the program must pass (security audit 2026-10-29): where a redirect may end, how much
/// of an answer is read, which names a file may have and who must have signed a foreign installer.
/// </summary>
public static class DownloadPolicy
{
    /// <summary>GitHub sends release files through its own CDN; nothing else is accepted as the end of a redirect.</summary>
    public static bool IsTrustedRedirectTarget(Uri? finalUri)
    {
        if (finalUri is null || finalUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(finalUri.UserInfo)) return false;
        var host = finalUri.Host.ToLowerInvariant();
        return host == "github.com" || host == "api.github.com" || host == "raw.githubusercontent.com"
            || host.EndsWith(".githubusercontent.com", StringComparison.Ordinal);
    }

    /// <summary>A plain file name: no folders, no "..", no reserved characters, no trailing dot or space, not too long.</summary>
    public static bool IsSafeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120) return false;
        if (name is "." or "..") return false;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        return !name.EndsWith('.') && !name.EndsWith(' ');
    }

    /// <summary>True when the certificate subject has exactly the organisation "Microsoft Corporation" (not just text that contains it).</summary>
    public static bool IsMicrosoftSigner(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return false;
        return Regex.IsMatch(subject, @"(^|,\s*)O=Microsoft Corporation(\s*,|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>Reads at most <paramref name="maxBytes"/> of an answer as text; longer answers give null (a checksum file is a few dozen bytes).</summary>
    public static async Task<string?> ReadLimitedTextAsync(HttpClient http, string url, int maxBytes, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (!IsTrustedRedirectTarget(response.RequestMessage?.RequestUri)) return null;
        if (response.Content.Headers.ContentLength is > 0 and var length && length > maxBytes) return null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buffer = new byte[maxBytes + 1];
        var total = 0;
        int n;
        while (total < buffer.Length && (n = await stream.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false)) > 0) total += n;
        return total > maxBytes ? null : System.Text.Encoding.UTF8.GetString(buffer, 0, total);
    }

    /// <summary>The user's profile folder in a log line becomes %USERPROFILE%, so a log the user sends for support does not carry the Windows account name.</summary>
    public static string ScrubForLog(string? message)
    {
        if (string.IsNullOrEmpty(message)) return "";
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? message : message.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
    }
}
