using System.Diagnostics;
using Microsoft.Win32;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The components the program needs next to itself: the .NET 10 Desktop Runtime (it is running, so it is there) and the Microsoft Edge
/// WebView2 Runtime (live video preview, weather map). Detection, and installing a missing one from Microsoft after checking its signature.
/// </summary>
internal static class Dependencies
{
    private const string WebView2Url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
    private const string WebView2Guid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    public static string DotNetVersion => Environment.Version.ToString();

    /// <summary>The installed WebView2 Runtime version, or null when it is missing.</summary>
    public static string? WebView2Version()
    {
        foreach (var (root, path) in new[]
                 {
                     (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\" + WebView2Guid),
                     (Registry.LocalMachine, @"SOFTWARE\Microsoft\EdgeUpdate\Clients\" + WebView2Guid),
                     (Registry.CurrentUser, @"Software\Microsoft\EdgeUpdate\Clients\" + WebView2Guid)
                 })
        {
            try
            {
                using var key = root.OpenSubKey(path);
                if (key?.GetValue("pv") is string v && v.Length > 0 && v != "0.0.0.0") return v;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
            }
        }
        return null;
    }

    public enum InstallResult { Installed, DownloadFailed, NotSignedByMicrosoft, InstallFailed }

    /// <summary>Downloads the WebView2 bootstrapper, runs it only when it carries a valid Microsoft signature, and installs silently for the user.</summary>
    public static async Task<InstallResult> InstallWebView2Async(CancellationToken ct = default)
    {
        string file;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3), MaxResponseContentBufferSize = 16 * 1024 * 1024 };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler-Dependencies");
            var bytes = await http.GetByteArrayAsync(WebView2Url, ct).ConfigureAwait(false);
            var folder = Path.Combine(Path.GetTempPath(), "OpsisNewsCrawler-deps-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            file = Path.Combine(folder, "MicrosoftEdgeWebview2Setup.exe");
            await File.WriteAllBytesAsync(file, bytes, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            return InstallResult.DownloadFailed;
        }

        if (!await SignedByMicrosoftAsync(file).ConfigureAwait(false))
        {
            try { Directory.Delete(Path.GetDirectoryName(file)!, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return InstallResult.NotSignedByMicrosoft;
        }
        try
        {
            using var process = Process.Start(new ProcessStartInfo(file, "/silent /install") { UseShellExecute = false, CreateNoWindow = true });
            if (process is null) return InstallResult.InstallFailed;
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            var ok = process.ExitCode == 0 && WebView2Version() is not null;
            try { Directory.Delete(Path.GetDirectoryName(file)!, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return ok ? InstallResult.Installed : InstallResult.InstallFailed;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or OperationCanceledException)
        {
            return InstallResult.InstallFailed;
        }
    }

    /// <summary>True when the file has a valid Authenticode signature whose signer is Microsoft Corporation.</summary>
    private static async Task<bool> SignedByMicrosoftAsync(string file)
    {
        try
        {
            // The path travels in an environment variable, never inside the script text; the script only prints the subject of a VALID signature.
            var command = "$s = Get-AuthenticodeSignature -LiteralPath $env:OPSIS_SIGCHECK_FILE; if ($s.Status -eq 'Valid') { [Console]::Out.Write($s.SignerCertificate.Subject) } else { exit 1 }";
            var info = new ProcessStartInfo("powershell.exe")
            {
                ArgumentList = { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", command },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            info.Environment["OPSIS_SIGCHECK_FILE"] = file;
            using var ps = Process.Start(info);
            if (ps is null) return false;
            var subject = await ps.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await ps.WaitForExitAsync().ConfigureAwait(false);
            return ps.ExitCode == 0 && DownloadPolicy.IsMicrosoftSigner(subject);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
