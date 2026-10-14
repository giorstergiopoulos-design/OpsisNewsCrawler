using System.Security.Cryptography;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>Downloads the setup of a newer release, checks its SHA-256 against the one published next to it, and starts it.</summary>
internal static class UpdateInstaller
{
    public enum Result { Started, NoSetup, ChecksumMissing, ChecksumMismatch, Failed }

    private const long MaxSetupBytes = 120L * 1024 * 1024;

    public static async Task<Result> DownloadAndStartAsync(UpdateInfo update, CancellationToken ct = default)
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
