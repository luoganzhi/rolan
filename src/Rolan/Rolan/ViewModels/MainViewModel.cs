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
    private readonly IHotkeyService _hotkeyService;
    private readonly IThemeService _themeService;

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
        IHotkeyService hotkeyService,
        IThemeService themeService)
    {
        _dataService = dataService;
        _shellService = shellService;
        _panelService = panelService;
        _hotkeyService = hotkeyService;
        _themeService = themeService;

        _panelService.VisibilityChanged += OnPanelVisibilityChanged;

        _ = LoadDataAsync();

        // 应用主题
        var settings = AppSettings.Load();
        _themeService.ApplyTheme(settings.Theme);
    }

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
    }

    /// <summary>
    /// 获取过滤后的快捷方式列表
    /// </summary>
    public IEnumerable<ShortcutItem> GetFilteredItems()
    {
        var group = SelectedGroup;
        if (group == null) return Enumerable.Empty<ShortcutItem>();

        if (IsSearching)
        {
            return Groups.SelectMany(g => g.Items)
                .Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                         || i.TargetPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Name);
        }

        return group.Items.OrderBy(i => i.Order);
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
            Order = Groups.Count
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
            Name = Path.GetFileNameWithoutExtension(targetPath),
            TargetPath = targetPath,
            Type = type,
            Order = SelectedGroup.Items.Count
        };

        // 尝试提取图标
        try
        {
            var icon = _shellService.ExtractIcon(targetPath);
            if (icon != null)
            {
                item.IconData = Helpers.IconHelper.BitmapSourceToBytes(icon);
            }
        }
        catch { }

        await _dataService.SaveItemAsync(item);
        SelectedGroup.Items.Add(item);
        OnPropertyChanged(nameof(GetFilteredItems));
    }

    [RelayCommand]
    private async Task DeleteShortcut(ShortcutItem? item)
    {
        if (item == null) return;
        await _dataService.DeleteItemAsync(item.Id);
        SelectedGroup?.Items.Remove(item);
        OnPropertyChanged(nameof(GetFilteredItems));
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsWindow = new Views.SettingsWindow();
        settingsWindow.DataContext = new SettingsViewModel(this, _panelService);
        settingsWindow.ShowDialog();
    }

    [RelayCommand]
    private void ToggleVisibility()
    {
        _panelService.ToggleVisibility();
    }

    public void AddShortcutFromDragDrop(string[] files)
    {
        if (SelectedGroup == null) return;
        foreach (var file in files)
        {
            _ = AddShortcut(file);
        }
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
                    : ShortcutType.File;
            }
            if (Directory.Exists(path))
                return ShortcutType.Folder;
        }
        catch { }

        return ShortcutType.File;
    }

    private void OnPanelVisibilityChanged()
    {
        OnPropertyChanged(nameof(PanelService));
    }
}
