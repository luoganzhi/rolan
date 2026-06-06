using System.Diagnostics;

namespace Rolan.Helpers;

internal sealed record SystemCommandDefinition(string TargetPath, string Name);

internal static class SystemCommandHelper
{
    private const string Scheme = "rolan:";

    public static IReadOnlyList<SystemCommandDefinition> Commands { get; } =
    [
        new("rolan:lock", "锁定电脑"),
        new("rolan:explorer", "文件资源管理器"),
        new("rolan:settings", "Windows 设置"),
        new("rolan:control-panel", "控制面板"),
        new("rolan:task-manager", "任务管理器")
    ];

    public static bool IsSystemCommand(string targetPath)
        => targetPath.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);

    public static string? GetDisplayName(string targetPath)
        => Commands.FirstOrDefault(c => string.Equals(c.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase))?.Name;

    public static bool TryExecute(string targetPath)
    {
        if (!IsSystemCommand(targetPath))
            return false;

        switch (targetPath.ToLowerInvariant())
        {
            case "rolan:lock":
                NativeMethods.LockWorkStation();
                return true;
            case "rolan:explorer":
                StartShell("explorer.exe");
                return true;
            case "rolan:settings":
                StartShell("ms-settings:");
                return true;
            case "rolan:control-panel":
                StartShell("control.exe");
                return true;
            case "rolan:task-manager":
                StartShell("taskmgr.exe");
                return true;
            default:
                return false;
        }
    }

    private static void StartShell(string fileName)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true
        });
    }
}
