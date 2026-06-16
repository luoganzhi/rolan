using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Rolan.Helpers;
using Rolan.Models;

namespace Rolan.Services;

public class PanelService
{
    private AppSettings _settings;
    private Window? _window;
    private IntPtr _handle;
    private DispatcherTimer? _hideTimer;
    private DispatcherTimer? _slideTimer;
    private DispatcherTimer? _edgeMonitor;
    private DispatcherTimer? _savePlacementTimer;
    private bool _isHidden;
    private bool _isHovering;
    private bool _sliding;
    private bool _positioning;
    private double _slideTarget;
    private double? _pendingDesiredHeight;
    private WorkingArea? _lastWorkingArea;
    private int _autoHideSuppressionCount;
    private PanelVisibilityState _visibilityState = PanelVisibilityState.Shown;

    private const int TriggerEdgeWidth = 5;
    private const int HiddenEdgeWidth = 3;
    private const int PanelMargin = 12;
    private const int MinPanelWidth = 280;
    private const int MaxPanelWidth = 720;
    private const int MinPanelHeight = 240;
    private const int SlideStepPx = 16;
    private const int SlideIntervalMs = 10;
    private const int EdgePollIntervalMs = 200;
    private const int SavePlacementDelayMs = 350;

    public bool IsHidden => _visibilityState is PanelVisibilityState.Hidden or PanelVisibilityState.Hiding;
    public event Action? VisibilityChanged;

    public PanelService()
    {
        _settings = AppSettings.Load();
    }

    public void Attach(Window window)
    {
        _window = window;
        _window.Loaded += (_, _) =>
        {
            _handle = new WindowInteropHelper(_window).Handle;
            PositionPanel();

            var source = HwndSource.FromHwnd(_handle);
            source?.AddHook(WndProcHook);
        };
        _window.LocationChanged += OnLocationChanged;
        _window.SizeChanged += OnSizeChanged;
        _window.Deactivated += OnDeactivated;
        _window.Closed += OnClosed;
        _window.MouseEnter += (_, _) => OnMouseEnter();
        _window.MouseLeave += (_, _) => OnMouseLeave();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_DPICHANGED)
            _window?.Dispatcher.BeginInvoke(PositionPanel);

        return IntPtr.Zero;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_settings.AutoHide && _settings.HideWhenLostFocus && !IsHidden && !IsAutoHideSuppressed())
            AnimateHide();
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (_sliding || _positioning)
            return;

        SavePlacementDebounced();
        CheckAutoHide();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_window == null || _positioning || _sliding)
            return;

        ApplyWindowLimits();
        ClampWindowToWorkingArea(snapToSide: true);
        SavePlacementDebounced();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _lastWorkingArea = null;
        _window?.Dispatcher.BeginInvoke(PositionPanel);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        StopHideTimer();
        StopSlide();
        StopEdgeMonitor();
        StopSavePlacementTimer(saveNow: true);
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    public void PositionPanel()
    {
        if (_window == null) return;
        var workingArea = GetWorkingArea(refresh: !IsHidden);
        var maxWidth = GetMaxPanelWidth(workingArea.Width);
        var maxHeight = GetMaxPanelHeight(workingArea.Height);
        var minHeight = Math.Min(MinPanelHeight, maxHeight);
        var panelWidth = Clamp(_settings.PanelWidth, MinPanelWidth, maxWidth);
        var panelHeight = Clamp(_settings.PanelHeight, minHeight, maxHeight);
        var centeredTop = workingArea.Y + Math.Max(0, (workingArea.Height - panelHeight) / 2);
        var top = Clamp(
            _settings.PanelTop ?? centeredTop,
            workingArea.Y,
            GetMaxPanelTop(workingArea.Y, workingArea.Height, panelHeight));

        _positioning = true;
        try
        {
            ApplyWindowLimits();
            _window.Width = panelWidth;
            _window.Height = panelHeight;
            _window.Top = top;
            _window.Left = IsHidden
                ? (_settings.PanelSide == PanelSide.Left
                    ? workingArea.X - panelWidth + HiddenEdgeWidth
                    : workingArea.Right - HiddenEdgeWidth)
                : (_settings.PanelSide == PanelSide.Left
                    ? workingArea.X
                    : workingArea.Right - panelWidth);
            _settings.PanelTop = top;
        }
        finally
        {
            _positioning = false;
        }

        ApplyPendingHeight();
    }

    public void CheckAutoHide()
    {
        if (_window == null || !_settings.AutoHide || _isHovering || _sliding || IsAutoHideSuppressed()) return;

        var mousePos = WindowHelper.GetCursorPosition(_window);
        var workingArea = GetWorkingArea(refresh: !IsHidden);
        var panelTop = _window.Top;
        var panelBottom = _window.Top + _window.Height;

        bool nearPanel = _settings.PanelSide == PanelSide.Left
            ? mousePos.X >= workingArea.X &&
              mousePos.X <= workingArea.X + _window.Width + 20 &&
              mousePos.Y >= panelTop &&
              mousePos.Y <= panelBottom
            : mousePos.X <= workingArea.Right &&
              mousePos.X >= workingArea.Right - _window.Width - 20 &&
              mousePos.Y >= panelTop &&
              mousePos.Y <= panelBottom;

        if (!nearPanel && !IsHidden)
            StartHideTimer();
    }

    private void StartHideTimer()
    {
        StopHideTimer();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _hideTimer.Tick += (_, _) =>
        {
            StopHideTimer();
            if (_window != null && !_isHovering && !_sliding && !IsAutoHideSuppressed())
                AnimateHide();
        };
        _hideTimer.Start();
    }

    private void StopHideTimer()
    {
        _hideTimer?.Stop();
        _hideTimer = null;
    }

    // ---- 滑出隐藏 (面板移出屏幕) ----

    public void AnimateHide()
    {
        if (_window == null ||
            _visibilityState is PanelVisibilityState.Hidden or PanelVisibilityState.Hiding ||
            IsAutoHideSuppressed())
        {
            return;
        }

        StopSlide();
        _visibilityState = PanelVisibilityState.Hiding;
        _isHidden = false;

        var workingArea = GetWorkingArea(refresh: true);
        _slideTarget = _settings.PanelSide == PanelSide.Left
            ? workingArea.X - _window.Width + HiddenEdgeWidth
            : workingArea.Right - HiddenEdgeWidth;

        StartSlideTowards(_slideTarget, onComplete: () =>
        {
            _isHidden = true;
            _visibilityState = PanelVisibilityState.Hidden;
            if (_settings.AutoHide)
                StartEdgeMonitor();
            VisibilityChanged?.Invoke();
        });
    }

    // ---- 滑入显示 (面板滑回屏幕) ----

    public void AnimateShow()
    {
        if (_window == null ||
            _visibilityState is PanelVisibilityState.Shown or PanelVisibilityState.Showing)
        {
            return;
        }

        StopSlide();
        StopEdgeMonitor();

        var workingArea = GetWorkingArea();
        _slideTarget = _settings.PanelSide == PanelSide.Left
            ? workingArea.X
            : workingArea.Right - _window.Width;

        _isHidden = false;
        _visibilityState = PanelVisibilityState.Showing;
        StartSlideTowards(_slideTarget, onComplete: () =>
        {
            _visibilityState = PanelVisibilityState.Shown;
            VisibilityChanged?.Invoke();
        });
    }

    // ---- 切换 ----

    public void ToggleVisibility()
    {
        if (IsHidden) AnimateShow();
        else AnimateHide();
    }

    // ---- 鼠标交互 ----

    public void OnMouseEnter()
    {
        _isHovering = true;
        StopHideTimer();
        if (_settings.AutoHide && IsHidden)
            AnimateShow();
    }

    public void OnMouseLeave()
    {
        _isHovering = false;
        if (_settings.AutoHide && !IsHidden && !IsAutoHideSuppressed())
            StartHideTimer();
    }

    // ---- 边缘监控 (隐藏时检测鼠标靠近) ----

    private void StartEdgeMonitor()
    {
        StopEdgeMonitor();
        _edgeMonitor = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(EdgePollIntervalMs) };
        _edgeMonitor.Tick += (_, _) =>
        {
            if (_window == null || !_isHidden) { StopEdgeMonitor(); return; }
            var mousePos = WindowHelper.GetCursorPosition(_window);
            var workingArea = GetWorkingArea();
            var panelTop = _window.Top;
            var panelBottom = _window.Top + _window.Height;

            bool touchingEdge = _settings.PanelSide == PanelSide.Left
                ? mousePos.X >= workingArea.X
                  && mousePos.X <= workingArea.X + TriggerEdgeWidth
                  && mousePos.Y >= panelTop
                  && mousePos.Y <= panelBottom
                : mousePos.X <= workingArea.Right
                  && mousePos.X >= workingArea.Right - TriggerEdgeWidth
                  && mousePos.Y >= panelTop
                  && mousePos.Y <= panelBottom;

            if (touchingEdge)
                AnimateShow();
        };
        _edgeMonitor.Start();
    }

    private void StopEdgeMonitor()
    {
        _edgeMonitor?.Stop();
        _edgeMonitor = null;
    }

    // ---- 动画引擎 ----

    private void StartSlideTowards(double targetX, Action? onComplete = null)
    {
        StopSlide();
        _sliding = true;
        int direction = targetX > (_window?.Left ?? 0) ? 1 : -1;
        bool increasing = direction > 0;

        _slideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SlideIntervalMs) };
        _slideTimer.Tick += (_, _) =>
        {
            if (_window == null) { StopSlide(); return; }
            double current = _window.Left;
            if ((increasing && current >= targetX - SlideStepPx) ||
                (!increasing && current <= targetX + SlideStepPx))
            {
                _window.Left = targetX;
                StopSlide();
                onComplete?.Invoke();
                ApplyPendingHeight();
                return;
            }
            _window.Left = current + direction * SlideStepPx;
        };
        _slideTimer.Start();
    }

    private void StopSlide()
    {
        _slideTimer?.Stop();
        _slideTimer = null;
        _sliding = false;
    }

    // ---- 设置实时应用 ----

    public void SetMousePenetration(bool enable)
    {
        if (_window != null)
            WindowHelper.SetMousePenetration(_window, enable);
    }

    public void SetTopMost(bool topMost)
    {
        if (_window != null)
            WindowHelper.SetTopMost(_window, topMost);
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        if (!_settings.AutoHide)
        {
            StopHideTimer();
            StopEdgeMonitor();
            if (IsHidden)
            {
                StopSlide();
                _isHidden = false;
                _visibilityState = PanelVisibilityState.Shown;
                PositionPanel();
                VisibilityChanged?.Invoke();
            }
        }
    }

    public void SnapToSideAndSavePlacement()
    {
        if (_window == null || _sliding)
            return;

        ClampWindowToWorkingArea(snapToSide: true);
        SavePlacementNow();
    }

    public void FitHeightToContent(double desiredHeight)
    {
        if (_window == null || !_settings.AutoFitPanelHeight)
            return;

        if (_sliding || _positioning)
        {
            _pendingDesiredHeight = desiredHeight;
            return;
        }

        _pendingDesiredHeight = null;
        ApplyHeight(desiredHeight);
    }

    public IDisposable SuspendAutoHide()
    {
        _autoHideSuppressionCount++;
        StopHideTimer();
        return new AutoHideScope(this);
    }

    private void ResumeAutoHide()
    {
        _autoHideSuppressionCount = Math.Max(0, _autoHideSuppressionCount - 1);
        if (_autoHideSuppressionCount == 0)
            CheckAutoHide();
    }

    private bool IsAutoHideSuppressed()
        => _autoHideSuppressionCount > 0 ||
           (_window?.OwnedWindows.OfType<Window>().Any(window => window.IsVisible) ?? false);

    private void ApplyWindowLimits()
    {
        if (_window == null)
            return;

        var workingArea = GetWorkingArea(refresh: !IsHidden);
        _window.MinWidth = MinPanelWidth;
        _window.MaxWidth = GetMaxPanelWidth(workingArea.Width);
        _window.MinHeight = Math.Min(MinPanelHeight, GetMaxPanelHeight(workingArea.Height));
        _window.MaxHeight = GetMaxPanelHeight(workingArea.Height);
    }

    private void ClampWindowToWorkingArea(bool snapToSide)
    {
        if (_window == null)
            return;

        var workingArea = GetWorkingArea(refresh: !IsHidden);
        var maxWidth = GetMaxPanelWidth(workingArea.Width);
        var maxHeight = GetMaxPanelHeight(workingArea.Height);
        var minHeight = Math.Min(MinPanelHeight, maxHeight);
        var width = Clamp(_window.Width, MinPanelWidth, maxWidth);
        var height = Clamp(_window.Height, minHeight, maxHeight);
        var top = Clamp(
            _window.Top,
            workingArea.Y,
            GetMaxPanelTop(workingArea.Y, workingArea.Height, height));

        _positioning = true;
        try
        {
            _window.Width = width;
            _window.Height = height;
            _window.Top = top;

            if (snapToSide)
            {
                _window.Left = IsHidden
                    ? (_settings.PanelSide == PanelSide.Left
                        ? workingArea.X - width + HiddenEdgeWidth
                        : workingArea.Right - HiddenEdgeWidth)
                    : (_settings.PanelSide == PanelSide.Left
                        ? workingArea.X
                        : workingArea.Right - width);
            }
        }
        finally
        {
            _positioning = false;
        }

        ApplyPendingHeight();
    }

    private void SavePlacementDebounced()
    {
        if (_window == null)
            return;

        _savePlacementTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SavePlacementDelayMs) };
        _savePlacementTimer.Tick -= OnSavePlacementTimerTick;
        _savePlacementTimer.Tick += OnSavePlacementTimerTick;
        _savePlacementTimer.Stop();
        _savePlacementTimer.Start();
    }

    private void OnSavePlacementTimerTick(object? sender, EventArgs e)
        => StopSavePlacementTimer(saveNow: true);

    private void StopSavePlacementTimer(bool saveNow)
    {
        if (_savePlacementTimer == null)
            return;

        _savePlacementTimer.Stop();
        _savePlacementTimer.Tick -= OnSavePlacementTimerTick;
        _savePlacementTimer = null;

        if (saveNow)
            SavePlacementNow();
    }

    private void SavePlacementNow()
    {
        if (_window == null)
            return;

        var workingArea = GetWorkingArea(refresh: !IsHidden);
        var width = Clamp(_window.Width, MinPanelWidth, GetMaxPanelWidth(workingArea.Width));
        var maxHeight = GetMaxPanelHeight(workingArea.Height);
        var height = Clamp(_window.Height, Math.Min(MinPanelHeight, maxHeight), maxHeight);
        var top = Clamp(
            _window.Top,
            workingArea.Y,
            GetMaxPanelTop(workingArea.Y, workingArea.Height, height));

        _settings.PanelWidth = (int)Math.Round(width);
        _settings.PanelHeight = (int)Math.Round(height);
        _settings.PanelTop = top;
        _settings.Save();
    }

    private void ApplyHeight(double desiredHeight)
    {
        if (_window == null || double.IsNaN(desiredHeight) || double.IsInfinity(desiredHeight))
            return;

        var workingArea = GetWorkingArea(refresh: !IsHidden);
        var maxHeight = GetMaxPanelHeight(workingArea.Height);
        var height = Clamp(desiredHeight, Math.Min(MinPanelHeight, maxHeight), maxHeight);
        if (Math.Abs(_window.Height - height) < 1)
            return;

        var top = Clamp(
            _window.Top,
            workingArea.Y,
            GetMaxPanelTop(workingArea.Y, workingArea.Height, height));

        _positioning = true;
        try
        {
            ApplyWindowLimits();
            _window.Height = height;
            _window.Top = top;
            _window.Left = IsHidden
                ? (_settings.PanelSide == PanelSide.Left
                    ? workingArea.X - _window.Width + HiddenEdgeWidth
                    : workingArea.Right - HiddenEdgeWidth)
                : (_settings.PanelSide == PanelSide.Left
                    ? workingArea.X
                    : workingArea.Right - _window.Width);
        }
        finally
        {
            _positioning = false;
        }

        _settings.PanelHeight = (int)Math.Round(height);
        _settings.PanelTop = top;
    }

    private void ApplyPendingHeight()
    {
        if (_pendingDesiredHeight is not double desiredHeight ||
            _window == null ||
            _sliding ||
            _positioning ||
            !_settings.AutoFitPanelHeight)
        {
            return;
        }

        _pendingDesiredHeight = null;
        ApplyHeight(desiredHeight);
    }

    private WorkingArea GetWorkingArea(bool refresh = false)
    {
        if (_window == null)
            return _lastWorkingArea ?? new WorkingArea(0, 0, SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height);

        if (refresh || _lastWorkingArea == null)
        {
            var (x, y, width, height) = WindowHelper.GetWorkingArea(_window);
            _lastWorkingArea = new WorkingArea(x, y, width, height);
        }

        return _lastWorkingArea.Value;
    }

    private static double GetMaxPanelWidth(double workingAreaWidth)
        => Math.Max(MinPanelWidth, Math.Min(MaxPanelWidth, workingAreaWidth - PanelMargin * 2));

    private static double GetMaxPanelHeight(double workingAreaHeight)
        => Math.Max(240, workingAreaHeight - PanelMargin * 2);

    private static double GetMaxPanelTop(double workingAreaY, double workingAreaHeight, double panelHeight)
        => Math.Max(workingAreaY, workingAreaY + workingAreaHeight - panelHeight);

    private static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));

    private sealed class AutoHideScope : IDisposable
    {
        private PanelService? _owner;

        public AutoHideScope(PanelService owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.ResumeAutoHide();
        }
    }

    private enum PanelVisibilityState
    {
        Shown,
        Hidden,
        Showing,
        Hiding
    }

    private readonly record struct WorkingArea(double X, double Y, double Width, double Height)
    {
        public double Right => X + Width;
    }
}
