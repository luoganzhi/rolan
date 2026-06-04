using System.Windows;

namespace Rolan.Services;

public class ThemeService : IThemeService
{
    public string[] AvailableThemes => new[] { "Default", "Dark" };

    public void ApplyTheme(string themeName)
    {
        var app = Application.Current;
        if (app == null) return;

        var oldDict = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString?.Contains("Theme") == true);

        if (oldDict != null)
            app.Resources.MergedDictionaries.Remove(oldDict);

        string uri = themeName switch
        {
            "Dark" => "Styles/DarkTheme.xaml",
            _ => "Styles/DefaultTheme.xaml"
        };

        try
        {
            var newDict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };
            app.Resources.MergedDictionaries.Add(newDict);
        }
        catch
        {
            // 如果主题文件不存在，回退到默认
        }
    }
}
