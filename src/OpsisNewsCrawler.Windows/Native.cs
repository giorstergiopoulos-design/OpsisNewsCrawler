using System.Runtime.InteropServices;
using System.Text;

namespace OpsisNewsCrawler.Windows;

internal static class Native
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    private const uint ABM_GETSTATE = 0x00000004;
    private const uint ABS_AUTOHIDE = 0x0000001;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    // Windows that belong to the desktop / shell itself. Showing them must not pause the ticker.
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "SHELLDLL_DefView",
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow", "TaskListThumbnailWnd", "TaskListOverlayWnd"
    };

    /// <summary>
    /// True when the desktop (or one of our own / host windows) is in the foreground.
    /// Windows of the current process (ticker, settings, Opsis when hosted as a plugin) count as desktop.
    /// </summary>
    internal static bool IsDesktopForeground(IntPtr ownHandle, Func<IntPtr, bool>? isHostWindow = null)
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == ownHandle) return true;
        if (isHostWindow?.Invoke(fg) == true) return true;

        GetWindowThreadProcessId(fg, out var pid);
        if (pid == (uint)Environment.ProcessId) return true;

        if (fg == GetShellWindow()) return true;

        var name = new StringBuilder(256);
        var len = GetClassName(fg, name, name.Capacity);
        return len > 0 && ShellClasses.Contains(name.ToString(0, len));
    }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);

    private const int GWL_STYLE = -16;
    private const int WS_CAPTION = 0x00C00000;

    /// <summary>
    /// True when another program (video player, browser in F11, game, presentation) is shown fullscreen.
    /// The ticker must not be drawn on top of it.
    /// </summary>
    internal static bool IsFullscreenAppActive(IntPtr ownHandle, Func<IntPtr, bool>? isHostWindow = null)
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == ownHandle || fg == GetShellWindow()) return false;

        GetWindowThreadProcessId(fg, out var pid);
        // Windows of this process (Opsis when we are its plugin: DeskFlip, wallpaper preview, ...) only count when they
        // really cover the screen without a title bar; the notification-state API says nothing reliable about them.
        var ownProcess = pid == (uint)Environment.ProcessId;
        if (ownProcess && isHostWindow?.Invoke(fg) == true) return false;

        var name = new StringBuilder(256);
        var len = GetClassName(fg, name, name.Capacity);
        if (len > 0 && ShellClasses.Contains(name.ToString(0, len))) return false;

        // 2 = busy (fullscreen app), 3 = Direct3D fullscreen, 4 = presentation mode.
        if (!ownProcess && SHQueryUserNotificationState(out var qs) == 0 && qs is 2 or 3 or 4) return true;

        // Geometric check. A maximized window with an auto-hide taskbar also covers the monitor,
        // but it keeps its title bar; real fullscreen windows have none.
        if (!GetWindowRect(fg, out var r)) return false;
        if ((GetWindowLong(fg, GWL_STYLE) & WS_CAPTION) == WS_CAPTION) return false;
        var bounds = Screen.FromHandle(fg).Bounds;
        return r.Left <= bounds.Left && r.Top <= bounds.Top && r.Right >= bounds.Right && r.Bottom >= bounds.Bottom;
    }

    // ---- taskbar geometry ----------------------------------------------------------------
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string? title);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? title);

    internal static IntPtr GetTaskbarHandle() => FindWindow("Shell_TrayWnd", null);

    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    /// <summary>
    /// With auto-hide on, current Windows does not move the taskbar window (its rectangle stays the same): the desktop
    /// compositor "cloaks" it while it is hidden and uncloaks it while it is shown. This is the signal to follow.
    /// </summary>
    internal static bool IsTaskbarCloaked()
    {
        var h = GetTaskbarHandle();
        return h != IntPtr.Zero && DwmGetWindowAttribute(h, 14 /* DWMWA_CLOAKED */, out var cloaked, sizeof(int)) == 0 && cloaked != 0;
    }

    /// <summary>Notification area (clock and tray icons) of the primary taskbar.</summary>
    internal static Rectangle GetTrayNotifyRect()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        var notify = tray == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        if (notify == IntPtr.Zero || !GetWindowRect(notify, out var r)) return Rectangle.Empty;
        return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }

    /// <summary>Rectangle of the primary taskbar in screen coordinates (empty when not found).</summary>
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);

    /// <summary>DPI of the taskbar window and of the given window (0 when unknown), for the log.</summary>
    internal static (uint Taskbar, uint Own) DescribeDpi(IntPtr own)
    {
        try { return (GetDpiForWindow(FindWindow("Shell_TrayWnd", null)), GetDpiForWindow(own)); }
        catch (EntryPointNotFoundException) { return (0, 0); }
    }

    internal static Rectangle GetTaskbarRect()
    {
        var h = FindWindow("Shell_TrayWnd", null);
        if (h == IntPtr.Zero || !GetWindowRect(h, out var r)) return Rectangle.Empty;
        return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }

    /// <summary>
    /// Height of the part of a BOTTOM taskbar that is currently on screen (0 when hidden, or when the taskbar is not at the bottom).
    /// With auto-hide the taskbar slides in and out, so this changes while the user moves the mouse to the edge.
    /// </summary>
    internal static int GetVisibleBottomTaskbarHeight(Rectangle screenBounds)
    {
        if (IsTaskbarCloaked()) return 0; // hidden by auto-hide
        var tb = GetTaskbarRect();
        if (tb.IsEmpty) return 0;
        // Only a horizontal taskbar on the bottom half of this screen.
        if (tb.Width < tb.Height || tb.Top < screenBounds.Top + screenBounds.Height / 2) return 0;
        var visible = Rectangle.Intersect(tb, screenBounds);
        return Math.Max(0, visible.Height);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetProp(IntPtr hWnd, string name, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr RemoveProp(IntPtr hWnd, string name);

    /// <summary>Window property that tells Opsis DeskZones (and any other window manager that checks it) never to snap this window.</summary>
    internal const string NoSnapProperty = "Opsis.NoSnap";

    internal static void MarkNoSnap(IntPtr hwnd) => SetProp(hwnd, NoSnapProperty, new IntPtr(1));
    internal static void ClearNoSnap(IntPtr hwnd) => RemoveProp(hwnd, NoSnapProperty);

    /// <summary>Sends a window to the bottom of the normal windows: it stays on the desktop and every other window covers it.</summary>
    internal static void SendToBottom(IntPtr hwnd)
    {
        const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;
        SetWindowPos(hwnd, new IntPtr(1), 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
    }

    internal static void KeepTopMost(IntPtr hwnd)
    {
        const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;
        SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
    }

    // ---- AppBar (reserves screen space so maximized windows stop above/below the strip) ----
    private const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3;
    internal const int ABE_TOP = 1, ABE_BOTTOM = 3;
    internal const int ABN_POSCHANGED = 1;
    internal static uint AppBarCallbackMessage { get; } = RegisterWindowMessage("OpsisNewsCrawler.AppBar");

    internal static bool AppBarRegister(IntPtr hwnd)
    {
        var d = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = hwnd, uCallbackMessage = AppBarCallbackMessage };
        return SHAppBarMessage(ABM_NEW, ref d) != UIntPtr.Zero;
    }

    internal static void AppBarRemove(IntPtr hwnd)
    {
        var d = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = hwnd };
        SHAppBarMessage(ABM_REMOVE, ref d);
    }

    /// <summary>Claims a strip of the given height at the top or bottom of the screen and returns where Windows put it.</summary>
    internal static Rectangle AppBarSetPos(IntPtr hwnd, Rectangle screenBounds, int edge, int height)
    {
        var d = new APPBARDATA
        {
            cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = hwnd, uEdge = (uint)edge,
            rc = new RECT { Left = screenBounds.Left, Top = screenBounds.Top, Right = screenBounds.Right, Bottom = screenBounds.Bottom }
        };
        SHAppBarMessage(ABM_QUERYPOS, ref d);
        if (edge == ABE_BOTTOM) d.rc.Top = d.rc.Bottom - height;
        else d.rc.Bottom = d.rc.Top + height;
        SHAppBarMessage(ABM_SETPOS, ref d);
        return Rectangle.FromLTRB(d.rc.Left, d.rc.Top, d.rc.Right, d.rc.Bottom);
    }

    // ---- window backdrop (DWM accent policy; the same call Opsis uses for its widgets and containers) ----
    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy { public int AccentState, AccentFlags, GradientColor, AnimationId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }
    [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    private static void SetAccent(IntPtr hwnd, int state, int gradientColor)
    {
        var policy = new AccentPolicy { AccentState = state, GradientColor = gradientColor };
        var size = Marshal.SizeOf(policy);
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new WindowCompositionAttributeData { Attribute = 19, Data = ptr, SizeOfData = size }; // WCA_ACCENT_POLICY
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    /// <summary>Removes any backdrop: the window is drawn opaque again.</summary>
    internal static void ClearBackdrop(IntPtr hwnd) => SetAccent(hwnd, 0, 0);

    /// <summary>The accent state of a backdrop preset: gradient = 2, blur = 3, acrylic = 4.</summary>
    internal static int BackdropState(string style) => style switch { "gradient" => 2, "blur" => 3, _ => 4 };

    /// <summary>
    /// The window's background material. <paramref name="style"/> is the preset ("gradient" / "blur" / "acrylic"),
    /// <paramref name="tint"/> the colour and <paramref name="alpha"/> (0-255) how strong it is; alpha 0 = no tint at all, so the
    /// taskbar shows through unchanged. The window content (text, logos) is never affected.
    /// </summary>
    internal static void SetBackdrop(IntPtr hwnd, string style, Color tint, int alpha) =>
        SetAccent(hwnd, BackdropState(style), (Math.Clamp(alpha, 0, 255) << 24) | (tint.B << 16) | (tint.G << 8) | tint.R); // ABGR

    /// <summary>
    /// Opsis' "glass" tint (copied from Opsis GlassBlur.Apply): the base colour mixed 16 % with the accent, and its alpha
    /// 0x99 x opacity (never below <paramref name="minAlpha"/>).
    /// </summary>
    internal static (Color Tint, int Alpha) GlassTint(Color baseColor, Color accent, double opacityFraction, int minAlpha = 18)
    {
        const double t = 0.16;
        var tint = Color.FromArgb((int)(baseColor.R + (accent.R - baseColor.R) * t), (int)(baseColor.G + (accent.G - baseColor.G) * t), (int)(baseColor.B + (accent.B - baseColor.B) * t));
        return (tint, (int)Math.Clamp(0x99 * opacityFraction, minAlpha, 255));
    }

    internal static bool IsTaskbarAutoHide()
    {
        var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
        var state = (ulong)SHAppBarMessage(ABM_GETSTATE, ref data);
        return (state & ABS_AUTOHIDE) != 0;
    }
}
