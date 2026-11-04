using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace OpsisNewsCrawler.Core.Tests;

/// <summary>
/// What a release must not carry and must carry: no trace of the machine it was built on (local folders, build-agent folders, the person's e-mail address and
/// user name) in the program files, in the tracked files or in the portable downloads, and the same publisher / product information in every exe and dll.
/// </summary>
public class ReleaseHygieneTests
{
    // Forbidden text, compared without regard to case, as UTF-8 / ASCII and as UTF-16 (the form .NET stores strings in).
    private static readonly string[] Forbidden =
    {
        @"C:\Users\", @"C:\w\", @"D:\a\", "gstrj", "LOGISTIRIO", "giorstergiopoulos@gmail", @"\Log9"
    };

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any()) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    internal static IEnumerable<string> FindTraces(byte[] data)
    {
        // The .NET apphost (the exe stub that Microsoft ships) carries the build-agent path of the .NET runtime build ("D:\_work\...apphost.pdb"): not ours to remove.
        var microsoftApphost = Contains(data, Encoding.ASCII.GetBytes(@"\corehost\apphost\"));
        foreach (var word in Forbidden)
        {
            if (microsoftApphost && word == @"D:\a\") continue;
            foreach (var encoded in new[] { Encoding.UTF8.GetBytes(word), Encoding.Unicode.GetBytes(word) })
                if (Contains(data, encoded)) { yield return word; break; }
        }
    }

    private static bool Contains(byte[] data, byte[] pattern)
    {
        // case-insensitive for ASCII letters
        for (var i = 0; i + pattern.Length <= data.Length; i++)
        {
            var j = 0;
            for (; j < pattern.Length; j++)
            {
                var a = data[i + j]; var b = pattern[j];
                if (a == b) continue;
                if (a is >= (byte)'A' and <= (byte)'Z') a += 32;
                if (b is >= (byte)'A' and <= (byte)'Z') b += 32;
                if (a != b) break;
            }
            if (j == pattern.Length) return true;
        }
        return false;
    }

    [Fact]
    public void TheDetectorFindsTracesInBothEncodings()
    {
        Assert.Contains(@"C:\Users\", FindTraces(Encoding.UTF8.GetBytes(@"x C:\users\someone y")));
        Assert.Contains(@"C:\w\", FindTraces(Encoding.Unicode.GetBytes(@"build C:\W\repo")));
        Assert.Empty(FindTraces(Encoding.UTF8.GetBytes(@"/src/Program.cs and %LOCALAPPDATA%")));
        Assert.Empty(FindTraces(Encoding.UTF8.GetBytes(@"D:\a\_work\1\s\src\runtime\artifacts\obj\win-x64.Release\corehost\apphost\standalone\apphost.pdb")));   // Microsoft's own apphost stub
        Assert.Contains(@"D:\a\", FindTraces(Encoding.UTF8.GetBytes(@"D:\a\1\s\OpsisNewsCrawler.pdb")));
    }

    [Fact]
    public void TheBuiltProgramFilesCarryNoTraceOfTheBuildMachine()
    {
        var root = Root();
        var found = new List<string>();
        var scanned = 0;
        foreach (var dir in Directory.GetDirectories(Path.Combine(root, "src")))
        {
            var bin = Path.Combine(dir, "bin", "Release");
            if (!Directory.Exists(bin)) continue;
            foreach (var file in Directory.EnumerateFiles(bin, "*", SearchOption.AllDirectories).Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            {
                var name = Path.GetFileName(file);
                if (!name.StartsWith("OpsisNewsCrawler", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("Opsis.UI", StringComparison.OrdinalIgnoreCase)) continue;   // third-party files are not ours to change
                scanned++;
                foreach (var trace in FindTraces(File.ReadAllBytes(file))) found.Add($"{Path.GetRelativePath(root, file)}: {trace}");
            }
        }
        Assert.True(found.Count == 0, "traces in program files:\n" + string.Join("\n", found));
        Assert.True(scanned > 0 || !Directory.Exists(Path.Combine(root, "src", "OpsisNewsCrawler.App", "bin")), "no program file was scanned");
    }

    [Fact]
    public void TheCommittedBinariesCarryNoTrace()
    {
        var root = Root();
        var found = new List<string>();
        var files = Directory.EnumerateFiles(Path.Combine(root, "libs"), "*.dll")
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.dll", SearchOption.AllDirectories).Where(f => f.Contains(Path.DirectorySeparatorChar + "reference" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)));
        foreach (var file in files)
            foreach (var trace in FindTraces(File.ReadAllBytes(file))) found.Add($"{Path.GetRelativePath(root, file)}: {trace}");
        Assert.True(found.Count == 0, "traces in committed binaries:\n" + string.Join("\n", found));
    }

    [Fact]
    public void TheReleaseZipsCarryNoTraceAndNoSymbolFiles()
    {
        var releases = Path.Combine(Root(), "release");
        if (!Directory.Exists(releases)) return;
        var found = new List<string>();
        var zips = Directory.EnumerateFiles(releases, "*.zip", SearchOption.AllDirectories).OrderByDescending(f => f).Take(1);   // the newest set: older published ones are history
        foreach (var zip in zips)
        {
            using var archive = ZipFile.OpenRead(zip);
            foreach (var entry in archive.Entries.Where(e => e.Length > 0))
            {
                if (entry.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) found.Add($"{Path.GetFileName(zip)}: symbol file {entry.FullName}");
                var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (ext is not (".dll" or ".exe" or ".json" or ".config" or ".txt" or ".md")) continue;
                if (!entry.Name.StartsWith("OpsisNewsCrawler", StringComparison.OrdinalIgnoreCase) && !entry.Name.StartsWith("Opsis.UI", StringComparison.OrdinalIgnoreCase) && ext is ".dll" or ".exe") continue;
                using var stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                foreach (var trace in FindTraces(buffer.ToArray())) found.Add($"{Path.GetFileName(zip)}!{entry.FullName}: {trace}");
            }
        }
        Assert.True(found.Count == 0, "traces in a portable zip:\n" + string.Join("\n", found));
    }

    [Fact]
    public void TheTrackedTextFilesNameNoLocalFoldersOrPersonalData()
    {
        var root = Root();
        var skipped = new[] { "bin", "obj", ".git", "release", "Output", ".vs" };
        var own = new[] { "ReleaseHygieneTests.cs", "security-scan.yml", "Arabic.isl" };   // the scan's own patterns; a third-party translation credit
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var parts = file.Substring(root.Length).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Any(p => skipped.Contains(p, StringComparer.OrdinalIgnoreCase)) || own.Contains(Path.GetFileName(file))) continue;
            if (!new[] { ".cs", ".md", ".json", ".iss", ".ps1", ".yml", ".csproj", ".bat", ".txt", ".props" }.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
            foreach (var trace in FindTraces(File.ReadAllBytes(file))) found.Add($"{Path.GetRelativePath(root, file)}: {trace}");
        }
        Assert.True(found.Count == 0, "personal data / local folders in tracked files:\n" + string.Join("\n", found));
    }

    [Fact]
    public void EveryProgramFileHasThePublisherAndProductInformation()
    {
        var root = Root();
        var checkedFiles = 0;
        foreach (var dir in Directory.GetDirectories(Path.Combine(root, "src")))
        {
            var bin = Path.Combine(dir, "bin", "Release");
            if (!Directory.Exists(bin)) continue;
            foreach (var file in Directory.EnumerateFiles(bin, "OpsisNewsCrawler*.*", SearchOption.AllDirectories).Where(f => f.EndsWith(".dll") || f.EndsWith(".exe")))
            {
                var info = FileVersionInfo.GetVersionInfo(file);
                var name = Path.GetFileName(file);
                Assert.True(info.CompanyName == "giorstergiopoulos-design", $"{name}: company '{info.CompanyName}'");
                Assert.False(string.IsNullOrWhiteSpace(info.ProductName), $"{name}: product");
                Assert.False(string.IsNullOrWhiteSpace(info.FileDescription), $"{name}: description");
                Assert.Contains("giorstergiopoulos-design", info.LegalCopyright ?? "");
                Assert.False(string.IsNullOrWhiteSpace(info.FileVersion), $"{name}: file version");
                checkedFiles++;
            }
        }
        Assert.True(checkedFiles > 0 || !Directory.Exists(Path.Combine(root, "src", "OpsisNewsCrawler.App", "bin")), "nothing was checked");
    }

    [Fact]
    public void TheInstallerScriptNamesThePublisherEverywhereAndRequiresInnoSetup7()
    {
        var iss = File.ReadAllText(Path.Combine(Root(), "OpsisNewsCrawler.iss"), Encoding.UTF8);
        Assert.Contains("#define MyPublisher \"giorstergiopoulos-design\"", iss);
        Assert.Contains("AppPublisher={#MyPublisher}", iss);
        foreach (var key in new[] { "AppPublisherURL=", "AppSupportURL=", "VersionInfoCompany={#MyPublisher}", "VersionInfoDescription=", "VersionInfoVersion=", "VersionInfoCopyright=", "VersionInfoProductName=" })
            Assert.Contains(key, iss);
        Assert.Contains("#if Ver < EncodeVer(7,0,0)", iss);
        Assert.Contains("#error", iss);
    }

    [Fact]
    public void TheLicenseIsTheStandardMitText()
    {
        var text = File.ReadAllText(Path.Combine(Root(), "LICENSE"), Encoding.UTF8).Replace("\r\n", "\n");
        Assert.StartsWith("MIT License\n\nCopyright (c) 2026 giorstergiopoulos-design\n\nPermission is hereby granted, free of charge, to any person obtaining a copy", text);
        Assert.Contains("THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND", text);
        Assert.Contains("OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE\nSOFTWARE.", text);
        var props = File.ReadAllText(Path.Combine(Root(), "Directory.Build.props"));
        Assert.Contains("<PackageLicenseExpression>MIT</PackageLicenseExpression>", props);
    }
}
