using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NetSpeedWidget.Services
{
    public enum TaskbarOverlayAction
    {
        ShowTopmost,
        ForceShowTopmost,
        Hide
    }

    public readonly record struct ForegroundWindowState(
        bool IsFullscreenApplication,
        bool IsShellOverlayWindow);

    public readonly record struct ForegroundWindowInfo(
        string? ProcessName,
        string? ClassName,
        string? WindowTitle,
        RectBounds Bounds,
        RectBounds MonitorBounds);

    public static class WindowForegroundService
    {
        private const int FullscreenTolerance = 2;
        private const uint MonitorDefaultToNearest = 2;

        /// <summary>
        /// Chooses how the taskbar overlay should behave for the current foreground window.
        /// </summary>
        public static TaskbarOverlayAction GetTaskbarOverlayAction(
            ForegroundWindowState state)
        {
            if (state.IsShellOverlayWindow)
            {
                return TaskbarOverlayAction.ForceShowTopmost;
            }

            return state.IsFullscreenApplication
                ? TaskbarOverlayAction.Hide
                : TaskbarOverlayAction.ShowTopmost;
        }

        /// <summary>
        /// Determines whether a window covers the full monitor bounds.
        /// </summary>
        public static bool IsFullscreenWindow(
            RectBounds windowBounds,
            RectBounds monitorBounds)
        {
            if (windowBounds.Width <= 0 ||
                windowBounds.Height <= 0 ||
                monitorBounds.Width <= 0 ||
                monitorBounds.Height <= 0)
            {
                return false;
            }

            var windowRight = windowBounds.X + windowBounds.Width;
            var windowBottom = windowBounds.Y + windowBounds.Height;
            var monitorRight = monitorBounds.X + monitorBounds.Width;
            var monitorBottom = monitorBounds.Y + monitorBounds.Height;

            return windowBounds.X <= monitorBounds.X + FullscreenTolerance &&
                   windowBounds.Y <= monitorBounds.Y + FullscreenTolerance &&
                   windowRight >= monitorRight - FullscreenTolerance &&
                   windowBottom >= monitorBottom - FullscreenTolerance;
        }

        /// <summary>
        /// Identifies Windows Shell overlays such as Start menu and Windows Search.
        /// </summary>
        public static bool IsShellOverlayProcess(string? processName)
        {
            return string.Equals(processName, "StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "SearchHost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "SearchApp", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(processName, "ShellExperienceHost", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Identifies Explorer-owned desktop and taskbar shell windows.
        /// </summary>
        public static bool IsDesktopShellWindow(
            string? processName,
            string? className)
        {
            if (!string.Equals(processName, "explorer", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase);
        }

        public static string DescribeForegroundState(
            ForegroundWindowInfo info,
            ForegroundWindowState state,
            TaskbarOverlayAction action)
        {
            return
                "Foreground window: " +
                $"process={info.ProcessName ?? "<unknown>"}, " +
                $"class={info.ClassName ?? "<unknown>"}, " +
                $"title={info.WindowTitle ?? "<empty>"}, " +
                $"bounds={FormatBounds(info.Bounds)}, " +
                $"monitor={FormatBounds(info.MonitorBounds)}, " +
                $"fullscreen={state.IsFullscreenApplication}, " +
                $"shell={state.IsShellOverlayWindow}, " +
                $"action={action}";
        }

        public static ForegroundWindowInfo GetForegroundWindowInfo()
        {
            var foregroundWindow = GetForegroundWindow();

            if (foregroundWindow == IntPtr.Zero)
            {
                return default;
            }

            _ = TryGetWindowBounds(foregroundWindow, out var windowBounds);
            _ = TryGetMonitorBounds(foregroundWindow, out var monitorBounds);

            return new ForegroundWindowInfo(
                GetProcessName(foregroundWindow),
                GetWindowClassName(foregroundWindow),
                GetWindowTitle(foregroundWindow),
                windowBounds,
                monitorBounds);
        }

        /// <summary>
        /// Reads the current foreground window and classifies it for taskbar overlay behavior.
        /// </summary>
        public static ForegroundWindowState GetForegroundWindowState(
            IntPtr ignoredWindow)
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();

                if (foregroundWindow == IntPtr.Zero ||
                    foregroundWindow == ignoredWindow)
                {
                    return new ForegroundWindowState(false, false);
                }

                var processName = GetProcessName(foregroundWindow);
                var className = GetWindowClassName(foregroundWindow);

                if (IsShellOverlayProcess(processName))
                {
                    return new ForegroundWindowState(false, true);
                }

                if (IsDesktopShellWindow(processName, className))
                {
                    return new ForegroundWindowState(false, false);
                }

                if (!TryGetWindowBounds(foregroundWindow, out var windowBounds) ||
                    !TryGetMonitorBounds(foregroundWindow, out var monitorBounds))
                {
                    return new ForegroundWindowState(false, false);
                }

                return new ForegroundWindowState(
                    IsFullscreenWindow(windowBounds, monitorBounds),
                    false);
            }
            catch (Exception ex)
            {
                AppLogService.Write("Failed to inspect foreground window.", ex);
                return new ForegroundWindowState(false, false);
            }
        }

        private static string? GetProcessName(IntPtr hwnd)
        {
            _ = GetWindowThreadProcessId(hwnd, out var processId);

            if (processId == 0)
            {
                return null;
            }

            try
            {
                using var process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                return null;
            }
        }

        private static string? GetWindowClassName(IntPtr hwnd)
        {
            var builder = new StringBuilder(256);
            var length = GetClassName(hwnd, builder, builder.Capacity);

            return length > 0
                ? builder.ToString()
                : null;
        }

        private static string? GetWindowTitle(IntPtr hwnd)
        {
            var builder = new StringBuilder(256);
            var length = GetWindowText(hwnd, builder, builder.Capacity);

            return length > 0
                ? builder.ToString()
                : null;
        }

        private static string FormatBounds(RectBounds bounds)
        {
            return $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";
        }

        private static bool TryGetWindowBounds(
            IntPtr hwnd,
            out RectBounds bounds)
        {
            bounds = default;

            if (!GetWindowRect(hwnd, out var rect))
            {
                return false;
            }

            bounds = new RectBounds(
                rect.Left,
                rect.Top,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top);

            return true;
        }

        private static bool TryGetMonitorBounds(
            IntPtr hwnd,
            out RectBounds bounds)
        {
            bounds = default;

            var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);

            if (monitor == IntPtr.Zero)
            {
                return false;
            }

            var monitorInfo = new MonitorInfo
            {
                Size = Marshal.SizeOf<MonitorInfo>()
            };

            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                return false;
            }

            bounds = new RectBounds(
                monitorInfo.Monitor.Left,
                monitorInfo.Monitor.Top,
                monitorInfo.Monitor.Right - monitorInfo.Monitor.Left,
                monitorInfo.Monitor.Bottom - monitorInfo.Monitor.Top);

            return true;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(
            IntPtr hwnd,
            out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(
            IntPtr hwnd,
            StringBuilder className,
            int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(
            IntPtr hwnd,
            StringBuilder text,
            int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(
            IntPtr hwnd,
            out NativeRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr MonitorFromWindow(
            IntPtr hwnd,
            uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetMonitorInfo(
            IntPtr monitor,
            ref MonitorInfo monitorInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;
        }
    }
}
