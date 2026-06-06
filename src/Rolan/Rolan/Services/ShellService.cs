using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using Rolan.Helpers;

namespace Rolan.Services;

public class ShellService : IShellService
{
    public bool Launch(string targetPath, string? arguments = null, string? workingDirectory = null)
    {
        try
        {
            if (SystemCommandHelper.TryExecute(targetPath))
                return true;

            var psi = new ProcessStartInfo
            {
                FileName = TargetPathHelper.Resolve(targetPath),
                UseShellExecute = true
            };

            var resolvedWorkingDirectory = ResolveWorkingDirectory(targetPath, workingDirectory);
            if (!string.IsNullOrEmpty(resolvedWorkingDirectory))
                psi.WorkingDirectory = resolvedWorkingDirectory;

            if (!string.IsNullOrEmpty(arguments))
                psi.Arguments = arguments;

            return Process.Start(psi) != null || TargetPathHelper.IsUrl(targetPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"无法启动: {targetPath}\n{ex.Message}", "Rolan",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return false;
        }
    }

    public BitmapSource? ExtractIcon(string filePath)
    {
        return IconHelper.ExtractIcon(filePath);
    }

    private static string? ResolveWorkingDirectory(string targetPath, string? workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            var resolvedWorkingDirectory = TargetPathHelper.Resolve(workingDirectory);
            if (Directory.Exists(resolvedWorkingDirectory))
                return resolvedWorkingDirectory;
        }

        var resolvedTargetPath = TargetPathHelper.Resolve(targetPath);
        if (Directory.Exists(resolvedTargetPath))
            return resolvedTargetPath;

        if (File.Exists(resolvedTargetPath))
        {
            var directory = Path.GetDirectoryName(resolvedTargetPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                return directory;
        }

        return null;
    }
}
