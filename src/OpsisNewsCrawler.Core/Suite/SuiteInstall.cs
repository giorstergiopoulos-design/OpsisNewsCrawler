using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>What the detector needs from the machine (a fake in the tests, files and the registry in the program). Everything here only READS.</summary>
public interface IInstallProbe
{
    bool FileExists(string path);
    string? FileVersion(string path);
    string Expand(string text);
    IReadOnlyList<UninstallEntry> UninstallEntries();
}

public sealed record UninstallEntry(string DisplayName, string? Version, string? InstallLocation);

public sealed record InstalledInfo(bool Installed, string? Version, string? ExePath)
{
    public static readonly InstalledInfo None = new(false, null, null);
}

public static class SuiteDetector
{
    // Names under which an installer registers the app when they differ from the catalogue (the Greek name of Kombos).
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kombos"] = new[] { "Κόμβος" },
    };

    /// <summary>First the known file paths, then the uninstall list of Windows (by name; also "Name - something" such as "GearWin - Complete PC Care").</summary>
    public static InstalledInfo Detect(SuiteApp app, IInstallProbe probe)
    {
        foreach (var raw in app.Paths)
        {
            var path = probe.Expand(raw);
            if (path.Contains('%')) continue;           // an unknown variable
            if (probe.FileExists(path)) return new InstalledInfo(true, probe.FileVersion(path), path);
        }

        var names = new List<string> { app.Name };
        if (!string.IsNullOrWhiteSpace(app.UninstallDisplayName)) names.Add(app.UninstallDisplayName!);
        if (Aliases.TryGetValue(app.Id, out var extra)) names.AddRange(extra);

        foreach (var entry in probe.UninstallEntries())
        {
            if (!names.Any(n => entry.DisplayName.Equals(n, StringComparison.OrdinalIgnoreCase)
                                || entry.DisplayName.StartsWith(n + " - ", StringComparison.OrdinalIgnoreCase))) continue;
            string? exe = null;
            if (!string.IsNullOrWhiteSpace(entry.InstallLocation) && app.Paths.Count > 0)
            {
                var candidate = Path.Combine(entry.InstallLocation!, Path.GetFileName(probe.Expand(app.Paths[0])));
                if (probe.FileExists(candidate)) exe = candidate;
            }
            return new InstalledInfo(true, entry.Version ?? (exe is null ? null : probe.FileVersion(exe)), exe);
        }
        return InstalledInfo.None;
    }

    /// <summary>"6.5.4.0" and "v6.5.4" compare as equal; unreadable versions are never "newer".</summary>
    public static bool IsNewer(string? latestTag, string? installedVersion)
    {
        if (latestTag is null || installedVersion is null) return false;
        return UpdateChecker.TryParseVersion(latestTag, out var latest) && UpdateChecker.TryParseVersion(installedVersion, out var current)
               && Normalize(latest) > Normalize(current);
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
}

/// <summary>The setup file of a release of one app of the family.</summary>
public sealed record SuiteDownload(string Tag, string PageUrl, string FileName, string Url, long Size, string? Sha256, string? ChecksumUrl)
{
    public bool CanVerify => Sha256 is not null || ChecksumUrl is not null;
}

public static class SuiteReleases
{
    public static string LatestApiUrl(SuiteCatalog catalog, SuiteApp app) => $"https://api.github.com/repos/{catalog.Owner}/{app.Repo}/releases/latest";

    public static string RepoUrl(SuiteCatalog catalog, SuiteApp app) => $"https://github.com/{catalog.Owner}/{app.Repo}";

    /// <summary>
    /// Finds the setup asset in a "latest release" answer. Only files of this repository's own releases (https, github.com, exact path) are accepted;
    /// drafts and pre-releases are ignored. Returns null when nothing usable is there. <paramref name="tag"/> is given even when no asset matches.
    /// </summary>
    public static SuiteDownload? PickDownload(string releaseJson, SuiteCatalog catalog, SuiteApp app, out string? tag)
    {
        tag = null;
        try
        {
            using var doc = JsonDocument.Parse(releaseJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) return null;
            if (root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True) return null;
            tag = root.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            if (tag is null) return null;
            var page = root.TryGetProperty("html_url", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;
            var prefix = $"https://github.com/{catalog.Owner}/{app.Repo}/releases/download/";
            page = page is not null && page.StartsWith($"https://github.com/{catalog.Owner}/{app.Repo}/", StringComparison.Ordinal) ? page : RepoUrl(catalog, app) + "/releases";
            if (app.InstallerAsset is null || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;

            var pattern = new Regex(app.InstallerAsset, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
            string? checksumUrl = null;
            SuiteDownload? found = null;
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                var url = a.TryGetProperty("browser_download_url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                if (name is null || url is null || !url.StartsWith(prefix, StringComparison.Ordinal) || !SuiteCatalog.IsAllowedSource(url)) continue;
                if (!DownloadPolicy.IsSafeFileName(name)) continue;
                if (app.ChecksumAsset is not null && string.Equals(name, app.ChecksumAsset, StringComparison.OrdinalIgnoreCase)) checksumUrl = url;
                if (found is null && pattern.IsMatch(name))
                {
                    var size = a.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : 0;
                    var digest = UpdateChecker.ParseDigest(a.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String ? dg.GetString() : null);
                    found = new SuiteDownload(tag, page, name, url, size, digest, null);
                }
            }
            return found is null ? null : found with { ChecksumUrl = checksumUrl };
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or RegexMatchTimeoutException or InvalidOperationException)
        {
            return null;
        }
    }
}

public static class Sha256Check
{
    /// <summary>The 64 hex digits of the first token of a ".sha256" file ("abc…  file.exe", "abc…" or "SHA256 (file) = abc…"), lower case, or null.</summary>
    public static string? ParseChecksumFile(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Regex.Match(text, @"(?<![0-9a-fA-F])[0-9a-fA-F]{64}(?![0-9a-fA-F])");
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    public static string OfFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>True only when the file's hash equals <paramref name="expectedHex"/> (case-insensitive). A missing or malformed expectation is a mismatch.</summary>
    public static bool Matches(string path, string? expectedHex)
    {
        if (expectedHex is null || !Regex.IsMatch(expectedHex, "^[0-9a-fA-F]{64}$") || !File.Exists(path)) return false;
        return string.Equals(OfFile(path), expectedHex, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>When the welcome guide shows: the first start of a new major or minor version, never inside Opsis (Opsis has its own), never when switched off.</summary>
public static class WelcomePolicy
{
    public static bool ShouldShow(Version current, string? seenVersion, bool disabled, bool hostedByOpsis)
    {
        if (disabled || hostedByOpsis) return false;
        if (string.IsNullOrWhiteSpace(seenVersion) || !Version.TryParse(seenVersion, out var seen)) return true;
        return current.Major != seen.Major || current.Minor != seen.Minor;
    }
}
