using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Rolan.Models;
using Rolan.ViewModels;

namespace Rolan.Views;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        // 注册全局热键
        var settings = Models.AppSettings.Load();
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var source = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);

        // 延迟注册热键以确保窗口句柄有效
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
        {
            var hotkeyService = App.Current?.FindResource("HotkeyService") as Services.IHotkeyService;
            hotkeyService?.Register(hwnd, 1, settings.HotkeyModifiers, settings.HotkeyKey);
        });

        // 绑定 PanelService
        vm.PanelService.Attach(this);
        vm.PanelService.PositionPanel();
        vm.PanelService.SetMousePenetration(settings.MousePenetration);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Helpers.NativeMethods.WM_HOTKEY && wParam.ToInt32() == 1)
        {
            ViewModel?.ToggleVisibilityCommand.Execute(null);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnPanelMouseEnter(object sender, MouseEventArgs e)
    {
        ViewModel?.PanelService.OnMouseEnter();
    }

    private void OnPanelMouseLeave(object sender, MouseEventArgs e)
    {
        ViewModel?.PanelService.OnMouseLeave();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.OpenSettingsCommand.Execute(null);
    }

    private void OnHideClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleVisibilityCommand.Execute(null);
    }

    private void OnAddGroupClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.AddGroupCommand.Execute(null);
    }

    // 拖拽添加
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ViewModel?.AddShortcutFromDragDrop(files);
        }
        else if (e.Data.GetDataPresent(DataFormats.Text))
        {
            var text = e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrEmpty(text) && (text.StartsWith("http://") || text.StartsWith("https://")))
            {
                ViewModel?.AddShortcutCommand.Execute(text);
            }
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    // ContextMenu 事件处理
    private void OnOpenFileLocation(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ShortcutItem item)
        {
            try
            {
                var dir = Path.GetDirectoryName(item.TargetPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.TargetPath}\"");
            }
            catch { }
        }
    }

    private void OnEditShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ShortcutItem item)
        {
            // 简单的编辑对话框
            var name = Microsoft.VisualBasic.Interaction.InputBox("名称:", "编辑快捷方式", item.Name);
            if (!string.IsNullOrEmpty(name))
            {
                item.Name = name;
                ViewModel?.GetType().GetMethod("RenameGroupCommand")?.Invoke(ViewModel, new[] { item });
            }
        }
    }

    private void OnDeleteShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ShortcutItem item)
        {
            ViewModel?.DeleteShortcutCommand.Execute(item);
        }
    }

    // 快捷方式单击启动
    private void OnShortcutMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is ShortcutItem item)
        {
            ViewModel?.LaunchItemCommand.Execute(item);
        }
        e.Handled = true;
    }

    // 窗口拖动
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
}
