using System.Reflection;
using Xunit;

namespace OpsisNewsCrawler.Plugin.Tests;

/// <summary>The signature check of downloaded installers uses the Windows API (no script engine); it must accept a signed Windows file and reject an unsigned one.</summary>
public class AuthenticodeTests
{
    private static string? Check(string file)
    {
        var type = typeof(OpsisNewsCrawler.OpsisPlugin.OpsisNewsCrawlerPlugin).Assembly.GetReferencedAssemblies().Select(a => Assembly.Load(a))
            .First(a => a.GetName().Name == "OpsisNewsCrawler.Windows").GetType("OpsisNewsCrawler.Windows.Authenticode", throwOnError: true)!;
        return (string?)type.GetMethod("ValidSignerSubject", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { file });
    }

    [Fact]
    public void ASignedWindowsFileHasAValidSigner()
    {
        // dotnet.exe carries an embedded Microsoft signature (many Windows files are only signed through catalogs, which this check does not read)
        var file = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
        if (!File.Exists(file)) return;   // not found on this machine
        var subject = Check(file);
        Assert.NotNull(subject);
        Assert.Contains("Microsoft", subject);
    }

    [Fact]
    public void AnUnsignedFileHasNoSigner() => Assert.Null(Check(typeof(AuthenticodeTests).Assembly.Location));

    [Fact]
    public void AMissingFileHasNoSigner() => Assert.Null(Check(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".exe")));
}
