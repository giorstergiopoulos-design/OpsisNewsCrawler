using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// <c>--snapshot-bar &lt;png&gt;</c>: draws the strip with made-up headlines at three bar heights (100 %, 150 %, 200 % DPI) and the stages of the
/// breaking-news intro, composed over a mid-grey background, into one PNG. Used to look at the icon work without a desktop session.
/// </summary>
public static class BarSnapshot
{
    public static void Save(string path)
    {
        var rows = new List<Bitmap>();
        foreach (var height in new[] { 42, 63, 84 })
        {
            rows.Add(Frame(height, intro: -1, breaking: false, seedFavicon: true));
            foreach (var t in new[] { 0.6, 1.2, 2.0 })
                rows.Add(Frame(height, intro: t, breaking: true, seedFavicon: false));
        }

        var width = rows.Max(r => r.Width);
        var total = rows.Sum(r => r.Height + 6);
        using var sheet = new Bitmap(width, total);
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(70, 78, 96));
            var y = 0;
            foreach (var r in rows)
            {
                g.DrawImageUnscaled(r, 0, y);
                y += r.Height + 6;
                r.Dispose();
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        sheet.Save(path);
    }

    private static Bitmap Frame(int height, double intro, bool breaking, bool seedFavicon)
    {
        using var canvas = new TickerCanvas { Size = new Size(1000, height) };
        canvas.SetFontScale(height / 42f);
        if (seedFavicon)
        {
            using var fav = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(fav)) { g.Clear(Color.FromArgb(28, 28, 36)); g.FillRectangle(Brushes.DimGray, 14, 14, 36, 36); }
            FaviconCache.Seed("dark-logo.example", fav);
        }

        var now = DateTimeOffset.Now;
        canvas.SetItems(new[]
        {
            new NewsItem("Eurogroup keeps the fiscal path unchanged", "https://dark-logo.example/a", "Dark Logo", now),
            new NewsItem("Athens stocks close higher on banks", "https://alpha.invalid/b", "Alpha News", now),
            new NewsItem("Defence ministers meet in Brussels", "https://zeta.invalid/c", "Zeta", now),
            new NewsItem("Sports: derby ends in a draw", "https://omega.invalid/d", "Omega Sport", now),
        });
        if (breaking)
        {
            canvas.SetFill(Color.FromArgb(200, 24, 24));
            canvas.SetAlertIntro(intro);
        }
        using var frame = canvas.RenderFrame();
        return new Bitmap(frame);
    }
}

/// <summary><c>--snapshot-hazard-bar &lt;png&gt;</c>: the disaster line of the World region (continent headings, kind icons) at 100 % and 150 %, left-to-right.</summary>
public static class HazardBarSnapshot
{
    public static void Save(string path)
    {
        var t = DateTimeOffset.Now.AddHours(-2);
        HazardEvent E(string id, HazardKind kind, string place, double lat, double lon, int level, double? mag = null) =>
            new(id, kind, place, lat, lon, t, "https://example.org/" + id, mag, mag, mag, level, new[] { "USGS", "EMSC" }, 12);
        var events = new[]
        {
            E("a", HazardKind.Earthquake, "Crete, Greece", 35.2, 25.0, 2, 5.7), E("b", HazardKind.Wildfire, "Evia", 38.8, 23.3, 2),
            E("c", HazardKind.Earthquake, "Honshu, Japan", 36.0, 140.0, 3, 7.1), E("d", HazardKind.Storm, "Bay of Bengal", 15.0, 88.0, 2),
            E("e", HazardKind.Flood, "Lagos", 6.5, 3.4, 1), E("f", HazardKind.Volcano, "Etna", 37.7, 15.0, 1),
            E("g", HazardKind.Drought, "Texas", 31.0, -99.0, 1), E("h", HazardKind.Tsunami, "Chile coast", -33.0, -72.0, 3), E("i", HazardKind.Other, "Sydney", -33.9, 151.2, 1),
        };
        var items = new List<NewsItem>();
        foreach (var (continent, list) in HazardWorld.Group(events))
        {
            items.Add(HazardText.ContinentItem(continent));
            items.AddRange(list.Select(e => HazardText.ToItem(e, DateTimeOffset.Now)));
        }
        var rows = new List<Bitmap>();
        foreach (var height in new[] { 42, 63 })
        {
            using var canvas = new TickerCanvas { Size = new Size(2200, height) };
            canvas.SetFontScale(height / 42f);
            canvas.SetItems(items);
            using var frame = canvas.RenderFrame();
            rows.Add(new Bitmap(frame));
        }
        using var sheet = new Bitmap(rows.Max(r => r.Width), rows.Sum(r => r.Height + 6));
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(40, 44, 56));
            var y = 0;
            foreach (var r in rows) { g.DrawImageUnscaled(r, 0, y); y += r.Height + 6; r.Dispose(); }
        }
        sheet.Save(path);
    }
}
