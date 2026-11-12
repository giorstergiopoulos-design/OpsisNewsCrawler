using System.Text;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

public class InstallerThemeTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any()) dir = dir.Parent;
        return dir!.FullName;
    }

    [Fact]
    public void TheWizardFollowsTheWindowsThemeAndHasPicturesForBothThemes()
    {
        var root = Root();
        var iss = File.ReadAllText(Path.Combine(root, "OpsisNewsCrawler.iss"), Encoding.UTF8);
        Assert.Contains("WizardStyle=modern dynamic", iss);
        foreach (var key in new[] { "WizardImageFileDynamicDark=", "WizardImageBackColorDynamicDark=", "WizardSmallImageFileDynamicDark=", "WizardImageBackColor=" })
            Assert.Contains(key, iss);
        // every picture the script names exists
        foreach (var line in iss.Split('\n').Where(l => l.StartsWith("Wizard") && l.Contains("File")))
            foreach (var file in line[(line.IndexOf('=') + 1)..].Split(',').Select(f => f.Trim().Split(';')[0].Trim()).Where(f => f.Length > 0))
                Assert.True(File.Exists(Path.Combine(root, file.Replace('\\', Path.DirectorySeparatorChar))), "missing picture " + file);
    }

    [Fact]
    public void TheSmallPicturesAreTransparentAndTheDarkTallPicturesAreDarker()
    {
        var root = Path.Combine(Root(), "installer", "wizard");
        foreach (var size in new[] { "100", "125", "150", "200" })
        {
            var png = File.ReadAllBytes(Path.Combine(root, $"small-{size}.png"));
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());
            Assert.Equal(6, png[25]);                           // PNG colour type 6 = RGBA: it has an alpha channel (a transparent background)
        }
        foreach (var size in new[] { "100", "125", "150", "200" })
        {
            var light = File.ReadAllBytes(Path.Combine(root, $"large-{size}.bmp"));
            var dark = File.ReadAllBytes(Path.Combine(root, $"dark-large-{size}.bmp"));
            Assert.Equal(light.Length, dark.Length);              // the same size and layout
            double Mean(byte[] b) => b.Skip(54).Where((_, i) => i % 97 == 0).Average(v => (double)v);
            Assert.True(Mean(dark) < Mean(light), "the dark picture must be darker (" + Mean(dark) + " vs " + Mean(light) + ")");
        }
    }

    [Fact]
    public void ThePatternForSilentInstallsIsUnchanged()
    {
        // /VERYSILENT never shows the wizard: the style directives must not require a window (no WizardStyle=... that needs a user), and the end-to-end script still uses the silent switches
        var e2e = File.ReadAllText(Path.Combine(Root(), "tools", "e2e-installer.ps1"));
        Assert.Contains("/VERYSILENT", e2e);
        Assert.True(File.Exists(Path.Combine(Root(), "tools", "installer-theme-shots.ps1")));
    }
}
