namespace Rolan.Services;

public interface IThemeService
{
    void ApplyTheme(string themeName);
    string[] AvailableThemes { get; }
}
