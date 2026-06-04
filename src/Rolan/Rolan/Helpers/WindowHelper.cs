using System.Windows;
using Rolan.Helpers;

namespace Rolan.Helpers;

internal static class WindowHelper
{
    /// <summary>
    /// 获取当前屏幕的工作区（不含任务栏）
    /// </summary>
    public static (int x, int y, int width, int height) GetWorkingArea(Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var monitor = NativeMethods.MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST

        var mi = new NativeMethods.MONITORINFO();
        mi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(mi);
        NativeMethods.GetMonitorInfo(monitor, ref mi);

        return (mi.rcWork.left, mi.rcWork.top,
                mi.rcWork.right - mi.rcWork.left,
                mi.rcWork.bottom - mi.rcWork.top);
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
        NativeMethods.SetWindowPos(hwnd,
            topMost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }
}
