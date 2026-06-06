using System.IO;

namespace Rolan.Helpers;

internal static class AppStorage
{
    private const string AppDataDirectoryName = "Rolan";
    private const string LocalDataDirectoryName = "data";
    private const string PortableMarkerFileName = "portable.flag";
    private const string DataDirectoryEnvironmentVariable = "ROLAN_DATA_DIR";

    public static string GetDataDirectory()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
            return NormalizeDirectory(overrideDirectory);

        if (IsPortableMode())
            return GetWritablePortableDirectoryOrFallback();

        return GetRoamingDataDirectory();
    }

    public static bool IsPortableMode()
    {
        var baseDirectory = AppContext.BaseDirectory;
        return File.Exists(Path.Combine(baseDirectory, PortableMarkerFileName)) ||
               Directory.Exists(Path.Combine(baseDirectory, LocalDataDirectoryName));
    }

    private static string GetWritablePortableDirectoryOrFallback()
    {
        var portableDirectory = Path.Combine(AppContext.BaseDirectory, LocalDataDirectoryName);
        try
        {
            Directory.CreateDirectory(portableDirectory);
            return portableDirectory;
        }
        catch
        {
            return GetRoamingDataDirectory();
        }
    }

    private static string GetRoamingDataDirectory()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataDirectoryName);

    private static string NormalizeDirectory(string directory)
    {
        var expanded = Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"'));
        return Path.GetFullPath(expanded);
    }
}
