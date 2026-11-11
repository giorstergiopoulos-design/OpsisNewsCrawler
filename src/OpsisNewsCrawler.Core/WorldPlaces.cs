using System.Globalization;

namespace OpsisNewsCrawler.Core;

public sealed record WorldCountry(string Code, string NameEl, string NameEn, IReadOnlyList<GreekCity> Cities)
{
    public string DisplayName => Loc.Pick(NameEl, NameEn);
}

public sealed record WorldContinent(string Key, string NameEl, string NameEn, IReadOnlyList<WorldCountry> Countries)
{
    public string DisplayName => Loc.Pick(NameEl, NameEn);
}

/// <summary>
/// A built-in list for the weather flow: continent, then country, then its main cities (the capital, the largest cities and a few well-known ones), Europe first. The places are
/// added to the weather list like any place found by name (<see cref="GeoSearch.IdFor"/>), the forecast comes from the coordinates. Coordinates are city centres to two or three
/// decimals (about 100 m to 1 km; a forecast does not need more). The names of the cities of other continents are English in both languages unless noted. Greece has its own list.
/// </summary>
public static class WorldPlaces
{
    // "#key|Greek name|English name" starts a continent, "@code|English name|Greek name" a country, then "English city|Greek city|latitude|longitude".
    private const string Data = """
#europe|Ευρώπη|Europe
@AL|Albania|Αλβανία
Tirana|Τίρανα|41.3275|19.8187
Durrës|Δυρράχιο|41.3246|19.4565
@AD|Andorra|Ανδόρρα
Andorra la Vella|Ανδόρρα λα Βέλα|42.5063|1.5218
@AT|Austria|Αυστρία
Vienna|Βιέννη|48.2082|16.3738
Salzburg|Σάλτσμπουργκ|47.8095|13.0550
Graz|Γκρατς|47.0707|15.4395
Innsbruck|Ίνσμπρουκ|47.2692|11.4041
@BY|Belarus|Λευκορωσία
Minsk|Μινσκ|53.9006|27.5590
@BE|Belgium|Βέλγιο
Brussels|Βρυξέλλες|50.8503|4.3517
Antwerp|Αμβέρσα|51.2194|4.4025
Ghent|Γάνδη|51.0543|3.7174
@BA|Bosnia and Herzegovina|Βοσνία-Ερζεγοβίνη
Sarajevo|Σαράγεβο|43.8563|18.4131
@BG|Bulgaria|Βουλγαρία
Sofia|Σόφια|42.6977|23.3219
Plovdiv|Φιλιππούπολη|42.1354|24.7453
Varna|Βάρνα|43.2141|27.9147
@HR|Croatia|Κροατία
Zagreb|Ζάγκρεμπ|45.8150|15.9819
Split|Σπλιτ|43.5081|16.4402
Dubrovnik|Ντουμπρόβνικ|42.6507|18.0944
@CY|Cyprus|Κύπρος
Nicosia|Λευκωσία|35.1856|33.3823
Limassol|Λεμεσός|34.7071|33.0226
Larnaca|Λάρνακα|34.9229|33.6233
Paphos|Πάφος|34.7754|32.4218
@CZ|Czechia|Τσεχία
Prague|Πράγα|50.0755|14.4378
Brno|Μπρνο|49.1951|16.6068
@DK|Denmark|Δανία
Copenhagen|Κοπεγχάγη|55.6761|12.5683
Aarhus|Ώρχους|56.1629|10.2039
@EE|Estonia|Εσθονία
Tallinn|Ταλίν|59.4370|24.7536
@FI|Finland|Φινλανδία
Helsinki|Ελσίνκι|60.1699|24.9384
Tampere|Τάμπερε|61.4978|23.7610
@FR|France|Γαλλία
Paris|Παρίσι|48.8566|2.3522
Marseille|Μασσαλία|43.2965|5.3698
Lyon|Λυών|45.7640|4.8357
Nice|Νίκαια|43.7102|7.2620
Toulouse|Τουλούζη|43.6047|1.4442
Strasbourg|Στρασβούργο|48.5734|7.7521
@DE|Germany|Γερμανία
Berlin|Βερολίνο|52.5200|13.4050
Munich|Μόναχο|48.1351|11.5820
Hamburg|Αμβούργο|53.5511|9.9937
Frankfurt|Φρανκφούρτη|50.1109|8.6821
Cologne|Κολωνία|50.9375|6.9603
Stuttgart|Στουτγάρδη|48.7758|9.1829
Düsseldorf|Ντίσελντορφ|51.2277|6.7735
@HU|Hungary|Ουγγαρία
Budapest|Βουδαπέστη|47.4979|19.0402
Debrecen|Ντέμπρετσεν|47.5316|21.6273
@IS|Iceland|Ισλανδία
Reykjavik|Ρέικιαβικ|64.1466|-21.9426
@IE|Ireland|Ιρλανδία
Dublin|Δουβλίνο|53.3498|-6.2603
Cork|Κορκ|51.8985|-8.4756
@IT|Italy|Ιταλία
Rome|Ρώμη|41.9028|12.4964
Milan|Μιλάνο|45.4642|9.1900
Naples|Νάπολη|40.8518|14.2681
Turin|Τορίνο|45.0703|7.6869
Florence|Φλωρεντία|43.7696|11.2558
Venice|Βενετία|45.4408|12.3155
Palermo|Πάλερμο|38.1157|13.3615
Bologna|Μπολόνια|44.4949|11.3426
@XK|Kosovo|Κόσοβο
Pristina|Πρίστινα|42.6629|21.1655
@LV|Latvia|Λετονία
Riga|Ρίγα|56.9496|24.1052
@LI|Liechtenstein|Λιχτενστάιν
Vaduz|Βαντούζ|47.1410|9.5209
@LT|Lithuania|Λιθουανία
Vilnius|Βίλνιους|54.6872|25.2797
Kaunas|Κάουνας|54.8985|23.9036
@LU|Luxembourg|Λουξεμβούργο
Luxembourg|Λουξεμβούργο|49.6116|6.1319
@MT|Malta|Μάλτα
Valletta|Βαλέτα|35.8989|14.5146
@MD|Moldova|Μολδαβία
Chișinău|Κισινάου|47.0105|28.8638
@MC|Monaco|Μονακό
Monaco|Μονακό|43.7384|7.4246
@ME|Montenegro|Μαυροβούνιο
Podgorica|Ποντγκορίτσα|42.4304|19.2594
@NL|Netherlands|Ολλανδία
Amsterdam|Άμστερνταμ|52.3676|4.9041
Rotterdam|Ρότερνταμ|51.9244|4.4777
The Hague|Χάγη|52.0705|4.3007
Utrecht|Ουτρέχτη|52.0907|5.1214
@MK|North Macedonia|Βόρεια Μακεδονία
Skopje|Σκόπια|41.9981|21.4254
@NO|Norway|Νορβηγία
Oslo|Όσλο|59.9139|10.7522
Bergen|Μπέργκεν|60.3913|5.3221
@PL|Poland|Πολωνία
Warsaw|Βαρσοβία|52.2297|21.0122
Kraków|Κρακοβία|50.0647|19.9450
Gdańsk|Γκντανσκ|54.3520|18.6466
Wrocław|Βρότσλαβ|51.1079|17.0385
@PT|Portugal|Πορτογαλία
Lisbon|Λισαβόνα|38.7223|-9.1393
Porto|Πόρτο|41.1579|-8.6291
@RO|Romania|Ρουμανία
Bucharest|Βουκουρέστι|44.4268|26.1025
Cluj-Napoca|Κλουζ-Ναπόκα|46.7712|23.6236
Timișoara|Τιμισοάρα|45.7489|21.2087
@RU|Russia|Ρωσία
Moscow|Μόσχα|55.7558|37.6173
Saint Petersburg|Αγία Πετρούπολη|59.9343|30.3351
@SM|San Marino|Άγιος Μαρίνος
San Marino|Άγιος Μαρίνος|43.9424|12.4578
@RS|Serbia|Σερβία
Belgrade|Βελιγράδι|44.7866|20.4489
Novi Sad|Νόβι Σαντ|45.2671|19.8335
@SK|Slovakia|Σλοβακία
Bratislava|Μπρατισλάβα|48.1486|17.1077
Košice|Κόσιτσε|48.7164|21.2611
@SI|Slovenia|Σλοβενία
Ljubljana|Λιουμπλιάνα|46.0569|14.5058
@ES|Spain|Ισπανία
Madrid|Μαδρίτη|40.4168|-3.7038
Barcelona|Βαρκελώνη|41.3874|2.1686
Valencia|Βαλένθια|39.4699|-0.3763
Seville|Σεβίλλη|37.3891|-5.9845
Bilbao|Μπιλμπάο|43.2630|-2.9350
Málaga|Μάλαγα|36.7213|-4.4214
@SE|Sweden|Σουηδία
Stockholm|Στοκχόλμη|59.3293|18.0686
Gothenburg|Γκέτεμποργκ|57.7089|11.9746
Malmö|Μάλμε|55.6050|13.0038
@CH|Switzerland|Ελβετία
Bern|Βέρνη|46.9480|7.4474
Zurich|Ζυρίχη|47.3769|8.5417
Geneva|Γενεύη|46.2044|6.1432
Basel|Βασιλεία|47.5596|7.5886
@TR|Turkey|Τουρκία
Istanbul|Κωνσταντινούπολη|41.0082|28.9784
Ankara|Άγκυρα|39.9334|32.8597
Izmir|Σμύρνη|38.4237|27.1428
@UA|Ukraine|Ουκρανία
Kyiv|Κίεβο|50.4501|30.5234
Lviv|Λβιβ|49.8397|24.0297
Odesa|Οδησσός|46.4825|30.7233
@GB|United Kingdom|Ηνωμένο Βασίλειο
London|Λονδίνο|51.5074|-0.1278
Manchester|Μάντσεστερ|53.4808|-2.2426
Birmingham|Μπέρμιγχαμ|52.4862|-1.8904
Edinburgh|Εδιμβούργο|55.9533|-3.1883
Glasgow|Γλασκώβη|55.8642|-4.2518
Cardiff|Κάρντιφ|51.4816|-3.1791
Belfast|Μπέλφαστ|54.5973|-5.9301
#asia|Ασία|Asia
@JP|Japan|Ιαπωνία
Tokyo|Tokyo|35.6762|139.6503
Osaka|Osaka|34.6937|135.5023
Kyoto|Kyoto|35.0116|135.7681
@CN|China|Κίνα
Beijing|Beijing|39.9042|116.4074
Shanghai|Shanghai|31.2304|121.4737
Hong Kong|Hong Kong|22.3193|114.1694
Shenzhen|Shenzhen|22.5431|114.0579
@IN|India|Ινδία
New Delhi|New Delhi|28.6139|77.2090
Mumbai|Mumbai|19.0760|72.8777
Bengaluru|Bengaluru|12.9716|77.5946
Kolkata|Kolkata|22.5726|88.3639
Chennai|Chennai|13.0827|80.2707
@KR|South Korea|Νότια Κορέα
Seoul|Seoul|37.5665|126.9780
Busan|Busan|35.1796|129.0756
@TH|Thailand|Ταϊλάνδη
Bangkok|Bangkok|13.7563|100.5018
Phuket|Phuket|7.8804|98.3923
@SG|Singapore|Σιγκαπούρη
Singapore|Singapore|1.3521|103.8198
@ID|Indonesia|Ινδονησία
Jakarta|Jakarta|-6.2088|106.8456
Denpasar (Bali)|Denpasar (Bali)|-8.6705|115.2126
@IL|Israel|Ισραήλ
Jerusalem|Jerusalem|31.7683|35.2137
Tel Aviv|Tel Aviv|32.0853|34.7818
@AE|United Arab Emirates|Ηνωμένα Αραβικά Εμιράτα
Dubai|Dubai|25.2048|55.2708
Abu Dhabi|Abu Dhabi|24.4539|54.3773
@SA|Saudi Arabia|Σαουδική Αραβία
Riyadh|Riyadh|24.7136|46.6753
Jeddah|Jeddah|21.4858|39.1925
@IR|Iran|Ιράν
Tehran|Tehran|35.6892|51.3890
@PK|Pakistan|Πακιστάν
Islamabad|Islamabad|33.6844|73.0479
Karachi|Karachi|24.8607|67.0011
@VN|Vietnam|Βιετνάμ
Hanoi|Hanoi|21.0285|105.8542
Ho Chi Minh City|Ho Chi Minh City|10.8231|106.6297
@PH|Philippines|Φιλιππίνες
Manila|Manila|14.5995|120.9842
@MY|Malaysia|Μαλαισία
Kuala Lumpur|Kuala Lumpur|3.1390|101.6869
@KZ|Kazakhstan|Καζακστάν
Astana|Astana|51.1694|71.4491
Almaty|Almaty|43.2220|76.8512
@LB|Lebanon|Λίβανος
Beirut|Beirut|33.8938|35.5018
@JO|Jordan|Ιορδανία
Amman|Amman|31.9454|35.9284
@QA|Qatar|Κατάρ
Doha|Doha|25.2854|51.5310
@GE|Georgia|Γεωργία
Tbilisi|Tbilisi|41.7151|44.8271
@AM|Armenia|Αρμενία
Yerevan|Yerevan|40.1792|44.4991
@AZ|Azerbaijan|Αζερμπαϊτζάν
Baku|Baku|40.4093|49.8671
#africa|Αφρική|Africa
@EG|Egypt|Αίγυπτος
Cairo|Cairo|30.0444|31.2357
Alexandria|Alexandria|31.2001|29.9187
@ZA|South Africa|Νότια Αφρική
Cape Town|Cape Town|-33.9249|18.4241
Johannesburg|Johannesburg|-26.2041|28.0473
Pretoria|Pretoria|-25.7479|28.2293
@NG|Nigeria|Νιγηρία
Lagos|Lagos|6.5244|3.3792
Abuja|Abuja|9.0765|7.3986
@KE|Kenya|Κένυα
Nairobi|Nairobi|-1.2921|36.8219
@MA|Morocco|Μαρόκο
Rabat|Rabat|34.0209|-6.8416
Casablanca|Casablanca|33.5731|-7.5898
Marrakesh|Marrakesh|31.6295|-7.9811
@DZ|Algeria|Αλγερία
Algiers|Algiers|36.7538|3.0588
@TN|Tunisia|Τυνησία
Tunis|Tunis|36.8065|10.1815
@ET|Ethiopia|Αιθιοπία
Addis Ababa|Addis Ababa|9.0320|38.7469
@GH|Ghana|Γκάνα
Accra|Accra|5.6037|-0.1870
@TZ|Tanzania|Τανζανία
Dar es Salaam|Dar es Salaam|-6.7924|39.2083
@SN|Senegal|Σενεγάλη
Dakar|Dakar|14.7167|-17.4677
@LY|Libya|Λιβύη
Tripoli|Tripoli|32.8872|13.1913
#north-america|Βόρεια Αμερική|North America
@US|United States|ΗΠΑ
Washington, D.C.|Washington, D.C.|38.9072|-77.0369
New York|New York|40.7128|-74.0060
Los Angeles|Los Angeles|34.0522|-118.2437
Chicago|Chicago|41.8781|-87.6298
Houston|Houston|29.7604|-95.3698
San Francisco|San Francisco|37.7749|-122.4194
Miami|Miami|25.7617|-80.1918
Seattle|Seattle|47.6062|-122.3321
Boston|Boston|42.3601|-71.0589
@CA|Canada|Καναδάς
Ottawa|Ottawa|45.4215|-75.6972
Toronto|Toronto|43.6532|-79.3832
Vancouver|Vancouver|49.2827|-123.1207
Montréal|Montréal|45.5017|-73.5673
@MX|Mexico|Μεξικό
Mexico City|Mexico City|19.4326|-99.1332
Guadalajara|Guadalajara|20.6597|-103.3496
Cancún|Cancún|21.1619|-86.8515
@CU|Cuba|Κούβα
Havana|Havana|23.1136|-82.3666
@CR|Costa Rica|Κόστα Ρίκα
San José|San José|9.9281|-84.0907
@PA|Panama|Παναμάς
Panama City|Panama City|8.9824|-79.5199
@DO|Dominican Republic|Δομινικανή Δημοκρατία
Santo Domingo|Santo Domingo|18.4861|-69.9312
@JM|Jamaica|Τζαμάικα
Kingston|Kingston|17.9714|-76.7936
#south-america|Νότια Αμερική|South America
@BR|Brazil|Βραζιλία
Brasília|Brasília|-15.7939|-47.8828
São Paulo|São Paulo|-23.5505|-46.6333
Rio de Janeiro|Rio de Janeiro|-22.9068|-43.1729
@AR|Argentina|Αργεντινή
Buenos Aires|Buenos Aires|-34.6037|-58.3816
Córdoba|Córdoba|-31.4201|-64.1888
@CL|Chile|Χιλή
Santiago|Santiago|-33.4489|-70.6693
@PE|Peru|Περού
Lima|Lima|-12.0464|-77.0428
Cusco|Cusco|-13.5319|-71.9675
@CO|Colombia|Κολομβία
Bogotá|Bogotá|4.7110|-74.0721
Medellín|Medellín|6.2476|-75.5658
@VE|Venezuela|Βενεζουέλα
Caracas|Caracas|10.4806|-66.9036
@EC|Ecuador|Ισημερινός
Quito|Quito|-0.1807|-78.4678
@UY|Uruguay|Ουρουγουάη
Montevideo|Montevideo|-34.9011|-56.1645
@BO|Bolivia|Βολιβία
La Paz|La Paz|-16.4897|-68.1193
@PY|Paraguay|Παραγουάη
Asunción|Asunción|-25.2637|-57.5759
#oceania|Ωκεανία|Oceania
@AU|Australia|Αυστραλία
Canberra|Canberra|-35.2809|149.1300
Sydney|Sydney|-33.8688|151.2093
Melbourne|Melbourne|-37.8136|144.9631
Brisbane|Brisbane|-27.4698|153.0251
Perth|Perth|-31.9505|115.8605
Adelaide|Adelaide|-34.9285|138.6007
@NZ|New Zealand|Νέα Ζηλανδία
Wellington|Wellington|-41.2866|174.7756
Auckland|Auckland|-36.8485|174.7633
@FJ|Fiji|Φίτζι
Suva|Suva|-18.1416|178.4419
@PG|Papua New Guinea|Παπούα Νέα Γουινέα
Port Moresby|Port Moresby|-9.4438|147.1803
""";

    private static readonly Lazy<IReadOnlyList<WorldContinent>> Parsed = new(Parse);

    public static IReadOnlyList<WorldContinent> Continents => Parsed.Value;

    public static WorldContinent? Find(string? key) => Continents.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<WorldContinent> Parse()
    {
        var continents = new List<WorldContinent>();
        string? key = null, nameEl = null, nameEn = null;
        var countries = new List<WorldCountry>();
        string? code = null, countryEn = null, countryEl = null;
        var cities = new List<GreekCity>();

        void CloseCountry()
        {
            if (code is not null && cities.Count > 0) countries.Add(new WorldCountry(code, countryEl!, countryEn!, cities.ToList()));
            code = null; cities.Clear();
        }
        void CloseContinent()
        {
            CloseCountry();
            if (key is not null && countries.Count > 0) continents.Add(new WorldContinent(key, nameEl!, nameEn!, countries.OrderBy(c => c.NameEn, StringComparer.Ordinal).ToList()));
            key = null; countries = new List<WorldCountry>();
        }

        foreach (var raw in Data.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var p = line.Substring(1).Split('|');
            switch (line[0])
            {
                case '#': CloseContinent(); (key, nameEl, nameEn) = (p[0], p[1], p[2]); break;
                case '@': CloseCountry(); (code, countryEn, countryEl) = (p[0], p[1], p[2]); break;
                default:
                    p = line.Split('|');
                    var lat = double.Parse(p[2], CultureInfo.InvariantCulture);
                    var lon = double.Parse(p[3], CultureInfo.InvariantCulture);
                    cities.Add(new GreekCity(GeoSearch.IdFor(lat, lon), p[1], p[0], lat, lon));
                    break;
            }
        }
        CloseContinent();
        return continents;
    }
}
