using Microsoft.Win32;

namespace Rolan.Services;

public class AutoStartService : IAutoStartService
{
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Rolan";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public void SetEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null)
                    key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue(AppName) != null)
                    key.DeleteValue(AppName);
            }
        }
        catch
        {
            // 没有权限写入注册表时静默失败
        }
    }
}
