using System.IO;

namespace Rolan.Helpers;

internal static class AppStorage
{
    private const string AppDataDirectoryName = "Rolan";
    private const string LocalDataDirectoryName = "data";
    private const string PortableMarkerFileName = "portable.flag";
    private const string DataDirectoryEnvironmentVariable = "ROLAN_DATA_DIR";

    public static string PortableDataDirectory => Path.Combine(AppContext.BaseDirectory, LocalDataDirectoryName);
    public static string PortableMarkerPath => Path.Combine(AppContext.BaseDirectory, PortableMarkerFileName);
    public static bool IsEnvironmentOverrideActive => !string.IsNullOrWhiteSpace(GetEnvironmentOverrideDirectory());

    public static string GetDataDirectory()
    {
        var overrideDirectory = GetEnvironmentOverrideDirectory();
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
            return NormalizeDirectory(overrideDirectory);

        if (IsPortableMode())
            return GetWritablePortableDirectoryOrFallback();

        return GetRoamingDataDirectory();
    }

    public static bool IsPortableMode()
    {
        return File.Exists(PortableMarkerPath) ||
               Directory.Exists(PortableDataDirectory);
    }

    private static string GetWritablePortableDirectoryOrFallback()
    {
        try
        {
            Directory.CreateDirectory(PortableDataDirectory);
            return PortableDataDirectory;
        }
        catch
        {
            return GetRoamingDataDirectory();
        }
    }

    private static string GetRoamingDataDirectory()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataDirectoryName);

    private static string? GetEnvironmentOverrideDirectory()
        => Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);

    private static string NormalizeDirectory(string directory)
    {
        var expanded = Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"'));
        return Path.GetFullPath(expanded);
    }
}
