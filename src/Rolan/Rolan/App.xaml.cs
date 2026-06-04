using System.Drawing;
using System.Windows;
using Rolan.Services;
using Rolan.ViewModels;
using Rolan.Views;

namespace Rolan;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private MainViewModel? _mainVm;

    public App()
    {
        _services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        services.AddSingleton<IDataService, DataService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAutoStartService, AutoStartService>();
        services.AddSingleton<IDataExportService, DataExportService>();
        services.AddSingleton<PanelService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>();

        return services.BuildServiceProvider();
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _mainVm = _services.GetRequiredService<MainViewModel>();
        _mainWindow = _services.GetRequiredService<MainWindow>();
        _mainWindow.DataContext = _mainVm;

        // 处理窗口关闭（最小化到托盘）
        _mainWindow.Closing += OnMainWindowClosing;
        _mainWindow.IsVisibleChanged += (_, _) => UpdateTrayMenuText();

        _mainWindow.Show();
        CreateTrayIcon();
    }

    // ---- 系统托盘 ----

    private void CreateTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Rolan",
            Icon = SystemIcons.Application,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => ToggleMainWindow();

        var menu = new ContextMenuStrip();

        menu.Items.Add("显示/隐藏", null, (_, _) => ToggleMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("添加分组", null, (_, _) =>
        {
            _mainWindow?.Show();
            _mainVm?.AddGroupCommand.Execute(null);
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("设置", null, (_, _) =>
        {
            _mainWindow?.Show();
            _mainVm?.OpenSettingsCommand.Execute(null);
        });
        menu.Items.Add("关于", null, (_, _) =>
        {
            var about = new AboutWindow();
            about.ShowDialog();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());

        _notifyIcon.ContextMenuStrip = menu;
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow == null) return;

        if (_mainWindow.IsVisible && _mainWindow.IsActive)
            _mainWindow.Hide();
        else
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        }
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 防止直接关闭，改为隐藏到托盘
        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private void UpdateTrayMenuText()
    {
        // 可扩展
    }

    private void ExitApplication()
    {
        _notifyIcon?.Dispose();
        _notifyIcon = null;

        if (_mainWindow != null)
        {
            _mainWindow.Closing -= OnMainWindowClosing;
            _mainWindow.Close();
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }
}
