namespace OpsisNewsCrawler.Core;

public sealed record WorldFeed(string ContinentKey, string CountryCode, string CountryEn, string CountryEl, string Name, string Url);

/// <summary>
/// News feeds by continent and country (national public broadcasters and major newspapers that publish an open RSS / Atom feed for readers). Every address was fetched on 2026-11-11:
/// HTTP 200 and at least three items; those that failed (403, 404, time-out, rate limit) are not in the list. Only the feeds a publisher offers openly are listed, and a feed is only fetched
/// when the user adds it (the program never scrapes pages: see docs/DATA-SOURCES.md). The user can check any of them with the "Test" button of the news page.
/// </summary>
public static class WorldFeeds
{
    // continent|country code|country (English)|country (Greek)|name|address
    private const string Data = """
europe|GB|United Kingdom|Ηνωμένο Βασίλειο|BBC News|https://feeds.bbci.co.uk/news/rss.xml
europe|GB|United Kingdom|Ηνωμένο Βασίλειο|The Guardian|https://www.theguardian.com/uk/rss
europe|DE|Germany|Γερμανία|Tagesschau|https://www.tagesschau.de/index~rss2.xml
europe|DE|Germany|Γερμανία|Deutsche Welle|https://rss.dw.com/xml/rss-en-all
europe|FR|France|Γαλλία|France 24|https://www.france24.com/en/rss
europe|FR|France|Γαλλία|Le Monde|https://www.lemonde.fr/rss/une.xml
europe|ES|Spain|Ισπανία|El País|https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/portada
europe|IT|Italy|Ιταλία|ANSA|https://www.ansa.it/sito/ansait_rss.xml
europe|NL|Netherlands|Ολλανδία|NOS|https://feeds.nos.nl/nosnieuwsalgemeen
europe|NO|Norway|Νορβηγία|NRK|https://www.nrk.no/toppsaker.rss
europe|FI|Finland|Φινλανδία|Yle News|https://yle.fi/rss/uutiset/paauutiset
europe|DK|Denmark|Δανία|DR Nyheder|https://www.dr.dk/nyheder/service/feeds/allenyheder
europe|IE|Ireland|Ιρλανδία|RTÉ News|https://www.rte.ie/feeds/rss/?index=/news/
europe|AT|Austria|Αυστρία|ORF News|https://rss.orf.at/news.xml
europe|BE|Belgium|Βέλγιο|VRT NWS|https://www.vrt.be/vrtnws/en.rss.articles.xml
europe|PL|Poland|Πολωνία|Notes from Poland|https://notesfrompoland.com/feed/
europe|PT|Portugal|Πορτογαλία|RTP Notícias|https://www.rtp.pt/noticias/rss
europe|TR|Turkey|Τουρκία|Daily Sabah|https://www.dailysabah.com/rssFeed/home-page
europe|UA|Ukraine|Ουκρανία|Ukrinform|https://www.ukrinform.net/rss/block-lastnews
europe|HU|Hungary|Ουγγαρία|Hungary Today|https://hungarytoday.hu/feed/
europe|RO|Romania|Ρουμανία|Romania Insider|https://www.romania-insider.com/feed
europe|XX|Europe|Ευρώπη (γενικά)|Euronews|https://www.euronews.com/rss
asia|JP|Japan|Ιαπωνία|NHK World|https://www3.nhk.or.jp/rss/news/cat0.xml
asia|JP|Japan|Ιαπωνία|The Japan Times|https://www.japantimes.co.jp/feed/
asia|IN|India|Ινδία|The Hindu|https://www.thehindu.com/news/national/feeder/default.rss
asia|SG|Singapore|Σιγκαπούρη|Channel NewsAsia|https://www.channelnewsasia.com/rssfeeds/8395986
asia|IL|Israel|Ισραήλ|The Times of Israel|https://www.timesofisrael.com/feed/
asia|QA|Qatar|Κατάρ|Al Jazeera|https://www.aljazeera.com/xml/rss/all.xml
asia|TH|Thailand|Ταϊλάνδη|Bangkok Post|https://www.bangkokpost.com/rss/data/topstories.xml
asia|HK|Hong Kong|Χονγκ Κονγκ|South China Morning Post|https://www.scmp.com/rss/91/feed
asia|PK|Pakistan|Πακιστάν|Dawn|https://www.dawn.com/feeds/home
africa|NG|Nigeria|Νιγηρία|Punch|https://punchng.com/feed/
africa|KE|Kenya|Κένυα|Capital FM Kenya|https://www.capitalfm.co.ke/news/feed/
africa|XX|Africa|Αφρική (γενικά)|Africanews|https://www.africanews.com/feed/rss
north-america|US|United States|ΗΠΑ|NPR News|https://feeds.npr.org/1001/rss.xml
north-america|US|United States|ΗΠΑ|The New York Times|https://rss.nytimes.com/services/xml/rss/nyt/HomePage.xml
north-america|MX|Mexico|Μεξικό|Mexico News Daily|https://mexiconewsdaily.com/feed/
south-america|BR|Brazil|Βραζιλία|G1|https://g1.globo.com/rss/g1/
south-america|AR|Argentina|Αργεντινή|Buenos Aires Times|https://www.batimes.com.ar/feed
south-america|XX|South America|Νότια Αμερική (γενικά)|MercoPress|https://en.mercopress.com/rss
oceania|AU|Australia|Αυστραλία|ABC News Australia|https://www.abc.net.au/news/feed/51120/rss.xml
oceania|NZ|New Zealand|Νέα Ζηλανδία|RNZ|https://www.rnz.co.nz/rss/national.xml
oceania|NZ|New Zealand|Νέα Ζηλανδία|NZ Herald|https://www.nzherald.co.nz/arc/outboundfeeds/rss/section/nz/?outputType=xml
""";

    private static readonly Lazy<IReadOnlyList<WorldFeed>> Parsed = new(() => Data.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)
        .Select(l => l.Split('|')).Where(p => p.Length == 6).Select(p => new WorldFeed(p[0], p[1], p[2], p[3], p[4], p[5])).ToList());

    public static IReadOnlyList<WorldFeed> All => Parsed.Value;

    /// <summary>The countries of a continent that have a feed, by their name in the language of the program.</summary>
    public static IReadOnlyList<(string Code, string Name)> Countries(string continentKey) =>
        All.Where(f => f.ContinentKey == continentKey).GroupBy(f => f.CountryCode)
            .Select(g => (g.Key, Loc.Pick(g.First().CountryEl, g.First().CountryEn))).OrderBy(c => c.Item2, StringComparer.CurrentCultureIgnoreCase).ToList();

    public static IReadOnlyList<WorldFeed> Of(string continentKey, string countryCode) =>
        All.Where(f => f.ContinentKey == continentKey && f.CountryCode == countryCode).ToList();
}
