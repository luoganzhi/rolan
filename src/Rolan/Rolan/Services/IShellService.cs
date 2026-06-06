using System.Windows.Media.Imaging;

namespace Rolan.Services;

public interface IShellService
{
    bool Launch(string targetPath, string? arguments = null, string? workingDirectory = null);
    BitmapSource? ExtractIcon(string filePath);
}
