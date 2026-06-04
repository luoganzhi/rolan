namespace Rolan.Models;

public enum ShortcutType
{
    Application,
    File,
    Folder,
    Url
}

public class ShortcutItem
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public byte[]? IconData { get; set; }
    public int Order { get; set; }
    public ShortcutType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
