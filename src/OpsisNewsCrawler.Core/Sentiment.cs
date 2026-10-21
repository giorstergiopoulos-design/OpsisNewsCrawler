using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

public enum Mood { Negative = -1, Neutral = 0, Positive = 1 }

/// <summary>How the news talks about one watched share: the headlines that mention it and their average mood.</summary>
public sealed record ShareMood(QuoteInstrument Instrument, int Headlines, double Average, IReadOnlyList<NewsItem> Items);

/// <summary>
/// A deliberately simple news-mood estimate (the idea of "news sentiment" of greek-stocks-ai, written from scratch): short lists of Greek and
/// English words that make a headline sound good or bad, accents and letter case ignored, a negation word in front of a word flips it.
/// It is a rough guide to the tone of the headlines, nothing more - not a forecast and not investment advice.
/// </summary>
public static class NewsSentiment
{
    // whole words
    private static readonly HashSet<string> PositiveWords = new(StringComparer.Ordinal)
    {
        "gain", "gains", "rise", "rises", "rose", "surge", "surges", "jump", "jumps", "rally", "rallies", "record", "profit", "profits", "growth", "beat", "beats",
        "upgrade", "upgrades", "strong", "stronger", "boost", "rebound", "recover", "recovers", "recovery", "bullish", "soar", "soars", "outperform", "dividend", "dividends",
        "deal", "wins", "win", "approve", "approved", "approval", "up", "higher", "optimism", "positive",
        "ανοδος", "ανεβαινει", "ανεβηκε", "κερδη", "κερδος", "ρεκορ", "θετικο", "θετικα", "ισχυρο", "ισχυρη", "ανακαμψη", "μερισμα", "συμφωνια", "επιτυχια", "αισιοδοξια",
    };

    private static readonly HashSet<string> NegativeWords = new(StringComparer.Ordinal)
    {
        "fall", "falls", "fell", "drop", "drops", "dropped", "plunge", "plunges", "slump", "loss", "losses", "decline", "declines", "cut", "cuts", "weak", "weaker", "downgrade",
        "downgrades", "warning", "warns", "warn", "fraud", "probe", "lawsuit", "crash", "bearish", "slide", "slides", "sink", "sinks", "worry", "worries", "fears", "fear", "risk",
        "default", "bankruptcy", "layoffs", "sanctions", "strike", "debt", "recession", "tumble", "tumbles", "down", "lower", "pessimism", "negative", "fine", "fined",
        "πτωση", "πεφτει", "επεσε", "ζημια", "ζημιες", "απωλειες", "πτωχευση", "αρνητικο", "αρνητικα", "ανησυχια", "κινδυνος", "υφεση", "κριση", "χρεος", "κυρωσεις", "απεργια", "προστιμο",
    };

    // word beginnings (at least 5 letters) - they cover the Greek and English endings
    private static readonly string[] PositiveStems =
    {
        "surg", "rebound", "recover", "outperf", "upgrad", "bullish", "dividen", "κερδιζ", "κερδη", "ανοδικ", "αναπτυξ", "ενισχυ", "αναβαθμ", "εγκριθ", "εγκριση", "ανακαμψ", "ανεβα", "ρεκορ", "επιτυχ", "υπερβαιν",
    };

    private static readonly string[] NegativeStems =
    {
        "plung", "slump", "declin", "downgrad", "bearish", "bankrupt", "layoff", "tumbl", "recess", "πτωτικ", "πτωχευ", "ζημι", "απωλει", "υποβαθμ", "προειδοπ", "ελλειμμ", "καταρρευ", "καθοδικ", "μειωση", "μειωθ",
        "αρνητικ", "ανησυχ", "διαρροη", "πλειστηρ", "εξωδικ",
    };

    private static readonly HashSet<string> Negations = new(StringComparer.Ordinal) { "not", "no", "never", "without", "δεν", "μη", "μην", "χωρις", "ουτε" };

    /// <summary>Mood of a text from -1 (all bad words) to +1 (all good words); 0 when it has none of them.</summary>
    public static double Score(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var words = Regex.Split(StockNewsFilter.Normalize(text), " ").Where(w => w.Length > 0).ToArray();
        double good = 0, bad = 0;
        for (var i = 0; i < words.Length; i++)
        {
            var w = words[i];
            int sign = PositiveWords.Contains(w) || (w.Length >= 4 && PositiveStems.Any(w.StartsWith)) ? 1
                     : NegativeWords.Contains(w) || (w.Length >= 4 && NegativeStems.Any(w.StartsWith)) ? -1 : 0;
            if (sign == 0) continue;
            if (i > 0 && Negations.Contains(words[i - 1])) sign = -sign;   // "no gain", "δεν ανεβαίνει"
            if (sign > 0) good++; else bad++;
        }
        return good + bad == 0 ? 0 : (good - bad) / (good + bad);
    }

    public static Mood Classify(double score) => score > 0.2 ? Mood.Positive : score < -0.2 ? Mood.Negative : Mood.Neutral;

    public static double ScoreOf(NewsItem item) => Score(item.Title + " " + item.Summary);

    /// <summary>For every watched share the headlines that mention it (symbol or name) and their average mood; shares without headlines are left out.</summary>
    public static IReadOnlyList<ShareMood> PerShare(IEnumerable<NewsItem> items, IEnumerable<QuoteInstrument> watchlist)
    {
        var list = items.Where(i => i.Kind == NewsItemKind.News).ToList();
        var result = new List<ShareMood>();
        foreach (var inst in watchlist.Where(i => i is not null && i.Source is QuoteSource.Athens or QuoteSource.Euronext).GroupBy(i => i.StoreKey).Select(g => g.First()))
        {
            var probe = inst.Clone();
            probe.Enabled = true;
            var mentions = StockNewsFilter.Filter(list, new[] { probe });
            if (mentions.Count == 0) continue;
            result.Add(new ShareMood(inst, mentions.Count, mentions.Average(ScoreOf), mentions));
        }
        return result.OrderByDescending(r => r.Headlines).ThenBy(r => r.Instrument.Symbol, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
