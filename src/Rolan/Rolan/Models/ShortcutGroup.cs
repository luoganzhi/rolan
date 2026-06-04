namespace Rolan.Models;

public class ShortcutGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public byte[]? IconData { get; set; }
    public List<ShortcutItem> Items { get; set; } = new();
}
