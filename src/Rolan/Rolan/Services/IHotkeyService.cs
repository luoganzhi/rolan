namespace Rolan.Services;

public interface IHotkeyService
{
    event Action? HotkeyPressed;
    bool Register(IntPtr windowHandle, int id, int modifiers, int key);
    void Unregister(IntPtr windowHandle, int id);
}
