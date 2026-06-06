using System.IO;
using System.Text.Json;
using Rolan.Helpers;
using Rolan.Models;

namespace Rolan.Services;

public class DataExportService : IDataExportService
{
    public async Task ExportAsync(string filePath, List<ShortcutGroup> groups)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var normalizedGroups = groups
            .OrderBy(g => g.Order)
            .Select(g => new ShortcutGroup
            {
                Id = g.Id,
                Name = g.Name,
                Order = g.Order,
                IconData = g.IconData,
                Items = g.Items
                    .OrderBy(i => i.Order)
                    .Select(i => new ShortcutItem
                    {
                        Id = i.Id,
                        GroupId = i.GroupId,
                        Name = i.Name,
                        TargetPath = i.TargetPath,
                        Arguments = i.Arguments,
                        WorkingDirectory = i.WorkingDirectory,
                        IconData = i.IconData,
                        Order = i.Order,
                        Type = i.Type,
                        CreatedAt = i.CreatedAt,
                        LaunchCount = i.LaunchCount,
                        LastLaunchedAt = i.LastLaunchedAt
                    })
                    .ToList()
            })
            .ToList();

        var data = new ExportData
        {
            Version = 1,
            ExportTime = DateTime.Now,
            Groups = normalizedGroups
        };

        var json = JsonSerializer.Serialize(data, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<ShortcutGroup>> ImportAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        ExportData? data;
        try
        {
            data = JsonSerializer.Deserialize<ExportData>(json, new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
            });
        }
        catch (JsonException)
        {
            data = JsonSerializer.Deserialize<ExportData>(json);
        }

        if (data?.Groups == null)
            throw new InvalidDataException("导入文件格式错误");

        // 重置 ID，以便重新插入
        var groupOrder = 0;
        foreach (var group in data.Groups)
        {
            group.Id = 0;
            group.Name = string.IsNullOrWhiteSpace(group.Name) ? $"导入分组 {groupOrder + 1}" : group.Name.Trim();
            group.Order = groupOrder++;
            group.Items ??= new List<ShortcutItem>();

            var itemOrder = 0;
            foreach (var item in group.Items.ToList())
            {
                item.TargetPath = TargetPathHelper.NormalizeInput(item.TargetPath) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(item.TargetPath))
                {
                    group.Items.Remove(item);
                    continue;
                }

                item.Id = 0;
                item.GroupId = 0;
                item.Name = string.IsNullOrWhiteSpace(item.Name)
                    ? Path.GetFileNameWithoutExtension(item.TargetPath)
                    : item.Name.Trim();
                if (string.IsNullOrWhiteSpace(item.Name))
                    item.Name = item.TargetPath;

                item.Arguments = NullIfWhiteSpace(item.Arguments);
                item.WorkingDirectory = NullIfWhiteSpace(item.WorkingDirectory);
                item.Order = itemOrder++;
                if (item.CreatedAt == default)
                    item.CreatedAt = DateTime.Now;
                if (item.LaunchCount < 0)
                    item.LaunchCount = 0;
                if (item.LastLaunchedAt == default)
                    item.LastLaunchedAt = null;
            }
        }

        return data.Groups;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private class ExportData
    {
        public int Version { get; set; }
        public DateTime ExportTime { get; set; }
        public List<ShortcutGroup> Groups { get; set; } = new();
    }
}
