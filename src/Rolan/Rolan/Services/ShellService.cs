using System.Diagnostics;
using System.Windows.Media.Imaging;
using Rolan.Helpers;

namespace Rolan.Services;

public class ShellService : IShellService
{
    public void Launch(string targetPath, string? arguments = null, string? workingDirectory = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true,
                WorkingDirectory = workingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (!string.IsNullOrEmpty(arguments))
                psi.Arguments = arguments;

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"无法启动: {targetPath}\n{ex.Message}", "Rolan",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public BitmapSource? ExtractIcon(string filePath)
    {
        return IconHelper.ExtractIcon(filePath);
    }
}
