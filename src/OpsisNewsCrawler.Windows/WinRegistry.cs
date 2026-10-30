using Microsoft.Win32;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The real registry behind <see cref="IUserRegistry"/>. With <paramref name="isolated"/> every path is moved under
/// <see cref="TestIsolation.IsolatedRoot"/> (HKCU only): probes, snapshots and audits then read and write a private copy and the user's real
/// keys (start-up entry, uninstall entries) are never touched.
/// </summary>
public sealed class WinRegistry : IUserRegistry
{
    private readonly bool isolated;

    public WinRegistry(bool isolated) => this.isolated = isolated;

    /// <summary>The registry of this run: isolated when the command line is a probe (see <see cref="TestIsolation"/>), real otherwise.</summary>
    public static IUserRegistry Current { get; private set; } = new WinRegistry(false);

    public static void Configure(IEnumerable<string> args) => Current = new WinRegistry(TestIsolation.ShouldIsolate(args));

    public bool IsIsolated => isolated;

    private (RegistryKey Root, string Path) Map(UserHive hive, string path)
    {
        if (isolated) return (Registry.CurrentUser, TestIsolation.IsolatedRoot + "\\" + hive + "\\" + path);
        return (hive == UserHive.CurrentUser ? Registry.CurrentUser : Registry.LocalMachine, path);
    }

    public string? GetString(UserHive hive, string path, string name)
    {
        try
        {
            var (root, p) = Map(hive, path);
            using var key = root.OpenSubKey(p);
            return key?.GetValue(name) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return null; }
    }

    public void SetString(UserHive hive, string path, string name, string value)
    {
        try
        {
            var (root, p) = Map(hive, path);
            using var key = root.CreateSubKey(p);
            key?.SetValue(name, value);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
    }

    public void DeleteValue(UserHive hive, string path, string name)
    {
        try
        {
            var (root, p) = Map(hive, path);
            using var key = root.OpenSubKey(p, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
    }

    public bool KeyExists(UserHive hive, string path)
    {
        try
        {
            var (root, p) = Map(hive, path);
            using var key = root.OpenSubKey(p);
            return key is not null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return false; }
    }

    public void DeleteKey(UserHive hive, string path)
    {
        try
        {
            var (root, p) = Map(hive, path);
            root.DeleteSubKeyTree(p, throwOnMissingSubKey: false);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException) { }
    }
}
