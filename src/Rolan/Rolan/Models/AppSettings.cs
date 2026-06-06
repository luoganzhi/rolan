using System.IO;
using System.Text.Json;
using Rolan.Helpers;

namespace Rolan.Models;

public enum PanelSide
{
    Left,
    Right
}

public class AppSettings
{
    private const int CurrentSettingsVersion = 5;

    public int SettingsVersion { get; set; } = CurrentSettingsVersion;
    public bool AutoHide { get; set; }
    public bool HideWhenLostFocus { get; set; }
    public bool MousePenetration { get; set; }
    public bool TopMost { get; set; } = true;
    public string Theme { get; set; } = "Default";
    public int HotkeyModifiers { get; set; } = 1; // MOD_ALT
    public int HotkeyKey { get; set; } = 0x20;    // Space
    public PanelSide PanelSide { get; set; } = PanelSide.Left;
    public int PanelWidth { get; set; } = 360;
    public int PanelHeight { get; set; } = 720;
    public double? PanelTop { get; set; }
    public bool AutoStart { get; set; }
    public bool HideAfterLaunch { get; set; } = true;

    private static string SettingsPath => Path.Combine(AppStorage.GetDataDirectory(), "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                if (!HasSettingsVersion(json))
                    settings.SettingsVersion = 0;

                settings.Normalize();
                return settings;
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
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    private static bool HasSettingsVersion(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(nameof(SettingsVersion), out _);
        }
        catch
        {
            return false;
        }
    }

    private void Normalize()
    {
        var existingVersion = SettingsVersion;

        if (existingVersion < 2)
        {
            AutoHide = false;
            HideWhenLostFocus = false;
            if (PanelWidth <= 0) PanelWidth = 360;
            if (PanelHeight <= 0) PanelHeight = 720;
        }

        if (existingVersion < 3)
        {
            HotkeyModifiers = 1;
            HotkeyKey = 0x20;
        }

        SettingsVersion = CurrentSettingsVersion;
        PanelWidth = Math.Clamp(PanelWidth, 280, 720);
        PanelHeight = Math.Clamp(PanelHeight, 420, 1400);
        if (PanelTop is double top && (double.IsNaN(top) || double.IsInfinity(top)))
            PanelTop = null;
    }
}
