using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The update itself, modelled on GearWin's self-update: the setup comes only from this repository's GitHub releases, is verified against the
/// SHA-256 that GitHub publishes (or the .sha256 file next to it) and is deleted if it does not match; it is started only after the program
/// has ended (the installer replaces the running files), silently, and the program is opened again afterwards.
/// </summary>
internal static class UpdateInstaller
{
    private const long MaxSetupBytes = 120L * 1024 * 1024;

    /// <summary>Where the setup is kept until it runs (per user; the hash is checked again right before it starts).</summary>
    public static string UpdateDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpsisNewsCrawler", "update");

    /// <summary>Downloads and verifies. Returns the path and the SHA-256; throws <see cref="InvalidDataException"/> on a mismatch (the file is deleted),
    /// <see cref="InvalidOperationException"/> when there is nothing to verify against.</summary>
    public static async Task<(string Path, string Sha256)> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (update.SetupUrl is null || !UpdateChecker.IsTrustedDownloadUrl(update.SetupUrl)) throw new InvalidOperationException("untrusted-url");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OpsisNewsCrawler-Update");

        var expected = update.Sha256;
        if (expected is null && update.ChecksumUrl is not null && UpdateChecker.IsTrustedDownloadUrl(update.ChecksumUrl))
        {
            var text = await http.GetStringAsync(update.ChecksumUrl, ct).ConfigureAwait(false);
            var first = text.Trim().Split(' ', '\t', '\r', '\n')[0].ToLowerInvariant();
            if (first.Length == 64 && first.All(Uri.IsHexDigit)) expected = first;
        }
        if (expected is null) throw new InvalidOperationException("no-digest");

        Directory.CreateDirectory(UpdateDir);
        foreach (var old in Directory.GetFiles(UpdateDir)) { try { File.Delete(old); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        var path = Path.Combine(UpdateDir, "OpsisNewsCrawler-Setup.exe");

        using var response = await http.GetAsync(update.SetupUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? 0;
        if (total > MaxSetupBytes) throw new InvalidDataException("too-large");
        using var sha = SHA256.Create();
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                read += n;
                if (read > MaxSetupBytes) throw new InvalidDataException("too-large");
                await target.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                sha.TransformBlock(buffer, 0, n, null, 0);
                if (total > 0) progress?.Report(Math.Min(1.0, read / (double)total));
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        }
        var actual = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        if (actual != expected)
        {
            try { File.Delete(path); } catch (IOException) { }
            throw new InvalidDataException("hash-mismatch");
        }
        return (path, actual);
    }

    /// <summary>
    /// Starts a hidden helper that waits for this process to end, checks the hash of the file once more, runs the setup silently and
    /// (standalone only) opens the program again. The caller must end the process right after this call.
    /// </summary>
    public static void LaunchAfterExit(string setupPath, string sha256, bool hostedByOpsis)
    {
        static string Q(string s) => "'" + s.Replace("'", "''") + "'";
        var exe = Environment.ProcessPath ?? "";
        var args = hostedByOpsis
            ? "'/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/MODE=integrated'"
            : "'/SILENT','/SUPPRESSMSGBOXES','/NORESTART','/CLOSEAPPLICATIONS','/MODE=separate'";
        var script = new StringBuilder()
            .AppendLine("$ErrorActionPreference = 'Stop'")
            .AppendLine($"try {{ Wait-Process -Id {Environment.ProcessId} -Timeout 120 -ErrorAction SilentlyContinue }} catch {{ }}")
            .AppendLine($"if ((Get-FileHash -LiteralPath {Q(setupPath)} -Algorithm SHA256).Hash.ToLowerInvariant() -ne {Q(sha256)}) {{ exit 2 }}")
            .AppendLine($"$p = Start-Process -FilePath {Q(setupPath)} -ArgumentList {args} -Wait -PassThru")
            .Append(hostedByOpsis ? "" : $"if ($p.ExitCode -eq 0 -and (Test-Path -LiteralPath {Q(exe)})) {{ Start-Process -FilePath {Q(exe)} }}")
            .ToString();
        Process.Start(new ProcessStartInfo("powershell.exe")
        {
            ArgumentList = { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-Command", script },
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }
}
