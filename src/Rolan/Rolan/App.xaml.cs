using System.Windows;
using Rolan.Services;
using Rolan.ViewModels;
using Rolan.Views;

namespace Rolan;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App()
    {
        _services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // 数据
        services.AddSingleton<IDataService, DataService>();

        // 服务
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAutoStartService, AutoStartService>();
        services.AddSingleton<IDataExportService, DataExportService>();
        services.AddSingleton<PanelService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>();

        return services.BuildServiceProvider();
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var mainVm = _services.GetRequiredService<MainViewModel>();
        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = mainVm;
        mainWindow.Show();
    }
}
