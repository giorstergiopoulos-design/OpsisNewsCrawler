using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>An article the user kept with Ctrl+click on the bar.</summary>
public sealed record SavedArticle(string Title, string Url, string Source, DateTimeOffset SavedAt);

/// <summary>The "read later" list: kept in %AppData%\OpsisNewsCrawler\read-later.json, newest first, at most 300 articles.</summary>
public sealed class ReadLaterStore
{
    private const int MaxItems = 300;
    private readonly string path;
    private List<SavedArticle> items = new();

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpsisNewsCrawler", "read-later.json");

    public ReadLaterStore(string? path = null)
    {
        this.path = path ?? DefaultPath;
        Load();
    }

    /// <summary>Raised after the list changed (also when another part of the program changed it).</summary>
    public event Action? Changed;

    public IReadOnlyList<SavedArticle> Items => items;

    public bool Contains(string? url) => !string.IsNullOrEmpty(url) && items.Any(i => string.Equals(i.Url, url, StringComparison.OrdinalIgnoreCase));

    /// <summary>False when the article (same address) is already in the list.</summary>
    public bool Add(NewsItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Url) || Contains(item.Url)) return false;
        items.Insert(0, new SavedArticle(item.Title, item.Url, item.Source, DateTimeOffset.Now));
        if (items.Count > MaxItems) items.RemoveRange(MaxItems, items.Count - MaxItems);
        Save();
        Changed?.Invoke();
        return true;
    }

    public void Remove(IEnumerable<string> urls)
    {
        var set = new HashSet<string>(urls, StringComparer.OrdinalIgnoreCase);
        if (items.RemoveAll(i => set.Contains(i.Url)) == 0) return;
        Save();
        Changed?.Invoke();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(path)) return;
            items = JsonSerializer.Deserialize<List<SavedArticle>>(File.ReadAllText(path)) ?? new();
            items = items.Where(i => !string.IsNullOrEmpty(i.Url)).Take(MaxItems).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            items = new();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            SafeFile.WriteAllText(path, JsonSerializer.Serialize(items));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
