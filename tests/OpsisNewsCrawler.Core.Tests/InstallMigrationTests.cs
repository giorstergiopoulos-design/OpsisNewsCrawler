using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>The move from a per-user installation to the machine-wide one, the start-up entry and the update helper: all on a MemoryRegistry and temp folders, never the real registry.</summary>
public class InstallMigrationTests
{
    private const string UserDir = @"C:\Users\x\AppData\Local\Programs\Opsis News Crawler";
    private const string MachineDir = @"C:\Program Files\Opsis News Crawler";

    private static MemoryRegistry WithCopies(bool perUser, bool machine)
    {
        var r = new MemoryRegistry();
        if (perUser)
        {
            r.SetString(UserHive.CurrentUser, InstallMigration.UninstallKey, "InstallLocation", UserDir + "\\");
            r.SetString(UserHive.CurrentUser, InstallMigration.UninstallKey, "UninstallString", "\"" + UserDir + "\\unins000.exe\"");
            r.SetString(UserHive.CurrentUser, InstallMigration.UninstallKey, "DisplayVersion", "0.17.0");
        }
        if (machine)
        {
            r.SetString(UserHive.LocalMachine, InstallMigration.UninstallKey, "InstallLocation", MachineDir + "\\");
            r.SetString(UserHive.LocalMachine, InstallMigration.UninstallKey, "UninstallString", "\"" + MachineDir + "\\unins000.exe\"");
            r.SetString(UserHive.LocalMachine, InstallMigration.UninstallKey, "DisplayVersion", "0.18.5");
        }
        return r;
    }

    [Fact]
    public void FindsBothCopiesWithTheirScopeFolderAndVersion()
    {
        var copies = InstallMigration.Find(WithCopies(perUser: true, machine: true));
        Assert.Equal(2, copies.Count);
        var user = copies.Single(c => c.PerUser);
        Assert.Equal(UserDir, user.Directory);
        Assert.Equal("0.17.0", user.Version);
        Assert.Equal(UserDir + "\\unins000.exe", user.UninstallExe);
        Assert.Equal("0.18.5", copies.Single(c => !c.PerUser).Version);
    }

    [Fact]
    public void ADuplicateExistsOnlyWhenBothScopesAreInstalled()
    {
        Assert.NotNull(InstallMigration.DuplicatePerUserCopy(InstallMigration.Find(WithCopies(true, true))));
        Assert.Null(InstallMigration.DuplicatePerUserCopy(InstallMigration.Find(WithCopies(true, false))));   // only the old one: nothing to remove yet
        Assert.Null(InstallMigration.DuplicatePerUserCopy(InstallMigration.Find(WithCopies(false, true))));
        Assert.Null(InstallMigration.DuplicatePerUserCopy(InstallMigration.Find(WithCopies(false, false))));
    }

    [Fact]
    public void UninstallStringWithoutInstallLocationStillGivesTheFolder()
    {
        var r = new MemoryRegistry();
        r.SetString(UserHive.CurrentUser, InstallMigration.UninstallKey, "UninstallString", "\"" + UserDir + "\\unins000.exe\" /SILENT");
        Assert.Equal(UserDir, InstallMigration.Find(r).Single().Directory);
    }

    [Theory]
    [InlineData(@"""C:\Program Files\Opsis News Crawler\OpsisNewsCrawler.exe""", false)]
    [InlineData(@"C:\Program Files\Opsis News Crawler\OpsisNewsCrawler.exe", false)]
    [InlineData(@"""C:\w\OpsisNewsCrawler\src\OpsisNewsCrawler.App\bin\Release\net10.0-windows\OpsisNewsCrawler.exe""", true)]   // the build under test (the 0.18.5 audit bug)
    [InlineData(@"""C:\Users\x\AppData\Local\Programs\Opsis News Crawler\OpsisNewsCrawler.exe""", true)]                         // the old per-user copy
    [InlineData(@"""C:\Program Files\Opsis News Crawler2\OpsisNewsCrawler.exe""", true)]                                            // a folder that only starts like ours
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AutostartMustPointIntoProgramFiles(string? value, bool problem) =>
        Assert.Equal(problem, InstallMigration.AutostartProblem(value, new[] { MachineDir }) is not null);

    [Fact]
    public void CleanAutostartRemovesOnlyAWrongValue()
    {
        var wrong = new MemoryRegistry();
        wrong.SetString(UserHive.CurrentUser, InstallMigration.RunKey, InstallMigration.RunValue, "\"C:\\w\\build\\OpsisNewsCrawler.exe\"");
        wrong.SetString(UserHive.CurrentUser, InstallMigration.RunKey, "Other Program", "\"C:\\other.exe\"");
        Assert.NotNull(InstallMigration.CleanAutostart(wrong, new[] { MachineDir }));
        Assert.Null(wrong.GetString(UserHive.CurrentUser, InstallMigration.RunKey, InstallMigration.RunValue));
        Assert.Equal("\"C:\\other.exe\"", wrong.GetString(UserHive.CurrentUser, InstallMigration.RunKey, "Other Program"));   // other programs are never touched

        var right = new MemoryRegistry();
        right.SetString(UserHive.CurrentUser, InstallMigration.RunKey, InstallMigration.RunValue, "\"" + MachineDir + "\\OpsisNewsCrawler.exe\"");
        Assert.Null(InstallMigration.CleanAutostart(right, new[] { MachineDir }));
        Assert.NotNull(right.GetString(UserHive.CurrentUser, InstallMigration.RunKey, InstallMigration.RunValue));
    }

    [Theory]
    [InlineData(UserDir + @"\OpsisNewsCrawler.exe", true)]
    [InlineData(UserDir + @"2\OpsisNewsCrawler.exe", false)]      // another folder with a similar name
    [InlineData(MachineDir + @"\OpsisNewsCrawler.exe", false)]    // the new machine-wide program is never ended by the migration
    [InlineData(@"C:\w\OpsisNewsCrawler\x.exe", false)]
    [InlineData(null, false)]
    public void OnlyProcessesInsideTheOldFolderMayBeEnded(string? exe, bool expected) => Assert.Equal(expected, InstallMigration.IsInside(exe, UserDir));

    [Fact]
    public void RemovalPlanHasTheStepsInOrder()
    {
        var plan = InstallMigration.RemovalPlan(new InstalledCopy(true, UserDir, "0.17.0", UserDir + "\\unins000.exe"));
        Assert.StartsWith("end-processes-in:", plan[0]);
        Assert.StartsWith("run-uninstaller:", plan[1]);
        Assert.Equal("verify-gone:key+folder", plan[2]);
        Assert.Equal("delete-run-value:HKCU", plan[^1]);
    }

    [Fact]
    public void ProbesNeverUseTheRealRegistry()
    {
        Assert.True(TestIsolation.ShouldIsolate(new[] { "--audit-settings", "r.txt", "--config", "c.json" }));
        Assert.True(TestIsolation.ShouldIsolate(new[] { "--audit-layout", "r.txt" }));
        Assert.True(TestIsolation.ShouldIsolate(new[] { "--snapshot", "dir" }));
        Assert.True(TestIsolation.ShouldIsolate(new[] { "--snapshot-hazard-card", "a.png" }));
        Assert.True(TestIsolation.ShouldIsolate(new[] { "--config", "c.json" }));
        Assert.True(TestIsolation.ShouldIsolate(new[] { "--test-breaking" }));
        Assert.True(TestIsolation.ShouldIsolate(new[] { "--make-icon", "a.ico" }));
        Assert.True(TestIsolation.ShouldIsolate(new[] { "--welcome" }));
        Assert.False(TestIsolation.ShouldIsolate(Array.Empty<string>()));
        Assert.False(TestIsolation.ShouldIsolate(new[] { "--settings" }));
    }

    [Fact]
    public void FirstRunFileCarriesTheStartWithWindowsWish()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "first-run.json"), "{\"Position\":\"BottomTaskbar\",\"Language\":\"el\",\"StartWithWindows\":true}");
            var config = AppConfig.Load(Path.Combine(dir, "config.json"));
            Assert.True(config.StartWithWindowsRequested);
            Assert.False(File.Exists(Path.Combine(dir, "first-run.json")));     // applied once
            Assert.DoesNotContain("StartWithWindowsRequested", File.ReadAllText(Path.Combine(dir, "config.json")));   // never saved as a setting
        }
        finally { Directory.Delete(dir, true); }
    }
}

public class UpdateResultTests
{
    private static string TempLog() => Path.Combine(Path.GetTempPath(), "opsis-test-" + Guid.NewGuid().ToString("N"), "last-update.log");

    [Fact]
    public void ParsesTheLineOfTheHelper()
    {
        var e = UpdateResultLog.Parse("2026-10-30T10:00:00+02:00|failed|1223|0.18.6|the setup could not be started (UAC refused?): x")!;
        Assert.True(e.Failed);
        Assert.Equal(1223, e.ExitCode);
        Assert.Equal("0.18.6", e.Version);
        Assert.Contains("UAC", e.Message);
        Assert.False(UpdateResultLog.Parse("2026-10-30T10:00:00+02:00|ok|0|0.18.6|")!.Failed);
        Assert.Null(UpdateResultLog.Parse("garbage"));
        Assert.Null(UpdateResultLog.Parse(null));
    }

    [Fact]
    public void AFailureIsShownOnceAndASuccessNever()
    {
        var path = TempLog();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllText(path, "2026-10-30T10:00:00+02:00|failed|4|0.18.6|the setup ended with an error\n");
            Assert.NotNull(UpdateResultLog.TakeUnseenFailure(path));
            Assert.Null(UpdateResultLog.TakeUnseenFailure(path));                       // not twice
            File.AppendAllText(path, "2026-10-30T11:00:00+02:00|failed|2|0.18.6|hash\n");
            Assert.NotNull(UpdateResultLog.TakeUnseenFailure(path));                    // a newer failure is new again
            File.AppendAllText(path, "2026-10-30T12:00:00+02:00|ok|0|0.18.6|\n");
            Assert.Null(UpdateResultLog.TakeUnseenFailure(path));                       // the last attempt worked
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void MissingLogIsNotAnError() => Assert.Null(UpdateResultLog.TakeUnseenFailure(TempLog()));

    [Fact]
    public void HelperScriptRunsTheSetupElevatedChecksTheHashLogsAndReopens()
    {
        var script = UpdateHelperScript.Build(1234, @"C:\Users\o'neil\update\Setup.exe", new string('a', 64), @"C:\Users\o'neil\update\last-update.log", "0.18.6",
            "'/SILENT','/NORESTART'", @"C:\Program Files\Opsis News Crawler\OpsisNewsCrawler.exe");
        Assert.Contains("Wait-Process -Id 1234", script);
        Assert.Contains("-Verb RunAs -Wait -PassThru", script);                    // the setup is machine-wide: UAC is asked for explicitly
        Assert.Contains("Get-FileHash", script);
        Assert.Contains("Log 'failed' 1223", script);                              // a refused UAC prompt is a logged result
        Assert.Contains("Log 'ok' 0", script);
        Assert.Contains("C:\\Users\\o''neil\\update\\Setup.exe", script);          // apostrophes in a path are quoted
        Assert.Contains("Start-Process -FilePath 'C:\\Program Files\\Opsis News Crawler\\OpsisNewsCrawler.exe'", script);
        Assert.DoesNotContain("-WindowStyle", script);
    }

    [Fact]
    public void HostedUpdateDoesNotReopenTheProgram()
    {
        var script = UpdateHelperScript.Build(1, "s.exe", new string('b', 64), "l.log", "0.18.6", "'/VERYSILENT'", null);
        Assert.DoesNotContain("Test-Path -LiteralPath", script.Split("Get-FileHash")[1].Split("Log 'ok'")[1]);
    }
}
