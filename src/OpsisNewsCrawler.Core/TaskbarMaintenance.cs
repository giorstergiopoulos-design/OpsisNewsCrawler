namespace OpsisNewsCrawler.Core;

/// <summary>
/// What the bar inside the taskbar re-asserts, and when. It used to do everything about once a second: put itself on top of the z-order, set the backdrop (the
/// acrylic / blur accent policy) again, redraw and re-place itself. Setting the backdrop and the z-order again makes the desktop window manager recompose the bar, which is
/// the flicker seen when the strip refreshes, and putting itself above the tray overflow window while the user is using it fights with the shell. Now: nothing while a shell flyout
/// has the focus, the z-order only when the bar is really not on top, the look only when the taskbar's style changed (or rarely, to survive an Explorer restart).
/// </summary>
public static class TaskbarMaintenance
{
    public readonly record struct Actions(bool KeepOnTop, bool ApplyLook, bool Reposition);

    /// <summary>Ticks between the checks (the timer ticks every 30-90 ms depending on the performance tier, so this is about 1-3 s).</summary>
    public const int CheckEvery = 30;

    /// <summary>Ticks between two unconditional re-applications of the look (a recovery after Explorer restarted; about 30-90 s).</summary>
    public const int LookRefreshEvery = 900;

    /// <summary>Class names of the shell's own pop-up windows (tray overflow, notification centre, start, search...).</summary>
    public static readonly string[] ShellFlyoutClasses =
    {
        "TopLevelWindowForOverflowXamlIsland", "NotifyIconOverflowWindow", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
        "ControlCenterWindow", "TaskListThumbnailWnd", "TaskListOverlayWnd", "ForegroundStaging"
    };

    public static bool IsShellFlyout(string? className) => !string.IsNullOrEmpty(className) && ShellFlyoutClasses.Contains(className, StringComparer.OrdinalIgnoreCase);

    public static Actions Decide(int tick, bool taskbarStyleChanged, bool shellFlyoutHasFocus, bool alreadyOnTop)
    {
        if (shellFlyoutHasFocus) return new Actions(false, false, false);   // the user is using the tray overflow or another flyout: touch nothing
        var check = tick % CheckEvery == 0;
        return new Actions(
            KeepOnTop: check && !alreadyOnTop,
            ApplyLook: taskbarStyleChanged || (tick > 0 && tick % LookRefreshEvery == 0),
            Reposition: check);
    }
}
