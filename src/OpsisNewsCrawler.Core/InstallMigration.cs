using System.Text;

namespace OpsisNewsCrawler.Core;

public enum UserHive { CurrentUser, LocalMachine }

/// <summary>
/// The small part of the registry the program touches (start-up entry, uninstall entries). Real access is in the Windows project; tests, probes,
/// snapshots and audits get <see cref="MemoryRegistry"/> or an isolated sub-tree, never the user's real keys.
/// </summary>
public interface IUserRegistry
{
    string? GetString(UserHive hive, string path, string name);
    void SetString(UserHive hive, string path, string name, string value);
    void DeleteValue(UserHive hive, string path, string name);
    bool KeyExists(UserHive hive, string path);
    void DeleteKey(UserHive hive, string path);
}

/// <summary>An in-memory registry for tests and for runs that must never touch the real one.</summary>
public sealed class MemoryRegistry : IUserRegistry
{
    private readonly Dictionary<string, Dictionary<string, string>> keys = new(StringComparer.OrdinalIgnoreCase);

    private static string Id(UserHive hive, string path) => hive + "\\" + path.Trim('\\');

    public string? GetString(UserHive hive, string path, string name) =>
        keys.TryGetValue(Id(hive, path), out var values) && values.TryGetValue(name, out var v) ? v : null;

    public void SetString(UserHive hive, string path, string name, string value)
    {
        if (!keys.TryGetValue(Id(hive, path), out var values)) keys[Id(hive, path)] = values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        values[name] = value;
    }

    public void DeleteValue(UserHive hive, string path, string name)
    {
        if (keys.TryGetValue(Id(hive, path), out var values)) values.Remove(name);
    }

    public bool KeyExists(UserHive hive, string path) => keys.Keys.Any(k => k.Equals(Id(hive, path), StringComparison.OrdinalIgnoreCase) || k.StartsWith(Id(hive, path) + "\\", StringComparison.OrdinalIgnoreCase));

    public void DeleteKey(UserHive hive, string path)
    {
        var id = Id(hive, path);
        foreach (var k in keys.Keys.Where(k => k.Equals(id, StringComparison.OrdinalIgnoreCase) || k.StartsWith(id + "\\", StringComparison.OrdinalIgnoreCase)).ToList()) keys.Remove(k);
    }
}

/// <summary>
/// Which runs may touch the real registry. A start with its own configuration file or with a test, snapshot, audit or tool switch is a probe:
/// its "start with Windows" and installation entries go to an isolated place. (Until 0.18.5 the settings audit rewrote the real
/// <c>Run</c> value with the path of the build under test; this rule is what stops that.)
/// </summary>
public static class TestIsolation
{
    public static bool ShouldIsolate(IEnumerable<string> args) =>
        args.Any(a => a is "--config" || a.StartsWith("--snapshot", StringComparison.Ordinal) || a.StartsWith("--audit", StringComparison.Ordinal) ||
                      a.StartsWith("--test", StringComparison.Ordinal) || a.StartsWith("--make", StringComparison.Ordinal) || a is "--welcome" or "--suite" or "--portfolio");

    /// <summary>The sub-tree used instead of the real keys (under the program's own software key, removed by deleting that key).</summary>
    public const string IsolatedRoot = @"Software\OpsisNewsCrawler\IsolatedTest";
}

/// <summary>One installed copy of the program found in the registry.</summary>
public sealed record InstalledCopy(bool PerUser, string Directory, string Version, string UninstallExe);

/// <summary>
/// Decisions of the move from a per-user installation (%LocalAppData%\Programs, HKCU) to the machine-wide one (Program Files, HKLM), written
/// as pure functions so they can be tested with <see cref="MemoryRegistry"/>. The installer (Inno script) does the same steps in Pascal.
/// </summary>
public static class InstallMigration
{
    /// <summary>The uninstall key of the program (the same AppId in both scopes).</summary>
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{B7E3C9A2-5D41-4F68-9A0E-3C1D7F52A8B4}_is1";
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValue = "Opsis News Crawler";

    public static IReadOnlyList<InstalledCopy> Find(IUserRegistry registry)
    {
        var list = new List<InstalledCopy>();
        foreach (var (hive, perUser) in new[] { (UserHive.CurrentUser, true), (UserHive.LocalMachine, false) })
        {
            if (!registry.KeyExists(hive, UninstallKey)) continue;
            var dir = registry.GetString(hive, UninstallKey, "InstallLocation") ?? "";
            var uninstall = Unquote(registry.GetString(hive, UninstallKey, "UninstallString") ?? "");
            if (dir.Length == 0 && uninstall.Length > 0) dir = Path.GetDirectoryName(uninstall) ?? "";
            list.Add(new InstalledCopy(perUser, dir.TrimEnd('\\'), registry.GetString(hive, UninstallKey, "DisplayVersion") ?? "?", uninstall));
        }
        return list;
    }

    /// <summary>The older per-user copy that is still there although a machine-wide copy exists (null = nothing to do).</summary>
    public static InstalledCopy? DuplicatePerUserCopy(IReadOnlyList<InstalledCopy> copies) =>
        copies.Any(c => !c.PerUser) ? copies.FirstOrDefault(c => c.PerUser) : null;

    /// <summary>
    /// The start-up value is wrong when it points anywhere but into one of the program folders given (Program Files). Returns a short English reason, or null when it is fine
    /// or not set. A value that points to a build folder, to the per-user folder or to nowhere must be removed.
    /// </summary>
    public static string? AutostartProblem(string? runValue, IEnumerable<string> allowedDirectories)
    {
        if (string.IsNullOrWhiteSpace(runValue)) return null;
        var path = Unquote(runValue.Trim());
        var inside = allowedDirectories.Any(d => d.Length > 0 && path.StartsWith(d.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
        return inside ? null : "start-up entry points outside the program folder: " + path;
    }

    /// <summary>True when the process image path lies inside the folder of the old copy (only such a process may be ended by the migration).</summary>
    public static bool IsInside(string? exePath, string directory) =>
        !string.IsNullOrEmpty(exePath) && directory.Length > 0 && exePath.StartsWith(directory.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);

    /// <summary>The steps to take for an old per-user copy, in order. The runner performs them and checks the result of each.</summary>
    public static IReadOnlyList<string> RemovalPlan(InstalledCopy copy) => new[]
    {
        "end-processes-in:" + copy.Directory,
        "run-uninstaller:" + copy.UninstallExe,
        "verify-gone:key+folder",
        "force-clean-if-needed",
        "delete-run-value:HKCU"
    };

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.StartsWith('"')) { var end = s.IndexOf('"', 1); if (end > 0) return s[1..end]; }
        var space = s.IndexOf(" /", StringComparison.Ordinal);
        return space > 0 ? s[..space] : s;
    }

    /// <summary>Removes a stray start-up value (HKCU) when it does not point into the allowed folders; returns what was removed.</summary>
    public static string? CleanAutostart(IUserRegistry registry, IEnumerable<string> allowedDirectories)
    {
        var value = registry.GetString(UserHive.CurrentUser, RunKey, RunValue);
        if (AutostartProblem(value, allowedDirectories) is not { } problem) return null;
        registry.DeleteValue(UserHive.CurrentUser, RunKey, RunValue);
        return problem;
    }
}

/// <summary>The result of the last update attempt, written by the helper that runs the setup after the program ended and read at the next start.</summary>
public static class UpdateResultLog
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpsisNewsCrawler", "update", "last-update.log");

    public sealed record Entry(DateTimeOffset At, string Result, int ExitCode, string Version, string Message)
    {
        public bool Failed => !string.Equals(Result, "ok", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>"2026-10-30T10:00:00+02:00|failed|1223|0.18.6|text": the format the helper script appends (the text never contains a bar).</summary>
    public static Entry? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var parts = line.Split('|', 5);
        if (parts.Length < 4 || !DateTimeOffset.TryParse(parts[0], out var at) || !int.TryParse(parts[2], out var code)) return null;
        return new Entry(at, parts[1], code, parts[3], parts.Length > 4 ? parts[4] : "");
    }

    /// <summary>The last entry of the log, or null.</summary>
    public static Entry? Last(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (!File.Exists(path)) return null;
            return File.ReadLines(path, Encoding.UTF8).Select(Parse).LastOrDefault(e => e is not null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>A failed last attempt that was not shown yet; it is marked as shown (the log keeps the history, a marker file keeps the state).</summary>
    public static Entry? TakeUnseenFailure(string? path = null)
    {
        path ??= DefaultPath;
        var last = Last(path);
        if (last is null || !last.Failed) return null;
        var marker = path + ".seen";
        try
        {
            if (File.Exists(marker) && File.ReadAllText(marker).Trim() == last.At.ToString("O")) return null;
            File.WriteAllText(marker, last.At.ToString("O"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        return last;
    }
}

/// <summary>Builds the PowerShell of the update helper (pure text, so the rules can be tested): wait, check the hash, run the setup elevated, log the result, reopen.</summary>
public static class UpdateHelperScript
{
    public static string Quote(string s) => "'" + s.Replace("'", "''") + "'";

    public static string Build(int waitForPid, string setupPath, string sha256, string logPath, string version, string setupArguments, string? reopenExe)
    {
        var q = Quote;
        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine($"$log = {q(logPath)}");
        sb.AppendLine($"function Log($result, $code, $text) {{ try {{ New-Item -ItemType Directory -Force -Path (Split-Path -Parent $log) | Out-Null; Add-Content -LiteralPath $log -Value ((Get-Date -Format o) + '|' + $result + '|' + $code + '|' + {q(version)} + '|' + ($text -replace '[|\\r\\n]', ' ')) }} catch {{ }} }}");
        sb.AppendLine("try {");
        sb.AppendLine($"  try {{ Wait-Process -Id {waitForPid} -Timeout 120 -ErrorAction SilentlyContinue }} catch {{ }}");
        sb.AppendLine($"  if ((Get-FileHash -LiteralPath {q(setupPath)} -Algorithm SHA256).Hash.ToLowerInvariant() -ne {q(sha256)}) {{ Log 'failed' 2 'the downloaded setup does not match its SHA-256'; exit 2 }}");
        // The setup is machine-wide (administrator): ask Windows for the elevation explicitly; a refused UAC prompt is a result, not silence.
        sb.AppendLine($"  try {{ $p = Start-Process -FilePath {q(setupPath)} -ArgumentList {setupArguments} -Verb RunAs -Wait -PassThru }} catch {{ Log 'failed' 1223 ('the setup could not be started (UAC refused?): ' + $_.Exception.Message); exit 3 }}");
        sb.AppendLine("  if ($p.ExitCode -ne 0) { Log 'failed' $p.ExitCode 'the setup ended with an error'; exit 4 }");
        sb.AppendLine("  Log 'ok' 0 ''");
        if (!string.IsNullOrEmpty(reopenExe)) sb.AppendLine($"  if (Test-Path -LiteralPath {q(reopenExe)}) {{ Start-Process -FilePath {q(reopenExe)} }}");
        sb.AppendLine("} catch { Log 'failed' 5 $_.Exception.Message; exit 5 }");
        return sb.ToString();
    }
}
