namespace Rolan.Services;

public interface IDataDirectoryService
{
    string DataDirectory { get; }
    string DataModeDescription { get; }
    bool CanEnablePortableMode { get; }
    void EnablePortableMode();
    void OpenDataDirectory();
}
