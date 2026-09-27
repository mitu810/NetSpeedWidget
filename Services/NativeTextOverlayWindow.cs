using NetSpeedWidget.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace NetSpeedWidget.Services
{
    public readonly record struct TaskbarWindowInfo(
        IntPtr WindowHandle,
        RectBounds Bounds,
        RectBounds AvailableBounds);

    public sealed class NativeTextOverlayWindow : IDisposable
    {
        private const string WindowClassName = "NetSpeedWidgetNativeTextOverlay";
        private const string PrimaryTaskbarClassName = "Shell_TrayWnd";
        private const string SecondaryTaskbarClassName = "Shell_SecondaryTrayWnd";
        private const string TrayNotifyClassName = "TrayNotifyWnd";
        private const int GwlStyle = -16;
        private const uint WsPopup = 0x80000000;
        private const uint WsChild = 0x40000000;
        private const uint WsVisible = 0x10000000;
        private const uint WsExLayered = 0x00080000;
        private const uint WsExNoActivate = 0x08000000;
        private const uint WsExToolWindow = 0x00000080;
        private const uint WsExTopmost = 0x00000008;
        private const uint WsExTransparent = 0x00000020;
        private const int SwHide = 0;
        private const int SwShowNoActivate = 4;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;
        private const uint SwpShowWindow = 0x0040;
        private const byte AcSrcOver = 0;
        private const byte AcSrcAlpha = 1;
        private const int BiRgb = 0;
        private const int DibRgbColors = 0;
        private const int TransparentBackground = 1;
        private const uint MonitorDefaultToNull = 0;
        private const uint DefaultCharset = 1;
        private const uint OutDefaultPrecision = 0;
        private const uint ClipDefaultPrecision = 0;
        private const uint DefaultPitch = 0;
        private const uint TaLeft = 0x00000000;
        private const uint TaBaseline = 0x00000018;
        private const uint UlwAlpha = 0x00000002;
        private const int TextLeftPadding = 2;
        private static readonly IntPtr HwndTop = new(0);
        private static readonly IntPtr HwndTopmost = new(-1);
        private static readonly IntPtr HwndNoTopmost = new(-2);
        private static readonly WindowProcedureDelegate WindowProcedure = OverlayWindowProcedure;
        private static bool _classRegistered;

        private readonly int _width;
        private readonly int _height;
        private readonly uint _dpi;
        private readonly IntPtr _creationTaskbar;
        private bool _renderSucceeded;
        private int _lastScreenX;
        private int _lastScreenY;
        private IntPtr _hwnd;
        private IntPtr _parentWindow;
        private bool _disposed;

        public NativeTextOverlayWindow(int width, int height, IntPtr taskbarWindow = default)
        {
            _creationTaskbar = taskbarWindow;
            _dpi = GetTaskbarDpi(taskbarWindow);
            _width = ScaleForTaskbar(width, taskbarWindow);
            _height = ScaleForTaskbar(height, taskbarWindow);
        }

        /// <summary>获取目标任务栏 DPI，避免依赖隐藏的主窗口所在屏幕。</summary>
        public static uint GetTaskbarDpi(IntPtr taskbarWindow)
        {
            var dpi = taskbarWindow == IntPtr.Zero ? 0 : GetDpiForWindow(taskbarWindow);
            return dpi == 0 ? 96u : dpi;
        }

        /// <summary>把设置中的逻辑像素转换为目标屏幕物理像素。</summary>
        public static int ScaleForTaskbar(int value, IntPtr taskbarWindow) =>
            (int)Math.Round(value * GetTaskbarDpi(taskbarWindow) / 96.0);

        /// <summary>
        /// Shows transparent text at the specified screen position.
        /// </summary>
        public void Show(
            int x,
            int y,
            string uploadText,
            string downloadText,
            AppThemeMode theme,
            int speedFontSize,
            bool topmost,
            IntPtr taskbarWindow)
        {
            EnsureWindowCreated();
            _lastScreenX = x;
            _lastScreenY = y;
            var parentPoint = AttachToTaskbarIfPossible(x, y, taskbarWindow);
            RenderText(
                x,
                y,
                uploadText,
                downloadText,
                theme,
                speedFontSize);
            ShowWindow(_hwnd, SwShowNoActivate);
            if (_parentWindow != IntPtr.Zero)
            {
                MoveWithinParent(parentPoint.X, parentPoint.Y, showWindow: true);
                return;
            }

            SetTopmost(topmost);
        }

        /// <summary>
        /// Hides the overlay without destroying it.
        /// </summary>
        public void Hide()
        {
            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SwHide);
            }
        }

        /// <summary>
        /// Updates the overlay topmost state.
        /// </summary>
        public void SetTopmost(bool topmost)
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(
                _hwnd,
                topmost ? HwndTopmost : HwndNoTopmost,
                0,
                0,
                0,
                0,
                SwpNoMove |
                SwpNoSize |
                SwpNoActivate);
        }

        /// <summary>
        /// Clears the current taskbar parent so the next show can attach to the current taskbar.
        /// </summary>
        public void ResetTaskbarParent()
        {
            DetachFromTaskbar();
        }

        /// <summary>
        /// Forces the overlay to be visible and topmost without activating it.
        /// </summary>
        public void ForceTopmost()
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            ShowWindow(_hwnd, SwShowNoActivate);

            if (_parentWindow != IntPtr.Zero)
            {
                SetWindowPos(
                    _hwnd,
                    HwndTop,
                    0,
                    0,
                    0,
                    0,
                    SwpNoMove |
                    SwpNoSize |
                    SwpNoActivate |
                    SwpShowWindow);
                return;
            }

            SetWindowPos(
                _hwnd,
                HwndTopmost,
                0,
                0,
                0,
                0,
                SwpNoMove |
                SwpNoSize |
                SwpNoActivate |
                SwpShowWindow);
        }

        /// <summary>
        /// Returns whether the overlay is still parented to the expected taskbar window.
        /// </summary>
        public bool IsAttachedToTaskbar(IntPtr expectedTaskbarWindow)
        {
            if (_hwnd == IntPtr.Zero ||
                expectedTaskbarWindow == IntPtr.Zero)
            {
                return false;
            }

            return IsWindow(_hwnd) &&
                   IsWindow(expectedTaskbarWindow) &&
                   _parentWindow == expectedTaskbarWindow &&
                   GetParent(_hwnd) == expectedTaskbarWindow &&
                   _renderSucceeded &&
                   IsWindowVisible(_hwnd) &&
                   TryGetWindowBounds(_hwnd, out var bounds) &&
                   Math.Abs(bounds.X - _lastScreenX) <= 2 &&
                   Math.Abs(bounds.Y - _lastScreenY) <= 2;
        }

        /// <summary>
        /// Converts a screen point to taskbar-client coordinates when the overlay is parented to the taskbar.
        /// </summary>
        public static WidgetPoint CalculateParentedPoint(
            RectBounds taskbarBounds,
            int screenX,
            int screenY)
        {
            return new WidgetPoint(
                screenX - taskbarBounds.X,
                screenY - taskbarBounds.Y);
        }

        /// <summary>
        /// Gets every currently available Windows taskbar window.
        /// </summary>
        public static IReadOnlyList<TaskbarWindowInfo> GetTaskbarWindows()
        {
            var taskbarWindows = new List<TaskbarWindowInfo>();

            EnumWindows((hwnd, _) =>
            {
                if (IsAvailableTaskbarWindow(hwnd, out var bounds))
                {
                    taskbarWindows.Add(
                        new TaskbarWindowInfo(
                            hwnd,
                            bounds,
                            GetTaskbarAvailableBounds(hwnd, bounds)));
                }

                return true;
            }, IntPtr.Zero);

            taskbarWindows.Sort(CompareTaskbarWindows);
            return taskbarWindows;
        }

        /// <summary>
        /// Gets bounds for a known taskbar window when it is still available.
        /// </summary>
        public static bool TryGetAvailableTaskbarBounds(
            IntPtr taskbarWindow,
            out RectBounds bounds)
        {
            if (taskbarWindow == IntPtr.Zero)
            {
                bounds = default;
                return false;
            }

            if (!IsAvailableTaskbarWindow(taskbarWindow, out bounds))
            {
                return false;
            }

            bounds = GetTaskbarAvailableBounds(taskbarWindow, bounds);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
        }

        private void EnsureWindowCreated()
        {
            if (_hwnd != IntPtr.Zero)
            {
                return;
            }

            RegisterWindowClass();

            var targetContext = _creationTaskbar == IntPtr.Zero ? IntPtr.Zero : GetWindowDpiAwarenessContext(_creationTaskbar);
            var previousContext = targetContext == IntPtr.Zero ? IntPtr.Zero : SetThreadDpiAwarenessContext(targetContext);
            try
            {
            _hwnd =
                CreateWindowEx(
                    WsExLayered |
                    WsExNoActivate |
                    WsExToolWindow |
                    WsExTopmost |
                    WsExTransparent,
                    WindowClassName,
                    string.Empty,
                    WsPopup,
                    0,
                    0,
                    _width,
                    _height,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    GetModuleHandle(null),
                    IntPtr.Zero);
            }
            finally { if (previousContext != IntPtr.Zero) SetThreadDpiAwarenessContext(previousContext); }

            if (_hwnd == IntPtr.Zero)
            {
                AppLogService.Write(
                    "Failed to create native text overlay window.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        private WidgetPoint AttachToTaskbarIfPossible(
            int screenX,
            int screenY,
            IntPtr taskbarWindow)
        {
            if (_hwnd == IntPtr.Zero)
            {
                return new WidgetPoint(screenX, screenY);
            }

            if (taskbarWindow != IntPtr.Zero && !IsWindow(taskbarWindow))
            {
                DetachFromTaskbar();
                return new WidgetPoint(screenX, screenY);
            }

            if (taskbarWindow == IntPtr.Zero)
            {
                taskbarWindow = FindTaskbarWindowForPoint(screenX, screenY);
            }

            if (taskbarWindow == IntPtr.Zero)
            {
                DetachFromTaskbar();
                return new WidgetPoint(screenX, screenY);
            }

            if (_parentWindow != IntPtr.Zero &&
                GetParent(_hwnd) != taskbarWindow)
            {
                _parentWindow = IntPtr.Zero;
            }

            if (_parentWindow != taskbarWindow)
            {
                var style = GetWindowLong(_hwnd, GwlStyle);
                SetWindowLong(_hwnd, GwlStyle, (style & ~unchecked((int)WsPopup)) | (int)(WsChild | WsVisible));
                SetLastError(0);
                var previousParent = SetParent(_hwnd, taskbarWindow);
                var parentError = Marshal.GetLastWin32Error();

                if (previousParent == IntPtr.Zero &&
                    parentError != 0)
                {
                    SetWindowLong(_hwnd, GwlStyle, style);
                    _parentWindow = IntPtr.Zero;
                    AppLogService.Write(
                        $"Failed to attach native text overlay window to taskbar. overlay=0x{_hwnd.ToInt64():X}; taskbar=0x{taskbarWindow.ToInt64():X}; dpi={_dpi}",
                        new Win32Exception(parentError));
                    return new WidgetPoint(screenX, screenY);
                }

                _parentWindow = taskbarWindow;

                SetWindowPos(
                    _hwnd,
                    HwndTop,
                    0,
                    0,
                    0,
                    0,
                    SwpNoMove |
                    SwpNoSize |
                    SwpNoActivate |
                    SwpFrameChanged);
            }

            if (!TryGetWindowBounds(taskbarWindow, out var taskbarBounds))
            {
                return new WidgetPoint(screenX, screenY);
            }

            return CalculateParentedPoint(taskbarBounds, screenX, screenY);
        }

        private void DetachFromTaskbar()
        {
            if (_hwnd == IntPtr.Zero ||
                _parentWindow == IntPtr.Zero)
            {
                return;
            }

            SetParent(_hwnd, IntPtr.Zero);
            _parentWindow = IntPtr.Zero;

            var style = GetWindowLong(_hwnd, GwlStyle);
            SetWindowLong(_hwnd, GwlStyle, (style & ~unchecked((int)WsChild)) | unchecked((int)WsPopup));
            SetWindowPos(
                _hwnd,
                HwndTopmost,
                0,
                0,
                0,
                0,
                SwpNoMove |
                SwpNoSize |
                SwpNoActivate |
                SwpFrameChanged);
        }

        private void MoveWithinParent(int x, int y, bool showWindow)
        {
            SetWindowPos(
                _hwnd,
                HwndTop,
                x,
                y,
                _width,
                _height,
                SwpNoActivate |
                (showWindow ? SwpShowWindow : 0));
        }

        private static IntPtr FindTaskbarWindowForPoint(int screenX, int screenY)
        {
            var point = new NativePoint(screenX, screenY);
            var result = IntPtr.Zero;

            EnumWindows((hwnd, _) =>
            {
                if (!IsAvailableTaskbarWindow(hwnd, out var bounds) ||
                    !ContainsPoint(bounds, point))
                {
                    return true;
                }

                result = hwnd;
                return false;
            }, IntPtr.Zero);

            return result;
        }

        private static bool IsAvailableTaskbarWindow(
            IntPtr hwnd,
            out RectBounds bounds)
        {
            bounds = default;
            var className = GetWindowClassName(hwnd);
            return IsTaskbarClass(className) &&
                   IsWindowVisible(hwnd) &&
                   TryGetWindowBounds(hwnd, out bounds) &&
                   bounds.Width > 0 &&
                   bounds.Height > 0 &&
                   IsTaskbarOnCurrentDisplay(bounds);
        }

        private static bool IsTaskbarClass(string? className)
        {
            return string.Equals(className, PrimaryTaskbarClassName, StringComparison.Ordinal) ||
                   string.Equals(className, SecondaryTaskbarClassName, StringComparison.Ordinal);
        }

        private static bool IsTaskbarOnCurrentDisplay(RectBounds bounds)
        {
            var rect = new NativeRect
            {
                Left = bounds.X,
                Top = bounds.Y,
                Right = bounds.X + bounds.Width,
                Bottom = bounds.Y + bounds.Height
            };

            return MonitorFromRect(ref rect, MonitorDefaultToNull) != IntPtr.Zero;
        }

        private static RectBounds GetTaskbarAvailableBounds(
            IntPtr taskbarWindow,
            RectBounds taskbarBounds)
        {
            var availableBounds = taskbarBounds;

            EnumChildWindows(taskbarWindow, (hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd) ||
                    !string.Equals(
                        GetWindowClassName(hwnd),
                        TrayNotifyClassName,
                        StringComparison.Ordinal) ||
                    !TryGetWindowBounds(hwnd, out var trayBounds))
                {
                    return true;
                }

                availableBounds =
                    ExcludeTrayNotifyBounds(taskbarBounds, trayBounds);
                return false;
            }, IntPtr.Zero);

            return availableBounds;
        }

        private static RectBounds ExcludeTrayNotifyBounds(
            RectBounds taskbarBounds,
            RectBounds trayBounds)
        {
            var taskbarRight = taskbarBounds.X + taskbarBounds.Width;
            var taskbarBottom = taskbarBounds.Y + taskbarBounds.Height;
            var trayRight = trayBounds.X + trayBounds.Width;
            var trayBottom = trayBounds.Y + trayBounds.Height;

            if (trayBounds.Width <= 0 ||
                trayBounds.Height <= 0 ||
                !Intersects(taskbarBounds, trayBounds))
            {
                return taskbarBounds;
            }

            if (trayBounds.X > taskbarBounds.X &&
                trayRight >= taskbarRight)
            {
                var width = Math.Max(1, trayBounds.X - taskbarBounds.X);
                return new RectBounds(
                    taskbarBounds.X,
                    taskbarBounds.Y,
                    width,
                    taskbarBounds.Height);
            }

            if (trayRight < taskbarRight &&
                trayBounds.X <= taskbarBounds.X)
            {
                var x = trayRight;
                var width = Math.Max(1, taskbarRight - x);
                return new RectBounds(
                    x,
                    taskbarBounds.Y,
                    width,
                    taskbarBounds.Height);
            }

            if (trayBounds.Y > taskbarBounds.Y &&
                trayBottom >= taskbarBottom)
            {
                var height = Math.Max(1, trayBounds.Y - taskbarBounds.Y);
                return new RectBounds(
                    taskbarBounds.X,
                    taskbarBounds.Y,
                    taskbarBounds.Width,
                    height);
            }

            if (trayBottom < taskbarBottom &&
                trayBounds.Y <= taskbarBounds.Y)
            {
                var y = trayBottom;
                var height = Math.Max(1, taskbarBottom - y);
                return new RectBounds(
                    taskbarBounds.X,
                    y,
                    taskbarBounds.Width,
                    height);
            }

            return taskbarBounds;
        }

        private static bool Intersects(
            RectBounds left,
            RectBounds right)
        {
            return left.X < right.X + right.Width &&
                   left.X + left.Width > right.X &&
                   left.Y < right.Y + right.Height &&
                   left.Y + left.Height > right.Y;
        }

        private static string? GetWindowClassName(IntPtr hwnd)
        {
            var builder = new StringBuilder(256);
            var length = GetClassName(hwnd, builder, builder.Capacity);

            return length > 0
                ? builder.ToString()
                : null;
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

        private static bool ContainsPoint(
            RectBounds bounds,
            NativePoint point)
        {
            return point.X >= bounds.X &&
                   point.Y >= bounds.Y &&
                   point.X < bounds.X + bounds.Width &&
                   point.Y < bounds.Y + bounds.Height;
        }

        private static int CompareTaskbarWindows(
            TaskbarWindowInfo left,
            TaskbarWindowInfo right)
        {
            var yComparison = left.Bounds.Y.CompareTo(right.Bounds.Y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            var xComparison = left.Bounds.X.CompareTo(right.Bounds.X);
            if (xComparison != 0)
            {
                return xComparison;
            }

            return left.WindowHandle.ToInt64().CompareTo(right.WindowHandle.ToInt64());
        }

        private static void RegisterWindowClass()
        {
            if (_classRegistered)
            {
                return;
            }

            var windowClass = new WindowClassEx
            {
                Size = (uint)Marshal.SizeOf<WindowClassEx>(),
                WindowProcedure = Marshal.GetFunctionPointerForDelegate(WindowProcedure),
                Instance = GetModuleHandle(null),
                ClassName = WindowClassName
            };

            var atom = RegisterClassEx(ref windowClass);
            var error = Marshal.GetLastWin32Error();

            if (atom == 0 && error != 1410)
            {
                AppLogService.Write(
                    "Failed to register native text overlay window class.",
                    new Win32Exception(error));
                return;
            }

            _classRegistered = true;
        }

        private void RenderText(
            int x,
            int y,
            string uploadText,
            string downloadText,
            AppThemeMode theme,
            int speedFontSize)
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            speedFontSize = (int)Math.Round(speedFontSize * _dpi / 96.0);
            _renderSucceeded = false;
            var screenDc = GetDC(IntPtr.Zero);
            var memoryDc = CreateCompatibleDC(screenDc);
            var bitmapInfo = BitmapInfo.CreateTopDown(_width, _height);
            var bitmapHandle =
                CreateDIBSection(
                    screenDc,
                    ref bitmapInfo,
                    DibRgbColors,
                    out var bitmapBits,
                    IntPtr.Zero,
                    0);
            var oldBitmap = SelectObject(memoryDc, bitmapHandle);
            var fontHandle =
                CreateFont(
                    NativeTextOverlayRenderService.GetFontHeight(speedFontSize),
                    0,
                    0,
                    0,
                    NativeTextOverlayRenderService.FontWeight,
                    0,
                    0,
                    0,
                    DefaultCharset,
                    OutDefaultPrecision,
                    ClipDefaultPrecision,
                    NativeTextOverlayRenderService.FontQuality,
                    DefaultPitch,
                    NativeTextOverlayRenderService.FontFaceName);
            var oldFont = SelectObject(memoryDc, fontHandle);

            try
            {
                DrawTextToBitmap(
                    memoryDc,
                    bitmapBits,
                    uploadText,
                    downloadText,
                    theme,
                    speedFontSize);

                var destinationPoint = new NativePoint(x, y);
                var sourcePoint = new NativePoint(0, 0);
                var size = new NativeSize(_width, _height);
                var blend = new BlendFunction
                {
                    BlendOp = AcSrcOver,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AcSrcAlpha
                };

                if (!UpdateLayeredWindow(
                    _hwnd,
                    screenDc,
                    ref destinationPoint,
                    ref size,
                    memoryDc,
                    ref sourcePoint,
                    0,
                    ref blend,
                    UlwAlpha))
                {
                    AppLogService.Write(
                        "Failed to update native text overlay window.",
                        new Win32Exception(Marshal.GetLastWin32Error()));
                }
                else { _renderSucceeded = true; }
            }
            finally
            {
                SelectObject(memoryDc, oldFont);
                SelectObject(memoryDc, oldBitmap);
                DeleteObject(fontHandle);
                DeleteObject(bitmapHandle);
                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private void DrawTextToBitmap(
            IntPtr memoryDc,
            IntPtr bitmapBits,
            string uploadText,
            string downloadText,
            AppThemeMode theme,
            int speedFontSize)
        {
            var byteCount = _width * _height * 4;
            var pixels = new byte[byteCount];
            Marshal.Copy(pixels, 0, bitmapBits, byteCount);
            var baselines =
                NativeTextOverlayRenderService.CalculateTextBaselines(
                    _height,
                    Math.Abs(NativeTextOverlayRenderService.GetFontHeight(speedFontSize)));

            SetBkMode(memoryDc, TransparentBackground);
            SetTextColor(memoryDc, ToColorRef(new OverlayTextColor(255, 255, 255)));
            SetTextAlign(memoryDc, TaLeft | TaBaseline);
            TextOut(
                memoryDc,
                TextLeftPadding,
                baselines.UploadBaseline,
                uploadText,
                uploadText.Length);
            TextOut(
                memoryDc,
                TextLeftPadding,
                baselines.DownloadBaseline,
                downloadText,
                downloadText.Length);

            Marshal.Copy(bitmapBits, pixels, 0, byteCount);

            NativeTextOverlayRenderService.ApplyTextMaskToPixels(
                pixels,
                NativeTextOverlayRenderService.GetTextColor(theme));

            Marshal.Copy(pixels, 0, bitmapBits, byteCount);
        }

        private static uint ToColorRef(OverlayTextColor color)
        {
            return (uint)(color.Red | (color.Green << 8) | (color.Blue << 16));
        }

        private static IntPtr OverlayWindowProcedure(
            IntPtr hwnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam)
        {
            return DefWindowProc(hwnd, message, wParam, lParam);
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassEx(ref WindowClassEx windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint extendedStyle,
            string className,
            string windowName,
            uint style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parentWindow,
            IntPtr menu,
            IntPtr instance,
            IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(
            IntPtr childWindow,
            IntPtr newParentWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(
            IntPtr hwnd,
            int index);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(
            IntPtr hwnd,
            int index,
            int value);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hwnd, int command);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hwnd,
            IntPtr insertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr DefWindowProc(
            IntPtr hwnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(
            string className,
            string? windowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(
            EnumWindowsProcedure callback,
            IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumChildWindows(
            IntPtr parentWindow,
            EnumWindowsProcedure callback,
            IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(
            IntPtr hwnd,
            StringBuilder className,
            int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(
            IntPtr hwnd,
            out NativeRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr MonitorFromRect(
            ref NativeRect rect,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(
            IntPtr dc,
            ref BitmapInfo bitmapInfo,
            int usage,
            out IntPtr bits,
            IntPtr section,
            uint offset);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFont(
            int height,
            int width,
            int escapement,
            int orientation,
            int weight,
            uint italic,
            uint underline,
            uint strikeOut,
            uint charSet,
            uint outputPrecision,
            uint clipPrecision,
            uint quality,
            uint pitchAndFamily,
            string faceName);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr graphicsObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr graphicsObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int SetBkMode(IntPtr dc, int mode);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern uint SetTextColor(IntPtr dc, uint color);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern uint SetTextAlign(IntPtr dc, uint align);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool TextOut(
            IntPtr dc,
            int x,
            int y,
            string text,
            int textLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? moduleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void SetLastError(int errorCode);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd,
            IntPtr destinationDc,
            ref NativePoint destinationPoint,
            ref NativeSize size,
            IntPtr sourceDc,
            ref NativePoint sourcePoint,
            uint colorKey,
            ref BlendFunction blend,
            uint flags);

        private delegate IntPtr WindowProcedureDelegate(
            IntPtr hwnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam);

        private delegate bool EnumWindowsProcedure(
            IntPtr hwnd,
            IntPtr parameter);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WindowClassEx
        {
            public uint Size;
            public uint Style;
            public IntPtr WindowProcedure;
            public int ClassExtra;
            public int WindowExtra;
            public IntPtr Instance;
            public IntPtr Icon;
            public IntPtr Cursor;
            public IntPtr Background;
            public string? MenuName;
            public string ClassName;
            public IntPtr SmallIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint RedMask;
            public uint GreenMask;
            public uint BlueMask;

            public static BitmapInfo CreateTopDown(int width, int height)
            {
                return new BitmapInfo
                {
                    Header = new BitmapInfoHeader
                    {
                        Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                        Width = width,
                        Height = -height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = BiRgb,
                        SizeImage = (uint)(width * height * 4)
                    }
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public int Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public NativePoint(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public NativeSize(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width;
            public int Height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }
    }
}
