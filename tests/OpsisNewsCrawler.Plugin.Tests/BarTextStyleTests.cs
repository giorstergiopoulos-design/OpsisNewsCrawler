using System.Reflection;
using OpsisNewsCrawler.Windows;
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
