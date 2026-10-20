using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>A stored copy of some data with the time it was fetched.</summary>
public sealed record Cached<T>(DateTimeOffset At, T Data);

/// <summary>
/// The last good news, prices and weather, kept in %AppData%\OpsisNewsCrawler\cache so that they can still be shown when
/// the network or a provider is down (and right after a start, before the first download has finished).
/// </summary>
public static class DataCache
{
    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpsisNewsCrawler", "cache");

    /// <summary>Only letters, digits, '-' and '_' are kept: the name ends up in a file name.</summary>
    public static string SafeName(string name)
    {
        var chars = (name ?? "").Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        return chars.Length == 0 ? "data" : new string(chars);
    }

    private static string PathOf(string name) => Path.Combine(Folder, SafeName(name) + ".json");

    public static void Save<T>(string name, T data)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            SafeFile.WriteAllText(PathOf(name), JsonSerializer.Serialize(new Cached<T>(DateTimeOffset.Now, data)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A cache that cannot be written is not a problem for the bar.
        }
    }

    /// <summary>The stored copy, or null when there is none or it cannot be read.</summary>
    public static Cached<T>? Load<T>(string name)
    {
        try
        {
            var path = PathOf(name);
            return File.Exists(path) ? JsonSerializer.Deserialize<Cached<T>>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
