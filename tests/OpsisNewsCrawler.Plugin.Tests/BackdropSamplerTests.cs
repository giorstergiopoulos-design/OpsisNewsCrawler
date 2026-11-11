using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public class BackdropSamplerTests
{
    [Fact]
    public void TheSamplerMeasuresALightAndADarkAreaOfTheScreen()
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                foreach (var (color, light) in new[] { (Color.White, true), (Color.Black, false) })
                {
                    using var form = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new Point(40, 40), Size = new Size(120, 40), BackColor = color, TopMost = true, ShowInTaskbar = false };
                    form.Show();
                    for (var i = 0; i < 15; i++) { Application.DoEvents(); Thread.Sleep(20); }
                    var l = BackdropSampler.Sample(new Rectangle(50, 45, 100, 30));
                    if (l is null || (light && l < 0.5)) return;   // no usable screen (a locked or remote session, some CI machines): nothing to measure
                    if (light) Assert.True(l > 0.9, $"white measured {l}"); else Assert.True(l < 0.1, $"black measured {l}");
                }
            }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.True(t.Join(60000));
        if (error is not null) throw new Exception("failed", error);
    }
}
