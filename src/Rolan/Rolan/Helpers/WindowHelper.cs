using System.Windows;
using System.Windows.Media;

namespace Rolan.Helpers;

internal static class WindowHelper
{
    /// <summary>
    /// 获取当前屏幕的工作区（不含任务栏）
    /// </summary>
    public static (double x, double y, double width, double height) GetWorkingArea(Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var monitor = NativeMethods.MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST

        var mi = new NativeMethods.MONITORINFO();
        mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(mi);
        if (monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref mi))
        {
            var fallback = SystemParameters.WorkArea;
            return (fallback.Left, fallback.Top, fallback.Width, fallback.Height);
        }

        var topLeft = TransformFromDevice(window, new System.Windows.Point(mi.rcWork.left, mi.rcWork.top));
        var bottomRight = TransformFromDevice(window, new System.Windows.Point(mi.rcWork.right, mi.rcWork.bottom));

        return (topLeft.X, topLeft.Y,
                Math.Max(0, bottomRight.X - topLeft.X),
                Math.Max(0, bottomRight.Y - topLeft.Y));
    }

    public static System.Windows.Point GetCursorPosition(Window window)
    {
        var cursorPosition = System.Windows.Forms.Cursor.Position;
        return TransformFromDevice(window, new System.Windows.Point(cursorPosition.X, cursorPosition.Y));
    }

    private static System.Windows.Point TransformFromDevice(Window window, System.Windows.Point point)
    {
        var source = PresentationSource.FromVisual(window);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return transform.Transform(point);
    }

    /// <summary>
    /// 设置窗口鼠标穿透
    /// </summary>
    public static void SetMousePenetration(Window window, bool enable)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);

        if (enable)
            style |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED;
        else
            style &= ~(NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED);

        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style);
    }

    /// <summary>
    /// 设置窗口置顶状态
    /// </summary>
    public static void SetTopMost(Window window, bool topMost)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        window.Topmost = topMost;
        NativeMethods.SetWindowPos(hwnd,
            topMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }
}
