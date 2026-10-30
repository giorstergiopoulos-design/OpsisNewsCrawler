using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// "Start with Windows" of the standalone program: one per-user Run value (the user's own choice; the machine-wide setup writes no start-up entry
/// itself). Goes through <see cref="WinRegistry"/>, so a probe (audit, snapshot, own --config) writes into an isolated copy, never into the real keys.
/// </summary>
internal static class StartupRegistration
{
    public static bool IsEnabled() =>
        !string.IsNullOrEmpty(WinRegistry.Current.GetString(UserHive.CurrentUser, InstallMigration.RunKey, InstallMigration.RunValue));

    public static void Set(bool enabled)
    {
        var registry = WinRegistry.Current;
        if (enabled)
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe)) registry.SetString(UserHive.CurrentUser, InstallMigration.RunKey, InstallMigration.RunValue, "\"" + exe + "\"");
        }
        else registry.DeleteValue(UserHive.CurrentUser, InstallMigration.RunKey, InstallMigration.RunValue);
    }

    /// <summary>The program folders a start-up entry may point into: the folder of this program when it is in Program Files.</summary>
    public static IReadOnlyList<string> ProgramFilesDirectories()
    {
        var list = new List<string>();
        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            var root = Environment.GetFolderPath(folder);
            if (root.Length > 0) list.Add(Path.Combine(root, "Opsis News Crawler"));
        }
        return list;
    }

    /// <summary>
    /// A start-up value that points to a build folder, to the old per-user folder or to nowhere is removed (the user's choice "start with Windows" is then
    /// re-written for this program when it runs from Program Files). Returns what was found, or null.
    /// </summary>
    public static string? RepairIfWrong()
    {
        var registry = WinRegistry.Current;
        var value = registry.GetString(UserHive.CurrentUser, InstallMigration.RunKey, InstallMigration.RunValue);
        var dirs = ProgramFilesDirectories();
        if (InstallMigration.AutostartProblem(value, dirs) is not { } problem) return null;
        registry.DeleteValue(UserHive.CurrentUser, InstallMigration.RunKey, InstallMigration.RunValue);
        var exe = Environment.ProcessPath;
        // The value was the user's wish to start with Windows: keep the wish, pointed at the right program, when we run from Program Files.
        if (!string.IsNullOrEmpty(exe) && InstallMigration.AutostartProblem("\"" + exe + "\"", dirs) is null) Set(true);
        return problem;
    }
}
