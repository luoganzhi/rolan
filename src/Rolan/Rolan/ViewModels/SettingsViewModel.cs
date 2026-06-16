using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Rolan.Models;
using Rolan.Services;

namespace Rolan.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly HotkeyChoice[] ModifierChoices =
    [
        new("Alt", 1),
        new("Ctrl + Alt", 1 | 2),
        new("Ctrl + Shift", 2 | 4),
        new("Alt + Shift", 1 | 4)
    ];

    private static readonly HotkeyChoice[] KeyChoices =
    [
        new("Space", 0x20),
        new("R", 0x52),
        new("Q", 0x51),
        new("A", 0x41),
        new("S", 0x53),
        new("D", 0x44),
        new("F", 0x46)
    ];

    private readonly MainViewModel _mainVm;
    private readonly PanelService _panelService;
    private readonly IThemeService _themeService;
    private readonly IAutoStartService _autoStartService;
    private readonly IDataExportService _dataExportService;
    private readonly IDataDirectoryService _dataDirectoryService;
    private readonly AppSettings _settings;
    private readonly System.Windows.Window? _owner;

    [ObservableProperty]
    private bool _autoHide;

    [ObservableProperty]
    private bool _hideWhenLostFocus;

    [ObservableProperty]
    private bool _mousePenetration;

    [ObservableProperty]
    private bool _topMost;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _hideAfterLaunch;

    [ObservableProperty]
    private bool _autoFitPanelHeight;

    [ObservableProperty]
    private string _selectedTheme;

    [ObservableProperty]
    private int _selectedPanelSideIndex;

    [ObservableProperty]
    private int _selectedHotkeyModifierIndex;

    [ObservableProperty]
    private int _selectedHotkeyKeyIndex;

    public string[] Themes { get; }
    public string[] PanelSides { get; } = { "左侧", "右侧" };
    public string[] HotkeyModifierOptions { get; } = ModifierChoices.Select(c => c.Label).ToArray();
    public string[] HotkeyKeyOptions { get; } = KeyChoices.Select(c => c.Label).ToArray();
    public string DataDirectory => _dataDirectoryService.DataDirectory;

    public SettingsViewModel(
        MainViewModel mainVm,
        PanelService panelService,
        IThemeService themeService,
        IAutoStartService autoStartService,
        IDataExportService dataExportService,
        IDataDirectoryService dataDirectoryService,
        System.Windows.Window? owner = null)
    {
        _mainVm = mainVm;
        _panelService = panelService;
        _themeService = themeService;
        _autoStartService = autoStartService;
        _dataExportService = dataExportService;
        _dataDirectoryService = dataDirectoryService;
        _owner = owner;
        _settings = AppSettings.Load();

        _autoHide = _settings.AutoHide;
        _hideWhenLostFocus = _settings.HideWhenLostFocus;
        _mousePenetration = _settings.MousePenetration;
        _topMost = _settings.TopMost;
        _autoStart = _autoStartService.IsEnabled;
        _hideAfterLaunch = _settings.HideAfterLaunch;
        _autoFitPanelHeight = _settings.AutoFitPanelHeight;
        _selectedTheme = _settings.Theme;
        _selectedPanelSideIndex = (int)_settings.PanelSide;
        _selectedHotkeyModifierIndex = FindChoiceIndex(ModifierChoices, _settings.HotkeyModifiers);
        _selectedHotkeyKeyIndex = FindChoiceIndex(KeyChoices, _settings.HotkeyKey);

        Themes = _themeService.AvailableThemes;
    }

    [RelayCommand]
    private void Save()
    {
        var panelSideChanged = _settings.PanelSide != (PanelSide)SelectedPanelSideIndex;

        _settings.AutoHide = AutoHide;
        _settings.HideWhenLostFocus = HideWhenLostFocus;
        _settings.MousePenetration = MousePenetration;
        _settings.TopMost = TopMost;
        _settings.AutoStart = AutoStart;
        _settings.HideAfterLaunch = HideAfterLaunch;
        _settings.AutoFitPanelHeight = AutoFitPanelHeight;
        _settings.Theme = SelectedTheme;
        _settings.PanelSide = (PanelSide)SelectedPanelSideIndex;
        _settings.HotkeyModifiers = GetChoiceValue(ModifierChoices, SelectedHotkeyModifierIndex);
        _settings.HotkeyKey = GetChoiceValue(KeyChoices, SelectedHotkeyKeyIndex);
        _settings.Save();

        _panelService.UpdateSettings(_settings);
        _panelService.SetMousePenetration(MousePenetration);
        _panelService.SetTopMost(TopMost);
        if (panelSideChanged)
            _panelService.PositionPanel();
        else
            _panelService.SnapToSideAndSavePlacement();

        _themeService.ApplyTheme(SelectedTheme);
        _autoStartService.SetEnabled(AutoStart);
        _mainVm.NotifySettingsUpdated(_settings);
    }

    [RelayCommand]
    private void OpenDataDirectory()
    {
        try
        {
            _dataDirectoryService.OpenDataDirectory();
        }
        catch (Exception ex)
        {
            ShowMessage(
                $"无法打开数据目录：{ex.Message}",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task ExportData()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Rolan 数据文件 (*.rolan)|*.rolan|JSON 文件 (*.json)|*.json",
            DefaultExt = ".rolan",
            FileName = $"rolan_backup_{DateTime.Now:yyyyMMdd}"
        };

        bool? dialogResult;
        using (_panelService.SuspendAutoHide())
        {
            dialogResult = dialog.ShowDialog(_owner);
        }

        if (dialogResult == true)
        {
            var groups = _mainVm.Groups.OrderBy(g => g.Order).ToList();
            await _dataExportService.ExportAsync(dialog.FileName, groups);
            using (_panelService.SuspendAutoHide())
            {
                ShowMessage("导出成功！", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
    }

    [RelayCommand]
    private async Task ImportData()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Rolan 数据文件 (*.rolan)|*.rolan|JSON 文件 (*.json)|*.json"
        };

        bool? dialogResult;
        using (_panelService.SuspendAutoHide())
        {
            dialogResult = dialog.ShowDialog(_owner);
        }

        if (dialogResult == true)
        {
            try
            {
                await _mainVm.ImportDataAsync(dialog.FileName);
                using (_panelService.SuspendAutoHide())
                {
                    ShowMessage("导入成功！", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                using (_panelService.SuspendAutoHide())
                {
                    ShowMessage($"导入失败: {ex.Message}",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }

    private System.Windows.MessageBoxResult ShowMessage(
        string message,
        System.Windows.MessageBoxButton buttons,
        System.Windows.MessageBoxImage image)
        => _owner == null
            ? System.Windows.MessageBox.Show(message, "Rolan", buttons, image)
            : System.Windows.MessageBox.Show(_owner, message, "Rolan", buttons, image);

    private static int FindChoiceIndex(HotkeyChoice[] choices, int value)
    {
        var index = Array.FindIndex(choices, c => c.Value == value);
        return index < 0 ? 0 : index;
    }

    private static int GetChoiceValue(HotkeyChoice[] choices, int index)
    {
        return choices[Math.Clamp(index, 0, choices.Length - 1)].Value;
    }

    private readonly record struct HotkeyChoice(string Label, int Value);
}
