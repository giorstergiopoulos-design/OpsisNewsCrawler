using System.Text.Json;

namespace OpsisNewsCrawler.Core;

/// <summary>
/// What the user taught the program about urgent news, kept locally (no cloud): "this is not breaking news" (the story is not flagged again and its source weighs less),
/// a muted source, a muted topic. Saved in <c>breaking-feedback.json</c> next to the settings.
/// </summary>
public sealed class BreakingFeedback
{
    public const int MaxDelta = 20;
    private const int PerRejection = 4;

    public List<string> MutedSources { get; set; } = new();
    public List<string> MutedTopics { get; set; } = new();          // each topic: significant words joined by a space; all of them must be in the title
    public List<string> RejectedStories { get; set; } = new();      // a key of the significant words of a headline the user said was not breaking
    public Dictionary<string, int> SourceRejections { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static string KeyOf(IEnumerable<string> words) => string.Join(' ', words.Where(w => w.Length >= 4).OrderBy(w => w, StringComparer.Ordinal).Take(8));

    public bool IsSourceMuted(string publisher) => MutedSources.Contains(publisher, StringComparer.OrdinalIgnoreCase);

    public bool IsTopicMuted(IReadOnlyCollection<string> titleWords) =>
        MutedTopics.Any(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } words && words.All(titleWords.Contains));

    /// <summary>True when the story (almost) the same headline was rejected before.</summary>
    public bool IsRejected(IReadOnlyCollection<string> titleWords)
    {
        var mine = new HashSet<string>(titleWords.Where(w => w.Length >= 4));
        if (mine.Count < 3) return false;
        foreach (var key in RejectedStories)
        {
            var other = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (other.Length >= 3 && other.Count(mine.Contains) >= Math.Min(other.Length, mine.Count) * 0.7) return true;
        }
        return false;
    }

    /// <summary>The weight taken off a source that was rejected before (0 to -20).</summary>
    public int SourceDelta(string publisher) => SourceRejections.TryGetValue(publisher, out var n) ? -Math.Min(MaxDelta, n * PerRejection) : 0;

    public void Reject(NewsItem item)
    {
        var words = HeadlineDedup.Words(item.Title);
        var key = KeyOf(words);
        if (key.Length > 0 && !RejectedStories.Contains(key)) RejectedStories.Add(key);
        if (RejectedStories.Count > 300) RejectedStories.RemoveAt(0);
        var publisher = BreakingEngine.Publisher(item);
        if (publisher.Length > 0) SourceRejections[publisher] = SourceRejections.GetValueOrDefault(publisher) + 1;
    }

    public void MuteSource(NewsItem item)
    {
        var publisher = BreakingEngine.Publisher(item);
        if (publisher.Length > 0 && !IsSourceMuted(publisher)) MutedSources.Add(publisher);
    }

    /// <summary>The topic = the (up to three) longest significant words of the headline; every headline that has all of them is muted.</summary>
    public string MuteTopic(NewsItem item)
    {
        var topic = string.Join(' ', HeadlineDedup.Words(item.Title).Where(w => w.Length >= 5).OrderByDescending(w => w.Length).ThenBy(w => w, StringComparer.Ordinal).Take(3).OrderBy(w => w, StringComparer.Ordinal));
        if (topic.Length > 0 && !MutedTopics.Contains(topic)) MutedTopics.Add(topic);
        return topic;
    }

    public void Clear()
    {
        MutedSources.Clear(); MutedTopics.Clear(); RejectedStories.Clear(); SourceRejections.Clear();
    }

    public bool IsEmpty => MutedSources.Count == 0 && MutedTopics.Count == 0 && RejectedStories.Count == 0 && SourceRejections.Count == 0;

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpsisNewsCrawler", "breaking-feedback.json");

    public static BreakingFeedback Load(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (!File.Exists(path)) return new BreakingFeedback();
            return JsonSerializer.Deserialize<BreakingFeedback>(File.ReadAllText(path)) ?? new BreakingFeedback();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { return new BreakingFeedback(); }
    }

    public void Save(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>What the user chose for an urgent headline.</summary>
public enum BreakingFeedbackKind { NotBreaking, MuteSource, MuteTopic }
