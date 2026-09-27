using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NetSpeedWidget.Models;
using NetSpeedWidget.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Timers;
using System.Threading;
using Timer = System.Timers.Timer;
using Windows.Graphics;

namespace NetSpeedWidget
{
    public sealed partial class MainWindow : Window
    {
        private const string UploadPrefix = "\u2191";
        private const string DownloadPrefix = "\u2193";
        private const int WidgetWidth = 168;
        private const int WidgetHeight = 32;
        private const int FloatingRightMargin = 20;
        private const int FloatingBottomMargin = 30;
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const int WsCaption = 0x00C00000;
        private const int WsThickFrame = 0x00040000;
        private const int WsMinimizeBox = 0x00020000;
        private const int WsMaximizeBox = 0x00010000;
        private const int WsSysMenu = 0x00080000;
        private const int WsExDlgModalFrame = 0x00000001;
        private const int WsExWindowEdge = 0x00000100;
        private const int WsExClientEdge = 0x00000200;
        private const int WsExStaticEdge = 0x00020000;
        private const int DwmwaNcRenderingPolicy = 2;
        private const int DwmwaBorderColor = 34;
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmNcRenderingDisabled = 2;
        private const int DwmWindowCornerPreferenceDoNotRound = 1;
        private const int GwlpWndProc = -4;
        private const int WmDisplayChange = 0x007E;
        private const string TaskbarCreatedMessageName = "TaskbarCreated";
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;
        private const uint SwpShowWindow = 0x0040;
        private const uint DwmColorNone = 0xFFFFFFFE;
        private const int SwHide = 0;
        private const int SwShowNoActivate = 4;
        private const int TaskbarPlacementRefreshIntervalMilliseconds = 1000;
        private const int DisplayChangeForcedRefreshTicks = 100;

        private readonly NetworkMonitorService _netService;
        private readonly SettingsService _settingsService;
        private readonly SystemStatusHttpServerService _systemStatusHttpServerService;
        private readonly TrayIconService? _trayIconService;
        private readonly List<NativeTextOverlayWindow> _taskbarOverlayWindows = [];
        private readonly Timer _timer;
        private readonly Timer _overlayKeepAliveTimer = new(100);
        private readonly TaskbarOverlayRegistry<NativeTextOverlayWindow> _overlayRegistry =
            new(handle => new NativeTextOverlayWindow(WidgetWidth, WidgetHeight, handle));
        private readonly HardwareSamplingService _hardwareSamplingService = new();
        private int _speedRefreshPending;
        private int _overlayRefreshPending;
        private bool _isExiting;
        private readonly bool _isUpdateSmokeTest = Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-update") >= 0;
        private readonly bool _isDpiSmokeTest = Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-dpi") >= 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-update") >= 0;
        private readonly bool _isSmokeTest = Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-ui") >= 0 ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-dpi") >= 0 || Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-update") >= 0;
        private XamlRoot? _sizingRoot;
        private double _windowScale;
        private Microsoft.UI.Xaml.DispatcherTimer? _smokeTimer;
        private DateTime _lastOverlayRecoveryUtc;
        private readonly AppWindow _appWindow;
        private readonly IntPtr _hwnd;
        private readonly WindowId _windowId;
        private readonly WindowProcedureDelegate _windowProcedure;
        private readonly uint _taskbarCreatedMessage;
        private AppSettings _settings;
        private SettingsWindow? _settingsWindow;
        private IntPtr _originalWindowProcedure;
        private bool _isDragging;
        private bool _isTaskbarDisplayMode;
        private bool _taskbarOverlayHiddenByForeground;
        private string _lastUploadText = $"{UploadPrefix} 0 B/s";
        private string _lastDownloadText = $"{DownloadPrefix} 0 B/s";
        private PointInt32 _taskbarOverlayPosition;
        private IReadOnlyList<TaskbarOverlayPlacement> _taskbarOverlayPlacements = [];
        private DateTime _lastTaskbarPlacementRefreshUtc = DateTime.MinValue;
        private int _remainingDisplayChangeForcedRefreshTicks;
        private WidgetDisplaySettingsSnapshot _appliedWidgetDisplaySettings;
        private PointInt32 _dragStartWindowPosition;
        private Win32Point _dragStartCursorPosition;

        public MainWindow()
        {
            InitializeComponent();

            _hwnd =
                WinRT.Interop.WindowNative.GetWindowHandle(this);

            _windowId =
                Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);

            _appWindow = AppWindow.GetFromWindowId(_windowId);
            _windowProcedure = MainWindow_WindowProcedure;
            _taskbarCreatedMessage = RegisterWindowMessage(TaskbarCreatedMessageName);
            if (_taskbarCreatedMessage == 0)
            {
                AppLogService.Write(
                    "Failed to register TaskbarCreated message.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            AttachNativeWindowProcedure();

            ConfigureWidgetWindow();
            UpdateWidgetScale(WindowDpiService.GetScale(_hwnd));
            RootBackground.Loaded += (_, _) =>
            {
                _sizingRoot = RootBackground.XamlRoot;
                if (_sizingRoot is null) return;
                _sizingRoot.Changed += WidgetXamlRoot_Changed;
                UpdateWidgetScale(_sizingRoot.RasterizationScale);
            };

            _settingsService = new SettingsService();
            _settings = _settingsService.Load();
            if (_isSmokeTest) _settings = new AppSettings { StartupEnabled = false,
                DisplayMode = _isDpiSmokeTest ? WidgetDisplayMode.Floating : WidgetDisplayMode.TaskbarLeft };
            _systemStatusHttpServerService = new SystemStatusHttpServerService(
                new SystemStatusCollectorService(_hardwareSamplingService.GetStatus), _settingsService);
            _overlayKeepAliveTimer.Elapsed += OverlayKeepAliveTimer_Elapsed;
            AppLogService.Write("NetSpeedWidget started.");
            if (!_isSmokeTest) ApplyStartupSetting();
            ApplySystemStatusHttpServer();
            ApplySpeedFontSize(_settings.SpeedFontSize);
            ApplyTheme(_settings.Theme);
            ApplyDisplayMode(_settings.DisplayMode);
            ApplyTopmost(_settings.TopmostEnabled);
            _appliedWidgetDisplaySettings = WidgetDisplaySettingsSnapshot.From(_settings);
            _trayIconService = CreateTrayIconService();

            _netService = new NetworkMonitorService();

            _timer = new Timer(1000);
            _timer.Elapsed += Timer_Elapsed;
            _timer.Start();

            RootGrid.PointerPressed += RootGrid_PointerPressed;
            RootGrid.PointerMoved += RootGrid_PointerMoved;
            RootGrid.PointerReleased += RootGrid_PointerReleased;
            RootGrid.PointerCanceled += RootGrid_PointerCanceled;

            Activated += MainWindow_Activated;
            if (!_isSmokeTest) _ = CheckStartupUpdateAsync();
            if (_isSmokeTest)
            {
                if (_isDpiSmokeTest) OpenSettingsWindow();
                if (_isUpdateSmokeTest) _settingsWindow?.ShowUpdateVerificationPage();
                _smokeTimer = new Microsoft.UI.Xaml.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                _smokeTimer.Tick += (_, _) => CompleteUiSmokeTest();
                _smokeTimer.Start();
            }
        }

        /// <summary>验证当前任务栏的真实挂载、绘制和可见性，不注册自启、不请求 UAC。</summary>
        private void CompleteUiSmokeTest()
        {
            _smokeTimer?.Stop();
            if (_isDpiSmokeTest)
            {
                var sizing = new
                {
                    scale = RootBackground.XamlRoot?.RasterizationScale ?? _windowScale,
                    clientWidth = _appWindow.ClientSize.Width,
                    clientHeight = _appWindow.ClientSize.Height,
                    logicalWidth = RootBackground.ActualWidth,
                    logicalHeight = RootBackground.ActualHeight,
                    contentWidth = ContentPanel.ActualWidth,
                    settings = _settingsWindow?.GetSizingVerification(),
                    updates = _settingsWindow?.GetUpdateSizingVerification()
                };
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppPaths.ExecutableDirectory, "dpi-smoke-verification.json"),
                    System.Text.Json.JsonSerializer.Serialize(sizing), new System.Text.UTF8Encoding(false));
                ExitApplication();
                return;
            }
            var healthy = 0;
            for (var index = 0; index < _taskbarOverlayWindows.Count; index++)
                if (_taskbarOverlayWindows[index].IsAttachedToTaskbar(_taskbarOverlayPlacements[index].TaskbarWindow)) healthy++;
            var result = new
            {
                taskbars = NativeTextOverlayWindow.GetTaskbarWindows().Count,
                overlays = _taskbarOverlayWindows.Count,
                healthy,
                hiddenByFullscreen = _taskbarOverlayHiddenByForeground,
                placements = FormatTaskbarOverlayPlacements(_taskbarOverlayPlacements)
            };
            System.IO.File.WriteAllText(System.IO.Path.Combine(AppPaths.ExecutableDirectory, "ui-smoke-verification.json"),
                System.Text.Json.JsonSerializer.Serialize(result), new System.Text.UTF8Encoding(false));
            ExitApplication();
        }

        public void ActivateWidget()
        {
            Activate();

            if (_isTaskbarDisplayMode)
            {
                ShowWindow(_hwnd, SwHide);
                ShowTaskbarOverlay();
            }
        }

        private async void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (_isExiting || Interlocked.Exchange(ref _speedRefreshPending, 1) != 0) return;
            try
            {
                var speeds =
                    await _netService.GetNetworkSpeedAsync();
                var uploadText = $"{UploadPrefix} {speeds.Upload}";
                var downloadText = $"{DownloadPrefix} {speeds.Download}";

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_isExiting) return;
                    if (_lastUploadText == uploadText &&
                        _lastDownloadText == downloadText)
                    {
                        if (_isTaskbarDisplayMode)
                        {
                            RefreshTaskbarOverlayPlacementIfNeeded();
                            RefreshTaskbarOverlayVisibility();
                        }

                        return;
                    }

                    _lastUploadText = uploadText;
                    _lastDownloadText = downloadText;

                    UploadText.Text = _lastUploadText;
                    DownloadText.Text = _lastDownloadText;
                    ShowTaskbarOverlay();
                });
            }
            catch (Exception ex)
            {
                AppLogService.Write("Network speed refresh failed.", ex);
            }
            finally { Interlocked.Exchange(ref _speedRefreshPending, 0); }
        }

        private void OverlayKeepAliveTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (_isExiting || !_isTaskbarDisplayMode || Interlocked.Exchange(ref _overlayRefreshPending, 1) != 0)
            {
                return;
            }

            if (!DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (!_isExiting && _isTaskbarDisplayMode)
                    {
                        var forcePlacementRefresh =
                            _remainingDisplayChangeForcedRefreshTicks > 0;
                        if (forcePlacementRefresh)
                        {
                            _remainingDisplayChangeForcedRefreshTicks--;
                        }

                        RefreshTaskbarOverlayPlacement(force: forcePlacementRefresh);
                        RecreateInvalidTaskbarOverlays();
                        RefreshTaskbarOverlayVisibility();
                    }
                }
                finally { Interlocked.Exchange(ref _overlayRefreshPending, 0); }
            })) Interlocked.Exchange(ref _overlayRefreshPending, 0);
        }

        private IntPtr MainWindow_WindowProcedure(
            IntPtr hwnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam)
        {
            if (message == WmDisplayChange)
            {
                MainWindow_DisplaySettingsChanged();
            }
            else if (_taskbarCreatedMessage != 0 && message == _taskbarCreatedMessage)
            {
                MainWindow_TaskbarCreated();
            }

            return CallWindowProc(
                _originalWindowProcedure,
                hwnd,
                message,
                wParam,
                lParam);
        }

        private void MainWindow_DisplaySettingsChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!_isTaskbarDisplayMode)
                {
                    return;
                }

                _lastTaskbarPlacementRefreshUtc = DateTime.MinValue;
                _remainingDisplayChangeForcedRefreshTicks = DisplayChangeForcedRefreshTicks;
                AppLogService.Write(
                    "Taskbar overlay display settings changed. " +
                    $"forcedRefreshTicks={_remainingDisplayChangeForcedRefreshTicks}");
                RefreshTaskbarOverlayPlacement(force: true);
                RefreshTaskbarOverlayVisibility();
            });
        }

        private void MainWindow_TaskbarCreated()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _trayIconService?.RestoreAfterExplorerRestart();
                if (!_isTaskbarDisplayMode)
                {
                    return;
                }

                _lastTaskbarPlacementRefreshUtc = DateTime.MinValue;
                _remainingDisplayChangeForcedRefreshTicks = DisplayChangeForcedRefreshTicks;
                AppLogService.Write(
                    "Taskbar overlay taskbar created. " +
                    $"forcedRefreshTicks={_remainingDisplayChangeForcedRefreshTicks}");
                DisposeTaskbarOverlays();
                _taskbarOverlayPlacements = [];
                RefreshTaskbarOverlayPlacement(force: true);
                RefreshTaskbarOverlayVisibility();
            });
        }

        private void RootGrid_PointerPressed(
            object sender,
            PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(RootGrid).Properties;

            if (!properties.IsLeftButtonPressed)
            {
                return;
            }

            if (_settings.LockWindowPosition && !_isTaskbarDisplayMode)
            {
                return;
            }

            _isDragging = true;
            _dragStartWindowPosition = _appWindow.Position;
            GetCursorPos(out _dragStartCursorPosition);

            RootGrid.CapturePointer(e.Pointer);
        }

        private void RootGrid_PointerMoved(
            object sender,
            PointerRoutedEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            var properties = e.GetCurrentPoint(RootGrid).Properties;

            if (!properties.IsLeftButtonPressed)
            {
                StopDragging(e);
                return;
            }

            GetCursorPos(out Win32Point currentCursorPosition);

            var offsetX = currentCursorPosition.X - _dragStartCursorPosition.X;
            var offsetY = currentCursorPosition.Y - _dragStartCursorPosition.Y;

            _appWindow.Move(
                new PointInt32(
                    _dragStartWindowPosition.X + offsetX,
                    _dragStartWindowPosition.Y + offsetY));
        }

        private void RootGrid_PointerReleased(
            object sender,
            PointerRoutedEventArgs e)
        {
            StopDragging(e);
        }

        private void RootGrid_PointerCanceled(
            object sender,
            PointerRoutedEventArgs e)
        {
            StopDragging(e);
        }

        private void StopDragging(PointerRoutedEventArgs e)
        {
            _isDragging = false;
            RootGrid.ReleasePointerCapture(e.Pointer);
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_isTaskbarDisplayMode)
            {
                ShowWindow(_hwnd, SwHide);
                RefreshTaskbarOverlayVisibility();
                return;
            }

            if (_settings.TopmostEnabled)
            {
                ApplyTopmost(true);
            }
        }

        private void ConfigureWidgetWindow()
        {
            ExtendsContentIntoTitleBar = false;

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.IsResizable = false;
            }

            RemoveNativeWindowFrame();
        }

        public void OpenSettingsWindow()
        {
            try
            {
                if (_settingsWindow is not null)
                {
                    _settingsWindow.Activate();
                    return;
                }

                _settingsWindow =
                    new SettingsWindow(
                        _settingsService,
                        _settings,
                        ApplySettingsFromWindow,
                        _systemStatusHttpServerService.GetState,
                        _hardwareSamplingService,
                        ExitApplication);

                _settingsWindow.Closed += (_, _) =>
                {
                    _settingsWindow = null;
                };

                _settingsWindow.Activate();
            }
            catch (Exception ex)
            {
                _settingsWindow = null;
                AppLogService.Write("Failed to open settings window.", ex);
            }
        }

        private void ExitApplication()
        {
            if (_isExiting) return;
            _isExiting = true;
            if (_sizingRoot is not null) _sizingRoot.Changed -= WidgetXamlRoot_Changed;
            _smokeTimer?.Stop();
            _hardwareSamplingService.Dispose();
            RestoreNativeWindowProcedure();
            _timer.Stop();
            _timer.Dispose();
            _overlayKeepAliveTimer.Stop();
            _overlayKeepAliveTimer.Dispose();
            DisposeTaskbarOverlays();
            _systemStatusHttpServerService.Dispose();
            _trayIconService?.Dispose();
            _settingsWindow?.Close();
            Close();
        }

        private void ApplySettingsFromWindow(AppSettings settings)
        {
            _settings = settings;
            ApplySystemStatusHttpServer();

            // SettingsWindow mutates the shared AppSettings instance, so compare with an independent snapshot.
            if (ShouldApplyWidgetDisplaySettings(_settings))
            {
                ApplyWidgetDisplaySettings(_appliedWidgetDisplaySettings, _settings);
                _appliedWidgetDisplaySettings = WidgetDisplaySettingsSnapshot.From(_settings);
            }
        }

        private bool ShouldApplyWidgetDisplaySettings(AppSettings currentSettings)
        {
            return _appliedWidgetDisplaySettings != WidgetDisplaySettingsSnapshot.From(currentSettings);
        }

        private void ApplyWidgetDisplaySettings(
            WidgetDisplaySettingsSnapshot previousSettings,
            AppSettings currentSettings)
        {
            ApplySpeedFontSize(currentSettings.SpeedFontSize);
            ApplyTheme(currentSettings.Theme);

            if (previousSettings.DisplayMode != currentSettings.DisplayMode)
            {
                if (IsTaskbarSideSwitch(previousSettings, currentSettings))
                {
                    MoveTaskbarOverlayForCurrentSettings();
                }
                else
                {
                    ApplyDisplayMode(currentSettings.DisplayMode);
                }
            }
            else if (HasTaskbarPlacementSettingChanged(previousSettings, currentSettings))
            {
                MoveTaskbarOverlayForCurrentSettings();
            }
            else
            {
                ShowTaskbarOverlay();
            }

            ApplyTopmost(currentSettings.TopmostEnabled);
            ShowTaskbarOverlay();
        }

        private static bool IsTaskbarSideSwitch(
            WidgetDisplaySettingsSnapshot previousSettings,
            AppSettings currentSettings)
        {
            return WidgetPlacementService.IsTaskbarMode(previousSettings.DisplayMode) &&
                   WidgetPlacementService.IsTaskbarMode(currentSettings.DisplayMode);
        }

        private static bool HasTaskbarPlacementSettingChanged(
            WidgetDisplaySettingsSnapshot previousSettings,
            AppSettings currentSettings)
        {
            return WidgetPlacementService.IsTaskbarMode(currentSettings.DisplayMode) &&
                   previousSettings.TaskbarEdgePadding != currentSettings.TaskbarEdgePadding;
        }

        private void ApplySystemStatusHttpServer()
        {
            try
            {
                if (_settings.SystemStatus.Enabled) _hardwareSamplingService.Start();
                else _hardwareSamplingService.Stop();
                _systemStatusHttpServerService.ApplySettings(_settings);
            }
            catch (Exception ex)
            {
                AppLogService.Write("Failed to apply system status HTTP service.", ex);
            }
        }

        private void ApplyStartupSetting()
        {
            try
            {
                if (_settings.StartupEnabled)
                {
                    StartupService.EnableStartup();
                }
                else
                {
                    StartupService.DisableStartup();
                }
            }
            catch (Exception ex)
            {
                // Startup registration is optional; registry access failures must not block launch.
                AppLogService.Write("Failed to apply startup registration.", ex);
            }
        }

        private TrayIconService? CreateTrayIconService()
        {
            try
            {
                return new TrayIconService(
                    _hwnd,
                    OpenSettingsWindow,
                    ExitApplication);
            }
            catch (Exception ex)
            {
                AppLogService.Write("Failed to create tray icon.", ex);
                return null;
            }
        }

        private void ApplyTheme(AppThemeMode theme)
        {
            var palette = SettingsService.GetThemePalette(theme);
            var isTaskbarMode =
                WidgetPlacementService.IsTaskbarMode(_settings.DisplayMode);
            var backgroundBrush =
                isTaskbarMode
                    ? new SolidColorBrush(Colors.Black)
                    : new SolidColorBrush(ToColor(palette.BackgroundColor));
            var foregroundBrush = new SolidColorBrush(ToColor(palette.ForegroundColor));

            RootBackground.Background = backgroundBrush;
            RootGrid.Background = backgroundBrush;
            UploadText.Foreground = foregroundBrush;
            DownloadText.Foreground = foregroundBrush;
        }

        private void ApplyDisplayMode(WidgetDisplayMode displayMode)
        {
            var isTaskbarMode = WidgetPlacementService.IsTaskbarMode(displayMode);
            AppLogService.Write(
                "Taskbar overlay display mode changed. " +
                $"mode={displayMode}; isTaskbarMode={isTaskbarMode}");

            if (isTaskbarMode)
            {
                var placements = CalculateTaskbarOverlayPlacements(displayMode);

                _isTaskbarDisplayMode = true;
                ApplyTaskbarOverlayPlacements(placements);
                AppLogService.Write(
                    "Taskbar overlay placement applied for display mode. " +
                    $"count={placements.Count}; placements={FormatTaskbarOverlayPlacements(placements)}");
                ShowWindow(_hwnd, SwHide);
                ShowTaskbarOverlay();
                _overlayKeepAliveTimer.Start();
                return;
            }

            _isTaskbarDisplayMode = false;
            _overlayKeepAliveTimer.Stop();
            DisposeTaskbarOverlays();
            _taskbarOverlayPlacements = [];
            _taskbarOverlayHiddenByForeground = false;
            _lastTaskbarPlacementRefreshUtc = DateTime.MinValue;
            ShowWindow(_hwnd, SwShowNoActivate);

            var displayArea =
                DisplayArea.GetFromWindowId(
                    _windowId,
                    DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            _appWindow.Move(
                new PointInt32(
                    workArea.X + workArea.Width - _appWindow.Size.Width - WindowDpiService.ToPixels(FloatingRightMargin, _windowScale),
                    workArea.Y + workArea.Height - _appWindow.Size.Height - WindowDpiService.ToPixels(FloatingBottomMargin, _windowScale)));
        }

        private void ShowTaskbarOverlay()
        {
            if (!_isTaskbarDisplayMode)
            {
                return;
            }

            var overlayAction = GetTaskbarOverlayAction();

            if (overlayAction == TaskbarOverlayAction.Hide)
            {
                _taskbarOverlayHiddenByForeground = true;
                HideTaskbarOverlays();
                return;
            }

            _taskbarOverlayHiddenByForeground = false;
            EnsureTaskbarOverlayWindowCount(_taskbarOverlayPlacements.Count);

            for (var index = 0; index < _taskbarOverlayPlacements.Count; index++)
            {
                var placement = _taskbarOverlayPlacements[index];
                _taskbarOverlayWindows[index].Show(
                    placement.Point.X,
                    placement.Point.Y,
                    _lastUploadText,
                    _lastDownloadText,
                    _settings.Theme,
                    _settings.SpeedFontSize,
                    topmost: true,
                    placement.TaskbarWindow);
            }

            if (overlayAction == TaskbarOverlayAction.ForceShowTopmost)
            {
                ForceTaskbarOverlaysTopmost();
            }
        }

        private void RefreshTaskbarOverlayVisibility()
        {
            if (!_isTaskbarDisplayMode)
            {
                return;
            }

            switch (GetTaskbarOverlayAction())
            {
                case TaskbarOverlayAction.Hide:
                    _taskbarOverlayHiddenByForeground = true;
                    HideTaskbarOverlays();
                    break;
                case TaskbarOverlayAction.ForceShowTopmost:
                    if (_taskbarOverlayHiddenByForeground)
                    {
                        ShowTaskbarOverlay();
                    }
                    else
                    {
                        ForceTaskbarOverlaysTopmost();
                    }

                    _taskbarOverlayHiddenByForeground = false;
                    break;
                default:
                    if (_taskbarOverlayHiddenByForeground)
                    {
                        ShowTaskbarOverlay();
                    }
                    else
                    {
                        SetTaskbarOverlaysTopmost(true);
                    }

                    _taskbarOverlayHiddenByForeground = false;
                    break;
            }
        }

        private IReadOnlyList<TaskbarOverlayPlacement> CalculateTaskbarOverlayPlacements(
            WidgetDisplayMode displayMode)
        {
            var taskbarWindows = NativeTextOverlayWindow.GetTaskbarWindows();
            var placements = new List<TaskbarOverlayPlacement>();

            foreach (var taskbarWindow in taskbarWindows)
            {
                var point =
                    WidgetPlacementService.CalculateTaskbarPositionFromBounds(
                        displayMode,
                        taskbarWindow.AvailableBounds,
                        NativeTextOverlayWindow.ScaleForTaskbar(WidgetWidth, taskbarWindow.WindowHandle),
                        NativeTextOverlayWindow.ScaleForTaskbar(WidgetHeight, taskbarWindow.WindowHandle),
                        NativeTextOverlayWindow.ScaleForTaskbar(_settings.TaskbarEdgePadding, taskbarWindow.WindowHandle));

                placements.Add(
                    new TaskbarOverlayPlacement(
                        new PointInt32(point.X, point.Y),
                        taskbarWindow.AvailableBounds,
                        taskbarWindow.WindowHandle,
                        NativeTextOverlayWindow.GetTaskbarDpi(taskbarWindow.WindowHandle)));
            }

            if (placements.Count > 0)
            {
                return placements;
            }

            if (TryCalculateCachedTaskbarOverlayPlacements(displayMode, out var cachedPlacements))
            {
                AppLogService.Write(
                    "Taskbar overlay taskbar lookup returned no windows; using cached taskbar placement. " +
                    $"placements={FormatTaskbarOverlayPlacements(cachedPlacements)}");
                return cachedPlacements;
            }

            var displayArea =
                DisplayArea.GetFromWindowId(
                    _windowId,
                    DisplayAreaFallback.Primary);
            var displayBounds = ToRectBounds(displayArea.OuterBounds);
            var workAreaBounds = ToRectBounds(displayArea.WorkArea);
            var fallbackPoint =
                WidgetPlacementService.CalculateTaskbarPosition(
                    displayMode,
                    displayBounds,
                    workAreaBounds,
                    WidgetWidth,
                    WidgetHeight,
                    _settings.TaskbarEdgePadding);

            placements.Add(
                new TaskbarOverlayPlacement(
                    new PointInt32(fallbackPoint.X, fallbackPoint.Y),
                    WidgetPlacementService.GetTaskbarBounds(displayBounds, workAreaBounds),
                    IntPtr.Zero));

            return placements;
        }

        private bool TryCalculateCachedTaskbarOverlayPlacements(
            WidgetDisplayMode displayMode,
            out IReadOnlyList<TaskbarOverlayPlacement> placements)
        {
            var cachedPlacements = new List<TaskbarOverlayPlacement>();

            foreach (var placement in _taskbarOverlayPlacements)
            {
                if (placement.TaskbarWindow == IntPtr.Zero ||
                    !NativeTextOverlayWindow.TryGetAvailableTaskbarBounds(
                        placement.TaskbarWindow,
                        out var taskbarBounds))
                {
                    placements = [];
                    return false;
                }

                var point =
                    WidgetPlacementService.CalculateTaskbarPositionFromBounds(
                        displayMode,
                        taskbarBounds,
                        NativeTextOverlayWindow.ScaleForTaskbar(WidgetWidth, placement.TaskbarWindow),
                        NativeTextOverlayWindow.ScaleForTaskbar(WidgetHeight, placement.TaskbarWindow),
                        NativeTextOverlayWindow.ScaleForTaskbar(_settings.TaskbarEdgePadding, placement.TaskbarWindow));

                cachedPlacements.Add(
                    new TaskbarOverlayPlacement(
                        new PointInt32(point.X, point.Y),
                        taskbarBounds,
                        placement.TaskbarWindow,
                        NativeTextOverlayWindow.GetTaskbarDpi(placement.TaskbarWindow)));
            }

            placements = cachedPlacements;
            return cachedPlacements.Count > 0;
        }

        private void RefreshTaskbarOverlayPlacementIfNeeded()
        {
            RefreshTaskbarOverlayPlacement(force: false);
        }

        private void RefreshTaskbarOverlayPlacement(bool force)
        {
            if (!_isTaskbarDisplayMode)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (!force &&
                (now - _lastTaskbarPlacementRefreshUtc).TotalMilliseconds <
                TaskbarPlacementRefreshIntervalMilliseconds)
            {
                return;
            }

            _lastTaskbarPlacementRefreshUtc = now;
            var placements = CalculateTaskbarOverlayPlacements(_settings.DisplayMode);
            if (force)
            {
                AppLogService.Write(
                    "Taskbar overlay placement recalculated. " +
                    $"force={force}; placements={FormatTaskbarOverlayPlacements(placements)}");
            }

            if (IsCurrentTaskbarOverlayPlacement(placements))
            {
                return;
            }

            var topologyChanged = HasTaskbarSetChanged(placements);
            ApplyTaskbarOverlayPlacements(placements);
            AppLogService.Write(
                "Taskbar overlay placement applied. " +
                $"topologyChanged={topologyChanged}; count={placements.Count}; " +
                $"placements={FormatTaskbarOverlayPlacements(placements)}");
            ShowWindow(_hwnd, SwHide);
            ShowTaskbarOverlay();
        }

        private void MoveTaskbarOverlayForCurrentSettings()
        {
            if (!_isTaskbarDisplayMode)
            {
                return;
            }

            var placements = CalculateTaskbarOverlayPlacements(_settings.DisplayMode);
            ApplyTaskbarOverlayPlacements(placements);

            AppLogService.Write(
                "Taskbar overlay placement moved for settings. " +
                $"count={placements.Count}; placements={FormatTaskbarOverlayPlacements(placements)}");
            ShowWindow(_hwnd, SwHide);
            ShowTaskbarOverlay();
        }

        private bool IsCurrentTaskbarOverlayPlacement(
            IReadOnlyList<TaskbarOverlayPlacement> placements)
        {
            if (placements.Count != _taskbarOverlayPlacements.Count)
            {
                return false;
            }

            for (var index = 0; index < placements.Count; index++)
            {
                if (!placements[index].Equals(_taskbarOverlayPlacements[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>比较句柄集合，不把屏幕排序或托盘宽度变化当成任务栏重建。</summary>
        private bool HasTaskbarSetChanged(IReadOnlyList<TaskbarOverlayPlacement> placements)
        {
            var previous = new HashSet<IntPtr>();
            var current = new HashSet<IntPtr>();
            foreach (var placement in _taskbarOverlayPlacements) previous.Add(placement.TaskbarWindow);
            foreach (var placement in placements) current.Add(placement.TaskbarWindow);
            return !previous.SetEquals(current);
        }

        private void ApplyTaskbarOverlayPlacements(
            IReadOnlyList<TaskbarOverlayPlacement> placements)
        {
            // 1. 以 HWND 对应窗口，新增屏幕不会重挂已有屏幕。
            var handles = new List<IntPtr>(placements.Count);
            foreach (var placement in placements) handles.Add(placement.TaskbarWindow);
            var windows = _overlayRegistry.Synchronize(handles);
            // 2. 同一任务栏 DPI 改变时，仅重建该窗口。
            for (var index = 0; index < placements.Count; index++)
            {
                foreach (var previous in _taskbarOverlayPlacements)
                    if (previous.TaskbarWindow == placements[index].TaskbarWindow &&
                        previous.Dpi != placements[index].Dpi)
                    {
                        _overlayRegistry.Recreate(placements[index].TaskbarWindow);
                        break;
                    }
            }
            windows = _overlayRegistry.Synchronize(handles);
            _taskbarOverlayWindows.Clear();
            _taskbarOverlayWindows.AddRange(windows);
            _taskbarOverlayPlacements = placements;
            _taskbarOverlayPosition = placements.Count > 0 ? placements[0].Point : _taskbarOverlayPosition;
            _lastTaskbarPlacementRefreshUtc = DateTime.UtcNow;
            _appWindow.Move(_taskbarOverlayPosition);
        }

        private void EnsureTaskbarOverlayWindowCount(int count)
        {
            while (_taskbarOverlayWindows.Count < count)
            {
                _taskbarOverlayWindows.Add(new NativeTextOverlayWindow(WidgetWidth, WidgetHeight));
            }

            while (_taskbarOverlayWindows.Count > count)
            {
                var lastIndex = _taskbarOverlayWindows.Count - 1;
                _taskbarOverlayWindows[lastIndex].Dispose();
                _taskbarOverlayWindows.RemoveAt(lastIndex);
            }
        }

        private void HideTaskbarOverlays()
        {
            foreach (var overlayWindow in _taskbarOverlayWindows)
            {
                overlayWindow.Hide();
            }
        }

        private void RecreateInvalidTaskbarOverlays()
        {
            if (_taskbarOverlayHiddenByForeground ||
                DateTime.UtcNow - _lastOverlayRecoveryUtc < TimeSpan.FromSeconds(1))
            {
                return;
            }

            var recreated = false;
            for (var index = 0; index < _taskbarOverlayWindows.Count; index++)
            {
                if (index >= _taskbarOverlayPlacements.Count)
                {
                    continue;
                }

                var expectedTaskbarWindow =
                    _taskbarOverlayPlacements[index].TaskbarWindow;
                if (expectedTaskbarWindow == IntPtr.Zero)
                {
                    continue;
                }

                if (_taskbarOverlayWindows[index].IsAttachedToTaskbar(expectedTaskbarWindow))
                {
                    continue;
                }

                _lastOverlayRecoveryUtc = DateTime.UtcNow;
                _taskbarOverlayWindows[index] = _overlayRegistry.Recreate(expectedTaskbarWindow);
                recreated = true;

                AppLogService.Write(
                    "Taskbar overlay window recreated. " +
                    $"index={index}; expectedTaskbar=0x{expectedTaskbarWindow.ToInt64():X}");
            }

            if (recreated)
            {
                _lastTaskbarPlacementRefreshUtc = DateTime.MinValue;
                RefreshTaskbarOverlayPlacement(force: true);
                ShowTaskbarOverlay();
            }
        }

        private void ForceTaskbarOverlaysTopmost()
        {
            foreach (var overlayWindow in _taskbarOverlayWindows)
            {
                overlayWindow.ForceTopmost();
            }
        }

        private void SetTaskbarOverlaysTopmost(bool topmost)
        {
            foreach (var overlayWindow in _taskbarOverlayWindows)
            {
                overlayWindow.SetTopmost(topmost);
            }
        }

        private void DisposeTaskbarOverlays()
        {
            _overlayRegistry.Dispose();
            _taskbarOverlayWindows.Clear();
        }

        private TaskbarOverlayAction GetTaskbarOverlayAction()
        {
            var foregroundState =
                WindowForegroundService.GetForegroundWindowState(_hwnd);
            return WindowForegroundService.GetTaskbarOverlayAction(foregroundState);
        }

        private void ApplySpeedFontSize(int speedFontSize)
        {
            UploadText.FontSize = speedFontSize;
            DownloadText.FontSize = speedFontSize;
        }

        private void ApplyTopmost(bool topmostEnabled)
        {
            if (_isTaskbarDisplayMode)
            {
                ShowWindow(_hwnd, SwHide);
                RefreshTaskbarOverlayVisibility();
                return;
            }

            var flags =
                SwpNoMove |
                SwpNoSize |
                SwpNoActivate;

            if (topmostEnabled)
            {
                flags |= SwpShowWindow;
            }

            if (!SetWindowPos(
                _hwnd,
                WindowStyleService.GetTopmostInsertAfter(topmostEnabled),
                0,
                0,
                0,
                0,
                flags))
            {
                AppLogService.Write(
                    "Failed to apply topmost state.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        private void AttachNativeWindowProcedure()
        {
            var newProcedure =
                Marshal.GetFunctionPointerForDelegate(_windowProcedure);
            _originalWindowProcedure =
                SetWindowLongPtr(_hwnd, GwlpWndProc, newProcedure);

            if (_originalWindowProcedure == IntPtr.Zero)
            {
                AppLogService.Write(
                    "Failed to attach native window procedure.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        private void RestoreNativeWindowProcedure()
        {
            if (_originalWindowProcedure == IntPtr.Zero)
            {
                return;
            }

            SetWindowLongPtr(_hwnd, GwlpWndProc, _originalWindowProcedure);
            _originalWindowProcedure = IntPtr.Zero;
        }

        private static RectBounds ToRectBounds(RectInt32 rect)
        {
            return new RectBounds(
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height);
        }

        private static string FormatTaskbarOverlayPlacements(
            IReadOnlyList<TaskbarOverlayPlacement> placements)
        {
            if (placements.Count == 0)
            {
                return "none";
            }

            var details = new List<string>();
            for (var index = 0; index < placements.Count; index++)
            {
                var placement = placements[index];
                details.Add(
                    $"#{index}:hwnd=0x{placement.TaskbarWindow.ToInt64():X};" +
                    $"taskbar={placement.TaskbarBounds.X},{placement.TaskbarBounds.Y}," +
                    $"{placement.TaskbarBounds.Width}x{placement.TaskbarBounds.Height};" +
                    $"point={placement.Point.X},{placement.Point.Y}");
            }

            return string.Join(" | ", details);
        }

        private readonly record struct TaskbarOverlayPlacement(
            PointInt32 Point,
            RectBounds TaskbarBounds,
            IntPtr TaskbarWindow,
            uint Dpi = 96);

        private readonly record struct WidgetDisplaySettingsSnapshot(
            AppThemeMode Theme,
            bool TopmostEnabled,
            WidgetDisplayMode DisplayMode,
            int TaskbarEdgePadding,
            int SpeedFontSize)
        {
            public static WidgetDisplaySettingsSnapshot From(AppSettings settings)
            {
                return new WidgetDisplaySettingsSnapshot(
                    settings.Theme,
                    settings.TopmostEnabled,
                    settings.DisplayMode,
                    settings.TaskbarEdgePadding,
                    settings.SpeedFontSize);
            }
        }

        private static Windows.UI.Color ToColor(string hexColor)
        {
            var color = hexColor.TrimStart('#');

            return Windows.UI.Color.FromArgb(
                255,
                Convert.ToByte(color.Substring(0, 2), 16),
                Convert.ToByte(color.Substring(2, 2), 16),
                Convert.ToByte(color.Substring(4, 2), 16));
        }

        private void RemoveNativeWindowFrame()
        {
            var style = GetWindowLong(_hwnd, GwlStyle);

            style &= ~WsCaption;
            style &= ~WsThickFrame;
            style &= ~WsMinimizeBox;
            style &= ~WsMaximizeBox;
            style &= ~WsSysMenu;

            SetWindowLong(_hwnd, GwlStyle, style);

            var exStyle = GetWindowLong(_hwnd, GwlExStyle);

            exStyle &= ~WsExDlgModalFrame;
            exStyle &= ~WsExWindowEdge;
            exStyle &= ~WsExClientEdge;
            exStyle &= ~WsExStaticEdge;
            exStyle = WindowStyleService.HideFromTaskbar(exStyle);

            SetWindowLong(_hwnd, GwlExStyle, exStyle);
            SetWindowPos(
                _hwnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove |
                SwpNoSize |
                SwpNoZOrder |
                SwpNoActivate |
                SwpFrameChanged);

            var borderColor = DwmColorNone;
            DwmSetWindowAttribute(
                _hwnd,
                DwmwaBorderColor,
                ref borderColor,
                sizeof(uint));

            var ncRenderingPolicy = DwmNcRenderingDisabled;
            DwmSetWindowAttribute(
                _hwnd,
                DwmwaNcRenderingPolicy,
                ref ncRenderingPolicy,
                sizeof(int));

            var cornerPreference = DwmWindowCornerPreferenceDoNotRound;
            DwmSetWindowAttribute(
                _hwnd,
                DwmwaWindowCornerPreference,
                ref cornerPreference,
                sizeof(int));
        }

        /// <summary>将悬浮窗口和原生裁剪区域同步到当前 XAML 缩放比例。</summary>
        private void UpdateWidgetScale(double scale)
        {
            if (_isExiting || Math.Abs(_windowScale - scale) < 0.001) return;
            // 1. 字体由 XAML 自动缩放，原生窗口只转换一次物理尺寸。
            _windowScale = scale;
            _appWindow.ResizeClient(new SizeInt32(
                WindowDpiService.ToPixels(WidgetWidth, scale),
                WindowDpiService.ToPixels(WidgetHeight, scale)));
            // 2. 原生区域也使用实际物理尺寸，避免窗口扩大后仍被旧区域裁切。
            ApplyWindowRegion();
            AppLogService.Write($"Floating window sized. scale={scale}; client={_appWindow.ClientSize.Width}x{_appWindow.ClientSize.Height}");
        }

        private void WidgetXamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args) =>
            UpdateWidgetScale(sender.RasterizationScale);

        private void ApplyWindowRegion()
        {
            var region =
                CreateRoundRectRgn(
                    0,
                    0,
                    _appWindow.Size.Width + 1,
                    _appWindow.Size.Height + 1,
                    WindowDpiService.ToPixels(WidgetHeight, _windowScale),
                    WindowDpiService.ToPixels(WidgetHeight, _windowScale));

            if (region == IntPtr.Zero)
            {
                AppLogService.Write(
                    "Failed to create widget window region.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
                return;
            }

            if (SetWindowRgn(_hwnd, region, true) == 0)
            {
                var error = Marshal.GetLastWin32Error();
                DeleteObject(region);

                AppLogService.Write(
                    "Failed to apply widget window region.",
                    new Win32Exception(error));
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(
            IntPtr hWnd,
            int nIndex,
            IntPtr dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallWindowProc(
            IntPtr lpPrevWndFunc,
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowRgn(
            IntPtr hWnd,
            IntPtr hRgn,
            bool bRedraw);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out Win32Point point);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref uint pvAttribute,
            int cbAttribute);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Point
        {
            public int X;
            public int Y;
        }

        private delegate IntPtr WindowProcedureDelegate(
            IntPtr hwnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam);
    }
}
