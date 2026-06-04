using System.Windows;
using System.Windows.Threading;
using Rolan.Helpers;
using Rolan.Models;

namespace Rolan.Services;

/// <summary>
/// 管理悬浮面板的贴边隐藏/滑出、鼠标穿透等行为
/// </summary>
public class PanelService
{
    private readonly AppSettings _settings;
    private Window? _window;
    private DispatcherTimer? _hideTimer;
    private bool _isHidden;
    private bool _isHovering;
    private const int TriggerEdgeWidth = 3;      // 触发显示的边缘宽度
    private const int HiddenEdgeWidth = 2;        // 隐藏时露出的边缘宽度
    private const int SlideAnimationStep = 20;     // 动画步进像素
    private const int SlideIntervalMs = 10;        // 动画间隔

    public bool IsHidden => _isHidden;
    public event Action? VisibilityChanged;

    public PanelService()
    {
        _settings = AppSettings.Load();
    }

    public void Attach(Window window)
    {
        _window = window;
        _window.Loaded += OnWindowLoaded;
        _window.LocationChanged += OnLocationChanged;
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        if (_window == null) return;
        PositionPanel();
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (_window == null) return;
        CheckAutoHide();
    }

    /// <summary>
    /// 将面板定位到屏幕边缘
    /// </summary>
    public void PositionPanel()
    {
        if (_window == null) return;
        var (waX, waY, waWidth, _) = WindowHelper.GetWorkingArea(_window);
        var panelWidth = _settings.PanelWidth;

        double left = _settings.PanelSide == PanelSide.Left ? waX : waX + waWidth - panelWidth;
        double top = waY;

        _window.Left = left;
        _window.Top = top;
        _window.Width = panelWidth;
        _window.Height = 600; // 初始高度，可调整
    }

    /// <summary>
    /// 检查并执行贴边隐藏
    /// </summary>
    public void CheckAutoHide()
    {
        if (_window == null || !_settings.AutoHide || _isHovering) return;

        var mousePos = System.Windows.Forms.Cursor.Position;
        var (waX, _, waWidth, _) = WindowHelper.GetWorkingArea(_window);

        bool nearEdge = _settings.PanelSide == PanelSide.Left
            ? Math.Abs(mousePos.X - waX) <= TriggerEdgeWidth
            : Math.Abs(mousePos.X - (waX + waWidth)) <= TriggerEdgeWidth;

        if (!nearEdge && !_isHidden)
        {
            StartHideTimer();
        }
    }

    private void StartHideTimer()
    {
        StopHideTimer();
        _hideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _hideTimer.Tick += (_, _) =>
        {
            StopHideTimer();
            if (_window != null && !_isHovering)
                AnimateHide();
        };
        _hideTimer.Start();
    }

    private void StopHideTimer()
    {
        _hideTimer?.Stop();
        _hideTimer = null;
    }

    /// <summary>
    /// 隐藏面板动画
    /// </summary>
    public void AnimateHide()
    {
        if (_window == null || _isHidden) return;
        _isHidden = true;
        var targetLeft = _settings.PanelSide == PanelSide.Left
            ? _window.Left - _window.Width + HiddenEdgeWidth
            : _window.Left + _window.Width - HiddenEdgeWidth;

        _window.Left = targetLeft;
        VisibilityChanged?.Invoke();
    }

    /// <summary>
    /// 显示面板动画
    /// </summary>
    public void AnimateShow()
    {
        if (_window == null || !_isHidden) return;
        _isHidden = false;
        PositionPanel();
        VisibilityChanged?.Invoke();
    }

    /// <summary>
    /// 切换显示/隐藏
    /// </summary>
    public void ToggleVisibility()
    {
        if (_isHidden)
            AnimateShow();
        else
            AnimateHide();
    }

    /// <summary>
    /// 鼠标进入面板区域
    /// </summary>
    public void OnMouseEnter()
    {
        _isHovering = true;
        StopHideTimer();
        if (_settings.AutoHide && _isHidden)
            AnimateShow();
    }

    /// <summary>
    /// 鼠标离开面板区域
    /// </summary>
    public void OnMouseLeave()
    {
        _isHovering = false;
        if (_settings.AutoHide && !_isHidden)
            StartHideTimer();
    }

    /// <summary>
    /// 设置鼠标穿透模式
    /// </summary>
    public void SetMousePenetration(bool enable)
    {
        if (_window != null)
            WindowHelper.SetMousePenetration(_window, enable);
    }

    /// <summary>
    /// 设置置顶
    /// </summary>
    public void SetTopMost(bool topMost)
    {
        if (_window != null)
            WindowHelper.SetTopMost(_window, topMost);
    }
}
