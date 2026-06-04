using System.Text.Json;
using Rolan.Models;

namespace Rolan.Services;

public class DataExportService : IDataExportService
{
    public async Task ExportAsync(string filePath, List<ShortcutGroup> groups)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
        };

        var data = new ExportData
        {
            Version = 1,
            ExportTime = DateTime.Now,
            Groups = groups
        };

        var json = JsonSerializer.Serialize(data, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<ShortcutGroup>> ImportAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<ExportData>(json);

        if (data?.Groups == null)
            throw new InvalidDataException("导入文件格式错误");

        // 重置 ID，以便重新插入
        foreach (var group in data.Groups)
        {
            group.Id = 0;
            foreach (var item in group.Items)
            {
                item.Id = 0;
                item.GroupId = 0;
            }
        }

        return data.Groups;
    }

    private class ExportData
    {
        public int Version { get; set; }
        public DateTime ExportTime { get; set; }
        public List<ShortcutGroup> Groups { get; set; } = new();
    }
}
