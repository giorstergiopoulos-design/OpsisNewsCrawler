using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Plugin.Abstractions;

namespace OpsisNewsCrawler.Windows;

/// <summary>What the user chose for the lines of plugin feeds (Settings > Sources > History flow).</summary>
internal sealed record PluginFeedOptions(int MaxLines, string Order, string Country, string Language)
{
    public static PluginFeedOptions From(AppConfig c, string language) => new(c.PluginFeedMaxLines, c.PluginFeedOrder, c.PluginFeedCountry, language);
}

/// <summary>
/// Turns the lines of a plugin feed into the entries of the strip, in the same order every time. Rich feeds (<see cref="PluginRichFeed"/>) become
/// <see cref="NewsItemKind.Calendar"/> entries with the id of the line as key and its summary for the preview card; they never pass through the news filters or the urgent-news
/// detector. Text feeds keep the kind they always had.
/// </summary>
internal static class PluginFeedMapper
{
    public static IReadOnlyList<NewsItem> FromRich(IReadOnlyList<PluginRichFeedItem> items, string source, DateTimeOffset snapshot, PluginFeedOptions options)
    {
        var language = (options.Language ?? "").Split('-', '_')[0];
        var chosen = items.Where(i => !string.IsNullOrWhiteSpace(i.Text)).ToList();

        // the user's country: a line for another country is left out; a line with no country is for everybody
        if (!string.IsNullOrWhiteSpace(options.Country))
            chosen = chosen.Where(i => string.IsNullOrEmpty(i.CountryCode) || string.Equals(i.CountryCode, options.Country, StringComparison.OrdinalIgnoreCase)).ToList();

        // the language of the program: lines in another language are left out, unless that would leave nothing
        var inLanguage = chosen.Where(i => string.IsNullOrEmpty(i.LanguageCode) || string.Equals(i.LanguageCode.Split('-', '_')[0], language, StringComparison.OrdinalIgnoreCase)).ToList();
        if (inLanguage.Count > 0) chosen = inLanguage;

        // the same line twice (a feast day that is also in the history list): the first one by importance stays
        var ordered = Order(chosen, options.Order);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<NewsItem>();
        foreach (var i in ordered)
        {
            if (!string.IsNullOrEmpty(i.EntryId) && !seenIds.Add(i.EntryId)) continue;
            if (!seenTexts.Add(i.Text.Trim())) continue;
            result.Add(new NewsItem(i.Text.Trim(), i.Url ?? "", source, snapshot, string.IsNullOrWhiteSpace(i.Summary) ? null : i.Summary, null, NewsItemKind.Calendar, Key: string.IsNullOrEmpty(i.EntryId) ? null : i.EntryId));
            if (result.Count >= Math.Clamp(options.MaxLines, 1, 500)) break;
        }
        return result;
    }

    /// <summary>Every feed that plugins registered, rich ones too, each as a text feed (key, titles, lines): for the lists of the settings. A rich feed wins over a text feed of the same key.</summary>
    public static IReadOnlyList<PluginFeed> AllFeeds(Func<PluginFeedOptions>? options = null)
    {
        var rich = PluginFeeds.AllRich();
        var list = new List<PluginFeed>();
        foreach (var r in rich)
        {
            var feed = r;
            list.Add(new PluginFeed(feed.Key, feed.TitleEl, feed.TitleEn, () =>
                FromRich(feed.GetItems(), Loc.Pick(feed.TitleEl, feed.TitleEn), DateTimeOffset.Now, options?.Invoke() ?? new PluginFeedOptions(200, "priority", "", Loc.Language))
                    .Select(i => new PluginFeedItem(i.Title, string.IsNullOrEmpty(i.Url) ? null : i.Url)).ToList()));
        }
        list.AddRange(PluginFeeds.All().Where(t => !rich.Any(r => string.Equals(r.Key, t.Key, StringComparison.OrdinalIgnoreCase))));
        return list;
    }

    public static IReadOnlyList<NewsItem> FromText(IReadOnlyList<PluginFeedItem> items, string source, DateTimeOffset snapshot, PluginFeedOptions options) =>
        items.Where(i => !string.IsNullOrWhiteSpace(i.Text)).Take(Math.Clamp(options.MaxLines, 1, 500)).Select(i => new NewsItem(i.Text, i.Url ?? "", source, snapshot)).ToList();

    /// <summary>The same input always gives the same order: importance (higher first), or year (oldest or newest first, lines with no year last); ties by the id, then the text.</summary>
    internal static List<PluginRichFeedItem> Order(IEnumerable<PluginRichFeedItem> items, string order)
    {
        var list = items.ToList();
        IOrderedEnumerable<PluginRichFeedItem> sorted = order switch
        {
            "yearAsc" => list.OrderBy(i => i.HistoricalYear is null).ThenBy(i => i.HistoricalYear ?? 0).ThenByDescending(i => i.Priority),
            "yearDesc" => list.OrderBy(i => i.HistoricalYear is null).ThenByDescending(i => i.HistoricalYear ?? 0).ThenByDescending(i => i.Priority),
            _ => list.OrderByDescending(i => i.Priority).ThenBy(i => i.HistoricalYear is null).ThenByDescending(i => i.HistoricalYear ?? 0)
        };
        return sorted.ThenBy(i => i.EntryId, StringComparer.Ordinal).ThenBy(i => i.Text, StringComparer.Ordinal).ToList();
    }
}
