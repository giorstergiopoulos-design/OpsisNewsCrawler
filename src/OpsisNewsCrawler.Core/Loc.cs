using System.Reflection;
using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// Single-language UI texts. The language is chosen once (settings, or the host: Opsis passes its own language) and every
/// text is shown in that language only. Greek and English are built in (embedded JSON); any other language is a file
/// <c>locales\&lt;code&gt;.json</c> next to the program (flat "key": "text" pairs) and falls back to English per missing key.
/// Same layout as the Opsis locale files, so translations can be shared.
/// </summary>
public static class Loc
{
    private static Dictionary<string, string> current = new();
    private static readonly Dictionary<string, string> English = LoadEmbedded("en");

    /// <summary>Language code of the texts in use ("el", "en", "de", ...).</summary>
    public static string Language { get; private set; } = "el";

    public static bool IsGreek => Language == "el";

    public static event Action? Changed;

    static Loc()
    {
        SetLanguage("el", raiseChanged: false);
    }

    /// <summary>Codes of the languages that can be offered: the built-in ones plus every locales\*.json found.</summary>
    public static IReadOnlyList<(string Code, string Native)> Available()
    {
        var list = new List<(string, string)> { ("el", "Ελληνικά"), ("en", "English") };
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "locales");
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                {
                    var code = Path.GetFileNameWithoutExtension(file);
                    if (list.Any(l => l.Item1 == code)) continue;
                    var dict = ReadFile(file);
                    list.Add((code, dict is not null && dict.TryGetValue("language.native", out var n) ? n : code));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return list;
    }

    public static void SetLanguage(string? code) => SetLanguage(code, raiseChanged: true);

    private static void SetLanguage(string? code, bool raiseChanged)
    {
        code = string.IsNullOrWhiteSpace(code) ? "el" : code.Trim().ToLowerInvariant();
        // Opsis uses full culture codes in some places ("el-GR"): the part before the dash is enough.
        if (code.Contains('-')) code = code[..code.IndexOf('-')];

        Dictionary<string, string>? dict = code switch
        {
            "en" => English,
            _ => LoadEmbedded(code) is { Count: > 0 } builtIn ? builtIn : LoadExternal(code)
        };
        var changed = code != Language || dict is null;
        Language = dict is null ? "en" : code;
        current = dict ?? English;
        if (raiseChanged && changed) Changed?.Invoke();
    }

    public static string T(string key)
    {
        if (current.TryGetValue(key, out var text)) return text;
        return English.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public static string T(string key, params object[] args)
    {
        try { return string.Format(T(key), args); }
        catch (FormatException) { return T(key); }
    }

    /// <summary>Picks the Greek or English variant of a two-language value (e.g. a category name stored in the config).</summary>
    public static string Pick(string? el, string? en) => IsGreek || string.IsNullOrEmpty(en) ? (el ?? en ?? "") : en;

    private static Dictionary<string, string> LoadEmbedded(string code)
    {
        var asm = typeof(Loc).Assembly;
        var name = $"OpsisNewsCrawler.Locales.{code}.json";
        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private static Dictionary<string, string>? LoadExternal(string code)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "locales", code + ".json");
        return File.Exists(path) ? ReadFile(path) : null;
    }

    private static Dictionary<string, string>? ReadFile(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
