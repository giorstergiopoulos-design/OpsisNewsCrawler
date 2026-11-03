using System.Globalization;
using System.Text.RegularExpressions;

namespace OpsisNewsCrawler.Core;

public enum BreakingSensitivity { Strict, Normal, Sensitive }

/// <summary>One reason why a story counts as urgent: a code and up to two arguments; the settings and the preview card turn it into a sentence in the user's language.</summary>
public sealed record BreakingReason(string Code, string? Arg1 = null, string? Arg2 = null);

/// <summary>The verdict for one headline. <see cref="Score"/> is 0-100; <see cref="IsBreaking"/> is true for the one representative headline of an urgent story.</summary>
public sealed record BreakingAssessment(NewsItem Item, int Score, bool IsBreaking, IReadOnlyList<BreakingReason> Reasons, IReadOnlyList<string> Penalties, int Publishers);

public sealed record BreakingOptions(BreakingSensitivity Sensitivity, IReadOnlyCollection<string> InterestWords, TimeSpan MaxAge, BreakingFeedback? Feedback = null)
{
    public static BreakingOptions Default { get; } = new(BreakingSensitivity.Normal, Array.Empty<string>(), TimeSpan.FromHours(3));
}

/// <summary>
/// Decides which headlines are REALLY urgent, from many signals and not from one word: the same event reported by independent publishers in a short time
/// (confirmation), a burst of such headlines (speed), how fresh it is, how reliable the sources are (agencies, official bodies), objective data about a natural
/// disaster (the disaster feed), and a small weight for urgent words. Opinion, interviews, lists, sports / showbiz hyperbole, old events and announcements
/// of future events count against it. Everything runs locally; the user's "this is not breaking" choices adjust the weights (<see cref="BreakingFeedback"/>).
/// </summary>
public sealed class BreakingEngine
{
    // The score needed per sensitivity (0-100).
    public static int Threshold(BreakingSensitivity s) => s switch { BreakingSensitivity.Strict => 72, BreakingSensitivity.Sensitive => 42, _ => 58 };

    public static BreakingSensitivity ParseSensitivity(string? text) => (text ?? "").Trim().ToLowerInvariant() switch
    {
        "strict" => BreakingSensitivity.Strict, "sensitive" => BreakingSensitivity.Sensitive, _ => BreakingSensitivity.Normal
    };

    public static string SensitivityText(BreakingSensitivity s) => s.ToString().ToLowerInvariant();

    private static readonly string[] AgencyHosts = { "reuters.com", "apnews.com", "afp.com", "bbc.co.uk", "bbc.com", "ana.gr", "amna.gr", "aljazeera.com", "dpa.com", "dw.com", "usgs.gov", "gdacs.org", "emsc-csem.org", "civilprotection.gov.gr", "gov.gr", "europa.eu", "who.int", "un.org" };
    private static readonly string[] MajorHosts = { "ertnews.gr", "ekathimerini.com", "kathimerini.gr", "cnn.com", "nytimes.com", "theguardian.com", "washingtonpost.com", "euronews.com", "tovima.com", "naftemporiki.gr", "skai.gr", "protothema.gr", "in.gr", "lemonde.fr", "spiegel.de", "elpais.com", "corriere.it" };

    // Words that mark an urgent headline (a small weight only): Greek, English and the other languages of the program.
    public static readonly string[] UrgentWords =
    {
        "ΕΚΤΑΚΤΟ", "ΕΚΤΑΚΤΗ", "ΕΚΤΑΚΤΑ", "BREAKING", "URGENT", "FLASH", "ALERT", "EILMELDUNG", "DERNIERE MINUTE", "URGENTE", "ULTIMORA", "ULTIM'ORA", "ПОСЛЕДНИЕ НОВОСТИ", "МОЛНИЯ", "SON DAKIKA", "速報", "속보", "突发", "عاجل", "ब्रेकिंग"
    };

    private static readonly Regex Opinion = new(@"\b(opinion|analysis|explainer|interview|editorial|commentary|how to|top \d+|\d+ things|\d+ reasons|best of|review|podcast|column|αναλυση|γνωμη|συνεντευξη|σχολιο|αρθρο γνωμης|οδηγος|ολα οσα|τοπ \d+|kommentar|analyse|warum|tribune|pourquoi|opinion editorial|opinion|analisis|entrevista|por que|intervista|perche|opiniao|analise|porque|мнение|анализ|интервью|почему|yorum|analiz|roportaj|neden|分析|评论|인터뷰|분석|مقال|رأي|تحليل|विश्लेषण|राय)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Sports = new(@"(ποδοσφαιρ|γηπεδ|μεταγραφ|παοκ|ολυμπιακ|παναθηναικ|αεκ |football|soccer|transfer window|premier league|champions league|uefa|fifa|\bnba\b|\bnfl\b|tennis|formula 1|fussball|bundesliga|ligue 1|futbol|calcio|serie a|futebol|футбол|матч|futbol|maç|サッカー|축구|足球|كرة القدم|फुटबॉल)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Showbiz = new(@"(showbiz|celebrity|celebrities|singer|actress|actor|reality show|bachelor|x factor|red carpet|διασημ|τραγουδιστ|ηθοποι|ριαλιτι|gntm|prominente|star de|famosos|vip |celebrita|знаменитост|шоу|ünlü|芸能|연예|明星|مشاهير|सेलिब्रिटी)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Hyperbole = new(@"(shock|shocking|bomb|bombshell|explosive|stunning|σοκ|βομβα|εκρηκτικ|schock|choc|bombazo|choque|scioccante|chocante|шок|şok|衝撃|충격|震惊|صادم|चौंकाने)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Past = new(@"(years? ago|decades? ago|anniversary|on this day|remembering|throwback|χρονια πριν|χρονια απο|πριν απο \d+ χρον|επετειος|σαν σημερα|μνημοσυνο|jahre her|vor \d+ jahren|jahrestag|il y a \d+ ans|anniversaire|hace \d+ anos|aniversario|anni fa|anniversario|ha \d+ anos|aniversariu|лет назад|годовщина|yil once|yildonumu|年前|周年|년 전|주년|قبل \d+ عام|ذكرى|साल पहले|वर्षगांठ)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Schedule = new(@"(will be announced|to be announced|is expected to|are expected to|scheduled for|preview|what to expect|upcoming|τι να περιμενουμε|αναμενεται να|θα ανακοινωθει|προγραμμα|προγραμματιζεται|προεπισκοπηση|wird erwartet|geplant|a venir|se espera|previsto|atteso|previsto|ожидается|запланирован|beklenniyor|planlandi|予定|예정|计划|预计|متوقع|مقرر|अपेक्षित|निर्धारित)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Clickbait = new(@"(you won'?t believe|will blow your mind|what happened next|δεν θα πιστεψετε|θα εκπλαγειτε|τι ειπε|the truth about|unglaublich|vous ne croirez pas|no creeras|non crederai|voce nao vai acreditar|не поверите|inanamayacaksiniz|信じられない|믿을 수 없|你不会相信|لن تصدق|विश्वास नहीं)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly BreakingOptions options;

    public BreakingEngine(BreakingOptions? options = null) => this.options = options ?? BreakingOptions.Default;

    /// <summary>Assesses every news item; the verdict of a story that was reported by several publishers belongs to its representative headline only.</summary>
    public IReadOnlyList<BreakingAssessment> Assess(IEnumerable<NewsItem> items, IReadOnlyList<HazardEvent>? hazards, DateTimeOffset now)
    {
        var list = items.Where(i => i.Kind == NewsItemKind.News && !string.IsNullOrWhiteSpace(i.Title)).ToList();
        var clusters = GroupStories(list);
        var results = new List<BreakingAssessment>();
        foreach (var cluster in clusters)
        {
            var scored = Score(cluster, hazards, now);
            // the representative: the most reliable source, then the newest
            var rep = cluster.Items.OrderByDescending(i => SourceWeight(Publisher(i))).ThenByDescending(i => i.PublishedAt).First();
            foreach (var item in cluster.Items)
                results.Add(scored with { Item = item, IsBreaking = scored.IsBreaking && ReferenceEquals(item, rep) });
        }
        return results;
    }

    /// <summary>The headlines that count as urgent now (the representatives), best first.</summary>
    public IReadOnlyList<BreakingAssessment> Urgent(IEnumerable<NewsItem> items, IReadOnlyList<HazardEvent>? hazards, DateTimeOffset now) =>
        Assess(items, hazards, now).Where(a => a.IsBreaking).OrderByDescending(a => a.Score).ToList();

    // ---- story clusters ---------------------------------------------------------------------------------------------------------------------------

    private sealed record Report(string Publisher, DateTimeOffset At);

    private sealed class Cluster
    {
        public List<NewsItem> Items { get; } = new();
        public List<Report> Reports { get; } = new();
        public HashSet<string> Tokens { get; set; } = new();
    }

    private static List<Cluster> GroupStories(List<NewsItem> items)
    {
        var clusters = new List<Cluster>();
        foreach (var item in items.OrderBy(i => i.PublishedAt))
        {
            var tokens = StoryTokens(item.Title);
            var home = clusters.FirstOrDefault(c => SameStory(c.Tokens, tokens));
            if (home is null) { home = new Cluster { Tokens = tokens }; clusters.Add(home); }
            else home.Tokens.UnionWith(tokens.Where(t => t.Length >= 4));
            home.Items.Add(item);
            home.Reports.Add(new Report(Publisher(item), item.PublishedAt));
            // reports that the news provider folded into this headline when it removed near-duplicates of other publishers
            if (item.StoryReports is not null)
                foreach (var r in item.StoryReports) home.Reports.Add(new Report(RootHost(r.Host, r.Source), r.At));
        }
        return clusters;
    }

    /// <summary>Significant words (accents removed, lower case), numbers, and character pairs for languages written without spaces.</summary>
    public static HashSet<string> StoryTokens(string? title)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(title)) return set;
        // words keep their combining marks (Hindi vowel signs) and are cut to five letters, so "evacuation" / "evacuated" and "σεισμός" / "σεισμού" meet
        var plain = Breaking.Normalize(System.Net.WebUtility.HtmlDecode(title)).ToLowerInvariant().Replace('ς', 'σ');
        foreach (Match m in Regex.Matches(plain, @"[\p{L}\p{M}]+"))
        {
            if (m.Length < 3 || Regex.IsMatch(m.Value, @"^[\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}\p{IsHangulSyllables}]+$")) continue;
            set.Add(m.Length > 5 ? m.Value[..5] : m.Value);
        }
        foreach (Match m in Regex.Matches(title, @"\d+[.,]?\d*")) if (m.Length >= 2) set.Add(m.Value.Replace(',', '.'));
        foreach (Match run in Regex.Matches(title, @"[\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}\p{IsHangulSyllables}]{2,}"))
            for (var i = 0; i + 1 < run.Length; i++) set.Add(run.Value.Substring(i, 2));
        return set;
    }

    private static bool SameStory(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return false;
        if (HeadlineDedup.AreSameStory(a, b)) return true;
        var common = a.Count <= b.Count ? a.Count(b.Contains) : b.Count(a.Contains);
        return common >= 3 && common >= Math.Min(a.Count, b.Count) * 0.45;
    }

    // ---- scoring ---------------------------------------------------------------------------------------------------------------------------------

    private BreakingAssessment Score(Cluster c, IReadOnlyList<HazardEvent>? hazards, DateTimeOffset now)
    {
        var rep = c.Items.OrderByDescending(i => i.PublishedAt).First();
        var reasons = new List<BreakingReason>();
        var penalties = new List<string>();
        var title = Breaking.Normalize(string.Join(" ", c.Items.Select(i => i.Title))).ToLowerInvariant();
        var plain = HeadlineDedup.Words(string.Join(" ", c.Items.Select(i => i.Title)));
        var titleText = string.Join(" ", plain);

        var dated = c.Reports.Where(r => r.At != DateTimeOffset.MinValue).ToList();
        if (dated.Count == 0) return new BreakingAssessment(rep, 0, false, reasons, new[] { "undated" }, 0);
        var newest = dated.Max(r => r.At);
        var age = now - newest;
        if (age > options.MaxAge || age < TimeSpan.FromMinutes(-10)) return new BreakingAssessment(rep, 0, false, reasons, new[] { "too-old" }, c.Reports.Select(r => r.Publisher).Distinct().Count());

        // (1) confirmation by independent publishers within a short window
        var window = dated.Where(r => newest - r.At <= TimeSpan.FromMinutes(30)).ToList();
        var publishers = window.Select(r => r.Publisher).Distinct().ToList();
        var score = 0;
        var n = publishers.Count;
        if (n >= 2)
        {
            score += n switch { 2 => 22, 3 => 34, _ => 42 };
            var span = (int)Math.Max(1, Math.Round((window.Max(r => r.At) - window.Min(r => r.At)).TotalMinutes));
            reasons.Add(new BreakingReason("sources", n.ToString(CultureInfo.InvariantCulture), span.ToString(CultureInfo.InvariantCulture)));
        }

        // (2) speed: a burst in the last 10 minutes
        var recent = dated.Where(r => now - r.At <= TimeSpan.FromMinutes(10)).Select(r => r.Publisher).Distinct().Count();
        if (recent >= 2)
        {
            score += recent >= 4 ? 14 : recent == 3 ? 10 : 6;
            reasons.Add(new BreakingReason("fast", recent.ToString(CultureInfo.InvariantCulture)));
        }

        // (3) freshness: minutes, not hours
        var minutes = Math.Max(0, (int)Math.Round(age.TotalMinutes));
        var fresh = age <= TimeSpan.FromMinutes(3) ? 16 : age <= TimeSpan.FromMinutes(10) ? 12 : age <= TimeSpan.FromMinutes(30) ? 7 : age <= TimeSpan.FromMinutes(90) ? 3 : 0;
        score += fresh;
        if (fresh >= 12) reasons.Add(new BreakingReason("fresh", minutes.ToString(CultureInfo.InvariantCulture)));

        // (4) reliability of the sources
        var best = publishers.Select(SourceWeight).DefaultIfEmpty(3).Max();
        var feedbackDelta = publishers.Select(p => options.Feedback?.SourceDelta(p) ?? 0).DefaultIfEmpty(0).Min();
        score += best + feedbackDelta;
        if (best >= 10) reasons.Add(new BreakingReason("agency"));

        // (5) objective data about a natural disaster
        var hazardMatch = MatchHazard(titleText, title, hazards, now);
        if (hazardMatch is not null)
        {
            score += hazardMatch.Value.Weight;
            reasons.Add(new BreakingReason("hazard", hazardMatch.Value.Kind, hazardMatch.Value.Magnitude));
        }

        // (6) urgent words: a small hint, never the reason alone
        var normalized = Breaking.Normalize(string.Join(" ", c.Items.Select(i => i.Title)));
        var urgentWord = UrgentWords.Any(w => normalized.Contains(Breaking.Normalize(w), StringComparison.Ordinal));
        var interest = options.InterestWords.Any(w => Breaking.Normalize(w).Trim() is { Length: >= 3 } k && normalized.Contains(k, StringComparison.Ordinal));
        if (urgentWord || interest)
        {
            score += urgentWord && interest ? 14 : urgentWord ? 10 : 8;
            reasons.Add(new BreakingReason(interest && !urgentWord ? "interest" : "keyword"));
        }

        // (7) negative filters
        var hard = false;
        if (Opinion.IsMatch(title)) { score -= 30; penalties.Add("opinion"); hard = true; }
        if (Past.IsMatch(title)) { score -= 30; penalties.Add("past"); hard = true; }
        if (Schedule.IsMatch(title)) { score -= 22; penalties.Add("schedule"); hard = true; }
        if (Clickbait.IsMatch(title)) { score -= 25; penalties.Add("clickbait"); hard = true; }
        if (hazardMatch is null && (Sports.IsMatch(title) || Showbiz.IsMatch(title)))
        {
            score -= Hyperbole.IsMatch(title) || urgentWord ? 35 : 20;
            penalties.Add("sports-showbiz");
            hard = true;
        }
        if (c.Items.All(i => i.Title.TrimEnd().EndsWith('?'))) { score -= 15; penalties.Add("question"); }
        if (options.Feedback is { } fb)
        {
            if (publishers.All(fb.IsSourceMuted)) { score = 0; penalties.Add("source-muted"); }
            if (fb.IsTopicMuted(plain)) { score = 0; penalties.Add("topic-muted"); }
            if (fb.IsRejected(plain)) { score = 0; penalties.Add("rejected"); }
        }

        score = Math.Clamp(score, 0, 100);
        // A headline that is opinion, a list, an old event or an announcement never passes on the strength of a few other signals.
        var cap = Threshold(BreakingSensitivity.Sensitive) - 1;
        if (hard && score > cap) score = cap;
        var isBreaking = score >= Threshold(options.Sensitivity);
        return new BreakingAssessment(rep, score, isBreaking, reasons, penalties, n);
    }

    private (int Weight, string Kind, string Magnitude)? MatchHazard(string words, string normalizedTitle, IReadOnlyList<HazardEvent>? hazards, DateTimeOffset now)
    {
        if (hazards is null || hazards.Count == 0) return null;
        (int, string, string)? best = null;
        foreach (var h in hazards)
        {
            if (now - h.Time > TimeSpan.FromHours(3)) continue;
            var terms = HazardTerms(h.Kind);
            if (!terms.Any(t => normalizedTitle.Contains(t, StringComparison.Ordinal))) continue;
            var placeWords = HeadlineDedup.Words(h.Place);
            // same place when the title names it; when the title names no place of the event at all, a very strong event still counts
            var samePlace = placeWords.Count > 0 && placeWords.Any(w => w.Length >= 4 && words.Contains(w, StringComparison.Ordinal));
            // the magnitude written in the headline (6,4 / 6.4) is the same evidence in every language
            if (!samePlace && h.Magnitude is { } mag && Regex.Matches(normalizedTitle, @"\d+[.,]\d").Any(m => double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && Math.Abs(v - mag) < 0.11))
                samePlace = true;
            var strong = h.Level >= 3 || (h.Magnitude ?? 0) >= 7.0;
            if (!samePlace && !strong) continue;
            var weight = strong ? 45 : (h.Magnitude ?? 0) >= 6.0 || h.Level == 2 ? 38 : (h.Magnitude ?? 0) >= 5.0 ? 24 : 12;
            var magnitude = h.Magnitude is { } m ? m.ToString("0.0", CultureInfo.InvariantCulture) : "";
            if (best is null || weight > best.Value.Item1) best = (weight, h.Kind.ToString().ToLowerInvariant(), magnitude);
        }
        return best;
    }

    /// <summary>Words that name a kind of disaster, in the languages of the program (accents removed, lower case; the first one is the label).</summary>
    private static string[] HazardTerms(HazardKind kind) => kind switch
    {
        HazardKind.Earthquake => new[] { "earthquake", "σεισμ", "seism", "erdbeben", "tremblement de terre", "seisme", "terremoto", "sismo", "землетряс", "deprem", "地震", "지진", "زلزال", "भूकंप", "quake" },
        HazardKind.Wildfire => new[] { "wildfire", "πυρκαγι", "forest fire", "waldbrand", "feu de foret", "incendio", "incendie", "лесной пожар", "orman yangini", "山火事", "산불", "山火", "حريق", "जंगल की आग", "φωτια" },
        HazardKind.Flood => new[] { "flood", "πλημμυρ", "hochwasser", "inondation", "inundacion", "alluvione", "наводнен", "sel baskini", "洪水", "홍수", "فيضان", "बाढ़" },
        HazardKind.Storm => new[] { "storm", "hurricane", "cyclone", "typhoon", "καταιγιδ", "κυκλωνας", "sturm", "tempete", "tormenta", "tempesta", "шторм", "firtina", "台風", "태풍", "عاصفة", "तूफान" },
        HazardKind.Volcano => new[] { "volcano", "ηφαιστει", "vulkan", "volcan", "vulcano", "вулкан", "yanardag", "火山", "화산", "بركان", "ज्वालामुखी" },
        HazardKind.Tsunami => new[] { "tsunami", "τσουναμι", "цунами", "津波", "쓰나미", "سونامي", "सुनामी" },
        HazardKind.Drought => new[] { "drought", "ξηρασι", "dürre", "secheresse", "sequia", "siccita", "засух", "kuraklik" },
        _ => new[] { "disaster", "καταστροφ" }
    };

    // ---- sources ---------------------------------------------------------------------------------------------------------------------------------

    public static string Publisher(NewsItem item) => RootHost(Uri.TryCreate(item.Url, UriKind.Absolute, out var u) ? u.Host : "", item.Source);

    /// <summary>The publisher: the last two labels of the host (news.example.com and www.example.com are one publisher); the feed name when there is no address.</summary>
    public static string RootHost(string? host, string? fallback)
    {
        if (string.IsNullOrWhiteSpace(host)) return (fallback ?? "").Trim().ToLowerInvariant();
        var labels = host.ToLowerInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length <= 2) return string.Join('.', labels);
        // co.uk, gov.gr, com.tr ...: keep three labels when the second-level label is a registry word
        return labels[^2] is "co" or "com" or "gov" or "org" or "ac" ? string.Join('.', labels[^3..]) : string.Join('.', labels[^2..]);
    }

    public static int SourceWeight(string publisher)
    {
        if (AgencyHosts.Any(a => publisher == a || publisher.EndsWith("." + a, StringComparison.Ordinal) || a.EndsWith("." + publisher, StringComparison.Ordinal) && publisher.Contains('.'))) return 10;
        if (publisher.EndsWith(".gov") || publisher.Contains(".gov.")) return 10;
        if (MajorHosts.Any(a => publisher == a || publisher.EndsWith("." + a, StringComparison.Ordinal))) return 6;
        return 3;
    }
}
