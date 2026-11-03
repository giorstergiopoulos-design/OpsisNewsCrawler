using OpsisNewsCrawler.Core;
using Xunit;
using Xunit.Abstractions;

namespace OpsisNewsCrawler.Core.Tests;

public class BreakingEngineTests
{
    private readonly ITestOutputHelper output;
    public BreakingEngineTests(ITestOutputHelper output) => this.output = output;

    private static IReadOnlyList<BreakingAssessment> Run(BreakingCase c, BreakingSensitivity s = BreakingSensitivity.Normal, BreakingFeedback? feedback = null) =>
        new BreakingEngine(new BreakingOptions(s, Array.Empty<string>(), TimeSpan.FromHours(3), feedback)).Urgent(c.Items, c.Hazards, BreakingCorpus.Now);

    private static (int Tp, int Fp, int Fn, int Tn, List<string> Wrong) Evaluate(BreakingSensitivity s)
    {
        int tp = 0, fp = 0, fn = 0, tn = 0;
        var wrong = new List<string>();
        foreach (var c in BreakingCorpus.All)
        {
            var urgent = Run(c, s).Count > 0;
            if (c.ExpectUrgent && urgent) tp++;
            else if (c.ExpectUrgent) { fn++; wrong.Add("MISSED   " + c.Name + " [" + string.Join(", ", new BreakingEngine(new BreakingOptions(s, Array.Empty<string>(), TimeSpan.FromHours(3))).Assess(c.Items, c.Hazards, BreakingCorpus.Now).Select(a => a.Score).Distinct()) + "]"); }
            else if (urgent) { fp++; wrong.Add("FALSE    " + c.Name); }
            else tn++;
        }
        return (tp, fp, fn, tn, wrong);
    }

    [Fact]
    public void CorpusIsBigEnoughAndCoversEveryLanguage()
    {
        Assert.True(BreakingCorpus.All.Count >= 60, $"cases: {BreakingCorpus.All.Count}");
        var languages = new[] { "el", "en", "de", "fr", "es", "it", "pt", "ru", "tr", "ja", "ko", "zh", "ar", "hi" };
        foreach (var l in languages)
        {
            Assert.Contains(BreakingCorpus.All, c => c.Language == l && c.ExpectUrgent);
            Assert.Contains(BreakingCorpus.All, c => c.Language == l && !c.ExpectUrgent);
        }
    }

    [Fact]
    public void PrecisionAndRecallOfTheNormalSensitivityStayHigh()
    {
        var (tp, fp, fn, tn, wrong) = Evaluate(BreakingSensitivity.Normal);
        var precision = tp / (double)Math.Max(1, tp + fp);
        var recall = tp / (double)Math.Max(1, tp + fn);
        var report = $"Urgent-news detector, sensitivity Normal: {BreakingCorpus.All.Count} labelled cases, TP {tp} FP {fp} FN {fn} TN {tn}, precision {precision:P0}, recall {recall:P0}";
        output.WriteLine(report);
        foreach (var w in wrong) output.WriteLine(w);
        WriteMetrics(report, wrong, precision, recall);
        Assert.True(precision >= 0.92, report + "\n" + string.Join("\n", wrong));
        Assert.True(recall >= 0.85, report + "\n" + string.Join("\n", wrong));
    }

    [Fact]
    public void StrictIsMorePreciseAndSensitiveFindsMore()
    {
        var strict = Evaluate(BreakingSensitivity.Strict);
        var normal = Evaluate(BreakingSensitivity.Normal);
        var sensitive = Evaluate(BreakingSensitivity.Sensitive);
        Assert.True(strict.Tp <= normal.Tp && normal.Tp <= sensitive.Tp, $"TP {strict.Tp} {normal.Tp} {sensitive.Tp}");
        Assert.True(strict.Fp <= normal.Fp && normal.Fp <= sensitive.Fp, $"FP {strict.Fp} {normal.Fp} {sensitive.Fp}");
        Assert.True(strict.Fp == 0, "strict must raise no false alarm on the corpus: " + string.Join("; ", strict.Wrong));
    }

    private static void WriteMetrics(string report, List<string> wrong, double precision, double recall)
    {
        try
        {
            var text = "### Urgent-news detector (labelled corpus)\n\n" + report + "\n\n" + (wrong.Count == 0 ? "No mistakes on the corpus." : "```\n" + string.Join("\n", wrong) + "\n```") + "\n";
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "breaking-metrics.md"), text);
        }
        catch (IOException) { }
    }

    // ---------------- regression: keywords alone never make news urgent ----------------

    [Theory]
    [InlineData("ΕΚΤΑΚΤΟ: νέα προσφορά")]
    [InlineData("BREAKING: new colours for your phone")]
    [InlineData("Eilmeldung: neues Rezept des Tages")]
    [InlineData("速報：新しいメニュー")]
    [InlineData("عاجل: عرض جديد")]
    public void ATitleWithAnUrgentWordButNoConfirmationIsNotUrgent(string title)
    {
        var item = new NewsItem(title, "https://single-source.example.com/a", "single", BreakingCorpus.Now.AddMinutes(-1));
        foreach (var s in Enum.GetValues<BreakingSensitivity>())
            Assert.Empty(new BreakingEngine(new BreakingOptions(s, Array.Empty<string>(), TimeSpan.FromHours(3))).Urgent(new[] { item }, null, BreakingCorpus.Now));
    }

    [Fact]
    public void TheOldKeywordRuleWouldHaveRaisedEveryFalseAlarmOfTheCorpusThatHasAUrgentWord()
    {
        // documents the problem this engine solves: the keyword rule flags headlines the new engine rightly refuses
        var flaggedByKeywordOnly = BreakingCorpus.All.Where(c => !c.ExpectUrgent)
            .Count(c => c.Items.Any(i => Breaking.IsBreaking(i with { PublishedAt = DateTimeOffset.Now }, Breaking.DefaultKeywords, TimeSpan.FromHours(1))));
        var flaggedByEngine = BreakingCorpus.All.Where(c => !c.ExpectUrgent).Count(c => Run(c).Count > 0);
        Assert.True(flaggedByKeywordOnly >= 8, $"keyword rule: {flaggedByKeywordOnly}");
        Assert.True(flaggedByEngine < flaggedByKeywordOnly, $"engine {flaggedByEngine} vs keywords {flaggedByKeywordOnly}");
    }

    // ---------------- the signals one by one ----------------

    private static NewsItem N(string title, string host, double minutesAgo) =>
        new(title, $"https://{host}/x/{Math.Abs(title.GetHashCode())}", host, BreakingCorpus.Now.AddMinutes(-minutesAgo));

    private static BreakingAssessment Best(IEnumerable<NewsItem> items, IReadOnlyList<HazardEvent>? hz = null, BreakingSensitivity s = BreakingSensitivity.Normal, BreakingFeedback? fb = null) =>
        new BreakingEngine(new BreakingOptions(s, Array.Empty<string>(), TimeSpan.FromHours(3), fb)).Assess(items, hz, BreakingCorpus.Now).OrderByDescending(a => a.IsBreaking).ThenByDescending(a => a.Score).First();

    [Fact]
    public void ConfirmationByMorePublishersRaisesTheScoreAndTheReasonSaysHow()
    {
        var one = Best(new[] { N("Powerful explosion rocks the city centre of Lyon", "a.example.com", 4) });
        var three = Best(new[] { N("Powerful explosion rocks the city centre of Lyon", "a.example.com", 4), N("Explosion rocks the city centre of Lyon, injured reported", "b.example.org", 5), N("Lyon city centre rocked by powerful explosion", "c.example.net", 6) });
        Assert.True(three.Score > one.Score + 25);
        Assert.Equal(3, three.Publishers);
        Assert.Contains(three.Reasons, r => r.Code == "sources" && r.Arg1 == "3");
    }

    [Fact]
    public void ThePublishersOfOneSiteCountOnce()
    {
        var a = Best(new[] { N("Powerful explosion rocks the city centre of Lyon", "www.example.com", 4), N("Explosion rocks the city centre of Lyon, injured reported", "news.example.com", 5), N("Lyon city centre rocked by powerful explosion", "example.com", 6) });
        Assert.Equal(1, a.Publishers);
    }

    [Fact]
    public void OldStoriesAndUndatedItemsAreNeverUrgent()
    {
        var old = Best(new[] { N("Powerful explosion rocks the city centre of Lyon", "a.example.com", 400), N("Explosion rocks the city centre of Lyon, injured reported", "b.example.org", 401), N("Lyon city centre rocked by powerful explosion", "c.example.net", 402) });
        Assert.False(old.IsBreaking);
        var undated = Best(new[] { new NewsItem("Powerful explosion rocks the city centre of Lyon", "https://a.example.com/1", "a", DateTimeOffset.MinValue) });
        Assert.False(undated.IsBreaking);
        Assert.Contains("undated", undated.Penalties);
    }

    [Fact]
    public void DisasterDataMakesAStrongQuakeUrgentEvenFromOnePublisher_AndAWeakOneNot()
    {
        var strong = Best(new[] { N("Σεισμός 6,4 Ρίχτερ νότια της Ρόδου", "ana.gr", 2) }, new[] { new HazardEvent("u1", HazardKind.Earthquake, "Rhodes, Greece", 36, 28, BreakingCorpus.Now.AddMinutes(-3), "", 6.4, 6.4, 6.4, 2, new[] { "USGS" }) });
        Assert.True(strong.IsBreaking);
        Assert.Contains(strong.Reasons, r => r.Code == "hazard" && r.Arg1 == "earthquake" && r.Arg2 == "6.4");
        var weak = Best(new[] { N("Μικρός σεισμός 3,1 Ρίχτερ κοντά στη Ρόδο", "localblog.example.gr", 2) }, new[] { new HazardEvent("u2", HazardKind.Earthquake, "Rhodes, Greece", 36, 28, BreakingCorpus.Now.AddMinutes(-3), "", 3.1, 3.1, 3.1, 1, new[] { "USGS" }) });
        Assert.False(weak.IsBreaking);
    }

    [Fact]
    public void AHazardOfAnotherKindOrAnotherPlaceDoesNotLendItsWeight()
    {
        var hz = new[] { new HazardEvent("u3", HazardKind.Earthquake, "Rhodes, Greece", 36, 28, BreakingCorpus.Now.AddMinutes(-3), "", 6.4, 6.4, 6.4, 2, new[] { "USGS" }) };
        Assert.DoesNotContain(Best(new[] { N("Φωτιά στη Ρόδο, εκκενώνονται χωριά", "localblog.example.gr", 2) }, hz).Reasons, r => r.Code == "hazard");               // another kind
        Assert.DoesNotContain(Best(new[] { N("Σεισμός στη Θεσσαλονίκη, αισθητός σε όλη την πόλη", "localblog.example.gr", 2) }, hz).Reasons, r => r.Code == "hazard");  // another place
    }

    [Fact]
    public void TheRepresentativeIsTheMostReliableSourceAndOnlyOneHeadlinePerStoryIsFlagged()
    {
        var items = new[] { N("Powerful explosion rocks the city centre of Lyon", "blog.example.com", 3), N("Explosion rocks the city centre of Lyon, injured reported", "reuters.com", 4), N("Lyon city centre rocked by powerful explosion", "bbc.co.uk", 5) };
        var all = new BreakingEngine().Assess(items, null, BreakingCorpus.Now);
        var flagged = all.Where(a => a.IsBreaking).ToList();
        Assert.Single(flagged);
        Assert.Contains("reuters.com", flagged[0].Item.Url);
    }

    [Fact]
    public void ProviderReportsCountAsConfirmations()
    {
        var kept = N("Powerful explosion rocks the city centre of Lyon", "a.example.com", 4) with
        {
            StoryReports = new[] { new StoryReport("B", "b.example.org", BreakingCorpus.Now.AddMinutes(-5)), new StoryReport("C", "c.example.net", BreakingCorpus.Now.AddMinutes(-6)) }
        };
        var a = Best(new[] { kept });
        Assert.Equal(3, a.Publishers);
        Assert.True(a.IsBreaking);
    }

    [Fact]
    public void InterestWordsAreAHintAndNeverTheReasonAlone()
    {
        var engine = new BreakingEngine(new BreakingOptions(BreakingSensitivity.Sensitive, new[] { "Lyon" }, TimeSpan.FromHours(3)));
        Assert.Empty(engine.Urgent(new[] { N("Lyon opens a new bakery near the station", "a.example.com", 2) }, null, BreakingCorpus.Now));
    }

    // ---------------- the user's feedback (local learning) ----------------

    [Fact]
    public void NotBreakingRemovesTheStoryAndWeighsTheSourceLess()
    {
        var story = new[] { N("Powerful explosion rocks the city centre of Lyon", "a.example.com", 3), N("Explosion rocks the city centre of Lyon, injured reported", "b.example.org", 4), N("Lyon city centre rocked by powerful explosion", "c.example.net", 5) };
        Assert.True(Best(story).IsBreaking);
        var fb = new BreakingFeedback();
        fb.Reject(story[0]);
        Assert.False(Best(story, fb: fb).IsBreaking);                                                         // the same story is not flagged again
        var pa = BreakingEngine.Publisher(story[0]);
        Assert.True(fb.SourceDelta(pa) < 0);
        Assert.Equal(0, fb.SourceDelta(BreakingEngine.Publisher(story[1])));
        for (var i = 0; i < 20; i++) fb.Reject(story[0]);
        Assert.Equal(-BreakingFeedback.MaxDelta, fb.SourceDelta(pa));                          // never more than the cap
    }

    [Fact]
    public void AMutedSourceAndAMutedTopicNeverRaiseAnAlert()
    {
        var story = new[] { N("Powerful explosion rocks the city centre of Lyon", "a.example.com", 3), N("Explosion rocks the city centre of Lyon, injured reported", "b.example.org", 4), N("Lyon city centre rocked by powerful explosion", "c.example.net", 5) };
        var fb = new BreakingFeedback();
        foreach (var i in story) fb.MuteSource(i);
        Assert.False(Best(story, fb: fb).IsBreaking);
        var fb2 = new BreakingFeedback();
        var topic = fb2.MuteTopic(story[0]);
        Assert.NotEmpty(topic);
        Assert.False(Best(story, fb: fb2).IsBreaking);
        Assert.True(Best(new[] { N("Totally different story about a bridge collapse in Porto, many injured", "a.example.com", 2), N("Bridge collapse in Porto leaves many injured", "b.example.org", 3), N("Porto bridge collapses, many people injured", "c.example.net", 4) }, fb: fb2).Score >= 0);
    }

    [Fact]
    public void FeedbackIsSavedAndLoadedLocally()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "breaking-feedback.json");
        try
        {
            var fb = new BreakingFeedback();
            fb.Reject(N("Powerful explosion rocks the city centre of Lyon", "a.example.com", 3));
            fb.MuteSource(N("x y z", "muted.example.com", 1));
            fb.MuteTopic(N("Powerful explosion rocks the city centre of Lyon", "a.example.com", 3));
            fb.Save(path);
            var loaded = BreakingFeedback.Load(path);
            Assert.Equal(fb.RejectedStories, loaded.RejectedStories);
            Assert.Equal(fb.MutedSources, loaded.MutedSources);
            Assert.Equal(fb.MutedTopics, loaded.MutedTopics);
            Assert.True(loaded.SourceDelta(BreakingEngine.Publisher(N("x", "a.example.com", 1))) < 0);
            loaded.Clear();
            Assert.True(loaded.IsEmpty);
            File.WriteAllText(path, "not json");
            Assert.True(BreakingFeedback.Load(path).IsEmpty);                                                // a damaged file is ignored, not a crash
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ---------------- a burst over time ----------------

    [Fact]
    public void ABurstOverTimeBecomesUrgentOnlyWhenTheSecondAndThirdPublisherArrive()
    {
        var engine = new BreakingEngine();
        var t0 = BreakingCorpus.Now;
        var all = new List<NewsItem>();
        NewsItem At(string title, string host, double minute) => new(title, $"https://{host}/x/{Math.Abs(title.GetHashCode())}", host, t0.AddMinutes(minute));
        // minute 0: only one publisher, with an urgent word
        all.Add(At("BREAKING: Powerful explosion rocks the city centre of Lyon", "a.example.com", 0));
        Assert.Empty(engine.Urgent(all, null, t0.AddMinutes(1)));
        // minute 2: a second publisher
        all.Add(At("Explosion rocks the city centre of Lyon, injured reported", "reuters.com", 2));
        var afterTwo = engine.Urgent(all, null, t0.AddMinutes(3));
        // minute 4: a third and a fourth
        all.Add(At("Lyon city centre rocked by powerful explosion", "bbc.co.uk", 4));
        all.Add(At("Powerful blast in the centre of Lyon, several injured", "lemonde.fr", 5));
        var afterFour = engine.Urgent(all, null, t0.AddMinutes(6));
        Assert.NotEmpty(afterFour);
        Assert.True(afterFour[0].Score >= (afterTwo.Count > 0 ? afterTwo[0].Score : 0));
        // an hour later the burst is over: freshness is gone, the story leaves the urgent list
        Assert.Empty(new BreakingEngine(new BreakingOptions(BreakingSensitivity.Normal, Array.Empty<string>(), TimeSpan.FromMinutes(45))).Urgent(all, null, t0.AddMinutes(120)));
    }

    // ---------------- cost ----------------

    [Fact]
    public void ThousandHeadlinesAreAssessedQuickly()
    {
        var rng = new Random(7);
        var items = Enumerable.Range(0, 1000).Select(i => N($"Story number {i} about topic {rng.Next(80)} and subject {rng.Next(80)} in place {rng.Next(40)}", $"site{rng.Next(30)}.example.com", rng.Next(0, 170))).ToList();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        new BreakingEngine().Assess(items, null, BreakingCorpus.Now);
        Assert.True(sw.ElapsedMilliseconds < 3000, $"{sw.ElapsedMilliseconds} ms");
    }

    [Theory]
    [InlineData("www.bbc.co.uk", "bbc.co.uk")]
    [InlineData("news.example.com", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("www.civilprotection.gov.gr", "civilprotection.gov.gr")]
    [InlineData("", "feed name")]
    public void PublisherIsTheRootOfTheHost(string host, string expected) => Assert.Equal(expected, BreakingEngine.RootHost(host, "Feed Name"));

    // ---------------- settings ----------------

    [Fact]
    public void OldKeywordListsKeepOnlyTheUsersOwnWordsAndSensitivityIsNormalised()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "config.json");
        try
        {
            File.WriteAllText(path, "{\"BreakingKeywords\":[\"ΕΚΤΑΚΤΟ\",\"Breaking\",\"flash\",\"Lyon\"],\"BreakingSensitivity\":\"banana\"}");
            var c = AppConfig.Load(path);
            Assert.Equal(new[] { "Lyon" }, c.BreakingKeywords);
            Assert.Equal("normal", c.BreakingSensitivity);
            Assert.Empty(new AppConfig().BreakingKeywords);
            foreach (var s in new[] { "strict", "normal", "sensitive" }) Assert.Equal(s, BreakingEngine.SensitivityText(BreakingEngine.ParseSensitivity(s)));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void EveryReasonHasASentenceInEveryLanguage()
    {
        var keys = new[] { "why.prefix", "why.sources", "why.fast", "why.fresh", "why.agency", "why.hazard", "why.hazardMagnitude", "why.keyword", "why.interest",
            "menu.notBreaking", "menu.muteSource", "menu.muteTopic", "breaking.feedbackSaved", "breaking.sensitivity", "breaking.sens.strict", "breaking.sens.normal",
            "breaking.sens.sensitive", "breaking.sensitivity.note", "breaking.forget", "breaking.forgotten" };
        var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OpsisNewsCrawler.Core", "Locales");
        var files = Directory.GetFiles(dir, "*.json");
        Assert.Equal(14, files.Length);
        foreach (var f in files)
        {
            var d = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(f))!;
            foreach (var k in keys) Assert.True(d.TryGetValue(k, out var v) && v.Length > 0, $"{Path.GetFileName(f)}: {k}");
            Assert.Contains("{0}", d["why.sources"]); Assert.Contains("{1}", d["why.sources"]);
            Assert.Contains("0.20.0", d["history.text"]);
        }
    }
}
