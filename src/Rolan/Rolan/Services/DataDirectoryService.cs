using System.Diagnostics;
using System.IO;
using Rolan.Helpers;

namespace Rolan.Services;

public class DataDirectoryService : IDataDirectoryService
{
    public string DataDirectory => AppStorage.GetDataDirectory();

    public void OpenDataDirectory()
    {
        Directory.CreateDirectory(DataDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = DataDirectory,
            UseShellExecute = true
        });
    }
}
