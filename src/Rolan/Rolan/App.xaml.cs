using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Rolan.Helpers;
using Rolan.Services;
using Rolan.ViewModels;
using Rolan.Views;

namespace Rolan;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "Rolan.DesktopShortcutManager.SingleInstance";
    private const string ShowMainWindowEventName = "Rolan.DesktopShortcutManager.ShowMainWindow";

    private readonly IServiceProvider _services;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _toggleWindowMenuItem;
    private MainWindow? _mainWindow;
    private MainViewModel? _mainVm;
    private System.Threading.Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showMainWindowEvent;
    private Thread? _showMainWindowThread;
    private volatile bool _isExiting;

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
        services.AddSingleton<IDataDirectoryService, DataDirectoryService>();
        services.AddSingleton<PanelService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>();

        return services.BuildServiceProvider();
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _singleInstanceMutex = new System.Threading.Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            SignalRunningInstance();
            Shutdown();
            return;
        }

        _showMainWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowMainWindowEventName);
        StartShowMainWindowListener();

        _mainVm = _services.GetRequiredService<MainViewModel>();
        _mainWindow = _services.GetRequiredService<MainWindow>();
        _mainWindow.DataContext = _mainVm;

        // 处理窗口关闭（最小化到托盘）
        _mainWindow.Closing += OnMainWindowClosing;
        _mainWindow.IsVisibleChanged += (_, _) => UpdateTrayMenuText();
        _mainVm.PanelService.VisibilityChanged += UpdateTrayMenuText;

        CreateTrayIcon();
        if (ShouldStartMinimized(e.Args))
        {
            _mainWindow.Opacity = 0;
            _mainWindow.ShowActivated = false;
            _mainWindow.Loaded += HideMainWindowAfterStartup;
        }

        _mainWindow.Show();
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

        _toggleWindowMenuItem = new ToolStripMenuItem("显示面板", null, (_, _) => ToggleMainWindow())
        {
            Font = new Font(System.Drawing.SystemFonts.MenuFont ?? Control.DefaultFont, System.Drawing.FontStyle.Bold)
        };
        menu.Items.Add(_toggleWindowMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("添加分组", null, (_, _) =>
        {
            ShowMainWindow(focusSearchBox: false);
            _ = _mainWindow?.AddGroupWithNameAsync();
        });
        menu.Items.Add("打开数据目录", null, (_, _) => OpenDataDirectory());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("设置", null, (_, _) =>
        {
            ShowMainWindow(focusSearchBox: false);
            _mainVm?.OpenSettingsCommand.Execute(_mainWindow);
        });
        menu.Items.Add("关于", null, (_, _) =>
        {
            ShowMainWindow(focusSearchBox: false);
            var about = new AboutWindow
            {
                Owner = _mainWindow
            };
            about.ShowDialog();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());

        _notifyIcon.ContextMenuStrip = menu;
        UpdateTrayMenuText();
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow == null) return;

        if (_mainWindow.IsVisible && _mainWindow.IsActive && _mainVm?.PanelService.IsHidden != true)
        {
            _mainWindow.Hide();
        }
        else
        {
            ShowMainWindow();
        }
    }

    private void ShowMainWindowFromExternalSignal()
        => ShowMainWindow();

    private void ShowMainWindow(bool focusSearchBox = true)
    {
        if (_mainWindow == null) return;

        _mainWindow.Opacity = 1;
        _mainWindow.ShowActivated = true;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        if (_mainVm?.PanelService.IsHidden == true)
            _mainVm.PanelService.AnimateShow();
        if (focusSearchBox)
            _mainWindow.RequestSearchFocus();
        UpdateTrayMenuText();
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 防止直接关闭，改为隐藏到托盘
        if (_isExiting)
            return;

        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private void HideMainWindowAfterStartup(object sender, RoutedEventArgs e)
    {
        if (_mainWindow == null) return;

        _mainWindow.Loaded -= HideMainWindowAfterStartup;
        _mainWindow.Hide();
        _mainWindow.Opacity = 1;
        _mainWindow.ShowActivated = true;
        UpdateTrayMenuText();
    }

    private void UpdateTrayMenuText()
    {
        if (_toggleWindowMenuItem == null || _mainWindow == null)
            return;

        if (!_mainWindow.IsVisible)
            _toggleWindowMenuItem.Text = "显示面板";
        else if (_mainVm?.PanelService.IsHidden == true)
            _toggleWindowMenuItem.Text = "恢复面板";
        else
            _toggleWindowMenuItem.Text = "隐藏面板";
    }

    private void OpenDataDirectory()
    {
        try
        {
            _services.GetRequiredService<IDataDirectoryService>().OpenDataDirectory();
        }
        catch (Exception ex)
        {
            _mainVm?.PanelService.ShowMessage(
                $"无法打开数据目录：{ex.Message}",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _showMainWindowEvent?.Set();
        _notifyIcon?.Dispose();
        _notifyIcon = null;

        if (_mainVm != null)
            _mainVm.PanelService.VisibilityChanged -= UpdateTrayMenuText;

        if (_mainWindow != null)
        {
            _mainWindow.Closing -= OnMainWindowClosing;
            _mainWindow.Close();
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isExiting = true;
        _showMainWindowEvent?.Set();
        _notifyIcon?.Dispose();
        if (_mainVm != null)
            _mainVm.PanelService.VisibilityChanged -= UpdateTrayMenuText;
        _showMainWindowThread?.Join(TimeSpan.FromMilliseconds(500));
        _showMainWindowEvent?.Dispose();
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void SignalRunningInstance()
    {
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting(ShowMainWindowEventName);
            showEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            System.Windows.MessageBox.Show("Rolan 已在运行。", "Rolan",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void StartShowMainWindowListener()
    {
        _showMainWindowThread = new Thread(() =>
        {
            while (!_isExiting)
            {
                _showMainWindowEvent?.WaitOne();
                if (_isExiting)
                    break;

                Dispatcher.BeginInvoke(ShowMainWindowFromExternalSignal);
            }
        })
        {
            IsBackground = true,
            Name = "Rolan single-instance listener"
        };
        _showMainWindowThread.Start();
    }

    private static bool ShouldStartMinimized(IEnumerable<string> args)
        => args.Any(arg => string.Equals(arg, AutoStartService.MinimizedArgument, StringComparison.OrdinalIgnoreCase));
}
