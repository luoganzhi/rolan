namespace Rolan.Services;

public interface IDataDirectoryService
{
    string DataDirectory { get; }
    void OpenDataDirectory();
}
