using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Rolan.Helpers;
using Rolan.Models;
using Rolan.Services;

namespace Rolan.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly Encoding? ChineseEncoding = CreateChineseEncoding();

    private readonly IDataService _dataService;
    private readonly IShellService _shellService;
    private readonly PanelService _panelService;
    private readonly IThemeService _themeService;
    private readonly IAutoStartService _autoStartService;
    private readonly IDataExportService _dataExportService;
    private AppSettings _settings;

    public PanelService PanelService => _panelService;
    public event Action<AppSettings>? SettingsChanged;

    [ObservableProperty]
    private ObservableCollection<ShortcutGroup> _groups = new();

    [ObservableProperty]
    private ShortcutGroup? _selectedGroup;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isFrequentGroupSelected;

    [ObservableProperty]
    private ShortcutItem? _selectedShortcut;

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
        _settings = AppSettings.Load();
        _themeService.ApplyTheme(_settings.Theme);
    }

    public IEnumerable<ShortcutItem> FilteredItems => GetFilteredItems();
    public bool HasNoFilteredItems => !GetFilteredItems().Any();

    private async Task LoadDataAsync()
    {
        try
        {
            var groups = await _dataService.LoadAllAsync();
            Groups = new ObservableCollection<ShortcutGroup>(groups);
            SyncAllGroupNames();

            if (Groups.Count == 0)
            {
                await CreateDefaultGroupsAsync();
            }

            SelectedGroup = Groups.FirstOrDefault();
            RefreshFilteredItems();
        }
        catch (Exception ex)
        {
            var fallbackGroup = new ShortcutGroup { Name = "默认分组", Order = 0 };
            Groups = new ObservableCollection<ShortcutGroup>([fallbackGroup]);
            SelectedGroup = fallbackGroup;
            RefreshFilteredItems();

            System.Windows.MessageBox.Show($"加载快捷方式数据失败：{ex.Message}", "Rolan",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        IsSearching = !string.IsNullOrWhiteSpace(value);
        RefreshFilteredItems();
    }

    partial void OnSelectedGroupChanged(ShortcutGroup? value)
    {
        if (value != null && IsFrequentGroupSelected)
            IsFrequentGroupSelected = false;

        RefreshFilteredItems();
    }

    partial void OnIsFrequentGroupSelectedChanged(bool value)
    {
        if (value)
        {
            SelectedGroup = null;
        }
        else if (SelectedGroup == null)
        {
            SelectedGroup = Groups.FirstOrDefault();
        }

        RefreshFilteredItems();
    }

    /// <summary>
    /// 获取过滤后的快捷方式列表
    /// </summary>
    private IEnumerable<ShortcutItem> GetFilteredItems()
    {
        var group = SelectedGroup;
        if (!IsFrequentGroupSelected && group == null) return Enumerable.Empty<ShortcutItem>();

        if (IsSearching)
        {
            return Groups.SelectMany(g => g.Items)
                .Where(i => MatchesSearch(i, SearchText))
                .OrderByDescending(i => i.LaunchCount)
                .ThenByDescending(i => i.LastLaunchedAt ?? DateTime.MinValue)
                .ThenBy(i => i.Name)
                .ToList();
        }

        if (IsFrequentGroupSelected)
            return GetFrequentItems();

        if (group == null)
            return Enumerable.Empty<ShortcutItem>();

        return group.Items.OrderBy(i => i.Order).ToList();
    }

    private IEnumerable<ShortcutItem> GetFrequentItems()
    {
        return Groups.SelectMany(g => g.Items)
            .Where(i => i.LaunchCount > 0 || i.LastLaunchedAt != null)
            .OrderByDescending(i => i.LastLaunchedAt ?? DateTime.MinValue)
            .ThenByDescending(i => i.LaunchCount)
            .ThenBy(i => i.Name)
            .Take(24)
            .ToList();
    }

    private void RefreshFilteredItems()
    {
        EnsureSelectedShortcut();
        OnPropertyChanged(nameof(FilteredItems));
        OnPropertyChanged(nameof(HasNoFilteredItems));
    }

    private static bool MatchesSearch(ShortcutItem item, string searchText)
    {
        var normalizedSearchText = searchText.Trim();
        return Contains(item.Name, normalizedSearchText)
               || ContainsSearchAlias(item.Name, normalizedSearchText)
               || Contains(item.TargetPath, normalizedSearchText)
               || ContainsSearchAlias(item.TargetPath, normalizedSearchText)
               || Contains(item.Arguments, normalizedSearchText)
               || ContainsSearchAlias(item.Arguments, normalizedSearchText)
               || Contains(item.WorkingDirectory, normalizedSearchText)
               || ContainsSearchAlias(item.WorkingDirectory, normalizedSearchText)
               || Contains(item.Type.ToString(), normalizedSearchText);
    }

    private static bool Contains(string? value, string searchText)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsSearchAlias(string? value, string searchText)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(searchText))
            return false;

        var alias = BuildSearchAlias(value);
        var searchAlias = BuildSearchAlias(searchText);
        return alias.Length > 0 &&
               searchAlias.Length > 0 &&
               (alias.Contains(searchAlias, StringComparison.OrdinalIgnoreCase) ||
                IsOrderedSubsequence(alias, searchAlias));
    }

    private static string BuildSearchAlias(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                continue;
            }

            var initial = GetChineseInitial(ch);
            if (initial != null)
                builder.Append(initial.Value);
        }

        return builder.ToString();
    }

    private static char? GetChineseInitial(char ch)
    {
        if (ch is < '\u4e00' or > '\u9fff')
            return null;

        try
        {
            if (ChineseEncoding == null)
                return null;

            var bytes = ChineseEncoding.GetBytes([ch]);
            if (bytes.Length < 2)
                return null;

            var code = bytes[0] * 256 + bytes[1] - 65536;
            return code switch
            {
                >= -20319 and <= -20284 => 'a',
                >= -20283 and <= -19776 => 'b',
                >= -19775 and <= -19219 => 'c',
                >= -19218 and <= -18711 => 'd',
                >= -18710 and <= -18527 => 'e',
                >= -18526 and <= -18240 => 'f',
                >= -18239 and <= -17923 => 'g',
                >= -17922 and <= -17418 => 'h',
                >= -17417 and <= -16475 => 'j',
                >= -16474 and <= -16213 => 'k',
                >= -16212 and <= -15641 => 'l',
                >= -15640 and <= -15166 => 'm',
                >= -15165 and <= -14923 => 'n',
                >= -14922 and <= -14915 => 'o',
                >= -14914 and <= -14631 => 'p',
                >= -14630 and <= -14150 => 'q',
                >= -14149 and <= -14091 => 'r',
                >= -14090 and <= -13319 => 's',
                >= -13318 and <= -12839 => 't',
                >= -12838 and <= -12557 => 'w',
                >= -12556 and <= -11848 => 'x',
                >= -11847 and <= -11056 => 'y',
                >= -11055 and <= -10247 => 'z',
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static Encoding? CreateChineseEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("GB2312");
        }
        catch
        {
            return null;
        }
    }

    private static bool IsOrderedSubsequence(string value, string searchText)
    {
        var searchIndex = 0;
        foreach (var ch in value)
        {
            if (char.ToUpperInvariant(ch) != char.ToUpperInvariant(searchText[searchIndex]))
                continue;

            searchIndex++;
            if (searchIndex == searchText.Length)
                return true;
        }

        return false;
    }

    [RelayCommand]
    private async Task LaunchItem(ShortcutItem? item)
    {
        if (item == null) return;
        if (!_shellService.Launch(item.TargetPath, item.Arguments, item.WorkingDirectory))
            return;

        item.LaunchCount++;
        item.LastLaunchedAt = DateTime.Now;
        await _dataService.RecordItemLaunchAsync(item.Id, item.LaunchCount, item.LastLaunchedAt.Value);
        RefreshFilteredItems();

        if (_settings.HideAfterLaunch && !_panelService.IsHidden)
            _panelService.AnimateHide();
    }

    [RelayCommand]
    private async Task LaunchFirstFilteredItem()
    {
        var query = SearchText.Trim();
        if (TryBuildSearchUrl(query, out var searchUrl))
        {
            _shellService.Launch(searchUrl);
            SearchText = string.Empty;
            return;
        }

        await LaunchItem(SelectedShortcut ?? GetFilteredItems().FirstOrDefault());
    }

    [RelayCommand]
    private async Task LaunchFilteredItemByIndex(int index)
    {
        if (index < 0)
            return;

        var item = GetFilteredItems().Skip(index).FirstOrDefault();
        await LaunchItem(item);
    }

    [RelayCommand]
    private void SelectNextShortcut()
    {
        MoveShortcutSelection(1);
    }

    [RelayCommand]
    private void SelectPreviousShortcut()
    {
        MoveShortcutSelection(-1);
    }

    [RelayCommand]
    private void SelectNextGroup()
    {
        MoveGroupSelection(1);
    }

    [RelayCommand]
    private void SelectPreviousGroup()
    {
        MoveGroupSelection(-1);
    }

    [RelayCommand]
    private void SelectGroupByIndex(int index)
    {
        if (index == 0)
        {
            IsFrequentGroupSelected = true;
            return;
        }

        var groupIndex = index - 1;
        if (groupIndex < 0 || groupIndex >= Groups.Count)
            return;

        SelectedGroup = Groups[groupIndex];
    }

    [RelayCommand]
    private async Task AddGroup(string? groupName = null)
    {
        groupName = string.IsNullOrWhiteSpace(groupName)
            ? GenerateDefaultGroupName()
            : groupName.Trim();

        if (!ValidateGroupName(groupName, null, out var message))
        {
            ShowGroupValidationMessage(message);
            return;
        }

        var newGroup = new ShortcutGroup
        {
            Name = groupName,
            Order = Groups.Any() ? Groups.Max(g => g.Order) + 1 : 0
        };
        await _dataService.SaveGroupAsync(newGroup);
        Groups.Add(newGroup);
        SelectedGroup = newGroup;
    }

    private async Task CreateDefaultGroupsAsync()
    {
        var defaultGroups = LoadDefaultGroups().ToList();
        if (defaultGroups.Count == 0)
            defaultGroups.Add(new DefaultGroupDefinition("默认分组", []));

        for (var i = 0; i < defaultGroups.Count; i++)
        {
            var groupDefinition = defaultGroups[i];
            var group = new ShortcutGroup
            {
                Name = groupDefinition.Name,
                Order = i
            };
            await _dataService.SaveGroupAsync(group);

            for (var itemIndex = 0; itemIndex < groupDefinition.Items.Count; itemIndex++)
            {
                var itemDefinition = groupDefinition.Items[itemIndex];
                var targetPath = TargetPathHelper.NormalizeInput(itemDefinition.TargetPath);
                if (string.IsNullOrWhiteSpace(targetPath))
                    continue;

                var type = DetectType(targetPath);
                var item = new ShortcutItem
                {
                    GroupId = group.Id,
                    Name = string.IsNullOrWhiteSpace(itemDefinition.Name)
                        ? GetShortcutName(targetPath, type)
                        : itemDefinition.Name.Trim(),
                    TargetPath = targetPath,
                    Type = type,
                    GroupName = group.Name,
                    Order = itemIndex
                };
                await _dataService.SaveItemAsync(item);
                group.Items.Add(item);
            }

            Groups.Add(group);
        }
    }

    private static IEnumerable<DefaultGroupDefinition> LoadDefaultGroups()
    {
        var assembly = typeof(MainViewModel).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("Resources.default_groups.json", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(resourceName))
            return Enumerable.Empty<DefaultGroupDefinition>();

        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return Enumerable.Empty<DefaultGroupDefinition>();

            using var reader = new StreamReader(stream);
            using var document = JsonDocument.Parse(reader.ReadToEnd());
            if (!document.RootElement.TryGetProperty("defaultGroups", out var groups) ||
                groups.ValueKind != JsonValueKind.Array)
            {
                return Enumerable.Empty<DefaultGroupDefinition>();
            }

            return groups.EnumerateArray()
                .Select(ParseDefaultGroup)
                .Where(group => !string.IsNullOrWhiteSpace(group.Name))
                .DistinctBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Enumerable.Empty<DefaultGroupDefinition>();
        }
    }

    private static DefaultGroupDefinition ParseDefaultGroup(JsonElement group)
    {
        var name = group.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()?.Trim() ?? string.Empty
            : string.Empty;
        var items = new List<DefaultShortcutDefinition>();

        if (group.TryGetProperty("items", out var itemsElement) &&
            itemsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsElement.EnumerateArray())
            {
                var itemName = item.TryGetProperty("name", out var itemNameElement)
                    ? itemNameElement.GetString() ?? string.Empty
                    : string.Empty;
                var targetPath = item.TryGetProperty("targetPath", out var targetPathElement)
                    ? targetPathElement.GetString() ?? string.Empty
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(targetPath))
                    items.Add(new DefaultShortcutDefinition(itemName, targetPath));
            }
        }

        return new DefaultGroupDefinition(name, items);
    }

    [RelayCommand]
    private async Task DeleteGroup(DeleteGroupRequest? request)
    {
        var group = request?.Group;
        if (group == null) return;
        if (Groups.Count <= 1)
        {
            System.Windows.MessageBox.Show("至少需要保留一个分组。", "Rolan",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        if (request?.TargetGroup != null)
        {
            await MoveAllShortcutsToGroupAsync(group, request.TargetGroup);
        }

        await _dataService.DeleteGroupAsync(group.Id);
        Groups.Remove(group);
        SelectedGroup = request?.TargetGroup != null && Groups.Contains(request.TargetGroup)
            ? request.TargetGroup
            : Groups.FirstOrDefault();
        SelectedShortcut = SelectedGroup?.Items.OrderBy(i => i.Order).FirstOrDefault();
        RefreshFilteredItems();
    }

    [RelayCommand]
    private async Task RenameGroup(Tuple<ShortcutGroup, string>? args)
    {
        if (args == null) return;

        var group = args.Item1;
        var groupName = args.Item2.Trim();
        if (!ValidateGroupName(groupName, group.Id, out var message))
        {
            ShowGroupValidationMessage(message);
            return;
        }

        if (string.Equals(group.Name, groupName, StringComparison.Ordinal))
            return;

        group.Name = groupName;
        SyncGroupItemNames(group);
        await _dataService.SaveGroupAsync(group);
        RefreshFilteredItems();
    }

    private string GenerateDefaultGroupName()
    {
        var index = Groups.Count + 1;
        string groupName;
        do
        {
            groupName = $"分组 {index++}";
        }
        while (Groups.Any(group => string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase)));

        return groupName;
    }

    private bool ValidateGroupName(string groupName, int? currentGroupId, out string message)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            message = "分组名称不能为空。";
            return false;
        }

        var duplicate = Groups.Any(group =>
            (!currentGroupId.HasValue || group.Id != currentGroupId.Value) &&
            string.Equals(group.Name.Trim(), groupName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            message = $"已存在名为 \"{groupName.Trim()}\" 的分组。";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void ShowGroupValidationMessage(string message)
    {
        using (_panelService.SuspendAutoHide())
        {
            System.Windows.MessageBox.Show(message, "Rolan",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }

    private async Task MoveAllShortcutsToGroupAsync(ShortcutGroup sourceGroup, ShortcutGroup targetGroup)
    {
        if (sourceGroup.Id == targetGroup.Id || sourceGroup.Items.Count == 0)
            return;

        var movedItems = sourceGroup.Items.OrderBy(item => item.Order).ToList();
        var targetItems = targetGroup.Items.OrderBy(item => item.Order).ToList();
        targetItems.AddRange(movedItems);

        foreach (var item in movedItems)
        {
            item.GroupId = targetGroup.Id;
            item.GroupName = targetGroup.Name;
        }

        targetGroup.Items = targetItems;
        sourceGroup.Items.Clear();
        await SaveItemOrderAsync(targetGroup);
    }

    [RelayCommand]
    private async Task<bool> AddShortcut(string? targetPath)
    {
        targetPath = TargetPathHelper.NormalizeInput(targetPath);
        var targetGroup = SelectedGroup ?? Groups.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(targetPath) || targetGroup == null) return false;

        var existing = targetGroup.Items.FirstOrDefault(i =>
            string.Equals(i.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            SelectedGroup = targetGroup;
            SelectedShortcut = existing;
            RefreshFilteredItems();
            return false;
        }

        var type = DetectType(targetPath);
        var item = new ShortcutItem
        {
            GroupId = targetGroup.Id,
            Name = GetShortcutName(targetPath, type),
            TargetPath = targetPath,
            Type = type,
            GroupName = targetGroup.Name,
            Order = targetGroup.Items.Any() ? targetGroup.Items.Max(i => i.Order) + 1 : 0
        };

        // 尝试提取图标
        try
        {
            if (type != ShortcutType.SystemCommand)
            {
                var icon = _shellService.ExtractIcon(TargetPathHelper.Resolve(targetPath));
                if (icon != null)
                {
                    item.IconData = Rolan.Helpers.IconHelper.BitmapSourceToBytes(icon);
                }
            }
        }
        catch { }

        await _dataService.SaveItemAsync(item);
        targetGroup.Items.Add(item);
        SelectedGroup = targetGroup;
        SelectedShortcut = item;
        RefreshFilteredItems();
        return true;
    }

    [RelayCommand]
    private async Task DeleteShortcut(ShortcutItem? item)
    {
        if (item == null) return;
        var sourceGroup = Groups.FirstOrDefault(g => g.Id == item.GroupId);
        if (sourceGroup == null) return;
        await _dataService.DeleteItemAsync(item.Id);
        sourceGroup.Items.Remove(item);
        if (SelectedShortcut?.Id == item.Id)
            SelectedShortcut = null;
        RefreshFilteredItems();
    }

    [RelayCommand]
    private async Task DuplicateShortcut(ShortcutItem? item)
    {
        if (item == null) return;

        var sourceGroup = Groups.FirstOrDefault(group => group.Id == item.GroupId);
        if (sourceGroup == null) return;

        var copy = new ShortcutItem
        {
            GroupId = sourceGroup.Id,
            GroupName = sourceGroup.Name,
            Name = GenerateDuplicateShortcutName(sourceGroup, item.Name),
            TargetPath = item.TargetPath,
            Arguments = item.Arguments,
            WorkingDirectory = item.WorkingDirectory,
            IconData = item.IconData?.ToArray(),
            Type = item.Type,
            CreatedAt = DateTime.Now,
            Order = sourceGroup.Items.Any() ? sourceGroup.Items.Max(shortcut => shortcut.Order) + 1 : 0
        };

        await _dataService.SaveItemAsync(copy);
        sourceGroup.Items.Add(copy);
        SelectedGroup = sourceGroup;
        SelectedShortcut = copy;
        RefreshFilteredItems();
    }

    private static string GenerateDuplicateShortcutName(ShortcutGroup group, string name)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "快捷方式" : name.Trim();
        var copyName = $"{baseName} - 副本";
        if (group.Items.All(item => !string.Equals(item.Name, copyName, StringComparison.OrdinalIgnoreCase)))
            return copyName;

        for (var index = 2; ; index++)
        {
            var candidate = $"{baseName} - 副本 {index}";
            if (group.Items.All(item => !string.Equals(item.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsWindow = new Views.SettingsWindow();
        settingsWindow.DataContext = new SettingsViewModel(this, _panelService, _themeService, _autoStartService, _dataExportService);
        using (_panelService.SuspendAutoHide())
        {
            settingsWindow.ShowDialog();
        }
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

        bool? dialogResult;
        using (_panelService.SuspendAutoHide())
        {
            dialogResult = dialog.ShowDialog();
        }

        if (dialogResult == true)
        {
            foreach (var file in dialog.FileNames)
                await AddShortcut(file);
        }
    }

    [RelayCommand]
    private async Task BrowseAddFolder()
    {
        if (SelectedGroup == null) return;

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择要添加的文件夹",
            UseDescriptionForTitle = true
        };

        System.Windows.Forms.DialogResult dialogResult;
        using (_panelService.SuspendAutoHide())
        {
            dialogResult = dialog.ShowDialog();
        }

        if (dialogResult == System.Windows.Forms.DialogResult.OK)
            await AddShortcut(dialog.SelectedPath);
    }

    [RelayCommand]
    private async Task ImportStartMenuShortcuts()
    {
        await ImportShortcutFilesAsync(EnumerateStartMenuShortcuts(), "开始菜单");
    }

    [RelayCommand]
    private async Task ImportDesktopShortcuts()
    {
        await ImportShortcutFilesAsync(EnumerateDesktopShortcuts(), "桌面");
    }

    [RelayCommand]
    private async Task MoveToGroup(Tuple<ShortcutItem, ShortcutGroup> args)
    {
        var (item, targetGroup) = args;
        await MoveShortcutToGroupEndAsync(item, targetGroup);
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

    public async Task ReorderGroupAsync(ShortcutGroup movedGroup, ShortcutGroup targetGroup)
    {
        if (movedGroup.Id == targetGroup.Id)
            return;

        var oldIndex = Groups.IndexOf(movedGroup);
        var newIndex = Groups.IndexOf(targetGroup);
        if (oldIndex < 0 || newIndex < 0)
            return;

        Groups.Move(oldIndex, newIndex);
        await SaveGroupOrderAsync();
        SelectedGroup = movedGroup;
        RefreshFilteredItems();
    }

    public async Task ReorderShortcutAsync(ShortcutItem movedItem, ShortcutItem targetItem)
    {
        if (movedItem.Id == targetItem.Id)
            return;

        var sourceGroup = Groups.FirstOrDefault(g => g.Id == movedItem.GroupId);
        var targetGroup = Groups.FirstOrDefault(g => g.Id == targetItem.GroupId);
        if (sourceGroup == null || targetGroup == null)
            return;

        sourceGroup.Items.RemoveAll(i => i.Id == movedItem.Id);
        var targetItems = targetGroup.Items.OrderBy(i => i.Order).ToList();
        targetItems.RemoveAll(i => i.Id == movedItem.Id);

        var targetIndex = targetItems.FindIndex(i => i.Id == targetItem.Id);
        if (targetIndex < 0)
            targetIndex = targetItems.Count;

        movedItem.GroupId = targetGroup.Id;
        movedItem.GroupName = targetGroup.Name;
        targetItems.Insert(targetIndex, movedItem);
        targetGroup.Items = targetItems;

        if (sourceGroup.Id != targetGroup.Id)
            await SaveItemOrderAsync(sourceGroup);

        await SaveItemOrderAsync(targetGroup);
        SelectedGroup = targetGroup;
        SelectedShortcut = movedItem;
        RefreshFilteredItems();
    }

    public async Task MoveShortcutToGroupEndAsync(ShortcutItem item, ShortcutGroup targetGroup)
    {
        var sourceGroup = Groups.FirstOrDefault(g => g.Id == item.GroupId);
        if (sourceGroup == null)
            return;

        sourceGroup.Items.RemoveAll(i => i.Id == item.Id);
        var targetItems = targetGroup.Items.OrderBy(i => i.Order).ToList();
        targetItems.RemoveAll(i => i.Id == item.Id);

        item.GroupId = targetGroup.Id;
        item.GroupName = targetGroup.Name;
        targetItems.Add(item);
        targetGroup.Items = targetItems;

        if (sourceGroup.Id != targetGroup.Id)
            await SaveItemOrderAsync(sourceGroup);

        await SaveItemOrderAsync(targetGroup);
        SelectedGroup = targetGroup;
        SelectedShortcut = item;
        RefreshFilteredItems();
    }

    public void NotifySettingsUpdated(AppSettings settings)
    {
        _settings = settings;
        SettingsChanged?.Invoke(settings);
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
        ShortcutGroup? firstImportedGroup = null;

        foreach (var group in groups.OrderBy(g => g.Order))
        {
            group.Order = Groups.Any() ? Groups.Max(g => g.Order) + 1 : 0;
            group.Id = 0;

            await _dataService.SaveGroupAsync(group);
            Groups.Add(group);
            firstImportedGroup ??= group;

            foreach (var item in group.Items.OrderBy(i => i.Order))
            {
                item.Id = 0;
                item.GroupId = group.Id;
                item.GroupName = group.Name;
                await _dataService.SaveItemAsync(item);
            }
        }

        SelectedGroup = firstImportedGroup ?? SelectedGroup ?? Groups.FirstOrDefault();
        SelectedShortcut = SelectedGroup?.Items.OrderBy(i => i.Order).FirstOrDefault();

        RefreshFilteredItems();
    }

    public async Task UpdateShortcutAsync(ShortcutItem? item)
    {
        if (item == null) return;

        item.Type = DetectType(item.TargetPath);
        try
        {
            if (item.Type == ShortcutType.SystemCommand)
            {
                item.IconData = null;
            }
            else
            {
                var icon = _shellService.ExtractIcon(TargetPathHelper.Resolve(item.TargetPath));
                if (icon != null)
                    item.IconData = Rolan.Helpers.IconHelper.BitmapSourceToBytes(icon);
            }
        }
        catch { }

        await _dataService.SaveItemAsync(item);
        RefreshFilteredItems();
    }

    private static ShortcutType DetectType(string path)
    {
        if (SystemCommandHelper.IsSystemCommand(path))
            return ShortcutType.SystemCommand;

        if (TargetPathHelper.IsUrl(path))
            return ShortcutType.Url;

        var resolvedPath = TargetPathHelper.Resolve(path);
        try
        {
            if (File.Exists(resolvedPath))
            {
                var ext = Path.GetExtension(resolvedPath).ToLowerInvariant();
                return ext is ".exe" or ".lnk" or ".bat" or ".cmd"
                    ? ShortcutType.Application
                    : ext == ".url"
                        ? ShortcutType.Url
                        : ShortcutType.File;
            }
            if (Directory.Exists(resolvedPath))
                return ShortcutType.Folder;
        }
        catch { }

        return ShortcutType.File;
    }

    private static bool TryBuildSearchUrl(string query, out string url)
    {
        url = string.Empty;
        if (query.Length < 3)
            return false;

        var normalized = query.Replace('：', ':');
        var provider = normalized[..2].ToLowerInvariant();
        if (provider is not (":g" or ":b"))
            return false;

        var keywords = normalized[2..].Trim();
        if (string.IsNullOrWhiteSpace(keywords))
            return false;

        var escaped = Uri.EscapeDataString(keywords);
        url = provider == ":g"
            ? $"https://www.google.com/search?q={escaped}"
            : $"https://www.baidu.com/s?wd={escaped}";
        return true;
    }

    private static string GetShortcutName(string path, ShortcutType type)
    {
        if (type == ShortcutType.SystemCommand)
            return SystemCommandHelper.GetDisplayName(path) ?? path;

        if (type == ShortcutType.Url && Uri.TryCreate(path, UriKind.Absolute, out var uri))
            return string.IsNullOrWhiteSpace(uri.Host) ? path : uri.Host;

        var resolvedPath = TargetPathHelper.Resolve(path);
        if (Directory.Exists(resolvedPath))
        {
            var directoryName = new DirectoryInfo(resolvedPath).Name;
            return string.IsNullOrWhiteSpace(directoryName) ? path : directoryName;
        }

        if (File.Exists(resolvedPath))
            return Path.GetFileNameWithoutExtension(resolvedPath);

        var name = Path.GetFileNameWithoutExtension(resolvedPath);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static IEnumerable<string> EnumerateStartMenuShortcuts()
    {
        var directories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        foreach (var shortcut in EnumerateShortcutFiles(directories))
            yield return shortcut;
    }

    private static IEnumerable<string> EnumerateDesktopShortcuts()
    {
        var directories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

        foreach (var shortcut in EnumerateShortcutFiles(directories))
            yield return shortcut;
    }

    private static IEnumerable<string> EnumerateShortcutFiles(IEnumerable<string> directories)
    {
        foreach (var directory in directories.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(directory))
                continue;

            IEnumerable<string> shortcuts;
            try
            {
                shortcuts = Directory.EnumerateFiles(directory, "*.lnk", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true
                }).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var shortcut in shortcuts)
                yield return shortcut;
        }
    }

    private async Task ImportShortcutFilesAsync(IEnumerable<string> shortcutFiles, string sourceName)
    {
        if (SelectedGroup == null) return;

        var shortcuts = shortcutFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(Path.GetFileNameWithoutExtension)
            .ToList();

        if (shortcuts.Count == 0)
        {
            using (_panelService.SuspendAutoHide())
            {
                System.Windows.MessageBox.Show($"未找到{sourceName}快捷方式。", "Rolan",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            return;
        }

        var imported = 0;
        var skipped = 0;
        foreach (var shortcut in shortcuts)
        {
            if (await AddShortcut(shortcut))
                imported++;
            else
                skipped++;
        }

        var message = imported > 0
            ? $"已导入 {imported} 个{sourceName}快捷方式，跳过 {skipped} 个重复项。"
            : $"当前分组已包含找到的{sourceName}快捷方式，跳过 {skipped} 个重复项。";
        using (_panelService.SuspendAutoHide())
        {
            System.Windows.MessageBox.Show(message, "Rolan",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }

    private async Task SaveGroupOrderAsync()
    {
        for (var i = 0; i < Groups.Count; i++)
        {
            Groups[i].Order = i;
            await _dataService.ReorderGroupAsync(Groups[i].Id, i);
        }
    }

    private async Task SaveItemOrderAsync(ShortcutGroup group)
    {
        var items = group.Items.ToList();
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
            await _dataService.SaveItemAsync(items[i]);
        }

        group.Items = items;
        SyncGroupItemNames(group);
    }

    private void MoveShortcutSelection(int offset)
    {
        var items = GetFilteredItems().ToList();
        if (items.Count == 0)
        {
            SelectedShortcut = null;
            return;
        }

        var currentIndex = SelectedShortcut == null
            ? -1
            : items.FindIndex(i => i.Id == SelectedShortcut.Id);

        var nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + offset + items.Count) % items.Count;

        SelectedShortcut = items[nextIndex];
    }

    private void MoveGroupSelection(int offset)
    {
        var totalTabs = Groups.Count + 1;
        if (totalTabs == 0)
            return;

        var currentIndex = IsFrequentGroupSelected
            ? 0
            : SelectedGroup == null ? -1 : Groups.IndexOf(SelectedGroup) + 1;
        var nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + offset + totalTabs) % totalTabs;

        if (nextIndex == 0)
            IsFrequentGroupSelected = true;
        else
            SelectedGroup = Groups[nextIndex - 1];
    }

    private void EnsureSelectedShortcut()
    {
        var items = GetFilteredItems().ToList();
        if (items.Count == 0)
        {
            SelectedShortcut = null;
            return;
        }

        if (SelectedShortcut == null || items.All(i => i.Id != SelectedShortcut.Id))
            SelectedShortcut = items[0];
    }

    private sealed record DefaultGroupDefinition(string Name, List<DefaultShortcutDefinition> Items);

    private sealed record DefaultShortcutDefinition(string Name, string TargetPath);

    public sealed record DeleteGroupRequest(ShortcutGroup Group, ShortcutGroup? TargetGroup);

    private void SyncAllGroupNames()
    {
        foreach (var group in Groups)
            SyncGroupItemNames(group);
    }

    private static void SyncGroupItemNames(ShortcutGroup group)
    {
        foreach (var item in group.Items)
            item.GroupName = group.Name;
    }
}
