using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>One labelled situation: the headlines the program has seen at "now", and whether a REAL urgent story is among them.</summary>
public sealed record BreakingCase(string Name, string Language, bool ExpectUrgent, NewsItem[] Items, HazardEvent[]? Hazards = null);

/// <summary>
/// The labelled corpus of the urgent-news detector: real-style urgent stories (several independent publishers, official warnings, disasters with data) and false alarms
/// (one source with an urgent word, opinion, sports / showbiz hyperbole, old events, announcements, clickbait, stale stories) in Greek, English and the other languages
/// of the program. The tests compute precision and recall over it and fail when they drop; CI prints them.
/// </summary>
public static class BreakingCorpus
{
    public static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static NewsItem I(string title, string host, double minutesAgo) =>
        new(title, $"https://{host}/a/{Math.Abs(title.GetHashCode())}", host, Now.AddMinutes(-minutesAgo), null, null, NewsItemKind.News);

    private static HazardEvent Quake(string place, double lat, double lon, double mag, double minutesAgo, int level = 2) =>
        new("usgs:" + place, HazardKind.Earthquake, place, lat, lon, Now.AddMinutes(-minutesAgo), "https://earthquake.usgs.gov/x", mag, mag, mag, level, new[] { "USGS" }, 10);

    public static IReadOnlyList<BreakingCase> All { get; } = Build();

    private static List<BreakingCase> Build()
    {
        var c = new List<BreakingCase>();
        void Yes(string name, string lang, NewsItem[] items, HazardEvent[]? hz = null) => c.Add(new(name, lang, true, items, hz));
        void No(string name, string lang, NewsItem[] items, HazardEvent[]? hz = null) => c.Add(new(name, lang, false, items, hz));

        // ---------------- real urgent stories ----------------
        Yes("el quake with data and three publishers", "el", new[]
        {
            I("Ισχυρός σεισμός 6,1 Ρίχτερ στην Κρήτη - Έκτακτο", "ertnews.gr", 3), I("Σεισμός 6,1 Ρίχτερ ταρακούνησε την Κρήτη", "protothema.gr", 4), I("Μεγάλος σεισμός στην Κρήτη, αισθητός σε όλη τη χώρα", "skai.gr", 5),
        }, new[] { Quake("Crete, Greece", 35.2, 25.0, 6.1, 6) });
        Yes("el quake data only, one agency source", "el", new[] { I("Σεισμός 6,4 Ρίχτερ νότια της Ρόδου", "ana.gr", 2) }, new[] { Quake("Rhodes, Greece", 36.1, 28.2, 6.4, 3) });
        Yes("el fire in a factory, three publishers", "el", new[]
        {
            I("Έκτακτο: Μεγάλη φωτιά σε εργοστάσιο στον Ασπρόπυργο", "ertnews.gr", 2), I("Φωτιά σε εργοστάσιο στον Ασπρόπυργο - μαύρος καπνός πάνω από την Αττική", "skai.gr", 4), I("Ασπρόπυργος: μεγάλη φωτιά σε εργοστάσιο, στο σημείο η Πυροσβεστική", "in.gr", 5),
        });
        Yes("el official flood warning with a major publisher", "el", new[]
        {
            I("Έκτακτη προειδοποίηση για πλημμύρες στην Αττική από την Πολιτική Προστασία", "civilprotection.gov.gr", 3), I("Πολιτική Προστασία: προειδοποίηση για πλημμύρες στην Αττική τις επόμενες ώρες", "ertnews.gr", 5),
        });
        Yes("el explosion, four publishers in a burst", "el", new[]
        {
            I("Ισχυρή έκρηξη στο κέντρο της Θεσσαλονίκης", "ertnews.gr", 2), I("Έκρηξη στη Θεσσαλονίκη: πληροφορίες για τραυματίες", "skai.gr", 3), I("Θεσσαλονίκη: ισχυρή έκρηξη στο κέντρο, κλειστοί δρόμοι", "protothema.gr", 4), I("Έκρηξη στο κέντρο της Θεσσαλονίκης - τραυματίες", "in.gr", 4),
        });
        Yes("en explosion, three publishers incl. agency", "en", new[]
        {
            I("Explosion at port in Beirut, many injured", "reuters.com", 3), I("Huge explosion rocks Beirut port, dozens injured", "bbc.co.uk", 4), I("Beirut port explosion: scores injured, officials say", "aljazeera.com", 5),
        });
        Yes("en BREAKING plane crash, three publishers", "en", new[]
        {
            I("BREAKING: Passenger plane crashes near Lisbon airport", "cnn.com", 2), I("Passenger plane crashes on approach to Lisbon, emergency services at scene", "reuters.com", 3), I("Plane crash near Lisbon airport, casualties feared", "theguardian.com", 5),
        });
        Yes("en earthquake data, two publishers", "en", new[]
        {
            I("Magnitude 7.2 earthquake strikes off the coast of Japan", "reuters.com", 4), I("Strong earthquake off Japan triggers tsunami warning", "apnews.com", 6),
        }, new[] { Quake("Honshu, Japan", 38.0, 142.0, 7.2, 5, 3) });
        Yes("en wildfire, official source plus two outlets", "en", new[]
        {
            I("Wildfire forces evacuation of 10,000 near Athens", "reuters.com", 5), I("Athens wildfire: thousands evacuated as flames reach suburbs", "bbc.com", 7), I("Greek wildfire near Athens forces mass evacuation", "ekathimerini.com", 9),
        });
        Yes("en central bank shock, three publishers", "en", new[]
        {
            I("Central bank unexpectedly raises interest rates by 1 point", "reuters.com", 3), I("ECB unexpectedly raises interest rates by a full point", "apnews.com", 5), I("Surprise: central bank raises interest rates by 1 point", "euronews.com", 8),
        });
        Yes("de Eilmeldung, three publishers", "de", new[]
        {
            I("Eilmeldung: Schwere Explosion in Chemiefabrik bei Ludwigshafen", "dw.com", 3), I("Explosion in Chemiefabrik Ludwigshafen: mehrere Verletzte", "spiegel.de", 4), I("Ludwigshafen: schwere Explosion in Chemiefabrik, Großeinsatz der Feuerwehr", "zeit.de", 6),
        });
        Yes("fr dernière minute, three publishers", "fr", new[]
        {
            I("Dernière minute : une forte explosion secoue le centre de Lyon", "lemonde.fr", 3), I("Explosion à Lyon : plusieurs blessés dans le centre-ville", "francetvinfo.fr", 4), I("Forte explosion dans le centre de Lyon, secours sur place", "lefigaro.fr", 6),
        });
        Yes("es urgente, three publishers", "es", new[]
        {
            I("Última hora: fuerte terremoto sacude el centro de Chile", "elpais.com", 3), I("Un fuerte terremoto sacude el centro de Chile, hay alerta de tsunami", "reuters.com", 4), I("Terremoto en Chile: alerta de tsunami en la costa central", "elmundo.es", 6),
        }, new[] { Quake("Valparaiso, Chile", -33.0, -71.6, 7.0, 4, 3) });
        Yes("it ultim'ora, three publishers", "it", new[]
        {
            I("Ultim'ora: forte scossa di terremoto nel centro Italia", "corriere.it", 3), I("Terremoto nel centro Italia, forte scossa avvertita a Roma", "repubblica.it", 4), I("Forte terremoto in centro Italia: scossa avvertita fino a Roma", "ansa.it", 5),
        });
        Yes("pt urgente, three publishers", "pt", new[]
        {
            I("Urgente: incêndio de grandes dimensões atinge o porto de Santos", "folha.uol.com.br", 3), I("Incêndio de grandes dimensões no porto de Santos, fumaça visível de longe", "g1.globo.com", 4), I("Porto de Santos: incêndio de grandes dimensões, bombeiros no local", "estadao.com.br", 6),
        });
        Yes("ru срочно, three publishers", "ru", new[]
        {
            I("Срочно: мощный взрыв прогремел в центре Стамбула", "tass.ru", 3), I("Мощный взрыв в центре Стамбула, есть пострадавшие", "rbc.ru", 4), I("В центре Стамбула прогремел мощный взрыв, пострадавшие", "kommersant.ru", 5),
        });
        Yes("tr son dakika, three publishers", "tr", new[]
        {
            I("Son dakika: İstanbul'da şiddetli deprem, 6,2 büyüklüğünde", "hurriyet.com.tr", 3), I("İstanbul'da 6,2 büyüklüğünde şiddetli deprem oldu", "sabah.com.tr", 4), I("Şiddetli deprem İstanbul'u salladı, 6,2 büyüklüğünde", "ntv.com.tr", 5),
        }, new[] { Quake("Istanbul, Turkey", 41.0, 28.9, 6.2, 4) });
        Yes("ja 速報, three publishers", "ja", new[]
        {
            I("速報：東京で震度6強の地震、津波の心配なし", "nhk.or.jp", 3), I("東京で震度6強の地震が発生、津波の心配なし", "asahi.com", 4), I("震度6強の地震、東京で強い揺れ 津波の心配なし", "yomiuri.co.jp", 5),
        });
        Yes("ko 속보, three publishers", "ko", new[]
        {
            I("속보: 서울 인근 규모 5.8 지진 발생", "yna.co.kr", 3), I("서울 인근에서 규모 5.8 지진 발생, 건물 흔들려", "chosun.com", 4), I("규모 5.8 지진 서울 인근 발생 ... 시민 대피", "joongang.co.kr", 6),
        });
        Yes("zh 突发, three publishers", "zh", new[]
        {
            I("突发：四川发生强烈地震，震级6.8级", "xinhuanet.com", 3), I("四川发生6.8级强烈地震，多地震感明显", "people.com.cn", 4), I("四川强烈地震6.8级，救援队已出动", "cctv.com", 5),
        });
        Yes("ar عاجل, three publishers", "ar", new[]
        {
            I("عاجل: انفجار ضخم في وسط بيروت", "aljazeera.com", 3), I("انفجار ضخم يهز وسط بيروت ووقوع إصابات", "alarabiya.net", 4), I("انفجار ضخم في وسط العاصمة بيروت وسيارات الإسعاف في الموقع", "skynewsarabia.com", 6),
        });
        Yes("hi ब्रेकिंग, three publishers", "hi", new[]
        {
            I("ब्रेकिंग: दिल्ली में तेज भूकंप के झटके, रिक्टर पैमाने पर 6.0", "ndtv.com", 3), I("दिल्ली में तेज भूकंप के झटके महसूस किए गए, 6.0 तीव्रता", "aajtak.in", 4), I("तेज भूकंप से हिली दिल्ली, 6.0 तीव्रता दर्ज", "jagran.com", 5),
        });
        Yes("el tsunami warning with data", "el", new[] { I("Προειδοποίηση για τσουνάμι στην Ανατολική Μεσόγειο μετά από σεισμό 7,1", "gdacs.org", 2), I("Τσουνάμι: προειδοποίηση μετά από σεισμό 7,1 Ρίχτερ", "ertnews.gr", 4) },
            new[] { Quake("Eastern Mediterranean", 34.5, 26.0, 7.1, 4, 3) });
        Yes("el fast burst of two plus agency", "el", new[] { I("Έκτακτο: Συναγερμός στο αεροδρόμιο της Αθήνας - εκκένωση τερματικού", "ana.gr", 2), I("Συναγερμός στο «Ελ. Βενιζέλος»: εκκενώνεται το τερματικό", "ertnews.gr", 3), I("Εκκένωση τερματικού στο αεροδρόμιο της Αθήνας", "skai.gr", 5) });
        Yes("en earthquake, single agency with data M6.5", "en", new[] { I("Magnitude 6.5 earthquake hits southern Iran", "reuters.com", 3) }, new[] { Quake("Southern Iran", 27.0, 56.0, 6.5, 4) });
        Yes("en dam failure, four publishers", "en", new[]
        {
            I("Dam collapses in northern Brazil, towns evacuated", "reuters.com", 2), I("Brazil dam collapse: evacuation ordered for nearby towns", "apnews.com", 3), I("Northern Brazil dam collapses, thousands flee", "bbc.com", 4), I("Dam collapse in Brazil forces evacuations", "cnn.com", 6),
        });
        Yes("el sharp stock-market halt, two big outlets", "el", new[] { I("Έκτακτο: Αναστολή διαπραγμάτευσης στο Χρηματιστήριο Αθηνών μετά από απότομη πτώση", "ertnews.gr", 3), I("Αναστολή διαπραγμάτευσης στο Χρηματιστήριο Αθηνών λόγω απότομης πτώσης", "naftemporiki.gr", 5) });

        // ---------------- false alarms ----------------
        No("el keyword only, one source", "el", new[] { I("Έκτακτο: Νέα προσφορά σε κινητά τηλέφωνα μόνο σήμερα", "example-shop.gr", 2) });
        No("en BREAKING one blog source", "en", new[] { I("BREAKING: You can now customise your phone's home screen", "techblog.example.com", 2) });
        No("el opinion with urgent word", "el", new[] { I("Έκτακτο: Η ανάλυση για το τι πραγματικά σημαίνει η νέα κυβερνητική πολιτική", "protothema.gr", 3), I("Ανάλυση: τι σημαίνει η νέα κυβερνητική πολιτική", "in.gr", 4) });
        No("en opinion piece with three publishers", "en", new[] { I("Opinion: why the central bank got it wrong", "theguardian.com", 5), I("Opinion: the central bank got it wrong, again", "nytimes.com", 6), I("Analysis: where the central bank went wrong", "reuters.com", 7) });
        No("el football shock", "el", new[] { I("Σοκ στον Ολυμπιακό: μεταγραφική βόμβα με προπονητή από την Ισπανία", "sport24.gr", 3), I("Βόμβα στο ποδόσφαιρο: ο Ολυμπιακός κλείνει προπονητή", "gazzetta.gr", 4), I("Ολυμπιακός: σοκ με τον νέο προπονητή", "sdna.gr", 5) });
        No("en football breaking transfer", "en", new[] { I("BREAKING: Premier League striker agrees transfer to rivals", "sportsite.example.com", 2), I("Transfer shock: striker set to join rivals", "football-news.example.org", 4), I("Premier League transfer bombshell", "dailygoals.example.net", 5) });
        No("el showbiz shock", "el", new[] { I("Σοκ στο showbiz: διάσημη τραγουδίστρια ανακοίνωσε χωρισμό", "glossy.example.gr", 3), I("Βόμβα στο ριάλιτι: αποχώρηση ηθοποιού", "tv-stories.example.gr", 5) });
        No("en celebrity breaking", "en", new[] { I("BREAKING: Reality star shares new photo from holiday", "gossip.example.com", 2), I("Celebrity couple shock: singer and actress split", "stars.example.org", 4) });
        No("el anniversary of a past quake", "el", new[] { I("Έκτακτο: 20 χρόνια από τον μεγάλο σεισμό - οι μνήμες ζωντανές", "protothema.gr", 5), I("Επέτειος σεισμού: πριν από 20 χρόνια η Αθήνα ταράχτηκε", "in.gr", 6) });
        No("en on this day", "en", new[] { I("On this day: the 1999 earthquake that shook the region, 25 years ago", "bbc.com", 4), I("Remembering the 1999 earthquake: 25 years ago today", "theguardian.com", 6) });
        No("el schedule announcement", "el", new[] { I("Αναμένεται να ανακοινωθεί το πρόγραμμα των εκδηλώσεων του Δήμου την επόμενη εβδομάδα", "ertnews.gr", 3), I("Το πρόγραμμα των εκδηλώσεων θα ανακοινωθεί την επόμενη εβδομάδα", "skai.gr", 5) });
        No("en preview of an upcoming event", "en", new[] { I("What to expect: the summit is scheduled for next month", "reuters.com", 3), I("Summit preview: leaders are expected to meet next month", "bbc.com", 6) });
        No("el clickbait", "el", new[] { I("Δεν θα πιστέψετε τι ειπώθηκε στο εργοστάσιο - έκτακτο", "clickbait.example.gr", 2), I("Δεν θα πιστέψετε τι έγινε στο εργοστάσιο", "viral.example.gr", 3) });
        No("en clickbait", "en", new[] { I("You won't believe what happened next at the factory", "viral.example.com", 2), I("What happened next at the factory will blow your mind", "clicks.example.org", 3) });
        No("el stale story many publishers, six hours old", "el", new[] { I("Μεγάλη φωτιά σε εργοστάσιο στον Ασπρόπυργο", "ertnews.gr", 360), I("Φωτιά σε εργοστάσιο στον Ασπρόπυργο", "skai.gr", 361), I("Ασπρόπυργος: φωτιά σε εργοστάσιο", "in.gr", 362) });
        No("en old news, one day old", "en", new[] { I("Explosion at port injures dozens", "reuters.com", 1500), I("Port explosion injures dozens", "bbc.com", 1501) });
        No("el question headline", "el", new[] { I("Έκτακτο: Θα γίνει νέο lockdown;", "protothema.gr", 3) });
        No("en question headline", "en", new[] { I("Is a major earthquake about to hit the region?", "dailymail.example.co.uk", 4) });
        No("de Kommentar", "de", new[] { I("Eilmeldung: Kommentar zur Haushaltsdebatte", "zeit.de", 3), I("Kommentar: Die Haushaltsdebatte und ihre Folgen", "faz.net", 5) });
        No("fr tribune", "fr", new[] { I("Dernière minute : tribune sur la réforme des retraites", "lemonde.fr", 3), I("Tribune : pourquoi la réforme des retraites divise", "lefigaro.fr", 5) });
        No("es entrevista", "es", new[] { I("Última hora: entrevista con el ministro sobre los presupuestos", "elpais.com", 3), I("Entrevista: el ministro explica los presupuestos", "elmundo.es", 5) });
        No("it intervista", "it", new[] { I("Ultim'ora: intervista al presidente sul bilancio", "corriere.it", 3), I("Intervista: il presidente spiega il bilancio", "repubblica.it", 5) });
        No("pt entrevista", "pt", new[] { I("Urgente: entrevista com o presidente sobre o orçamento", "folha.uol.com.br", 3), I("Entrevista: o presidente explica o orçamento", "g1.globo.com", 5) });
        No("ru интервью", "ru", new[] { I("Срочно: интервью с министром о бюджете", "rbc.ru", 3), I("Интервью: министр объясняет бюджет", "kommersant.ru", 5) });
        No("tr röportaj", "tr", new[] { I("Son dakika: Bakanla röportaj, bütçe konuşuldu", "hurriyet.com.tr", 3), I("Röportaj: bakan bütçeyi anlattı", "sabah.com.tr", 5) });
        No("ja 分析", "ja", new[] { I("速報：予算案について専門家が分析", "nhk.or.jp", 3) });
        No("ko 인터뷰", "ko", new[] { I("속보: 장관 인터뷰, 예산안 설명", "yna.co.kr", 3) });
        No("zh 评论", "zh", new[] { I("突发：评论员谈新预算", "xinhuanet.com", 3) });
        No("ar مقال رأي", "ar", new[] { I("عاجل: مقال رأي حول الميزانية الجديدة", "aljazeera.com", 3) });
        No("hi विश्लेषण", "hi", new[] { I("ब्रेकिंग: बजट पर विश्लेषण", "ndtv.com", 3) });
        No("el two unrelated stories with a keyword each", "el", new[] { I("Έκτακτο: Νέο μενού στην εστίαση", "foodblog.example.gr", 2), I("Έκτακτο: Νέα συλλογή ρούχων", "fashion.example.gr", 3) });
        No("en weak earthquake with data and one blog", "en", new[] { I("Small earthquake felt in the region", "localblog.example.com", 3) }, new[] { Quake("Region", 38.0, 23.0, 3.1, 4, 1) });
        No("el repost from an hour and a half ago, one publisher", "el", new[] { I("Έκτακτο: συναγερμός για πυρκαγιά στον Υμηττό", "protothema.gr", 95) });
        No("el sports with earthquake word as metaphor", "el", new[] { I("Σεισμός στο ποδόσφαιρο: ο προπονητής αποχωρεί από την ομάδα", "sport24.gr", 3), I("Σεισμός στην ΑΕΚ: αποχώρησε ο προπονητής", "gazzetta.gr", 4) });
        No("en market chatter, one source", "en", new[] { I("Alert: shares might move lower this week, analysts say", "marketblog.example.com", 3) });
        No("el product launch with BREAKING", "el", new[] { I("BREAKING: Παρουσιάστηκε το νέο smartphone με κάμερα 200MP", "techgr.example.gr", 2), I("Το νέο smartphone με κάμερα 200MP παρουσιάστηκε", "gadget.example.gr", 4) });
        No("en top 10 list", "en", new[] { I("Top 10 breaking-news apps you need today", "listicles.example.com", 2), I("10 things to know about the new phone", "listicles.example.org", 3) });
        No("el live blog of a match", "el", new[] { I("LIVE: Ολυμπιακός - Παναθηναϊκός, ο μεγάλος ποδοσφαιρικός αγώνας", "sport24.gr", 5), I("Ολυμπιακός - Παναθηναϊκός: δείτε LIVE το ματς", "gazzetta.gr", 6), I("Ματς Ολυμπιακός - Παναθηναϊκός live", "sdna.gr", 6) });

        return c;
    }
}
