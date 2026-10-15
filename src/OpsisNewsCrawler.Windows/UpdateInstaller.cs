using System.Security.Cryptography;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>Downloads the setup of a newer release, checks its SHA-256 against the one published next to it, and starts it.</summary>
internal static class UpdateInstaller
{
    public enum Result { Started, NoSetup, ChecksumMissing, ChecksumMismatch, Failed }

    private const long MaxSetupBytes = 120L * 1024 * 1024;

    /// <param name="waitForExitOfHost">Inside Opsis its files cannot be replaced while it runs: the verified setup then starts silently, in the integrated mode, once the host process has ended.</param>
    public static async Task<Result> DownloadAndStartAsync(UpdateInfo update, bool waitForExitOfHost = false, CancellationToken ct = default)
    {
        if (update.SetupUrl is null) return Result.NoSetup;
        if (update.ChecksumUrl is null) return Result.ChecksumMissing; // never run something that cannot be verified
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5), MaxResponseContentBufferSize = MaxSetupBytes };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler-Update");

            var sumText = await http.GetStringAsync(update.ChecksumUrl, ct).ConfigureAwait(false);
            var expected = sumText.Trim().Split(' ', '\t', '\r', '\n')[0].ToLowerInvariant();
            if (expected.Length != 64 || !expected.All(Uri.IsHexDigit)) return Result.ChecksumMissing;

            var folder = Path.Combine(Path.GetTempPath(), "OpsisNewsCrawler-update-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "OpsisNewsCrawler-Setup.exe");
            var bytes = await http.GetByteArrayAsync(update.SetupUrl, ct).ConfigureAwait(false);
            if (bytes.LongLength > MaxSetupBytes) return Result.Failed;
            var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (actual != expected) return Result.ChecksumMismatch;

            await File.WriteAllBytesAsync(file, bytes, ct).ConfigureAwait(false);
            if (waitForExitOfHost)
            {
                // A hidden helper waits for this process (Opsis) to end, then runs the verified setup; the path is our own temp folder.
                var pid = Environment.ProcessId;
                var command = $"Wait-Process -Id {pid} -ErrorAction SilentlyContinue; Start-Process -FilePath '{file.Replace("'", "''")}' -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/MODE=integrated' -Wait";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell.exe")
                {
                    ArgumentList = { "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden", "-Command", command },
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                return Result.Started;
            }

            // The normal wizard (not silent): the user sees and confirms the update; setup asks to close the running bar.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true });
            return Result.Started;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return Result.Failed;
        }
    }
}
