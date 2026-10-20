using System.Runtime.InteropServices;

namespace OpsisNewsCrawler.Windows;

/// <summary>A system-wide hot key (RegisterHotKey) received by a hidden message-only window. UI thread only.</summary>
internal sealed class GlobalHotkey : NativeWindow, IDisposable
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4F4E;
    private static readonly IntPtr MessageOnlyParent = new(-3);

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private bool registered;

    public event Action? Pressed;

    public GlobalHotkey() => CreateHandle(new CreateParams { Parent = MessageOnlyParent });

    /// <summary>False when another program already owns the combination.</summary>
    public bool Register(uint modifiers, uint virtualKey)
    {
        Unregister();
        registered = Handle != IntPtr.Zero && RegisterHotKey(Handle, HotkeyId, modifiers | ModNoRepeat, virtualKey);
        return registered;
    }

    private void Unregister()
    {
        if (registered && Handle != IntPtr.Zero) UnregisterHotKey(Handle, HotkeyId);
        registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId) Pressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
}
