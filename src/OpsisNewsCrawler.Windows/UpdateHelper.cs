using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The helper of an update: the program itself, started with <c>--apply-update</c> (no window, no script engine). It waits for the old process to end,
/// checks the SHA-256 of the downloaded setup once more, runs the setup elevated (the installation is machine-wide, so Windows asks for the administrator;
/// a refused prompt is a logged result, not silence), writes the result to <c>last-update.log</c> and, standalone, opens the program again.
/// </summary>
public static class UpdateHelper
{
    public const string Switch = "--apply-update";

    /// <summary>Arguments after the switch: pid, setup path, sha256, log path, version, "hosted" | "standalone", program to reopen (optional).</summary>
    public static int Run(string[] args)
    {
        if (args.Length < 6 || !int.TryParse(args[0], out var pid)) return 1;
        var (setup, sha256, log, version, mode) = (args[1], args[2].ToLowerInvariant(), args[3], args[4], args[5]);
        var hosted = mode == "hosted";
        var reopen = args.Length > 6 && !hosted ? args[6] : null;
        try
        {
            try { using var old = Process.GetProcessById(pid); old.WaitForExit(120000); }
            catch (ArgumentException) { }   // already gone
            if (!File.Exists(setup) || !string.Equals(Hash(setup), sha256, StringComparison.Ordinal))
            {
                UpdateResultLog.Append(log, "failed", 2, version, "the downloaded setup does not match its SHA-256");
                return 2;
            }
            Process? p;
            try { p = Process.Start(new ProcessStartInfo(setup, UpdateSetupArguments.Build(hosted)) { UseShellExecute = true, Verb = "runas" }); }
            catch (Win32Exception ex)
            {
                UpdateResultLog.Append(log, "failed", 1223, version, "the setup could not be started (UAC refused?): " + ex.Message);
                return 3;
            }
            if (p is null) { UpdateResultLog.Append(log, "failed", 1, version, "the setup could not be started"); return 3; }
            p.WaitForExit();
            if (p.ExitCode != 0) { UpdateResultLog.Append(log, "failed", p.ExitCode, version, "the setup ended with an error"); return 4; }
            UpdateResultLog.Append(log, "ok", 0, version, "");
            if (!string.IsNullOrEmpty(reopen) && File.Exists(reopen)) Process.Start(new ProcessStartInfo(reopen) { UseShellExecute = true });
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            UpdateResultLog.Append(log, "failed", 5, version, ex.Message);
            return 5;
        }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// A copy of the program folder (without the plugin copy for Opsis) in <paramref name="destinationRoot"/>\helper, from which the helper runs. The helper must NOT run from
    /// the installed folder: the setup replaces the files of that folder, and a running helper would be a program that locks (and that the installer would try to close).
    /// Returns the exe of the copy, or null when it could not be made (the caller then runs the helper in place).
    /// </summary>
    public static string? PrepareCopy(string exe, string destinationRoot)
    {
        try
        {
            var source = Path.GetDirectoryName(exe);
            if (source is null) return null;
            var target = Path.Combine(destinationRoot, "helper");
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            CopyFolder(source, target, skipTopLevel: "plugins");
            var copy = Path.Combine(target, Path.GetFileName(exe));
            return File.Exists(copy) ? copy : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void CopyFolder(string source, string target, string? skipTopLevel)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            if (new FileInfo(file).Length <= 120L * 1024 * 1024) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(dir);
            if (skipTopLevel is not null && string.Equals(name, skipTopLevel, StringComparison.OrdinalIgnoreCase)) continue;
            CopyFolder(dir, Path.Combine(target, name), null);
        }
    }

    /// <summary>The program that can act as the helper: this exe when standalone; inside Opsis the crawler's own exe, found above the plugin folder (null when it is not there).</summary>
    public static string? HelperExecutable()
    {
        var own = Environment.ProcessPath;
        if (own is not null && string.Equals(Path.GetFileName(own), "OpsisNewsCrawler.exe", StringComparison.OrdinalIgnoreCase)) return own;
        var dir = Path.GetDirectoryName(typeof(UpdateHelper).Assembly.Location);
        for (var i = 0; i < 4 && dir is not null; i++, dir = Path.GetDirectoryName(dir))
            if (File.Exists(Path.Combine(dir, "OpsisNewsCrawler.exe"))) return Path.Combine(dir, "OpsisNewsCrawler.exe");
        return null;
    }
}
