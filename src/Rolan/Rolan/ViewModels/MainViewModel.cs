using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rolan.Models;
using Rolan.Services;

namespace Rolan.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IShellService _shellService;
    private readonly PanelService _panelService;
    private readonly IThemeService _themeService;
    private readonly IAutoStartService _autoStartService;
    private readonly IDataExportService _dataExportService;

    public PanelService PanelService => _panelService;

    [ObservableProperty]
    private ObservableCollection<ShortcutGroup> _groups = new();

    [ObservableProperty]
    private ShortcutGroup? _selectedGroup;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    public MainViewModel(
        IDataService dataService,
        IShellService shellService,
        PanelService panelService,
        IThemeService themeService,
        IAutoStartService autoStartService,
        IDataExportService dataExportService)
    {
        _dataService = dataService;
        _shellService = shellService;
        _panelService = panelService;
        _themeService = themeService;
        _autoStartService = autoStartService;
        _dataExportService = dataExportService;

        _ = LoadDataAsync();

        // 应用主题
        var settings = AppSettings.Load();
        _themeService.ApplyTheme(settings.Theme);
    }

    public IEnumerable<ShortcutItem> FilteredItems => GetFilteredItems();

    private async Task LoadDataAsync()
    {
        var groups = await _dataService.LoadAllAsync();
        Groups = new ObservableCollection<ShortcutGroup>(groups);

        if (Groups.Count == 0)
        {
            // 创建默认分组
            var defaultGroup = new ShortcutGroup { Name = "默认分组", Order = 0 };
            await _dataService.SaveGroupAsync(defaultGroup);
            Groups.Add(defaultGroup);
        }

        SelectedGroup = Groups.FirstOrDefault();
    }

    partial void OnSearchTextChanged(string value)
    {
        IsSearching = !string.IsNullOrWhiteSpace(value);
        RefreshFilteredItems();
    }

    partial void OnSelectedGroupChanged(ShortcutGroup? value)
    {
        RefreshFilteredItems();
    }

    /// <summary>
    /// 获取过滤后的快捷方式列表
    /// </summary>
    private IEnumerable<ShortcutItem> GetFilteredItems()
    {
        var group = SelectedGroup;
        if (group == null) return Enumerable.Empty<ShortcutItem>();

        if (IsSearching)
        {
            return Groups.SelectMany(g => g.Items)
                .Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                         || i.TargetPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Name)
                .ToList();
        }

        return group.Items.OrderBy(i => i.Order).ToList();
    }

    private void RefreshFilteredItems()
    {
        OnPropertyChanged(nameof(FilteredItems));
    }

    [RelayCommand]
    private void LaunchItem(ShortcutItem? item)
    {
        if (item == null) return;
        _shellService.Launch(item.TargetPath, item.Arguments, item.WorkingDirectory);
    }

    [RelayCommand]
    private async Task AddGroup()
    {
        var newGroup = new ShortcutGroup
        {
            Name = $"分组 {Groups.Count + 1}",
            Order = Groups.Any() ? Groups.Max(g => g.Order) + 1 : 0
        };
        await _dataService.SaveGroupAsync(newGroup);
        Groups.Add(newGroup);
        SelectedGroup = newGroup;
    }

    [RelayCommand]
    private async Task DeleteGroup(ShortcutGroup? group)
    {
        if (group == null) return;
        await _dataService.DeleteGroupAsync(group.Id);
        Groups.Remove(group);
        SelectedGroup = Groups.FirstOrDefault();
    }

    [RelayCommand]
    private async Task RenameGroup(ShortcutGroup? group)
    {
        if (group == null) return;
        await _dataService.SaveGroupAsync(group);
    }

    [RelayCommand]
    private async Task AddShortcut(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || SelectedGroup == null) return;

        var type = DetectType(targetPath);
        var item = new ShortcutItem
        {
            GroupId = SelectedGroup.Id,
            Name = GetShortcutName(targetPath, type),
            TargetPath = targetPath,
            Type = type,
            Order = SelectedGroup.Items.Any() ? SelectedGroup.Items.Max(i => i.Order) + 1 : 0
        };

        // 尝试提取图标
        try
        {
            var icon = _shellService.ExtractIcon(targetPath);
            if (icon != null)
            {
                item.IconData = Rolan.Helpers.IconHelper.BitmapSourceToBytes(icon);
            }
        }
        catch { }

        await _dataService.SaveItemAsync(item);
        SelectedGroup.Items.Add(item);
        RefreshFilteredItems();
    }

    [RelayCommand]
    private async Task DeleteShortcut(ShortcutItem? item)
    {
        if (item == null) return;
        var sourceGroup = Groups.FirstOrDefault(g => g.Id == item.GroupId);
        if (sourceGroup == null) return;
        await _dataService.DeleteItemAsync(item.Id);
        sourceGroup.Items.Remove(item);
        RefreshFilteredItems();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsWindow = new Views.SettingsWindow();
        settingsWindow.DataContext = new SettingsViewModel(this, _panelService, _themeService, _autoStartService, _dataExportService);
        settingsWindow.ShowDialog();
    }

    [RelayCommand]
    private void ToggleVisibility()
    {
        _panelService.ToggleVisibility();
    }

    [RelayCommand]
    private async Task BrowseAddShortcut()
    {
        if (SelectedGroup == null) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择要添加的文件",
            Filter = "所有文件 (*.*)|*.*|程序 (*.exe)|*.exe|快捷方式 (*.lnk)|*.lnk",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
                await AddShortcut(file);
        }
    }

    [RelayCommand]
    private async Task MoveToGroup(Tuple<ShortcutItem, ShortcutGroup> args)
    {
        var (item, targetGroup) = args;
        var sourceGroup = Groups.FirstOrDefault(g => g.Id == item.GroupId);
        if (sourceGroup == null) return;

        sourceGroup.Items.Remove(item);
        item.GroupId = targetGroup.Id;
        item.Order = targetGroup.Items.Any() ? targetGroup.Items.Max(i => i.Order) + 1 : 0;
        targetGroup.Items.Add(item);

        await _dataService.SaveItemAsync(item);
        RefreshFilteredItems();
    }

    [RelayCommand]
    private async Task MoveShortcutUp(ShortcutItem? item)
    {
        if (item == null) return;
        var sourceGroup = Groups.FirstOrDefault(g => g.Id == item.GroupId);
        if (sourceGroup == null) return;

        var items = sourceGroup.Items.OrderBy(i => i.Order).ToList();
        var idx = items.IndexOf(item);
        if (idx <= 0) return;

        var prev = items[idx - 1];
        (item.Order, prev.Order) = (prev.Order, item.Order);
        await _dataService.ReorderItemAsync(item.Id, item.Order);
        await _dataService.ReorderItemAsync(prev.Id, prev.Order);
        RefreshFilteredItems();
    }

    [RelayCommand]
    private async Task MoveShortcutDown(ShortcutItem? item)
    {
        if (item == null) return;
        var sourceGroup = Groups.FirstOrDefault(g => g.Id == item.GroupId);
        if (sourceGroup == null) return;

        var items = sourceGroup.Items.OrderBy(i => i.Order).ToList();
        var idx = items.IndexOf(item);
        if (idx < 0 || idx >= items.Count - 1) return;

        var next = items[idx + 1];
        (item.Order, next.Order) = (next.Order, item.Order);
        await _dataService.ReorderItemAsync(item.Id, item.Order);
        await _dataService.ReorderItemAsync(next.Id, next.Order);
        RefreshFilteredItems();
    }

    public async Task AddShortcutFromDragDrop(string[] files)
    {
        if (SelectedGroup == null) return;
        foreach (var file in files)
        {
            await AddShortcut(file);
        }
    }

    public async Task ImportDataAsync(string filePath)
    {
        var groups = await _dataExportService.ImportAsync(filePath);

        foreach (var group in groups.OrderBy(g => g.Order))
        {
            group.Order = Groups.Any() ? Groups.Max(g => g.Order) + 1 : 0;
            group.Id = 0;

            await _dataService.SaveGroupAsync(group);
            Groups.Add(group);

            foreach (var item in group.Items.OrderBy(i => i.Order))
            {
                item.Id = 0;
                item.GroupId = group.Id;
                await _dataService.SaveItemAsync(item);
            }
        }

        if (SelectedGroup == null)
            SelectedGroup = Groups.FirstOrDefault();

        RefreshFilteredItems();
    }

    public async Task UpdateShortcutAsync(ShortcutItem? item)
    {
        if (item == null) return;

        item.Type = DetectType(item.TargetPath);
        try
        {
            var icon = _shellService.ExtractIcon(item.TargetPath);
            if (icon != null)
                item.IconData = Rolan.Helpers.IconHelper.BitmapSourceToBytes(icon);
        }
        catch { }

        await _dataService.SaveItemAsync(item);
        RefreshFilteredItems();
    }

    private static ShortcutType DetectType(string path)
    {
        if (path.StartsWith("http://") || path.StartsWith("https://"))
            return ShortcutType.Url;

        try
        {
            if (File.Exists(path))
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                return ext is ".exe" or ".lnk" or ".bat" or ".cmd"
                    ? ShortcutType.Application
                    : ext == ".url"
                        ? ShortcutType.Url
                        : ShortcutType.File;
            }
            if (Directory.Exists(path))
                return ShortcutType.Folder;
        }
        catch { }

        return ShortcutType.File;
    }

    private static string GetShortcutName(string path, ShortcutType type)
    {
        if (type == ShortcutType.Url && Uri.TryCreate(path, UriKind.Absolute, out var uri))
            return string.IsNullOrWhiteSpace(uri.Host) ? path : uri.Host;

        if (Directory.Exists(path))
        {
            var name = new DirectoryInfo(path).Name;
            return string.IsNullOrWhiteSpace(name) ? path : name;
        }

        if (File.Exists(path))
            return Path.GetFileNameWithoutExtension(path);

        var name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }
}
