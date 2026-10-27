using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

/// <summary>One app of the Opsis family as the shared catalogue (Opsis repo, suite/apps.json) describes it.</summary>
public sealed record SuiteApp(
    string Id, string Name, string Repo, string? InstallerAsset, string? ChecksumAsset, string? UninstallDisplayName,
    IReadOnlyList<string> Paths,
    IReadOnlyDictionary<string, string> Tagline,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Capabilities)
{
    public string TaglineFor(string lang) => Pick(Tagline, lang) ?? "";

    public IReadOnlyList<string> CapabilitiesFor(string lang) =>
        Pick(Capabilities, lang) ?? Array.Empty<string>();

    private static T? Pick<T>(IReadOnlyDictionary<string, T> map, string lang) where T : class =>
        map.TryGetValue(lang, out var exact) ? exact : map.TryGetValue("en", out var en) ? en : map.Values.FirstOrDefault();
}

/// <summary>
/// The catalogue file: one list of apps for every program of the family. Parsing is strict about what matters for safety (size, schema, ids,
/// repository names, regular expressions) and ignores fields it does not know, so a newer catalogue never breaks an older program.
/// </summary>
public sealed class SuiteCatalog
{
    public const int MaxBytes = 256 * 1024;
    public const string DefaultOwner = "giorstergiopoulos-design";
    public const string LiveUrl = "https://raw.githubusercontent.com/giorstergiopoulos-design/Opsis/master/suite/apps.json";

    private static readonly string[] AllowedHosts = { "github.com", "raw.githubusercontent.com", "api.github.com" };

    public SuiteCatalog(string owner, string updated, IReadOnlyList<SuiteApp> apps)
    {
        Owner = owner;
        Updated = updated;
        Apps = apps;
    }

    public string Owner { get; }
    public string Updated { get; }
    public IReadOnlyList<SuiteApp> Apps { get; }

    public SuiteApp? Find(string id) => Apps.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>True for an https address on github.com, raw.githubusercontent.com or api.github.com and nothing else (no credentials in the address).</summary>
    public static bool IsAllowedSource(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(u.UserInfo)
        && AllowedHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(string? json, out SuiteCatalog? catalog, out string error)
    {
        catalog = null;
        error = "";
        if (string.IsNullOrWhiteSpace(json)) { error = "empty"; return false; }
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) { error = "too large"; return false; }
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16, CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { error = "not an object"; return false; }
            if (!root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number || schema.GetInt32() != 1) { error = "unknown schema"; return false; }
            var owner = Str(root, "owner") ?? DefaultOwner;
            if (!Regex.IsMatch(owner, "^[A-Za-z0-9-]{1,39}$")) { error = "bad owner"; return false; }
            if (!root.TryGetProperty("apps", out var apps) || apps.ValueKind != JsonValueKind.Array) { error = "no apps"; return false; }

            var list = new List<SuiteApp>();
            foreach (var a in apps.EnumerateArray())
            {
                if (list.Count >= 50) break;
                if (a.ValueKind != JsonValueKind.Object) continue;
                var app = ParseApp(a);
                if (app is not null && list.All(x => !string.Equals(x.Id, app.Id, StringComparison.OrdinalIgnoreCase))) list.Add(app);
            }
            if (list.Count == 0) { error = "no valid apps"; return false; }
            catalog = new SuiteCatalog(owner, Str(root, "updated") ?? "", list);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            error = "malformed";
            return false;
        }
    }

    private static SuiteApp? ParseApp(JsonElement a)
    {
        var id = Str(a, "id");
        var name = Str(a, "name");
        var repo = Str(a, "repo");
        if (id is null || name is null || repo is null) return null;
        if (!Regex.IsMatch(id, "^[a-z0-9_-]{1,32}$") || name.Length is 0 or > 80) return null;
        if (!Regex.IsMatch(repo, @"^[A-Za-z0-9._-]{1,100}$")) return null;

        var installer = Str(a, "installerAsset");
        if (installer is not null && !IsValidPattern(installer)) installer = null;
        var checksum = Str(a, "checksumAsset");
        if (checksum is not null && (checksum.Length > 200 || checksum.Contains('/') || checksum.Contains('\\'))) checksum = null;

        var paths = new List<string>();
        if (a.TryGetProperty("paths", out var p) && p.ValueKind == JsonValueKind.Array)
            foreach (var item in p.EnumerateArray().Take(8))
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 and <= 260 } path) paths.Add(path);

        var tagline = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (a.TryGetProperty("tagline", out var t) && t.ValueKind == JsonValueKind.Object)
            foreach (var kv in t.EnumerateObject())
                if (kv.Value.ValueKind == JsonValueKind.String && kv.Value.GetString() is { Length: > 0 and <= 300 } text) tagline[kv.Name] = text;

        var caps = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (a.TryGetProperty("capabilities", out var c) && c.ValueKind == JsonValueKind.Object)
            foreach (var kv in c.EnumerateObject().Take(30))
            {
                if (kv.Value.ValueKind != JsonValueKind.Array) continue;
                var items = kv.Value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)
                    .Where(s => s.Length is > 0 and <= 300).Take(10).ToList();
                if (items.Count > 0) caps[kv.Name] = items;
            }

        return new SuiteApp(id, name, repo, installer, checksum, Str(a, "uninstallDisplayName"), paths, tagline, caps);
    }

    private static bool IsValidPattern(string pattern)
    {
        if (pattern.Length > 200) return false;
        try { _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)); return true; }
        catch (ArgumentException) { return false; }
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
