using NetSpeedWidget.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace NetSpeedWidget.Services
{
    public sealed class NativeAppBarWindow : IDisposable
    {
        private const string WindowClassName = "NetSpeedWidgetNativeAppBar";
        private const uint WsPopup = 0x80000000;
        private const uint WsExNoActivate = 0x08000000;
        private const uint WsExToolWindow = 0x00000080;
        private const int SwHide = 0;
        private const int SwShowNoActivate = 4;
        private const uint WmUser = 0x0400;
        private const uint CallbackMessage = WmUser + 0x30;
        private const uint WmPaint = 0x000F;
        private const uint WmEraseBackground = 0x0014;
        private const uint AbmNew = 0x00000000;
        private const uint AbmRemove = 0x00000001;
        private const uint AbmQueryPos = 0x00000002;
        private const uint AbmSetPos = 0x00000003;
        private const uint AbnPosChanged = 0x00000001;
        private const uint AbeBottom = 3;
        private const int TransparentBackground = 1;
        private const int FontWeightSemiBold = 600;
        private const uint DefaultCharset = 1;
        private const uint OutDefaultPrecision = 0;
        private const uint ClipDefaultPrecision = 0;
        private const uint NonAntiAliasedQuality = 3;
        private const uint DefaultPitch = 0;
        private const uint TaCenter = 0x00000006;
        private const uint TaBaseline = 0x00000018;
        private const int DefaultEdgePadding = 4;

        private static readonly WindowProcedureDelegate WindowProcedure = AppBarWindowProcedure;
        private static readonly Dictionary<IntPtr, NativeAppBarWindow> Windows = new();
        private static bool _classRegistered;

        private readonly int _widgetWidth;
        private readonly int _widgetHeight;
        private IntPtr _hwnd;
        private bool _appBarRegistered;
        private bool _disposed;
        private RectBounds _displayBounds;
        private RectBounds _appBarBounds;
        private WidgetDisplayMode _displayMode = WidgetDisplayMode.TaskbarRight;
        private AppThemeMode _theme = AppThemeMode.Dark;
        private string _uploadText = string.Empty;
        private string _downloadText = string.Empty;

        public NativeAppBarWindow(int widgetWidth, int widgetHeight)
        {
            _widgetWidth = widgetWidth;
            _widgetHeight = widgetHeight;
        }

        /// <summary>
        /// Shows or updates the native appbar and repaints the current speed text.
        /// </summary>
        public void Show(
            RectBounds displayBounds,
            WidgetDisplayMode displayMode,
            string uploadText,
            string downloadText,
            AppThemeMode theme)
        {
            EnsureWindowCreated();

            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            var positionChanged =
                !_appBarRegistered ||
                !_displayBounds.Equals(displayBounds) ||
                _displayMode != displayMode;

            _displayBounds = displayBounds;
            _displayMode = displayMode;
            _theme = theme;
            _uploadText = uploadText;
            _downloadText = downloadText;

            RegisterAppBar();

            if (positionChanged)
            {
                SetAppBarPosition();
            }

            ShowWindow(_hwnd, SwShowNoActivate);
            InvalidateRect(_hwnd, IntPtr.Zero, true);
        }

        /// <summary>
        /// Unregisters and hides the native appbar.
        /// </summary>
        public void Hide()
        {
            UnregisterAppBar();

            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SwHide);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Hide();

            if (_hwnd != IntPtr.Zero)
            {
                Windows.Remove(_hwnd);
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

            _hwnd =
                CreateWindowEx(
                    WsExNoActivate | WsExToolWindow,
                    WindowClassName,
                    string.Empty,
                    WsPopup,
                    0,
                    0,
                    _widgetWidth,
                    _widgetHeight,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    GetModuleHandle(null),
                    IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                AppLogService.Write(
                    "Failed to create native appbar window.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
                return;
            }

            Windows[_hwnd] = this;
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
                    "Failed to register native appbar window class.",
                    new Win32Exception(error));
                return;
            }

            _classRegistered = true;
        }

        private void RegisterAppBar()
        {
            if (_appBarRegistered)
            {
                return;
            }

            var appBarData = CreateAppBarData();
            appBarData.CallbackMessage = CallbackMessage;

            if (SHAppBarMessage(AbmNew, ref appBarData) == UIntPtr.Zero)
            {
                AppLogService.Write(
                    "Failed to register native appbar.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
                return;
            }

            _appBarRegistered = true;
        }

        private void UnregisterAppBar()
        {
            if (!_appBarRegistered || _hwnd == IntPtr.Zero)
            {
                return;
            }

            var appBarData = CreateAppBarData();
            SHAppBarMessage(AbmRemove, ref appBarData);
            _appBarRegistered = false;
        }

        private void SetAppBarPosition()
        {
            if (!_appBarRegistered || _hwnd == IntPtr.Zero)
            {
                return;
            }

            var appBarData = CreateAppBarData();

            appBarData.Edge = AbeBottom;
            appBarData.Rect = new NativeRect(
                _displayBounds.X,
                _displayBounds.Y + _displayBounds.Height - _widgetHeight,
                _displayBounds.X + _displayBounds.Width,
                _displayBounds.Y + _displayBounds.Height);

            SHAppBarMessage(AbmQueryPos, ref appBarData);
            appBarData.Rect.Top = appBarData.Rect.Bottom - _widgetHeight;
            SHAppBarMessage(AbmSetPos, ref appBarData);

            _appBarBounds = new RectBounds(
                appBarData.Rect.Left,
                appBarData.Rect.Top,
                appBarData.Rect.Right - appBarData.Rect.Left,
                appBarData.Rect.Bottom - appBarData.Rect.Top);

            MoveWindow(
                _hwnd,
                _appBarBounds.X,
                _appBarBounds.Y,
                _appBarBounds.Width,
                _appBarBounds.Height,
                true);
        }

        private AppBarData CreateAppBarData()
        {
            return new AppBarData
            {
                Size = (uint)Marshal.SizeOf<AppBarData>(),
                WindowHandle = _hwnd
            };
        }

        private IntPtr HandleMessage(
            uint message,
            IntPtr wParam,
            IntPtr lParam)
        {
            if (message == CallbackMessage &&
                wParam.ToInt64() == AbnPosChanged)
            {
                SetAppBarPosition();
                return IntPtr.Zero;
            }

            if (message == WmEraseBackground)
            {
                return new IntPtr(1);
            }

            if (message == WmPaint)
            {
                Paint();
                return IntPtr.Zero;
            }

            return DefWindowProc(_hwnd, message, wParam, lParam);
        }

        private void Paint()
        {
            var paintDc = BeginPaint(_hwnd, out var paintStruct);

            try
            {
                DrawBackground(paintDc);
                DrawSpeedText(paintDc);
            }
            finally
            {
                EndPaint(_hwnd, ref paintStruct);
            }
        }

        private void DrawBackground(IntPtr dc)
        {
            var rect = new NativeRect(0, 0, _appBarBounds.Width, _appBarBounds.Height);
            var brush = CreateSolidBrush(ToColorRef(GetBackgroundColor(_theme)));

            try
            {
                FillRect(dc, ref rect, brush);
            }
            finally
            {
                DeleteObject(brush);
            }
        }

        private void DrawSpeedText(IntPtr dc)
        {
            var contentPoint =
                WidgetPlacementService.CalculateAppBarContentPosition(
                    _displayMode,
                    _appBarBounds,
                    _widgetWidth,
                    _widgetHeight,
                    DefaultEdgePadding);
            var relativeX = contentPoint.X - _appBarBounds.X;
            var relativeY = contentPoint.Y - _appBarBounds.Y;
            var fontHandle =
                CreateFont(
                    -12,
                    0,
                    0,
                    0,
                    FontWeightSemiBold,
                    0,
                    0,
                    0,
                    DefaultCharset,
                    OutDefaultPrecision,
                    ClipDefaultPrecision,
                    NonAntiAliasedQuality,
                    DefaultPitch,
                    "Tahoma");
            var oldFont = SelectObject(dc, fontHandle);

            try
            {
                SetBkMode(dc, TransparentBackground);
                SetTextColor(dc, ToColorRef(GetTextColor(_theme)));
                SetTextAlign(dc, TaCenter | TaBaseline);

                var text = $"{_uploadText}  {_downloadText}";
                TextOut(
                    dc,
                    relativeX + (_widgetWidth / 2),
                    relativeY + ((_widgetHeight + 12) / 2),
                    text,
                    text.Length);
            }
            finally
            {
                SelectObject(dc, oldFont);
                DeleteObject(fontHandle);
            }
        }

        private static uint ToColorRef(AppBarColor color)
        {
            return (uint)(color.Red | (color.Green << 8) | (color.Blue << 16));
        }

        private static AppBarColor GetBackgroundColor(AppThemeMode theme)
        {
            return theme == AppThemeMode.Light
                ? new AppBarColor(242, 242, 242)
                : new AppBarColor(32, 32, 32);
        }

        private static AppBarColor GetTextColor(AppThemeMode theme)
        {
            return theme == AppThemeMode.Light
                ? new AppBarColor(32, 32, 32)
                : new AppBarColor(255, 255, 255);
        }

        private static IntPtr AppBarWindowProcedure(
            IntPtr hwnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam)
        {
            return Windows.TryGetValue(hwnd, out var window)
                ? window.HandleMessage(message, wParam, lParam)
                : DefWindowProc(hwnd, message, wParam, lParam);
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern UIntPtr SHAppBarMessage(
            uint message,
            ref AppBarData data);

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
        private static extern bool ShowWindow(IntPtr hwnd, int command);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(
            IntPtr hwnd,
            int x,
            int y,
            int width,
            int height,
            bool repaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool InvalidateRect(
            IntPtr hwnd,
            IntPtr rect,
            bool erase);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr DefWindowProc(
            IntPtr hwnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr BeginPaint(
            IntPtr hwnd,
            out PaintStruct paint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EndPaint(
            IntPtr hwnd,
            ref PaintStruct paint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int FillRect(
            IntPtr dc,
            ref NativeRect rect,
            IntPtr brush);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateSolidBrush(uint color);

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

        private delegate IntPtr WindowProcedureDelegate(
            IntPtr hwnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct AppBarData
        {
            public uint Size;
            public IntPtr WindowHandle;
            public uint CallbackMessage;
            public uint Edge;
            public NativeRect Rect;
            public IntPtr Param;
        }

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
        private struct NativeRect
        {
            public NativeRect(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PaintStruct
        {
            public IntPtr Dc;
            public bool Erase;
            public NativeRect Paint;
            public bool Restore;
            public bool IncUpdate;
            public byte Reserved1;
            public byte Reserved2;
            public byte Reserved3;
            public byte Reserved4;
            public byte Reserved5;
            public byte Reserved6;
            public byte Reserved7;
            public byte Reserved8;
            public byte Reserved9;
            public byte Reserved10;
            public byte Reserved11;
            public byte Reserved12;
            public byte Reserved13;
            public byte Reserved14;
            public byte Reserved15;
            public byte Reserved16;
            public byte Reserved17;
            public byte Reserved18;
            public byte Reserved19;
            public byte Reserved20;
            public byte Reserved21;
            public byte Reserved22;
            public byte Reserved23;
            public byte Reserved24;
            public byte Reserved25;
            public byte Reserved26;
            public byte Reserved27;
            public byte Reserved28;
            public byte Reserved29;
            public byte Reserved30;
            public byte Reserved31;
            public byte Reserved32;
        }

        private readonly record struct AppBarColor(
            byte Red,
            byte Green,
            byte Blue);
    }
}
