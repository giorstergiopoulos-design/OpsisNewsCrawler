using System.Diagnostics;
using OpsisNewsCrawler.Core;
using OpsisNewsCrawler.Windows;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

public class UpdateHelperTests
{
    private static string Temp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void TheHelperRunsFromACopyOutsideTheInstalledFolderAndTheCopyIsReplacedNextTime()
    {
        var root = Temp();
        try
        {
            var app = Directory.CreateDirectory(Path.Combine(root, "Program Files", "Opsis News Crawler")).FullName;
            File.WriteAllText(Path.Combine(app, "OpsisNewsCrawler.exe"), "exe");
            File.WriteAllText(Path.Combine(app, "OpsisNewsCrawler.dll"), "dll");
            Directory.CreateDirectory(Path.Combine(app, "runtimes", "win-x64"));
            File.WriteAllText(Path.Combine(app, "runtimes", "win-x64", "native.dll"), "native");
            Directory.CreateDirectory(Path.Combine(app, "plugins", "Opsis - Desktop Customization"));
            File.WriteAllText(Path.Combine(app, "plugins", "Opsis - Desktop Customization", "plugin.dll"), "plugin");
            var update = Path.Combine(root, "update");

            var copy = UpdateHelper.PrepareCopy(Path.Combine(app, "OpsisNewsCrawler.exe"), update);
            Assert.NotNull(copy);
            Assert.StartsWith(Path.Combine(update, "helper"), copy);
            Assert.False(copy!.StartsWith(app, StringComparison.OrdinalIgnoreCase));          // not in the folder that the setup replaces
            Assert.True(File.Exists(Path.Combine(update, "helper", "OpsisNewsCrawler.dll")));
            Assert.True(File.Exists(Path.Combine(update, "helper", "runtimes", "win-x64", "native.dll")));
            Assert.False(Directory.Exists(Path.Combine(update, "helper", "plugins")));       // the copy for Opsis is not needed

            File.WriteAllText(Path.Combine(update, "helper", "stale.txt"), "old");
            Assert.NotNull(UpdateHelper.PrepareCopy(Path.Combine(app, "OpsisNewsCrawler.exe"), update));
            Assert.False(File.Exists(Path.Combine(update, "helper", "stale.txt")));           // each update starts from a clean copy
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [Fact]
    public void AMissingProgramGivesNoCopy() => Assert.Null(UpdateHelper.PrepareCopy(Path.Combine(Temp(), "nope", "OpsisNewsCrawler.exe"), Temp()));

    [Fact]
    public void AWrongHashIsLoggedAndNothingIsStarted()
    {
        var dir = Temp();
        try
        {
            var setup = Path.Combine(dir, "Setup.exe");
            File.WriteAllText(setup, "not a setup");
            var log = Path.Combine(dir, "last-update.log");
            var code = UpdateHelper.Run(new[] { "999999", setup, new string('0', 64), log, "0.0.1", "standalone" });
            Assert.Equal(2, code);
            var last = UpdateResultLog.Last(log)!;
            Assert.True(last.Failed);
            Assert.Equal(2, last.ExitCode);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void TooFewArgumentsDoNothing() => Assert.Equal(1, UpdateHelper.Run(new[] { "1", "x" }));
}
