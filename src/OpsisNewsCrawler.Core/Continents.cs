namespace OpsisNewsCrawler.Core;

public enum Continent { Europe, Asia, Africa, NorthAmerica, SouthAmerica, Oceania, Antarctica }

/// <summary>
/// Which continent a point belongs to (coarse rules, good enough to sort events; the sea next to a coast belongs to that coast) and a coarse
/// outline of each continent that the bar and the detail card draw as its picture.
/// </summary>
public static class Continents
{
    public static string Key(Continent c) => c switch
    {
        Continent.Europe => "europe", Continent.Asia => "asia", Continent.Africa => "africa", Continent.NorthAmerica => "northamerica",
        Continent.SouthAmerica => "southamerica", Continent.Oceania => "oceania", _ => "antarctica"
    };

    public static string Name(Continent c, Func<string, string>? t = null) => (t ?? Loc.T)("continent." + Key(c));

    public static Continent Of(double lat, double lon)
    {
        if (lat < -60) return Continent.Antarctica;
        // Pacific islands, Australia, New Zealand, Papua New Guinea
        if (lon >= 160 || lon <= -140 && lat < 30 || lat < -11 && lon >= 110 || lat is < 0 and > -12 && lon >= 141) return Continent.Oceania;
        if (lon >= -85 && lon <= -30 && lat < 8) return Continent.SouthAmerica;
        if (lon >= -170 && lon <= -25) return Continent.NorthAmerica;
        if (lon >= -26 && lon < 29 && lat >= 34.5) return Continent.Europe;
        if (lon >= -20 && lon <= 51 && lat >= -36 && lat <= 37.5 && !(lon > 34.5 && lat > 12)) return Continent.Africa;
        return Continent.Asia;
    }

    public static IReadOnlyList<Continent> Order { get; } = new[]
    {
        Continent.Europe, Continent.Asia, Continent.Africa, Continent.NorthAmerica, Continent.SouthAmerica, Continent.Oceania, Continent.Antarctica
    };

    /// <summary>Coarse outlines as (longitude, latitude) rings; several rings for islands and peninsulas.</summary>
    public static IReadOnlyList<(double Lon, double Lat)[]> Outline(Continent c) => c switch
    {
        Continent.Europe => new[]
        {
            new (double, double)[] { (-9.5, 37), (-9.5, 43.5), (-2, 43.5), (-4.5, 48.3), (2, 51), (8, 54), (8.5, 57.2), (10.5, 57.5), (12, 54.5), (20, 54.7), (21, 57), (24.5, 59.5), (30, 60), (40, 62), (40, 46), (30, 45), (28.5, 41), (26, 40.5), (23.5, 38), (22, 36.5), (21, 39.5), (19.5, 41.8), (16, 43.5), (13.5, 45.5), (12.5, 44.5), (18.5, 40), (16, 38), (15.5, 40), (12, 41.5), (8.5, 44.3), (3.5, 43.3), (0, 39), (-1, 37.5), (-5.5, 36) },
            new (double, double)[] { (5, 59), (5.5, 62), (10, 64), (14, 67), (18, 69.8), (25, 71), (30, 70), (28, 68), (25.5, 65.5), (21.5, 64.5), (17.5, 62), (19, 60), (17, 57.2), (14, 55.6), (12, 56.5), (11, 59), (8, 58) },
            new (double, double)[] { (-5.5, 50), (1.5, 51.2), (0, 53), (-2, 55.8), (-2, 57.6), (-5, 58.6), (-6, 56.5), (-3, 54.8), (-4.8, 53.4) },
            new (double, double)[] { (-10, 51.8), (-6, 52.2), (-6, 55), (-8.5, 54.5) },
        },
        Continent.Asia => new[]
        {
            new (double, double)[] { (28.5, 41), (30, 45), (36, 45), (40, 46), (40, 62), (45, 68), (55, 68), (60, 66), (60, 70), (68, 69), (70, 73), (80, 73), (95, 76), (105, 77), (113, 74), (140, 72), (160, 70), (180, 69), (180, 65), (170, 60), (162, 55), (156, 51), (163.5, 58), (163, 62), (155, 59.5), (142, 59), (135, 54), (141, 53), (140, 48), (135, 44), (130, 42), (129, 35), (126, 38), (122, 41), (118, 39), (121.5, 39), (122, 30), (120, 23), (109, 21.5), (108, 12), (105, 8.7), (103, 10), (100, 13), (98.5, 8), (98, 16), (94, 16), (92, 22), (87, 21.5), (80.5, 15.5), (80, 13.5), (77, 8), (73, 20), (67, 24.5), (60, 25), (57, 25.5), (48, 29.5), (50, 30), (56, 26), (58, 23), (53, 16.5), (43, 13), (35, 29.5), (34.5, 31), (36, 36.5), (28, 36.5) },
            new (double, double)[] { (130.5, 31), (132, 34), (136, 34.5), (140, 36), (141.5, 41), (142, 45), (145, 44), (141, 38), (139.5, 34.8), (135, 33.5) },
            new (double, double)[] { (95, 5.5), (98, 4), (104, -2), (106, -6), (101, -3), (97, 2) },
            new (double, double)[] { (109, 1), (113, 3), (117, 7), (119, 5), (116, -3), (111, -3) },
        },
        Continent.Africa => new[]
        {
            new (double, double)[] { (-17, 21), (-13, 28), (-6, 35.8), (10, 37.2), (11, 33), (20, 31), (25, 32), (32, 31), (34.5, 28), (37, 21), (43, 12), (51, 12), (51, 10), (46, 3), (41, -2), (40, -10), (41, -15), (35, -24), (32, -29), (27, -34), (20, -35), (18, -32), (14, -22), (12, -17), (13, -12), (9, -1), (9.5, 4), (5, 6), (-3, 5), (-8, 4.5), (-13, 8), (-17, 14) },
            new (double, double)[] { (44, -13), (50, -15), (49, -25), (44, -24.5), (43.5, -18) },
        },
        Continent.NorthAmerica => new[]
        {
            new (double, double)[] { (-168, 66), (-162, 70), (-140, 70), (-125, 70), (-110, 68), (-95, 72), (-85, 70), (-80, 63), (-93, 60), (-82, 52), (-78, 55), (-77, 62), (-65, 60), (-61, 56), (-56, 52), (-66, 45), (-70, 43), (-76, 38), (-76, 35), (-81, 31), (-80, 26), (-82, 26), (-84, 30), (-90, 29.5), (-97, 27), (-97.5, 22), (-95, 18.5), (-90, 21), (-87, 21), (-88, 16), (-84, 15.5), (-83, 10.5), (-79, 9), (-80, 7.2), (-85, 10), (-87.5, 13), (-92, 14.5), (-97, 16), (-105, 20), (-106, 23), (-112, 29), (-115, 30), (-110, 23), (-112, 25), (-115, 28.5), (-117, 32.5), (-121, 35), (-124, 40), (-124.5, 47), (-123, 49), (-130, 55), (-135, 58), (-142, 60), (-150, 59.5), (-153, 57.5), (-163, 55), (-158, 58.5), (-162, 60), (-165, 62), (-162, 64.5), (-168, 65) },
            new (double, double)[] { (-73, 78), (-60, 82), (-30, 83), (-20, 78), (-22, 70), (-32, 68), (-43, 60), (-50, 63), (-55, 68), (-67, 76) },
        },
        Continent.SouthAmerica => new[]
        {
            new (double, double)[] { (-77, 8), (-72, 12), (-62, 10.5), (-52, 5), (-50, 0), (-44, -2), (-35, -5.5), (-35, -9), (-39, -14), (-41, -22), (-48, -26), (-49, -29), (-53, -34), (-57, -35), (-58, -38.5), (-62, -39), (-65, -41), (-64, -46), (-68, -50), (-69, -52.5), (-68, -55), (-72, -53), (-75, -50), (-73, -43), (-73, -37), (-71, -30), (-70, -18), (-76, -14), (-81, -6), (-80, -1), (-78, 2), (-77.5, 6) },
        },
        Continent.Oceania => new[]
        {
            new (double, double)[] { (113, -22), (114, -26), (115, -34), (118, -35), (124, -33.5), (131, -31.5), (135, -34.5), (138, -35), (140, -38), (146, -39), (150, -37), (153, -31), (153, -25), (146, -19), (145, -14.5), (142, -10.8), (141, -17), (136, -15), (136, -12), (131, -11.5), (129, -15), (122, -17.5), (114, -22) },
            new (double, double)[] { (172.5, -34.5), (175, -37), (178, -37.5), (175, -41), (172.5, -40.5) },
            new (double, double)[] { (172.5, -41), (174, -41.5), (171, -45), (167, -46), (168.5, -44) },
            new (double, double)[] { (141, -2.5), (147, -6), (150.5, -10.5), (144, -7.8), (141, -9) },
        },
        _ => new[]
        {
            // a polar view (x, y are only picture coordinates)
            new (double, double)[] { (10.0, 0.0), (8.3, 3.4), (7.4, 7.4), (3.3, 7.9), (0.0, 10.0), (-3.6, 8.8), (-7.8, 7.8), (-8.3, 3.4), (-10.5, 0.0), (-8.1, -3.4), (-7.1, -7.1), (-3.5, -8.5), (-0.0, -10.8), (3.6, -8.7), (7.2, -7.2), (7.9, -3.3) },
        },
    };

    /// <summary>The box (min lon, min lat, max lon, max lat) around the outline, used to fit the picture.</summary>
    public static (double MinLon, double MinLat, double MaxLon, double MaxLat) Bounds(Continent c)
    {
        double minLon = 180, minLat = 90, maxLon = -180, maxLat = -90;
        foreach (var ring in Outline(c))
            foreach (var (lon, lat) in ring)
            {
                minLon = Math.Min(minLon, lon); maxLon = Math.Max(maxLon, lon);
                minLat = Math.Min(minLat, lat); maxLat = Math.Max(maxLat, lat);
            }
        return (minLon, minLat, maxLon, maxLat);
    }
}

/// <summary>For the "World" region: the events grouped by continent, each group in the fixed order of <see cref="Continents.Order"/>; empty continents are left out.</summary>
public static class HazardWorld
{
    public static IReadOnlyList<(Continent Continent, IReadOnlyList<HazardEvent> Events)> Group(IEnumerable<HazardEvent> events)
    {
        var byContinent = events.GroupBy(e => Continents.Of(e.Lat, e.Lon)).ToDictionary(g => g.Key, g => (IReadOnlyList<HazardEvent>)g.ToList());
        return Continents.Order.Where(byContinent.ContainsKey).Select(c => (c, byContinent[c])).ToList();
    }
}
