using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Rolan.Models;
using Rolan.Services;
using Rolan.ViewModels;

namespace Rolan.Views;

public partial class MainWindow : Window
{
    private readonly IHotkeyService _hotkeyService;
    private IntPtr _windowHandle;
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow(IHotkeyService hotkeyService)
    {
        _hotkeyService = hotkeyService;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        _windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var source = System.Windows.Interop.HwndSource.FromHwnd(_windowHandle);
        source?.AddHook(WndProc);

        // 注册全局热键
        var settings = AppSettings.Load();
        vm.PanelService.UpdateSettings(settings);
        if (!_hotkeyService.Register(_windowHandle, 1, settings.HotkeyModifiers, settings.HotkeyKey))
        {
            MessageBox.Show("全局热键注册失败，快捷键可能已被其他程序占用。", "Rolan",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // 绑定面板服务
        vm.PanelService.Attach(this);
        vm.PanelService.PositionPanel();
        vm.PanelService.SetMousePenetration(settings.MousePenetration);
        vm.PanelService.SetTopMost(settings.TopMost);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Rolan.Helpers.NativeMethods.WM_HOTKEY && wParam.ToInt32() == 1)
        {
            ViewModel?.ToggleVisibilityCommand.Execute(null);
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ---- 鼠标面板事件 ----

    private void OnPanelMouseEnter(object sender, MouseEventArgs e)
        => ViewModel?.PanelService.OnMouseEnter();

    private void OnPanelMouseLeave(object sender, MouseEventArgs e)
        => ViewModel?.PanelService.OnMouseLeave();

    // ---- 按钮事件 ----

    private void OnSettingsClick(object sender, RoutedEventArgs e)
        => ViewModel?.OpenSettingsCommand.Execute(null);

    private void OnHideClick(object sender, RoutedEventArgs e)
        => ViewModel?.ToggleVisibilityCommand.Execute(null);

    private void OnAddGroupClick(object sender, RoutedEventArgs e)
        => ViewModel?.AddGroupCommand.Execute(null);

    private void OnBrowseAdd(object sender, RoutedEventArgs e)
        => ViewModel?.BrowseAddShortcutCommand.Execute(null);

    // ---- 拖拽添加 ----

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (ViewModel != null)
                await ViewModel.AddShortcutFromDragDrop(files);
        }
        else if (e.Data.GetDataPresent(DataFormats.Text))
        {
            var text = e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrEmpty(text) &&
                (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                if (ViewModel != null)
                    await ViewModel.AddShortcutCommand.ExecuteAsync(text);
            }
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    // ---- 快捷方式点击启动 ----

    private void OnShortcutMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is ShortcutItem item)
            ViewModel?.LaunchItemCommand.Execute(item);
        e.Handled = true;
    }

    // ---- 右键菜单处理 ----

    private void OnOpenShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ShortcutItem item)
            ViewModel?.LaunchItemCommand.Execute(item);
    }

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

    private async void OnEditShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ShortcutItem item)
        {
            var dialog = new EditShortcutDialog(item);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && ViewModel != null)
                await ViewModel.UpdateShortcutAsync(item);
        }
    }

    private void OnDeleteShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ShortcutItem item)
            ViewModel?.DeleteShortcutCommand.Execute(item);
    }

    private void OnMoveUp(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ShortcutItem item)
            ViewModel?.MoveShortcutUpCommand.Execute(item);
    }

    private void OnMoveDown(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ShortcutItem item)
            ViewModel?.MoveShortcutDownCommand.Execute(item);
    }

    private void OnMoveToGroup(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is ShortcutGroup targetGroup &&
            mi.TryFindParent<ContextMenu>()?.DataContext is ShortcutItem item)
        {
            ViewModel?.MoveToGroupCommand.Execute(new Tuple<ShortcutItem, ShortcutGroup>(item, targetGroup));
        }
    }

    // ---- 标签页右键菜单处理 ----

    private void OnRenameGroup(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.DataContext is not ShortcutGroup group) return;

        var dialog = new InputDialog("重命名分组", "请输入新的分组名称:", group.Name);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.Result))
        {
            group.Name = dialog.Result;
            ViewModel?.RenameGroupCommand.Execute(group);
        }
    }

    private void OnDeleteGroup(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.DataContext is not ShortcutGroup group) return;

        if (MessageBox.Show($"确认删除分组 \"{group.Name}\" 及其所有快捷方式？",
                "Rolan", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            ViewModel?.DeleteGroupCommand.Execute(group);
        }
    }

    // ---- 窗口拖动 ----

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_windowHandle != IntPtr.Zero)
            _hotkeyService.Unregister(_windowHandle, 1);
    }
}

internal static class WpfExtensions
{
    public static T? TryFindParent<T>(this DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T t) return t;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
