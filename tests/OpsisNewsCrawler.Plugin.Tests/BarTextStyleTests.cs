using System.Reflection;
using OpsisNewsCrawler.Windows;
using System.Drawing;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public class BarTextStyleTests
{
    private static Font Title(TickerCanvas c) => (Font)typeof(TickerCanvas).GetField("titleFont", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(c)!;

    private static void OnSta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.True(t.Join(60000));
        if (error is not null) throw new Exception("failed on the UI thread", error);
    }

    [Fact]
    public void TheFontAndTheSizeOfTheTextFollowTheStyleOfTheBar()
    {
        OnSta(() =>
        {
            using var canvas = new TickerCanvas();
            var normal = Title(canvas).Size;
            Assert.Equal("Segoe UI", Title(canvas).Name);

            canvas.SetTextStyle("Consolas", 150);
            Assert.Equal("Consolas", Title(canvas).Name);
            Assert.Equal(normal * 1.5f, Title(canvas).Size, 1);

            canvas.SetFontScale(2f);                       // a taller bar: the text grows with it and keeps the user's percentage
            Assert.Equal(normal * 3f, Title(canvas).Size, 1);

            canvas.SetTextStyle("A font that is not installed 123", 100);   // falls back to the default font, never fails
            Assert.Equal("Segoe UI", Title(canvas).Name);
            canvas.SetTextStyle("", 100);
            Assert.Equal("Segoe UI", Title(canvas).Name);
            Assert.Equal(normal * 2f, Title(canvas).Size, 1);

            canvas.SetTextStyle("Consolas", 5000);         // out of range: the largest percentage
            Assert.Equal(normal * 2f * 1.6f, Title(canvas).Size, 1);
        });
    }
}

public class CanvasPerformanceTests
{
    private static void OnSta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.True(t.Join(120000));
        if (error is not null) throw new Exception("failed on the UI thread", error);
    }

    private static IReadOnlyList<OpsisNewsCrawler.Core.NewsItem> Items(int n, int seed)
    {
        var rng = new Random(seed);
        return Enumerable.Range(0, n).Select(i => new OpsisNewsCrawler.Core.NewsItem(
            "Headline number " + i + " " + new string('x', rng.Next(5, 60)), "https://site" + (i % 7) + ".example.com/a/" + i, "Source " + (i % 7), DateTimeOffset.Now)).ToList();
    }

    [Fact]
    public void TheBinarySearchHitTestFindsTheSameEntryAsTheOriginalWalk()
    {
        OnSta(() =>
        {
            using var canvas = new TickerCanvas { Size = new Size(1200, 42) };
            foreach (var count in new[] { 20, 80, 400 })
            {
                canvas.SetItems(Items(count, count));
                for (var offset = 0.0; offset < 5000; offset += 137.3)
                {
                    canvas.SetOffset(offset);
                    for (var x = -20; x < 1220; x += 7)
                    {
                        var p = new Point(x, 20);
                        Assert.Equal(canvas.HitTestLinear(p), canvas.HitTest(p));
                    }
                }
            }
        });
    }

    [Fact]
    public void MeasuredWidthsAreKeptAndTheCacheIsBounded()
    {
        OnSta(() =>
        {
            using var canvas = new TickerCanvas { Size = new Size(1200, 42) };
            canvas.SetItems(Items(100, 1));
            var after = TickerCanvas.MeasureCacheCount;
            Assert.True(after > 0);
            canvas.SetItems(Items(100, 1));                              // the same headlines again: nothing new is measured
            Assert.Equal(after, TickerCanvas.MeasureCacheCount);
            for (var round = 0; round < 6; round++) canvas.SetItems(Items(1500, 100 + round));   // many different headlines: the cache never grows without limit
            Assert.True(TickerCanvas.MeasureCacheCount <= 4000);
        });
    }

    [Fact]
    public void TheIconCacheKeepsAtMostTheLimit()
    {
        using var bmp = new Bitmap(16, 16);
        for (var i = 0; i < FaviconCache.MaxIcons + 60; i++) FaviconCache.Seed("host" + i + ".example.com", bmp);
        Assert.True(FaviconCache.Count <= FaviconCache.MaxIcons);
        Assert.Null(FaviconCache.TryGet("host0.example.com"));              // the oldest went first
        Assert.NotNull(FaviconCache.TryGet("host" + (FaviconCache.MaxIcons + 59) + ".example.com"));
    }
}
