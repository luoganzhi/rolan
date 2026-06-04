namespace Rolan.Models;

public enum PanelSide
{
    Left,
    Right
}

public class AppSettings
{
    public bool AutoHide { get; set; } = true;
    public bool HideWhenLostFocus { get; set; } = true;
    public bool MousePenetration { get; set; }
    public bool TopMost { get; set; } = true;
    public string Theme { get; set; } = "Default";
    public int HotkeyModifiers { get; set; } = 2 | 4; // MOD_CONTROL | MOD_ALT
    public int HotkeyKey { get; set; } = 0x52;         // 'R'
    public PanelSide PanelSide { get; set; } = PanelSide.Left;
    public int PanelWidth { get; set; } = 320;
    public bool AutoStart { get; set; }

    private static readonly string SettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "Rolan", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
