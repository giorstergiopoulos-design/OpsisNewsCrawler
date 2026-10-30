using System.Diagnostics;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// What the program checks about its own installation when it starts (standalone only, never in a probe run): a failed last update is shown, a wrong
/// start-up entry is repaired, and an older per-user copy that is still installed next to the machine-wide one is offered for removal.
/// </summary>
public static class InstallChecks
{
    public static void Run(TickerHost host)
    {
        if (host.HostedByOpsis || (WinRegistry.Current is WinRegistry { IsIsolated: true })) return;
        try
        {
            // The setup writes the user's wish "Start with Windows" into first-run.json; the per-user Run value is written here, in the user's own context.
            if (host.Config.StartWithWindowsRequested) StartupRegistration.Set(true);
            if (UpdateResultLog.TakeUnseenFailure() is { } failed)
                MessageBox.Show(Loc.T("upd.lastFailed", failed.Version, $"{failed.Message} ({failed.ExitCode})", AboutTexts.Version), Loc.T("upd.confirmTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else if (UpdateResultLog.Last() is { Failed: false })
                TryDelete(UpdateResultLog.DefaultPath);          // a successful update needs no trace

            var own = Environment.ProcessPath ?? "";
            var machineWide = StartupRegistration.ProgramFilesDirectories().Any(d => InstallMigration.IsInside(own, d));
            if (!machineWide) return;
            if (StartupRegistration.RepairIfWrong() is { } repaired) Log.Write("Start-up entry repaired: " + repaired);

            if (InstallMigration.DuplicatePerUserCopy(InstallMigration.Find(WinRegistry.Current)) is not { } old) return;
            var asked = host.Config.DuplicateInstallAskedAt;
            if (asked is { } when && DateTimeOffset.Now - when < TimeSpan.FromDays(7)) return;
            host.Config.DuplicateInstallAskedAt = DateTimeOffset.Now;
            host.SaveConfig();
            if (MessageBox.Show(Loc.T("dup.body", old.Version, old.Directory), Loc.T("dup.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var problem = RemovePerUser(old);
            MessageBox.Show(problem is null ? Loc.T("dup.removed") : Loc.T("dup.failed", problem), Loc.T("dup.title"), MessageBoxButtons.OK, problem is null ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Log.Write($"install check failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); File.Delete(path + ".seen"); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Removes an older per-user copy: ends its processes (only images inside its own folder), runs its uninstaller silently, checks that the uninstall entry and the
    /// folder are gone (a few retries), cleans what is left by hand, and removes the per-user start-up value. Returns null when everything is gone, else a short reason.
    /// </summary>
    public static string? RemovePerUser(InstalledCopy copy)
    {
        var registry = WinRegistry.Current;
        foreach (var process in Process.GetProcessesByName("OpsisNewsCrawler"))
        {
            try
            {
                if (process.Id != Environment.ProcessId && InstallMigration.IsInside(process.MainModule?.FileName, copy.Directory))
                {
                    process.Kill();
                    process.WaitForExit(8000);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            finally { process.Dispose(); }
        }

        // The uninstaller must lie in the folder of the copy (a value from the registry is never run from anywhere else).
        if (InstallMigration.IsInside(copy.UninstallExe, copy.Directory) && File.Exists(copy.UninstallExe))
        {
            try
            {
                using var uninstall = Process.Start(new ProcessStartInfo(copy.UninstallExe, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = false, CreateNoWindow = true });
                uninstall?.WaitForExit(90_000);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }

        for (var attempt = 0; attempt < 5 && StillThere(registry, copy); attempt++) Thread.Sleep(1000);
        if (StillThere(registry, copy))
        {
            // Last resort for our own leftovers: the uninstall entry of the old copy and its folder (the settings in %AppData% are not touched).
            registry.DeleteKey(UserHive.CurrentUser, InstallMigration.UninstallKey);
            try { if (copy.Directory.Length > 0 && Directory.Exists(copy.Directory)) Directory.Delete(copy.Directory, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        registry.DeleteValue(UserHive.CurrentUser, InstallMigration.RunKey, InstallMigration.RunValue);
        if (StillThere(registry, copy)) return StillThere(registry, copy) && Directory.Exists(copy.Directory) ? copy.Directory : "uninstall entry";
        return null;
    }

    private static bool StillThere(IUserRegistry registry, InstalledCopy copy) =>
        registry.KeyExists(UserHive.CurrentUser, InstallMigration.UninstallKey) || (copy.Directory.Length > 0 && Directory.Exists(copy.Directory));
}
