using Microsoft.Win32;

namespace Rolan.Services;

public class AutoStartService : IAutoStartService
{
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Rolan";
    public const string MinimizedArgument = "--minimized";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
                var command = key?.GetValue(AppName) as string;
                if (string.IsNullOrWhiteSpace(command))
                    return false;

                var registeredExePath = ExtractExecutablePath(command);
                var currentExePath = GetCurrentExecutablePath();
                return !string.IsNullOrWhiteSpace(registeredExePath) &&
                       !string.IsNullOrWhiteSpace(currentExePath) &&
                       string.Equals(registeredExePath, currentExePath, StringComparison.OrdinalIgnoreCase);
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
                var exePath = GetCurrentExecutablePath();
                if (exePath != null)
                    key.SetValue(AppName, BuildRunCommand(exePath));
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

    private static string? GetCurrentExecutablePath()
        => System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

    private static string BuildRunCommand(string exePath)
        => $"\"{exePath}\" {MinimizedArgument}";

    private static string? ExtractExecutablePath(string command)
    {
        var value = command.Trim();
        if (value.Length == 0)
            return null;

        if (value[0] == '"')
        {
            var closingQuote = value.IndexOf('"', 1);
            return closingQuote > 1
                ? value[1..closingQuote]
                : null;
        }

        var firstSpace = value.IndexOf(' ');
        return firstSpace > 0 ? value[..firstSpace] : value;
    }
}
