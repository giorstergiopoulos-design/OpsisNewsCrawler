namespace OpsisNewsCrawler.Windows;

public static class TaskbarPlacement
{
    /// <summary>Strip of the given height just above a bottom taskbar (the working area ends where the taskbar starts).</summary>
    public static Rectangle GetBottomWorkArea(Screen screen, int height)
    {
        var wa = screen.WorkingArea;
        return new Rectangle(wa.Left, wa.Bottom - height, wa.Width, height);
    }

    /// <summary>Strip at the very bottom of the screen, overlapping the taskbar.</summary>
    public static Rectangle GetScreenBottom(Screen screen, int height)
    {
        var b = screen.Bounds;
        return new Rectangle(b.Left, b.Bottom - height, b.Width, height);
    }

    public static bool IsAutoHide() => Native.IsTaskbarAutoHide();
}
