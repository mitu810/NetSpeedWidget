using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace NetSpeedWidget.Services
{
    public sealed class TrayIconService : IDisposable
    {
        private const int TrayIconId = 1;
        private const int CallbackMessage = 0x0400 + 1;
        private const int NifMessage = 0x00000001;
        private const int NifIcon = 0x00000002;
        private const int NifTip = 0x00000004;
        private const int NifShowTip = 0x00000080;
        private const int NimAdd = 0x00000000;
        private const int NimModify = 0x00000001;
        private const int NimDelete = 0x00000002;
        private const int NimSetVersion = 0x00000004;
        private const int NotifyIconVersion4 = 4;
        private const int WmRButtonUp = 0x0205;
        private const int WmLButtonDblClk = 0x0203;
        private const int WmContextMenu = 0x007B;
        private const int WmCommand = 0x0111;
        private const int WmDestroy = 0x0002;
        private const int WmClose = 0x0010;
        private const int WmUserTraySettings = 1001;
        private const int WmUserTrayExit = 1002;
        private const int ErrorClassAlreadyExists = 1410;
        private const int ImageIcon = 1;
        private const int LrDefaultSize = 0x00000040;
        private const int ApplicationIconResourceId = 32512;
        private const uint TpmRightButton = 0x0002;
        private const uint TpmReturnCommand = 0x0100;
        private const string TooltipText = "NetSpeedWidget";
        private static readonly IntPtr IdiApplication = new(ApplicationIconResourceId);

        private readonly IntPtr _ownerHwnd;
        private readonly Action _openSettings;
        private readonly Action _exitApplication;
        private readonly IntPtr _iconHandle;
        private readonly WndProc _wndProc;
        private readonly IntPtr _windowHandle;
        private bool _disposed;

        public TrayIconService(
            IntPtr ownerHwnd,
            Action openSettings,
            Action exitApplication)
        {
            _ownerHwnd = ownerHwnd;
            _openSettings = openSettings;
            _exitApplication = exitApplication;
            _wndProc = WindowProcedure;
            _windowHandle = CreateMessageWindow();
            _iconHandle = LoadCustomIcon();

            AddTrayIcon();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DeleteTrayIcon();

            if (_windowHandle != IntPtr.Zero)
            {
                DestroyWindow(_windowHandle);
            }

            _disposed = true;
        }

        private IntPtr CreateMessageWindow()
        {
            var className = "NetSpeedWidgetTrayWindow";
            var instanceHandle = GetModuleHandle(null);
            var windowClass = new WindowClass
            {
                ClassName = className,
                InstanceHandle = instanceHandle,
                WindowProcedure = Marshal.GetFunctionPointerForDelegate(_wndProc)
            };

            var classAtom = RegisterClass(ref windowClass);
            var classError = Marshal.GetLastWin32Error();

            if (classAtom == 0 && classError != ErrorClassAlreadyExists)
            {
                throw new Win32Exception(classError, "Failed to register tray window class.");
            }

            var hwnd = CreateWindowEx(
                0,
                className,
                className,
                0,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                instanceHandle,
                IntPtr.Zero);

            if (hwnd == IntPtr.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to create tray message window.");
            }

            return hwnd;
        }

        private static IntPtr LoadCustomIcon()
        {
            var instanceHandle = GetModuleHandle(null);
            var iconHandle =
                LoadImage(
                    instanceHandle,
                    IdiApplication,
                    ImageIcon,
                    0,
                    0,
                    LrDefaultSize);

            return iconHandle != IntPtr.Zero
                ? iconHandle
                : LoadIcon(IntPtr.Zero, IdiApplication);
        }

        private void AddTrayIcon()
        {
            var data = CreateNotifyIconData();

            if (!ShellNotifyIcon(NimAdd, ref data))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to add tray icon.");
            }

            data.VersionOrTimeout = NotifyIconVersion4;
            ShellNotifyIcon(NimSetVersion, ref data);
            ShellNotifyIcon(NimModify, ref data);
        }

        /// <summary>Explorer 重建任务栏后重新注册托盘图标，异常不影响网速窗口。</summary>
        public void RestoreAfterExplorerRestart()
        {
            if (_disposed) return;
            try { DeleteTrayIcon(); AddTrayIcon(); }
            catch (Win32Exception ex) { AppLogService.Write("Failed to restore tray icon after Explorer restart.", ex); }
        }

        private void DeleteTrayIcon()
        {
            var data = CreateNotifyIconData();
            ShellNotifyIcon(NimDelete, ref data);
        }

        private NotifyIconData CreateNotifyIconData()
        {
            var data = new NotifyIconData
            {
                Size = Marshal.SizeOf<NotifyIconData>(),
                WindowHandle = _windowHandle,
                Id = TrayIconId,
                Flags = GetNotifyIconFlags(),
                CallbackMessage = CallbackMessage,
                IconHandle = _iconHandle,
                Tip = GetTooltipText()
            };

            return data;
        }

        private IntPtr WindowProcedure(
            IntPtr hwnd,
            int message,
            IntPtr wParam,
            IntPtr lParam)
        {
            if (message == CallbackMessage)
            {
                var mouseMessage = lParam.ToInt32();

                if (IsContextMenuMessage(mouseMessage))
                {
                    ShowContextMenu();
                    return IntPtr.Zero;
                }

                if (mouseMessage == WmLButtonDblClk)
                {
                    _openSettings();
                    return IntPtr.Zero;
                }
            }

            if (message == WmCommand)
            {
                var commandId = wParam.ToInt32() & 0xFFFF;

                ExecuteMenuCommand(commandId);
                return IntPtr.Zero;
            }

            if (message is WmDestroy or WmClose)
            {
                return IntPtr.Zero;
            }

            return DefWindowProc(hwnd, message, wParam, lParam);
        }

        public static bool IsContextMenuMessage(int message)
        {
            var notificationMessage = message & 0xFFFF;

            return notificationMessage is WmContextMenu or WmRButtonUp;
        }

        public static int GetNotifyIconFlags()
        {
            return NifMessage | NifIcon | NifTip | NifShowTip;
        }

        public static string GetTooltipText()
        {
            return TooltipText;
        }

        private void ShowContextMenu()
        {
            GetCursorPos(out Win32Point cursor);

            var menuHandle = CreatePopupMenu();

            AppendMenu(menuHandle, 0, WmUserTraySettings, "设置");
            AppendMenu(menuHandle, 0, WmUserTrayExit, "退出");

            SetForegroundWindow(_ownerHwnd);
            var commandId = TrackPopupMenu(
                menuHandle,
                TpmRightButton | TpmReturnCommand,
                cursor.X,
                cursor.Y,
                0,
                _windowHandle,
                IntPtr.Zero);

            DestroyMenu(menuHandle);

            ExecuteMenuCommand(commandId);
        }

        private void ExecuteMenuCommand(int commandId)
        {
            if (commandId == WmUserTraySettings)
            {
                _openSettings();
                return;
            }

            if (commandId == WmUserTrayExit)
            {
                _exitApplication();
            }
        }

        private delegate IntPtr WndProc(
            IntPtr hwnd,
            int message,
            IntPtr wParam,
            IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WindowClass
        {
            public uint Style;
            public IntPtr WindowProcedure;
            public int ClassExtra;
            public int WindowExtra;
            public IntPtr InstanceHandle;
            public IntPtr IconHandle;
            public IntPtr CursorHandle;
            public IntPtr BackgroundBrush;
            public string? MenuName;
            public string ClassName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public int Size;
            public IntPtr WindowHandle;
            public int Id;
            public int Flags;
            public int CallbackMessage;
            public IntPtr IconHandle;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Tip;

            public int State;
            public int StateMask;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Info;

            public int VersionOrTimeout;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string InfoTitle;

            public int InfoFlags;
            public Guid ItemGuid;
            public IntPtr BalloonIconHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClass(ref WindowClass windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int extendedStyle,
            string className,
            string windowName,
            int style,
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

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(
            IntPtr hwnd,
            int message,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ShellNotifyIcon(
            int message,
            ref NotifyIconData data);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadIcon(
            IntPtr instance,
            IntPtr iconName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImage(
            IntPtr instance,
            IntPtr name,
            uint type,
            int width,
            int height,
            uint load);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? moduleName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out Win32Point point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool AppendMenu(
            IntPtr menu,
            uint flags,
            int itemId,
            string itemText);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int TrackPopupMenu(
            IntPtr menu,
            uint flags,
            int x,
            int y,
            int reserved,
            IntPtr hwnd,
            IntPtr rectangle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hwnd);
    }
}
