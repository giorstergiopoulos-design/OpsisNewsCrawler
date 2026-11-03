using System.Runtime.InteropServices;
using OpsisNewsCrawler.Core;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// The quick readings behind the performance tier: logical processors, physical memory, whether a real graphics adapter drives the screen (a
/// software renderer such as "Microsoft Basic Render Driver" counts as none), battery and battery saver. A few Win32 calls, no WMI, nothing slow.
/// </summary>
internal static class HardwareDetector
{
    public static HardwareSpecs Detect()
    {
        var cores = Math.Max(1, Environment.ProcessorCount);
        var ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
        var onBattery = false;
        var saver = false;
        try
        {
            if (GetSystemPowerStatus(out var status))
            {
                onBattery = status.ACLineStatus == 0 && status.BatteryFlag != 128;   // 128 = no battery (a desktop PC)
                saver = (status.SystemStatusFlag & 1) != 0;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        return new HardwareSpecs(cores, Math.Round(ram, 1), HasRealGpu(), onBattery, saver);
    }

    /// <summary>True when the adapter of the primary display is not a software renderer or a remote / virtual display driver.</summary>
    internal static bool HasRealGpu()
    {
        try
        {
            var device = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
            for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++)
            {
                if ((device.StateFlags & 4) == 0) continue;          // DISPLAY_DEVICE_PRIMARY_DEVICE
                return !IsSoftwareAdapter(device.DeviceString);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException) { }
        return true;                                                  // unknown: do not punish the PC
    }

    internal static bool IsSoftwareAdapter(string? name) =>
        !string.IsNullOrEmpty(name) && new[] { "Basic Render", "Basic Display", "Remote Display", "RDP", "Hyper-V Video", "Parallels Display", "VMware SVGA", "VirtualBox Graphics", "Citrix" }
            .Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase));

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice displayDevice, uint flags);
}
