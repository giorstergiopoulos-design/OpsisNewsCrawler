using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Accessibility;

namespace OpsisNewsCrawler.Windows;

/// <summary>
/// Measures where the empty space of the taskbar is (after the start/search buttons and the pinned + running app buttons,
/// before the notification area), so the strip can sit exactly in that space. Slow: call from a background thread.
/// Three sources are combined because Windows has several taskbar implementations:
/// Win32 child windows (start, search, task view), MSAA (the classic task list) and UI Automation (the XAML taskbar).
/// </summary>
internal static class TaskbarMetrics
{
    private const int MinGap = 180;
    private const int Pad = 6;

    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint objectId, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object? obj);

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    /// <summary>Largest empty horizontal span of the taskbar in screen coordinates, or null when it cannot be determined.</summary>
    public static (int Left, int Right)? MeasureFreeSpan()
    {
        try
        {
            var tb = Native.GetTaskbarRect();
            if (tb.IsEmpty || tb.Width < tb.Height) return null;
            var tray = Native.GetTaskbarHandle();
            var trayLeft = Native.GetTrayNotifyRect() is { IsEmpty: false } notify ? notify.Left : tb.Right - 300;

            var edges = new List<(int L, int R)>();
            void Add(Rectangle r)
            {
                if (r.Width < 8 || r.Height < 8 || !r.IntersectsWith(tb) || r.Left >= trayLeft) return;
                if (r.Width > tb.Width / 2) return; // a container, not a button
                edges.Add((r.Left, Math.Min(r.Right, trayLeft)));
            }

            foreach (var r in Win32Buttons(tray)) Add(r);
            // Each source is optional: a host without WPF/accessibility assemblies must still get the others.
            try { foreach (var r in AccessibleTaskButtons(tray)) Add(r); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
            try { foreach (var r in AutomationButtons(tray)) Add(r); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
            if (edges.Count == 0) return null;

            edges.Sort((a, b) => a.L.CompareTo(b.L));

            // Gaps: before the first button, between buttons, after the last one.
            var best = (Left: 0, Right: 0, Width: 0);
            void Consider(int from, int to)
            {
                if (to - from > best.Width) best = (from, to, to - from);
            }
            Consider(tb.Left, edges[0].L);
            var reach = edges[0].R;
            for (var i = 1; i < edges.Count; i++)
            {
                if (edges[i].L > reach) Consider(reach, edges[i].L);
                reach = Math.Max(reach, edges[i].R);
            }
            Consider(reach, trayLeft);

            return best.Width >= MinGap ? (best.Left + Pad, best.Right - Pad) : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or COMException or ElementNotAvailableException)
        {
            return null;
        }
    }

    /// <summary>Start button, search box, task view...: small visible child windows of the taskbar (not the big containers).</summary>
    private static IEnumerable<Rectangle> Win32Buttons(IntPtr tray)
    {
        var list = new List<Rectangle>();
        if (tray == IntPtr.Zero) return list;
        EnumChildWindows(tray, (h, _) =>
        {
            var name = new StringBuilder(128);
            GetClassName(h, name, name.Capacity);
            var cls = name.ToString();
            if (cls is "Start" or "SIBTrayButton" && IsWindowVisible(h) && GetWindowRect(h, out var r))
                list.Add(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    /// <summary>The running/pinned app buttons of the classic task list (MSTaskListWClass) via MSAA.</summary>
    private static IEnumerable<Rectangle> AccessibleTaskButtons(IntPtr tray)
    {
        var result = new List<Rectangle>();
        if (tray == IntPtr.Zero) return result;
        var taskList = IntPtr.Zero;
        EnumChildWindows(tray, (h, _) =>
        {
            var name = new StringBuilder(128);
            GetClassName(h, name, name.Capacity);
            if (name.ToString() != "MSTaskListWClass") return true;
            taskList = h;
            return false;
        }, IntPtr.Zero);
        if (taskList == IntPtr.Zero) return result;

        var iid = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71"); // IID_IAccessible
        if (AccessibleObjectFromWindow(taskList, 0xFFFFFFFC /* OBJID_CLIENT */, ref iid, out var obj) != 0 || obj is not IAccessible acc)
            return result;
        var count = Math.Min(acc.accChildCount, 200);
        for (var i = 1; i <= count; i++)
        {
            try
            {
                acc.accLocation(out var l, out var t, out var w, out var h, i);
                result.Add(new Rectangle(l, t, w, h));
            }
            catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
            {
            }
        }
        return result;
    }

    /// <summary>Buttons exposed by UI Automation (the XAML based Windows 11 taskbar).</summary>
    private static IEnumerable<Rectangle> AutomationButtons(IntPtr tray)
    {
        var result = new List<Rectangle>();
        if (tray == IntPtr.Zero) return result;
        var root = AutomationElement.FromHandle(tray);
        var buttons = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        foreach (AutomationElement b in buttons)
        {
            try
            {
                var r = b.Current.BoundingRectangle;
                if (!r.IsEmpty) result.Add(Rectangle.FromLTRB((int)r.Left, (int)r.Top, (int)r.Right, (int)r.Bottom));
            }
            catch (ElementNotAvailableException)
            {
            }
        }
        return result;
    }
}
