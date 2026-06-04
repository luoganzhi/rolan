using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Rolan.Models;
using Rolan.Services;

namespace Rolan.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _mainVm;
    private readonly PanelService _panelService;
    private readonly AppSettings _settings;

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
    private string _selectedTheme;

    [ObservableProperty]
    private int _selectedPanelSideIndex;

    public string[] Themes { get; }
    public string[] PanelSides { get; } = { "左侧", "右侧" };

    public SettingsViewModel(MainViewModel mainVm, PanelService panelService)
    {
        _mainVm = mainVm;
        _panelService = panelService;
        _settings = AppSettings.Load();

        // 从设置加载
        _autoHide = _settings.AutoHide;
        _hideWhenLostFocus = _settings.HideWhenLostFocus;
        _mousePenetration = _settings.MousePenetration;
        _topMost = _settings.TopMost;
        _autoStart = _settings.AutoStart;
        _selectedTheme = _settings.Theme;
        _selectedPanelSideIndex = (int)_settings.PanelSide;

        Themes = new[] { "Default", "Dark" };
    }

    [RelayCommand]
    private void Save()
    {
        _settings.AutoHide = AutoHide;
        _settings.HideWhenLostFocus = HideWhenLostFocus;
        _settings.MousePenetration = MousePenetration;
        _settings.TopMost = TopMost;
        _settings.AutoStart = AutoStart;
        _settings.Theme = SelectedTheme;
        _settings.PanelSide = (PanelSide)SelectedPanelSideIndex;
        _settings.Save();

        // 应用设置
        _panelService.SetMousePenetration(MousePenetration);
        _panelService.SetTopMost(TopMost);
        _panelService.PositionPanel();

        // 应用主题
        var themeService = App.Current?.FindResource("ThemeService") as IThemeService;
        themeService?.ApplyTheme(SelectedTheme);

        // 开机自启
        var autoStartService = new AutoStartService();
        autoStartService.SetEnabled(AutoStart);
    }

    [RelayCommand]
    private async Task ExportData()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Rolan 数据文件 (*.rolan)|*.rolan|JSON 文件 (*.json)|*.json",
            DefaultExt = ".rolan",
            FileName = $"rolan_backup_{DateTime.Now:yyyyMMdd}"
        };

        if (dialog.ShowDialog() == true)
        {
            var exportService = new DataExportService();
            var groups = _mainVm.Groups.ToList();
            await exportService.ExportAsync(dialog.FileName, groups);
            System.Windows.MessageBox.Show("导出成功！", "Rolan");
        }
    }

    [RelayCommand]
    private async Task ImportData()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Rolan 数据文件 (*.rolan)|*.rolan|JSON 文件 (*.json)|*.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var exportService = new DataExportService();
            try
            {
                var groups = await exportService.ImportAsync(dialog.FileName);
                // 清除现有数据
                foreach (var group in _mainVm.Groups.ToList())
                {
                    foreach (var item in group.Items.ToList())
                        await _mainVm.GetType().GetMethod("DeleteShortcutCommand")?.Invoke(_mainVm, new[] { item })!;
                    await _mainVm.GetType().GetMethod("DeleteGroupCommand")?.Invoke(_mainVm, new[] { group })!;
                }
                System.Windows.MessageBox.Show("导入功能需要在重启后完全生效。请重启 Rolan。", "Rolan");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"导入失败: {ex.Message}", "Rolan",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
