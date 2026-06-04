using Rolan.Models;

namespace Rolan.Services;

public interface IDataExportService
{
    Task ExportAsync(string filePath, List<ShortcutGroup> groups);
    Task<List<ShortcutGroup>> ImportAsync(string filePath);
}
