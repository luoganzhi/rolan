using System.IO;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rolan.Helpers;
using Rolan.Models;
using Rolan.Services;
using Rolan.ViewModels;
using WpfButton = System.Windows.Controls.Button;
using WpfDataObject = System.Windows.DataObject;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace Rolan.Views;

public partial class MainWindow : Window
{
    private const int HotkeyId = 1;
    private const int DefaultHotkeyModifiers = 1; // Alt
    private const int DefaultHotkeyKey = 0x20;    // Space
    private const int FallbackHotkeyModifiers = 1 | 2; // Ctrl + Alt
    private const int FallbackHotkeyKey = 0x52;        // R
    private const double ShortcutTileWidth = 76;
    private const double ShortcutTileHeight = 96;
    private const double PanelChromeHeight = 110;
    private const double EmptyContentHeight = 56;
    private const double ContentHeightPadding = 28;

    private readonly IHotkeyService _hotkeyService;
    private IntPtr _windowHandle;
    private bool _isHotkeyRegistered;
    private int _registeredHotkeyModifiers;
    private int _registeredHotkeyKey;
    private WpfPoint _shortcutDragStart;
    private WpfPoint _groupDragStart;
    private ShortcutItem? _pendingShortcutDragItem;
    private ShortcutGroup? _pendingGroupDragItem;
    private DateTime _lastShortcutDragCompleted = DateTime.MinValue;
    private bool _fittingPanelHeight;
    private bool _isPanelHeightFitAttached;
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
        vm.SettingsChanged += OnSettingsChanged;
        RegisterHotkeyWithWarning(settings);

        // 绑定面板服务
        vm.PanelService.Attach(this);
        vm.PanelService.PositionPanel();
        vm.PanelService.SetMousePenetration(settings.MousePenetration);
        vm.PanelService.SetTopMost(settings.TopMost);
        vm.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnMainWindowLoaded;
        FocusSearchBox();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Rolan.Helpers.NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            TogglePanelFromHotkey();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (!_isPanelHeightFitAttached)
        {
            SizeChanged += OnMainWindowSizeChanged;
            _isPanelHeightFitAttached = true;
        }

        ScheduleFitPanelHeightToContent();
    }

    private void OnMainWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged || _fittingPanelHeight)
            return;

        ScheduleFitPanelHeightToContent();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.FilteredItems) or nameof(MainViewModel.HasNoFilteredItems))
            ScheduleFitPanelHeightToContent();
    }

    // ---- 鼠标面板事件 ----

    private void OnPanelMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        => ViewModel?.PanelService.OnMouseEnter();

    private void OnPanelMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => ViewModel?.PanelService.OnMouseLeave();

    // ---- 按钮事件 ----

    private void OnSettingsClick(object sender, RoutedEventArgs e)
        => ViewModel?.OpenSettingsCommand.Execute(null);

    private void OnHideClick(object sender, RoutedEventArgs e)
        => ViewModel?.ToggleVisibilityCommand.Execute(null);

    private async void OnAddGroupClick(object sender, RoutedEventArgs e)
        => await AddGroupWithNameAsync();

    private void OnAddMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button)
            return;

        var menu = new ContextMenu
        {
            PlacementTarget = button,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };

        AddMenuItem(menu, "添加文件...", OnBrowseAdd);
        AddMenuItem(menu, "添加路径或网址...", OnAddPathOrUrl);
        AddMenuItem(menu, "添加文件夹...", OnBrowseAddFolder);
        AddMenuItem(menu, "添加系统命令...", OnAddSystemCommand);
        menu.Items.Add(new System.Windows.Controls.Separator());
        AddMenuItem(menu, "导入开始菜单快捷方式", OnImportStartMenu);
        AddMenuItem(menu, "导入桌面快捷方式", OnImportDesktop);

        SuppressAutoHideWhileOpen(menu);
        menu.IsOpen = true;
    }

    private void OnBrowseAdd(object sender, RoutedEventArgs e)
        => ViewModel?.BrowseAddShortcutCommand.Execute(null);

    private async void OnAddPathOrUrl(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("添加快捷方式", "输入文件、文件夹路径或网址:", SearchBox.Text);
        dialog.Owner = this;
        using (ViewModel?.PanelService.SuspendAutoHide())
        {
            if (dialog.ShowDialog() == true && ViewModel != null)
                await ViewModel.AddShortcutCommand.ExecuteAsync(dialog.Result);
        }
    }

    private void OnBrowseAddFolder(object sender, RoutedEventArgs e)
        => ViewModel?.BrowseAddFolderCommand.Execute(null);

    private void OnAddSystemCommand(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        var menu = new ContextMenu
        {
            PlacementTarget = ResolveMenuPlacementTarget(sender),
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };

        foreach (var command in SystemCommandHelper.Commands)
        {
            var item = new MenuItem
            {
                Header = command.Name,
                Tag = command.TargetPath
            };
            item.Click += async (_, _) =>
            {
                if (ViewModel != null)
                    await ViewModel.AddShortcutCommand.ExecuteAsync(command.TargetPath);
            };
            menu.Items.Add(item);
        }

        SuppressAutoHideWhileOpen(menu);
        menu.IsOpen = true;
    }

    private void OnImportStartMenu(object sender, RoutedEventArgs e)
        => ViewModel?.ImportStartMenuShortcutsCommand.Execute(null);

    private void OnImportDesktop(object sender, RoutedEventArgs e)
        => ViewModel?.ImportDesktopShortcutsCommand.Execute(null);

    public async Task AddGroupWithNameAsync()
    {
        if (ViewModel == null)
            return;

        var defaultName = $"分组 {ViewModel.Groups.Count + 1}";
        var dialog = new InputDialog("新建分组", "请输入分组名称:", defaultName)
        {
            Owner = this
        };

        using (ViewModel.PanelService.SuspendAutoHide())
        {
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Result))
                await ViewModel.AddGroupCommand.ExecuteAsync(dialog.Result);
        }
    }

    private static void AddMenuItem(ContextMenu menu, string header, RoutedEventHandler clickHandler)
    {
        var item = new MenuItem { Header = header };
        item.Click += clickHandler;
        menu.Items.Add(item);
    }

    private FrameworkElement? ResolveMenuPlacementTarget(object sender)
    {
        return sender switch
        {
            MenuItem menuItem => ResolveOwningContextMenu(menuItem)?.PlacementTarget as FrameworkElement,
            FrameworkElement element => element,
            _ => null
        };
    }

    private async void OnWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            var shortcutIndex = TryGetIndexFromKey(e.SystemKey);
            if (shortcutIndex >= 0)
            {
                e.Handled = true;

                if (ViewModel != null)
                    await ViewModel.LaunchFilteredItemByIndexCommand.ExecuteAsync(shortcutIndex);

                return;
            }
        }

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        if (e.Key == Key.V && TryGetShortcutTargetsFromClipboard(out var clipboardTargets))
        {
            e.Handled = true;
            await AddClipboardShortcutTargetsAsync(clipboardTargets);
            _ = Dispatcher.BeginInvoke(FocusSearchBox);
            return;
        }

        if (e.Key == Key.Tab)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                ViewModel?.SelectPreviousGroupCommand.Execute(null);
            else
                ViewModel?.SelectNextGroupCommand.Execute(null);

            _ = Dispatcher.BeginInvoke(FocusSearchBox);
            e.Handled = true;
            return;
        }

        var groupIndex = TryGetIndexFromKey(e.Key);
        if (groupIndex >= 0)
        {
            ViewModel?.SelectGroupByIndexCommand.Execute(groupIndex);
            _ = Dispatcher.BeginInvoke(FocusSearchBox);
            e.Handled = true;
        }
    }

    private void OnWindowPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Keyboard.FocusedElement == SearchBox ||
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ||
            Keyboard.Modifiers.HasFlag(ModifierKeys.Windows) ||
            string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        FocusSearchBox();
        SearchBox.SelectedText = e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        e.Handled = true;
    }

    private static int TryGetIndexFromKey(Key key)
    {
        if (key is >= Key.D1 and <= Key.D9)
            return key - Key.D1;

        if (key is >= Key.NumPad1 and <= Key.NumPad9)
            return key - Key.NumPad1;

        return -1;
    }

    private async Task AddClipboardShortcutTargetsAsync(string[] targets)
    {
        if (ViewModel == null)
            return;

        foreach (var target in targets)
            await ViewModel.AddShortcutCommand.ExecuteAsync(target);

        ScrollSelectedShortcutIntoView();
    }

    private static bool TryGetShortcutTargetsFromClipboard(out string[] targets)
    {
        targets = [];

        try
        {
            if (System.Windows.Clipboard.ContainsFileDropList())
            {
                targets = System.Windows.Clipboard.GetFileDropList()
                    .Cast<string>()
                    .Where(target => !string.IsNullOrWhiteSpace(target))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return targets.Length > 0;
            }

            if (!System.Windows.Clipboard.ContainsText())
                return false;

            targets = ParseShortcutTargetsFromText(System.Windows.Clipboard.GetText());
            return targets.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string[] ParseShortcutTargetsFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var targets = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
            .Select(TargetPathHelper.NormalizeInput)
            .OfType<string>()
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return targets.Length > 0 && targets.All(LooksLikeShortcutTarget)
            ? targets
            : [];
    }

    private static string[] ParseShortcutTargetsFromDragDropText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var targets = ParseShortcutTargetsFromText(text);
        if (targets.Length > 0)
            return targets;

        var normalized = TargetPathHelper.NormalizeInput(text);
        return string.IsNullOrWhiteSpace(normalized) ? [] : [normalized];
    }

    private static bool LooksLikeShortcutTarget(string target)
    {
        if (SystemCommandHelper.IsSystemCommand(target))
            return true;

        if (TargetPathHelper.IsUrl(target) && !target.Any(char.IsWhiteSpace))
            return true;

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(target);
            if (Path.IsPathFullyQualified(expanded))
                return File.Exists(expanded) || Directory.Exists(expanded);

            var resolved = TargetPathHelper.Resolve(target);
            return File.Exists(resolved) || Directory.Exists(resolved);
        }
        catch
        {
            return false;
        }
    }

    private void OnSearchKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (IsSearchTextCopyShortcut(e))
            return;

        if (HandleSelectedShortcutKey(e, allowPlainDelete: string.IsNullOrEmpty(SearchBox.Text)))
            return;

        if (HandleShortcutNavigationKey(e))
            return;

        switch (e.Key)
        {
            case Key.Escape:
                if (!string.IsNullOrWhiteSpace(SearchBox.Text))
                    SearchBox.Clear();
                else
                    ViewModel?.ToggleVisibilityCommand.Execute(null);

                e.Handled = true;
                break;
            case Key.Enter:
                ViewModel?.LaunchFirstFilteredItemCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private bool IsSearchTextCopyShortcut(System.Windows.Input.KeyEventArgs e)
        => SearchBox.SelectionLength > 0 &&
           e.Key == Key.C &&
           Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

    private void OnShortcutGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        => ScrollSelectedShortcutIntoView();

    private async void OnShortcutGridKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (HandleShortcutNavigationKey(e))
            return;

        await HandleSelectedShortcutKeyAsync(e, allowPlainDelete: true);
    }

    private bool HandleShortcutNavigationKey(System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                ViewModel?.SelectNextShortcutCommand.Execute(null);
                break;
            case Key.Up:
                ViewModel?.SelectPreviousShortcutCommand.Execute(null);
                break;
            case Key.PageDown:
                ViewModel?.SelectNextShortcutPageCommand.Execute(null);
                break;
            case Key.PageUp:
                ViewModel?.SelectPreviousShortcutPageCommand.Execute(null);
                break;
            case Key.Home:
                ViewModel?.SelectFirstShortcutCommand.Execute(null);
                break;
            case Key.End:
                ViewModel?.SelectLastShortcutCommand.Execute(null);
                break;
            default:
                return false;
        }

        ScrollSelectedShortcutIntoView();
        e.Handled = true;
        return true;
    }

    private bool HandleSelectedShortcutKey(System.Windows.Input.KeyEventArgs e, bool allowPlainDelete)
    {
        if (e.Key == Key.F2 ||
            e.Key == Key.Delete && (allowPlainDelete || Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) ||
            e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            e.Key == Key.D && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            e.Key == Key.O && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            _ = HandleSelectedShortcutKeyAsync(e, allowPlainDelete);
            return true;
        }

        return false;
    }

    private async Task HandleSelectedShortcutKeyAsync(System.Windows.Input.KeyEventArgs e, bool allowPlainDelete)
    {
        var item = ViewModel?.SelectedShortcut;
        if (item == null)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                ViewModel?.LaunchItemCommand.Execute(item);
                e.Handled = true;
                break;
            case Key.F2:
                e.Handled = true;
                await EditShortcutAsync(item);
                break;
            case Key.Delete when allowPlainDelete || Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                e.Handled = true;
                DeleteShortcutWithConfirmation(item);
                break;
            case Key.O when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                e.Handled = true;
                OpenFileLocation(item);
                break;
            case Key.C when Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
                            Keyboard.Modifiers.HasFlag(ModifierKeys.Shift):
                e.Handled = true;
                CopyTextToClipboard(BuildCommandLine(item));
                break;
            case Key.C when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                e.Handled = true;
                CopyTextToClipboard(item.TargetPath);
                break;
            case Key.D when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                e.Handled = true;
                ViewModel?.DuplicateShortcutCommand.Execute(item);
                break;
        }
    }

    // ---- 拖拽添加 ----

    private async void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ShortcutItem)) &&
            e.Data.GetData(typeof(ShortcutItem)) is ShortcutItem item &&
            ViewModel?.SelectedGroup is ShortcutGroup targetGroup)
        {
            await ViewModel.MoveShortcutToGroupEndAsync(item, targetGroup);
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (ViewModel != null)
                await ViewModel.AddShortcutFromDragDrop(files);
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(System.Windows.DataFormats.Text))
        {
            var text = e.Data.GetData(System.Windows.DataFormats.Text) as string;
            if (!string.IsNullOrWhiteSpace(text) && ViewModel != null)
            {
                foreach (var target in ParseShortcutTargetsFromDragDropText(text))
                    await ViewModel.AddShortcutCommand.ExecuteAsync(target);

                ScrollSelectedShortcutIntoView();
            }

            e.Handled = true;
        }
    }

    private void OnDragEnter(object sender, System.Windows.DragEventArgs e)
        => SetDragEffects(e);

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
        => SetDragEffects(e);

    private static void SetDragEffects(System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ShortcutItem)) ||
                    e.Data.GetDataPresent(typeof(ShortcutGroup))
            ? System.Windows.DragDropEffects.Move
            : System.Windows.DragDropEffects.Copy;
        e.Handled = true;
    }

    // ---- 快捷方式点击启动 ----

    private void OnShortcutPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pendingShortcutDragItem = (sender as FrameworkElement)?.DataContext as ShortcutItem;
        _shortcutDragStart = e.GetPosition(this);
    }

    private void OnShortcutMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_pendingShortcutDragItem == null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(this);
        if (!HasReachedDragThreshold(_shortcutDragStart, current))
            return;

        var data = new WpfDataObject();
        data.SetData(typeof(ShortcutItem), _pendingShortcutDragItem);
        System.Windows.DragDrop.DoDragDrop((DependencyObject)sender, data, WpfDragDropEffects.Move);
        _pendingShortcutDragItem = null;
        _lastShortcutDragCompleted = DateTime.UtcNow;
        e.Handled = true;
    }

    private void OnShortcutMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var completedDragRecently = DateTime.UtcNow - _lastShortcutDragCompleted < TimeSpan.FromMilliseconds(250);
        var item = (sender as FrameworkElement)?.DataContext as ShortcutItem;
        _pendingShortcutDragItem = null;

        if (!completedDragRecently && item != null)
            ViewModel?.LaunchItemCommand.Execute(item);

        e.Handled = true;
    }

    private void OnShortcutDragOver(object sender, System.Windows.DragEventArgs e)
        => SetDragEffects(e);

    private async void OnShortcutDrop(object sender, System.Windows.DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ShortcutItem targetItem ||
            !e.Data.GetDataPresent(typeof(ShortcutItem)) ||
            e.Data.GetData(typeof(ShortcutItem)) is not ShortcutItem movedItem ||
            ViewModel == null)
        {
            return;
        }

        await ViewModel.ReorderShortcutAsync(movedItem, targetItem);
        e.Handled = true;
    }

    private void OnGroupPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pendingGroupDragItem = (sender as FrameworkElement)?.DataContext as ShortcutGroup;
        _groupDragStart = e.GetPosition(this);
    }

    private void OnGroupMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_pendingGroupDragItem == null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(this);
        if (!HasReachedDragThreshold(_groupDragStart, current))
            return;

        var data = new WpfDataObject();
        data.SetData(typeof(ShortcutGroup), _pendingGroupDragItem);
        System.Windows.DragDrop.DoDragDrop((DependencyObject)sender, data, WpfDragDropEffects.Move);
        _pendingGroupDragItem = null;
        e.Handled = true;
    }

    private void OnGroupDragOver(object sender, System.Windows.DragEventArgs e)
        => SetDragEffects(e);

    private async void OnGroupDrop(object sender, System.Windows.DragEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ShortcutGroup targetGroup || ViewModel == null)
            return;

        if (e.Data.GetDataPresent(typeof(ShortcutGroup)) &&
            e.Data.GetData(typeof(ShortcutGroup)) is ShortcutGroup movedGroup)
        {
            await ViewModel.ReorderGroupAsync(movedGroup, targetGroup);
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(typeof(ShortcutItem)) &&
            e.Data.GetData(typeof(ShortcutItem)) is ShortcutItem movedItem)
        {
            await ViewModel.MoveShortcutToGroupEndAsync(movedItem, targetGroup);
            e.Handled = true;
        }
    }

    private static bool HasReachedDragThreshold(WpfPoint start, WpfPoint current)
        => Math.Abs(current.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance ||
           Math.Abs(current.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance;

    private async Task EditShortcutAsync(ShortcutItem item)
    {
        var dialog = new EditShortcutDialog(item);
        dialog.Owner = this;
        using (ViewModel?.PanelService.SuspendAutoHide())
        {
            if (dialog.ShowDialog() == true && ViewModel != null)
                await ViewModel.UpdateShortcutAsync(item);
        }
    }

    private void DeleteShortcutWithConfirmation(ShortcutItem item)
    {
        using (ViewModel?.PanelService.SuspendAutoHide())
        {
            if (System.Windows.MessageBox.Show($"确认删除快捷方式 \"{item.Name}\"？",
                    "Rolan", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) ==
                System.Windows.MessageBoxResult.Yes)
            {
                ViewModel?.DeleteShortcutCommand.Execute(item);
            }
        }
    }

    private static void OpenFileLocation(ShortcutItem item)
    {
        try
        {
            var resolvedTargetPath = TargetPathHelper.Resolve(item.TargetPath);
            if (Directory.Exists(resolvedTargetPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{resolvedTargetPath}\"");
            }
            else if (File.Exists(resolvedTargetPath))
            {
                var dir = Path.GetDirectoryName(resolvedTargetPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{resolvedTargetPath}\"");
            }
        }
        catch { }
    }

    private static bool CanOpenFileLocation(ShortcutItem item)
    {
        if (item.Type is ShortcutType.Url or ShortcutType.SystemCommand)
            return false;

        try
        {
            var resolvedTargetPath = TargetPathHelper.Resolve(item.TargetPath);
            return Directory.Exists(resolvedTargetPath) || File.Exists(resolvedTargetPath);
        }
        catch
        {
            return false;
        }
    }

    // ---- 右键菜单处理 ----

    private void OnShortcutContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu ||
            menu.PlacementTarget is not FrameworkElement placement ||
            placement.DataContext is not ShortcutItem item ||
            ViewModel == null)
        {
            return;
        }

        SuppressAutoHideWhileOpen(menu);
        menu.DataContext = item;
        var openLocationMenu = menu.Items.OfType<MenuItem>()
            .FirstOrDefault(mi => string.Equals(mi.Header as string, "打开文件位置", StringComparison.Ordinal));
        if (openLocationMenu != null)
            openLocationMenu.IsEnabled = CanOpenFileLocation(item);

        var sourceGroup = ViewModel.Groups.FirstOrDefault(group => group.Id == item.GroupId);
        var orderedItems = sourceGroup?.Items.OrderBy(shortcut => shortcut.Order).ToList() ?? new List<ShortcutItem>();
        var itemIndex = orderedItems.FindIndex(shortcut => shortcut.Id == item.Id);

        var moveUpMenu = menu.Items.OfType<MenuItem>()
            .FirstOrDefault(mi => string.Equals(mi.Header as string, "上移", StringComparison.Ordinal));
        if (moveUpMenu != null)
            moveUpMenu.IsEnabled = itemIndex > 0;

        var moveDownMenu = menu.Items.OfType<MenuItem>()
            .FirstOrDefault(mi => string.Equals(mi.Header as string, "下移", StringComparison.Ordinal));
        if (moveDownMenu != null)
            moveDownMenu.IsEnabled = itemIndex >= 0 && itemIndex < orderedItems.Count - 1;

        var resetStatsMenu = menu.Items.OfType<MenuItem>()
            .FirstOrDefault(mi => string.Equals(mi.Header as string, "重置使用记录", StringComparison.Ordinal));
        if (resetStatsMenu != null)
            resetStatsMenu.IsEnabled = item.LaunchCount > 0 || item.LastLaunchedAt != null;

        var moveMenu = menu.Items.OfType<MenuItem>()
            .FirstOrDefault(mi => string.Equals(mi.Header as string, "移动到分组", StringComparison.Ordinal));

        if (moveMenu == null) return;

        var targetGroups = ViewModel.Groups
            .Where(group => group.Id != item.GroupId)
            .OrderBy(group => group.Order)
            .ToList();

        moveMenu.Tag = item;
        moveMenu.ItemsSource = targetGroups;
        moveMenu.IsEnabled = targetGroups.Count > 0;
    }

    private void OnOpenShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && ResolveShortcutItem(mi) is ShortcutItem item)
            ViewModel?.LaunchItemCommand.Execute(item);
    }

    private void OnOpenFileLocation(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && ResolveShortcutItem(mi) is ShortcutItem item)
            OpenFileLocation(item);
    }

    private void OnCopyTargetPath(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && ResolveShortcutItem(mi) is ShortcutItem item)
            CopyTextToClipboard(item.TargetPath);
    }

    private void OnCopyCommandLine(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && ResolveShortcutItem(mi) is ShortcutItem item)
            CopyTextToClipboard(BuildCommandLine(item));
    }

    private void OnDuplicateShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && ResolveShortcutItem(mi) is ShortcutItem item)
            ViewModel?.DuplicateShortcutCommand.Execute(item);
    }

    private async void OnEditShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && ResolveShortcutItem(mi) is ShortcutItem item)
            await EditShortcutAsync(item);
    }

    private void OnResetShortcutStats(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && ResolveShortcutItem(mi) is ShortcutItem item)
            ViewModel?.ResetShortcutStatsCommand.Execute(item);
    }

    private void OnDeleteShortcut(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && ResolveShortcutItem(mi) is ShortcutItem item)
            DeleteShortcutWithConfirmation(item);
    }

    private void OnMoveUp(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && ResolveShortcutItem(mi) is ShortcutItem item)
            ViewModel?.MoveShortcutUpCommand.Execute(item);
    }

    private void OnMoveDown(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && ResolveShortcutItem(mi) is ShortcutItem item)
            ViewModel?.MoveShortcutDownCommand.Execute(item);
    }

    private void OnMoveToGroup(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi &&
            mi.DataContext is ShortcutGroup targetGroup &&
            ResolveMoveSourceItem(mi) is ShortcutItem item)
        {
            ViewModel?.MoveToGroupCommand.Execute(new Tuple<ShortcutItem, ShortcutGroup>(item, targetGroup));
        }
    }

    // ---- 标签页右键菜单处理 ----

    private void OnRenameGroup(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || ResolveGroupItem(mi) is not ShortcutGroup group) return;

        var dialog = new InputDialog("重命名分组", "请输入新的分组名称:", group.Name);
        dialog.Owner = this;
        using (ViewModel?.PanelService.SuspendAutoHide())
        {
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Result))
                ViewModel?.RenameGroupCommand.Execute(new Tuple<ShortcutGroup, string>(group, dialog.Result));
        }
    }

    private void OnDeleteGroup(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || ResolveGroupItem(mi) is not ShortcutGroup group || ViewModel is not { } vm) return;

        var targetGroup = vm.Groups
            .Where(candidate => candidate.Id != group.Id)
            .OrderBy(candidate => candidate.Order)
            .FirstOrDefault();

        using (vm.PanelService.SuspendAutoHide())
        {
            var hasItems = group.Items.Count > 0;
            var message = hasItems && targetGroup != null
                ? $"删除分组 \"{group.Name}\"？\n\n选择“是”会先把其中 {group.Items.Count} 个快捷方式移动到 \"{targetGroup.Name}\"。\n选择“否”会连同其中所有快捷方式一起删除。"
                : $"确认删除分组 \"{group.Name}\" 及其所有快捷方式？";

            var buttons = hasItems && targetGroup != null
                ? System.Windows.MessageBoxButton.YesNoCancel
                : System.Windows.MessageBoxButton.YesNo;

            var result = System.Windows.MessageBox.Show(message,
                "Rolan", buttons, System.Windows.MessageBoxImage.Warning);
            if (result is System.Windows.MessageBoxResult.Cancel or System.Windows.MessageBoxResult.None)
                return;

            if (hasItems && targetGroup != null && result == System.Windows.MessageBoxResult.Yes)
            {
                vm.DeleteGroupCommand.Execute(
                    new MainViewModel.DeleteGroupRequest(group, targetGroup));
            }
            else
            {
                vm.DeleteGroupCommand.Execute(new MainViewModel.DeleteGroupRequest(group, null));
            }
        }
    }

    // ---- 窗口拖动 ----

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        if (e.OriginalSource is WpfButton ||
            (e.OriginalSource as DependencyObject)?.TryFindParent<WpfButton>() != null)
            return;

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if the mouse button is released during event routing.
        }
    }

    private void ScrollSelectedShortcutIntoView()
    {
        if (ViewModel?.SelectedShortcut is ShortcutItem item)
            ShortcutGrid.ScrollIntoView(item);
    }

    private void ScheduleFitPanelHeightToContent()
    {
        if (!IsLoaded)
            return;

        _ = Dispatcher.BeginInvoke(FitPanelHeightToContent);
    }

    private void FitPanelHeightToContent()
    {
        if (ViewModel == null)
            return;

        var desiredHeight = CalculateDesiredPanelHeight();
        _fittingPanelHeight = true;
        try
        {
            ViewModel.PanelService.FitHeightToContent(desiredHeight);
        }
        finally
        {
            _fittingPanelHeight = false;
        }
    }

    private double CalculateDesiredPanelHeight()
    {
        var itemCount = ShortcutGrid.Items.Count;
        var availableGridWidth = Math.Max(ShortcutTileWidth, ShortcutGrid.ActualWidth - ShortcutGrid.Padding.Left - ShortcutGrid.Padding.Right);
        var columns = Math.Max(1, (int)Math.Floor(availableGridWidth / ShortcutTileWidth));
        var rows = itemCount == 0 ? 0 : (int)Math.Ceiling(itemCount / (double)columns);
        var contentHeight = itemCount == 0
            ? EmptyContentHeight
            : rows * ShortcutTileHeight + ContentHeightPadding;

        return PanelChromeHeight + contentHeight;
    }

    private void SuppressAutoHideWhileOpen(ContextMenu menu)
    {
        var autoHideScope = ViewModel?.PanelService.SuspendAutoHide();
        if (autoHideScope == null)
            return;

        RoutedEventHandler? closedHandler = null;
        closedHandler = (_, _) =>
        {
            menu.Closed -= closedHandler;
            autoHideScope.Dispose();
        };
        menu.Closed += closedHandler;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (ViewModel != null)
            ViewModel.SettingsChanged -= OnSettingsChanged;

        if (ViewModel != null)
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        Loaded -= OnMainWindowLoaded;
        if (_isPanelHeightFitAttached)
        {
            SizeChanged -= OnMainWindowSizeChanged;
            _isPanelHeightFitAttached = false;
        }

        if (_windowHandle != IntPtr.Zero && _isHotkeyRegistered)
            _hotkeyService.Unregister(_windowHandle, HotkeyId);
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        ViewModel?.PanelService.UpdateSettings(settings);
        ViewModel?.PanelService.SetMousePenetration(settings.MousePenetration);
        ViewModel?.PanelService.SetTopMost(settings.TopMost);
        ScheduleFitPanelHeightToContent();

        if (_windowHandle == IntPtr.Zero)
            return;

        var previousWasRegistered = _isHotkeyRegistered;
        var previousModifiers = _registeredHotkeyModifiers;
        var previousKey = _registeredHotkeyKey;

        if (_isHotkeyRegistered)
            _hotkeyService.Unregister(_windowHandle, HotkeyId);

        var newRegistration = TryRegisterHotkey(settings);
        if (newRegistration.Success)
        {
            ApplyHotkeyRegistration(newRegistration.Modifiers, newRegistration.Key);
            if (newRegistration.UsedFallback)
            {
                settings.HotkeyModifiers = newRegistration.Modifiers;
                settings.HotkeyKey = newRegistration.Key;
                settings.Save();
                System.Windows.MessageBox.Show(
                    $"Alt + Space 已被占用，已改用 {FormatHotkey(newRegistration.Modifiers, newRegistration.Key)}。",
                    "Rolan", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            return;
        }

        _isHotkeyRegistered = false;
        if (previousWasRegistered &&
            _hotkeyService.Register(_windowHandle, HotkeyId, previousModifiers, previousKey))
        {
            ApplyHotkeyRegistration(previousModifiers, previousKey);
            settings.HotkeyModifiers = previousModifiers;
            settings.HotkeyKey = previousKey;
            settings.Save();
            System.Windows.MessageBox.Show(
                $"新热键注册失败，已恢复为 {FormatHotkey(previousModifiers, previousKey)}。",
                "Rolan", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        System.Windows.MessageBox.Show("全局热键注册失败，快捷键可能已被其他程序占用。", "Rolan",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    }

    private void RegisterHotkeyWithWarning(AppSettings settings)
    {
        var registration = TryRegisterHotkey(settings);
        if (registration.Success)
        {
            ApplyHotkeyRegistration(registration.Modifiers, registration.Key);
            return;
        }

        _isHotkeyRegistered = false;
        if (!_isHotkeyRegistered)
        {
            System.Windows.MessageBox.Show("全局热键注册失败，快捷键可能已被其他程序占用。", "Rolan",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    private HotkeyRegistration TryRegisterHotkey(AppSettings settings)
    {
        if (_hotkeyService.Register(_windowHandle, HotkeyId, settings.HotkeyModifiers, settings.HotkeyKey))
            return new HotkeyRegistration(true, settings.HotkeyModifiers, settings.HotkeyKey, false);

        var usesDefaultRolanHotkey = settings.HotkeyModifiers == DefaultHotkeyModifiers &&
                                     settings.HotkeyKey == DefaultHotkeyKey;
        if (usesDefaultRolanHotkey &&
            _hotkeyService.Register(_windowHandle, HotkeyId, FallbackHotkeyModifiers, FallbackHotkeyKey))
        {
            return new HotkeyRegistration(true, FallbackHotkeyModifiers, FallbackHotkeyKey, true);
        }

        return new HotkeyRegistration(false, 0, 0, false);
    }

    private void ApplyHotkeyRegistration(int modifiers, int key)
    {
        _isHotkeyRegistered = true;
        _registeredHotkeyModifiers = modifiers;
        _registeredHotkeyKey = key;
    }

    private static string FormatHotkey(int modifiers, int key)
    {
        var parts = new List<string>();
        if ((modifiers & Rolan.Helpers.NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & Rolan.Helpers.NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & Rolan.Helpers.NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & Rolan.Helpers.NativeMethods.MOD_WIN) != 0) parts.Add("Win");
        parts.Add(KeyInterop.KeyFromVirtualKey(key).ToString());
        return string.Join(" + ", parts);
    }

    private void TogglePanelFromHotkey()
    {
        if (!IsVisible || !IsActive)
        {
            Show();
            Activate();
            if (ViewModel?.PanelService.IsHidden == true)
                ViewModel.PanelService.AnimateShow();
            FocusSearchBox();
            return;
        }

        var wasHidden = ViewModel?.PanelService.IsHidden == true;
        ViewModel?.ToggleVisibilityCommand.Execute(null);
        if (wasHidden)
            Dispatcher.BeginInvoke(FocusSearchBox);
    }

    private static ShortcutItem? ResolveShortcutItem(MenuItem menuItem)
        => menuItem.DataContext as ShortcutItem ?? ResolveOwningContextMenu(menuItem)?.DataContext as ShortcutItem;

    private static ShortcutItem? ResolveMoveSourceItem(MenuItem menuItem)
    {
        var owner = ItemsControl.ItemsControlFromItemContainer(menuItem);
        while (owner is MenuItem parentMenu)
        {
            if (parentMenu.Tag is ShortcutItem item)
                return item;

            owner = ItemsControl.ItemsControlFromItemContainer(parentMenu);
        }

        return ResolveOwningContextMenu(menuItem)?.DataContext as ShortcutItem;
    }

    private static ShortcutGroup? ResolveGroupItem(MenuItem menuItem)
    {
        if (menuItem.DataContext is ShortcutGroup group)
            return group;

        var contextMenu = ResolveOwningContextMenu(menuItem);
        return (contextMenu?.PlacementTarget as FrameworkElement)?.DataContext as ShortcutGroup;
    }

    private static ContextMenu? ResolveOwningContextMenu(MenuItem menuItem)
    {
        ItemsControl? owner = ItemsControl.ItemsControlFromItemContainer(menuItem);
        while (owner is MenuItem parentMenu)
            owner = ItemsControl.ItemsControlFromItemContainer(parentMenu);

        return owner as ContextMenu;
    }

    private static void CopyTextToClipboard(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"无法写入剪贴板：{ex.Message}", "Rolan",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    private static string BuildCommandLine(ShortcutItem item)
    {
        var target = QuoteForCommandLine(item.TargetPath);
        return string.IsNullOrWhiteSpace(item.Arguments)
            ? target
            : $"{target} {item.Arguments!.Trim()}";
    }

    private static string QuoteForCommandLine(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 ||
            trimmed.StartsWith("rolan:", StringComparison.OrdinalIgnoreCase) ||
            TargetPathHelper.IsUrl(trimmed))
        {
            return trimmed;
        }

        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
            return trimmed;

        var resolved = TryResolveForCommandLine(trimmed);
        return trimmed.Any(char.IsWhiteSpace) || resolved?.Any(char.IsWhiteSpace) == true
            ? $"\"{trimmed}\""
            : trimmed;
    }

    private static string? TryResolveForCommandLine(string value)
    {
        try
        {
            return TargetPathHelper.Resolve(value);
        }
        catch
        {
            return null;
        }
    }

    private readonly record struct HotkeyRegistration(bool Success, int Modifiers, int Key, bool UsedFallback);
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
