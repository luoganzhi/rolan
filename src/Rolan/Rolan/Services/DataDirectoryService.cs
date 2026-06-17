using System.Diagnostics;
using System.IO;
using Rolan.Helpers;

namespace Rolan.Services;

public class DataDirectoryService : IDataDirectoryService
{
    public string DataDirectory => AppStorage.GetDataDirectory();
    public string DataModeDescription
    {
        get
        {
            if (AppStorage.IsEnvironmentOverrideActive)
                return "环境变量目录";

            return AppStorage.IsPortableMode()
                ? "便携目录"
                : "用户数据目录";
        }
    }

    public bool CanEnablePortableMode =>
        !AppStorage.IsEnvironmentOverrideActive &&
        !IsSameDirectory(DataDirectory, AppStorage.PortableDataDirectory);

    public void EnablePortableMode()
    {
        if (!CanEnablePortableMode)
            return;

        var sourceDirectory = DataDirectory;
        var targetDirectory = AppStorage.PortableDataDirectory;

        Directory.CreateDirectory(targetDirectory);
        CopyDirectoryContents(sourceDirectory, targetDirectory);
        File.WriteAllText(
            AppStorage.PortableMarkerPath,
            "Rolan portable mode. Keep this file next to Rolan.exe to store settings and data in .\\data.");
    }

    public void OpenDataDirectory()
    {
        Directory.CreateDirectory(DataDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = DataDirectory,
            UseShellExecute = true
        });
    }

    private static void CopyDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        if (!Directory.Exists(sourceDirectory) || IsSameDirectory(sourceDirectory, targetDirectory))
            return;

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var targetFile = Path.Combine(targetDirectory, relativePath);
            var targetParent = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetParent))
                Directory.CreateDirectory(targetParent);

            File.Copy(file, targetFile, overwrite: true);
        }
    }

    private static bool IsSameDirectory(string left, string right)
    {
        var normalizedLeft = NormalizeDirectory(left);
        var normalizedRight = NormalizeDirectory(right);
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectory(string directory)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
}
