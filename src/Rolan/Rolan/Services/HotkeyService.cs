using Rolan.Helpers;

namespace Rolan.Services;

public class HotkeyService : IHotkeyService
{
    public event Action? HotkeyPressed;

    public bool Register(IntPtr windowHandle, int id, int modifiers, int key)
    {
        return NativeMethods.RegisterHotKey(windowHandle, id, (uint)modifiers, (uint)key);
    }

    public void Unregister(IntPtr windowHandle, int id)
    {
        NativeMethods.UnregisterHotKey(windowHandle, id);
    }

    /// <summary>
    /// 处理来自 WndProc 的热键消息
    /// </summary>
    public bool HandleHotkeyMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            HotkeyPressed?.Invoke();
            return true;
        }
        return false;
    }
}
