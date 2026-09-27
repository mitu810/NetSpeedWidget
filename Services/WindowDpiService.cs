using System;
using System.Runtime.InteropServices;

namespace NetSpeedWidget.Services;

public static class WindowDpiService
{
    /// <summary>在 XAML 首次加载前获取当前 HWND 的实际缩放比例。</summary>
    public static double GetScale(IntPtr window)
    {
        var dpi = GetDpiForWindow(window);
        return dpi == 0 ? 1 : dpi / 96.0;
    }

    /// <summary>将 XAML 逻辑尺寸转换为 AppWindow 和 Win32 使用的物理像素。</summary>
    public static int ToPixels(double effectivePixels, double scale) =>
        (int)Math.Round(effectivePixels * scale, MidpointRounding.AwayFromZero);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);
}
