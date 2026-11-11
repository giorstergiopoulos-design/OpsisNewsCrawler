using System.Text.RegularExpressions;
using OpsisNewsCrawler.Core;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>
/// The Pascal part of the setup cannot be run in a unit test, but its constants can be compared with the ones of the program. Until 0.19.0 the setup looked for the
/// per-user uninstall entry under a key with TWO opening braces (a typo: "{{" is the escape of [Setup] values, not of Pascal strings), never found it and never removed
/// the old copy: the installer end-to-end test on a clean machine showed it.
/// </summary>
public class InstallerScriptTests
{
    private static string Iss()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "OpsisNewsCrawler.iss");
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("OpsisNewsCrawler.iss not found above the test folder");
    }

    [Fact]
    public void ThePascalUninstallKeyIsTheOneOfTheProgramAndTheAppIdOfTheSetup()
    {
        var iss = Iss();
        var constant = Regex.Match(iss, @"OldUninstallKey\s*=\s*'([^']+)'").Groups[1].Value;
        Assert.Equal(InstallMigration.UninstallKey, constant);
        Assert.DoesNotContain("{{", constant);
        var appId = Regex.Match(iss, @"^AppId=\{\{([0-9A-Fa-f\-]+)\}", RegexOptions.Multiline).Groups[1].Value;
        Assert.Equal("{" + appId + "}_is1", InstallMigration.UninstallKey.Split('\\').Last());
    }

    [Fact]
    public void NoPascalStringOfTheScriptCarriesADoubleBrace()
    {
        // inside [Code] only the ISPP form "{{#Name}}" (a brace, an ISPP value, a brace) is valid; a plain "{{" in a string never matches anything
        var code = Iss().Split("[Code]", 2)[1];
        foreach (Match m in Regex.Matches(code, @"'[^'\r\n]*\{\{[^#][^'\r\n]*'"))
            Assert.Fail("double brace in a Pascal string: " + m.Value);
    }

    [Fact]
    public void TheSetupRemovesTheStartUpValuesAtUninstallAndOfAnOldCopy()
    {
        var iss = Iss();
        Assert.Contains("RegDeleteValue(HKCU, 'Software\\Microsoft\\Windows\\CurrentVersion\\Run', '{#MyAppName}')", iss);
        Assert.Contains("usPostUninstall", iss);
        Assert.Matches(@"(?s)usPostUninstall.*RegDeleteValue\(HKCU", iss);
        Assert.Contains("Root: HKLM; Subkey: \"Software\\Microsoft\\Windows\\CurrentVersion\\Run\"; ValueName: \"{#MyAppName}\"; Flags: deletevalue", iss);   // the machine-wide value of 0.18.x
        Assert.DoesNotContain("Root: HKA; Subkey: \"Software\\Microsoft\\Windows\\CurrentVersion\\Run\"", iss);                                              // and none is written any more
    }
}

public class InstallerNoScriptEngineTests
{
    [Fact]
    public void TheInstallerOfTheProgramStartsNoScriptEngineAndEndsTheOldProgramWithTaskkill()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any()) dir = dir.Parent;
        var iss = System.IO.File.ReadAllText(Path.Combine(dir!.FullName, "OpsisNewsCrawler.iss"), System.Text.Encoding.UTF8);
        // a 32-bit PowerShell cannot read the path of a 64-bit process, so the old per-user program was never ended and the removal failed (found by installer-e2e)
        Assert.DoesNotContain("powershell.exe", iss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WindowsPowerShell", iss, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("taskkill.exe", iss);
        Assert.Contains("Old per-user copy", iss);   // the Inno log says what happened
    }
}
