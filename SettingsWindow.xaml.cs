using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using NetSpeedWidget.Models;
using NetSpeedWidget.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.System;
using Windows.Graphics;

namespace NetSpeedWidget
{
    public sealed partial class SettingsWindow : Window
    {
        private readonly HardwareSamplingService? _hardwareSamplingService;
        private readonly Microsoft.UI.Xaml.DispatcherTimer _hardwareStateTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly SettingsService _settingsService;
        private readonly Action<AppSettings> _settingsChanged;
        private readonly Func<SystemStatusHttpServerState> _getSystemStatusHttpServerState;
        private const int MinTaskbarEdgePadding = 0;
        private const int MaxTaskbarEdgePadding = 200;
        private const int MinSpeedFontSize = 10;
        private const int MaxSpeedFontSize = 18;
        private const int MinSystemStatusPort = 1;
        private const int MaxSystemStatusPort = 65535;
        private AppWindow? _appWindow;
        private XamlRoot? _sizingRoot;
        private double _windowScale;
        private const int SettingsWidth = 640;
        private const int SettingsHeight = 590;
        private bool _isLoading;
        private AppSettings _settings;
        private AppSettings _lastSavedSettings;
        private readonly long _creationTick = Stopwatch.GetTimestamp();
        private readonly List<string> _openingStages = [];
        private int _serverStateRefreshGeneration;
        private bool _isClosed;
        private bool _firstFrameLogged;
        private bool _firstFrameSubscribed;
        private SettingsPage _currentSettingsPage = SettingsPage.NetSpeed;

        private enum SettingsPage
        {
            NetSpeed,
            SystemStatus,
            Update
        }

        public SettingsWindow(
            SettingsService settingsService,
            AppSettings settings,
            Action<AppSettings> settingsChanged,
            Func<SystemStatusHttpServerState>? getSystemStatusHttpServerState = null,
            HardwareSamplingService? hardwareSamplingService = null,
            Action? exitForUpdate = null)
        {
            InitializeComponent();
            LogFirstOpenStage("xaml-initialized");
            InitializeUpdates(exitForUpdate);
            _hardwareSamplingService = hardwareSamplingService;
            _hardwareStateTimer.Tick += (_, _) => HardwareStateText.Text = _hardwareSamplingService?.State ?? "采集程序未连接";
            _hardwareStateTimer.Start();
            Closed += (_, _) => _hardwareStateTimer.Stop();

            _settingsService = settingsService;
            _settings = SettingsService.Clone(settings);
            _lastSavedSettings = SettingsService.Clone(settings);
            _settingsChanged = settingsChanged;
            _getSystemStatusHttpServerState =
                getSystemStatusHttpServerState ?? (() => SystemStatusHttpServerState.Stopped());

            ConfigureWindow();
            LogFirstOpenStage("window-configured");
            SettingsRoot.Loaded += (_, _) =>
            {
                LogFirstOpenStage("root-loaded");
                if (!_firstFrameLogged && !_firstFrameSubscribed)
                {
                    CompositionTarget.Rendering += SettingsFirstFrame_Rendering;
                    _firstFrameSubscribed = true;
                }
                _sizingRoot = SettingsRoot.XamlRoot;
                if (_sizingRoot is null) return;
                _sizingRoot.Changed += SettingsXamlRoot_Changed;
                UpdateWindowScale(_sizingRoot.RasterizationScale);
            };
            Closed += (_, _) =>
            {
                _isClosed = true;
                _serverStateRefreshGeneration++;
                CompositionTarget.Rendering -= SettingsFirstFrame_Rendering;
                _firstFrameSubscribed = false;
                if (_sizingRoot is not null) _sizingRoot.Changed -= SettingsXamlRoot_Changed;
            };
            LoadSettings();
            LogFirstOpenStage("settings-loaded");
            ApplyTheme(_settings.Theme);
            RefreshSystemStatusServerState();

            DarkThemeRadio.Checked += ThemeRadio_Checked;
            LightThemeRadio.Checked += ThemeRadio_Checked;
            StartupToggle.Toggled += StartupToggle_Toggled;
            TopmostToggle.Toggled += TopmostToggle_Toggled;
            LockWindowToggle.Toggled += LockWindowToggle_Toggled;
            DisplayModeCombo.SelectionChanged += DisplayModeCombo_SelectionChanged;
            SystemStatusEnabledToggle.Toggled += SystemStatusEnabledToggle_Toggled;
            SystemStatusServerToggle.Toggled += SystemStatusServerToggle_Toggled;
            SystemStatusPortTextBox.LostFocus += SystemStatusPortTextBox_LostFocus;
            SystemStatusPortTextBox.KeyDown += SystemStatusPortTextBox_KeyDown;
            SystemStatusRefreshIntervalCombo.SelectionChanged += SystemStatusRefreshIntervalCombo_SelectionChanged;
            SystemStatusPasswordToggle.Toggled += SystemStatusPasswordToggle_Toggled;
            SystemStatusPasswordBox.LostFocus += SystemStatusPasswordBox_LostFocus;
            SystemStatusPasswordBox.KeyDown += SystemStatusPasswordBox_KeyDown;
            SystemStatusWebThemeCombo.SelectionChanged += SystemStatusWebThemeCombo_SelectionChanged;
            NetworkStatusCardToggle.Toggled += SystemStatusCardToggle_Toggled;
            CpuUsageCardToggle.Toggled += SystemStatusCardToggle_Toggled;
            CpuTemperatureCardToggle.Toggled += SystemStatusCardToggle_Toggled;
            GpuUsageCardToggle.Toggled += SystemStatusCardToggle_Toggled;
            GpuTemperatureCardToggle.Toggled += SystemStatusCardToggle_Toggled;
            MemoryUsageCardToggle.Toggled += SystemStatusCardToggle_Toggled;

            ShowSettingsPage(SettingsPage.NetSpeed);
            LogFirstOpenStage("constructor-complete");
        }

        private void LogFirstOpenStage(string stage)
        {
            // 仅在内存中计时；首帧事件后由后台线程写入一条日志，避免磁盘 I/O 拖慢界面。
            _openingStages.Add($"{stage}={Stopwatch.GetElapsedTime(_creationTick).TotalMilliseconds:F0}ms");
            if (stage == "first-render")
            {
                var timeline = string.Join("; ", _openingStages);
                _ = Task.Run(() => AppLogService.Write("Settings open timeline: " + timeline));
            }
        }

        /// <summary>记录首次 XAML 布局进入渲染的时间，用于定位偶发的设置窗口黑屏。</summary>
        private void SettingsFirstFrame_Rendering(object? sender, object args)
        {
            if (_firstFrameLogged || SettingsRoot.ActualWidth <= 0 || SettingsRoot.ActualHeight <= 0) return;
            _firstFrameLogged = true;
            CompositionTarget.Rendering -= SettingsFirstFrame_Rendering;
            _firstFrameSubscribed = false;
            LogFirstOpenStage("first-render");
        }

        private void ConfigureWindow()
        {
            IntPtr hwnd =
                WinRT.Interop.WindowNative.GetWindowHandle(this);

            WindowId windowId =
                Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);

            _appWindow = AppWindow.GetFromWindowId(windowId);
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarHost);
            UpdateWindowScale(WindowDpiService.GetScale(hwnd));
            CenterWindow(windowId, _appWindow.Size.Width, _appWindow.Size.Height);
        }

        /// <summary>首次加载及跨 DPI 屏幕时同步逻辑窗口尺寸，普通调整大小不会触发重置。</summary>
        private void UpdateWindowScale(double scale)
        {
            if (_appWindow is null || Math.Abs(_windowScale - scale) < 0.001) return;
            // 1. AppWindow 的客户区使用物理像素，XAML 控件保持原来的逻辑尺寸。
            _windowScale = scale;
            var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            _appWindow.ResizeClient(new SizeInt32(
                Math.Min(WindowDpiService.ToPixels(SettingsWidth, scale), area.Width - 16),
                Math.Min(WindowDpiService.ToPixels(SettingsHeight, scale), area.Height - 16)));
            // 2. 记录首次布局依据，便于区分原生 DPI 和 XAML 加载后的缩放。
            AppLogService.Write($"Settings window sized. scale={scale}; client={_appWindow.ClientSize.Width}x{_appWindow.ClientSize.Height}");
        }

        private void SettingsXamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args) =>
            UpdateWindowScale(sender.RasterizationScale);

        /// <summary>返回窗口实际尺寸与关键行布局，用于新进程的 DPI 冒烟验证。</summary>
        internal object GetSizingVerification() => new
        {
            scale = SettingsRoot.XamlRoot?.RasterizationScale ?? _windowScale,
            clientWidth = _appWindow?.ClientSize.Width,
            clientHeight = _appWindow?.ClientSize.Height,
            logicalWidth = SettingsRoot.ActualWidth,
            logicalHeight = SettingsRoot.ActualHeight,
            contentWidth = SettingsPageHost.ActualWidth,
            labelWidth = SpeedFontSizeLabel.ActualWidth,
            themeLabelWidth = ThemeLabel.ActualWidth
        };

        private void CenterWindow(WindowId windowId, int width, int height)
        {
            if (_appWindow is null)
            {
                return;
            }

            var displayArea =
                DisplayArea.GetFromWindowId(
                    windowId,
                    DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            _appWindow.Move(
                new PointInt32(
                    workArea.X + (workArea.Width - width) / 2,
                    workArea.Y + (workArea.Height - height) / 2));
        }

        private void LoadSettings()
        {
            _isLoading = true;

            DarkThemeRadio.IsChecked = _settings.Theme == AppThemeMode.Dark;
            LightThemeRadio.IsChecked = _settings.Theme == AppThemeMode.Light;
            StartupToggle.IsOn = StartupService.IsStartupEnabled();
            StartupStateText.Text = StartupToggle.IsOn ? "已登记登录启动；系统启动应用中的禁用设置仍生效" : "未登记登录启动";
            TopmostToggle.IsOn = _settings.TopmostEnabled;
            LockWindowToggle.IsOn = _settings.LockWindowPosition;
            DisplayModeCombo.SelectedIndex = GetDisplayModeIndex(_settings.DisplayMode);
            TaskbarEdgePaddingTextBox.Text = _settings.TaskbarEdgePadding.ToString();
            SpeedFontSizeTextBox.Text = _settings.SpeedFontSize.ToString();
            LoadSystemStatusSettings();

            _isLoading = false;
        }

        private void LoadSystemStatusSettings()
        {
            SystemStatusEnabledToggle.IsOn = _settings.SystemStatus.Enabled;
            SystemStatusServerToggle.IsOn = _settings.SystemStatus.ServerEnabled;
            SystemStatusPortTextBox.Text = _settings.SystemStatus.PreferredPort.ToString();
            SystemStatusRefreshIntervalCombo.SelectedIndex =
                GetRefreshIntervalIndex(_settings.SystemStatus.RefreshIntervalSeconds);
            SystemStatusPasswordToggle.IsOn = _settings.SystemStatus.PasswordEnabled;
            SystemStatusPasswordBox.Password = string.Empty;
            SystemStatusWebThemeCombo.SelectedIndex =
                GetWebThemeIndex(_settings.SystemStatus.WebTheme);
            NetworkStatusCardToggle.IsOn = _settings.SystemStatus.Cards.NetworkSpeedVisible;
            CpuUsageCardToggle.IsOn = _settings.SystemStatus.Cards.CpuUsageVisible;
            CpuTemperatureCardToggle.IsOn = _settings.SystemStatus.Cards.CpuTemperatureVisible;
            GpuUsageCardToggle.IsOn = _settings.SystemStatus.Cards.GpuUsageVisible;
            GpuTemperatureCardToggle.IsOn = _settings.SystemStatus.Cards.GpuTemperatureVisible;
            MemoryUsageCardToggle.IsOn = _settings.SystemStatus.Cards.MemoryUsageVisible;
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            _settings.Theme =
                LightThemeRadio.IsChecked == true
                    ? AppThemeMode.Light
                    : AppThemeMode.Dark;

            ApplyTheme(_settings.Theme);
            SaveAndNotify();
        }

        private void TaskbarEdgePaddingTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveTaskbarEdgePaddingFromTextBox();
        }

        private void SpeedFontSizeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveSpeedFontSizeFromTextBox();
        }

        private void TaskbarEdgePaddingTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            SaveTaskbarEdgePaddingFromTextBox();
            e.Handled = true;
        }

        private void SpeedFontSizeTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            SaveSpeedFontSizeFromTextBox();
            e.Handled = true;
        }

        private void SystemStatusPortTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveSystemStatusPortFromTextBox();
        }

        private void SystemStatusPortTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            SaveSystemStatusPortFromTextBox();
            e.Handled = true;
        }

        private void SaveSystemStatusPortFromTextBox()
        {
            if (_isLoading)
            {
                return;
            }

            if (!int.TryParse(SystemStatusPortTextBox.Text, out var inputValue))
            {
                SystemStatusPortTextBox.Text = _settings.SystemStatus.PreferredPort.ToString();
                return;
            }

            var port =
                Math.Clamp(
                    inputValue,
                    MinSystemStatusPort,
                    MaxSystemStatusPort);

            if (SystemStatusPortTextBox.Text != port.ToString())
            {
                SystemStatusPortTextBox.Text = port.ToString();
            }

            if (_settings.SystemStatus.PreferredPort == port)
            {
                return;
            }

            _settings.SystemStatus.PreferredPort = port;

            SaveAndNotify();
        }

        private void SaveSpeedFontSizeFromTextBox()
        {
            if (_isLoading)
            {
                return;
            }

            if (!int.TryParse(SpeedFontSizeTextBox.Text, out var inputValue))
            {
                SpeedFontSizeTextBox.Text = _settings.SpeedFontSize.ToString();
                return;
            }

            var fontSize =
                Math.Clamp(
                    inputValue,
                    MinSpeedFontSize,
                    MaxSpeedFontSize);

            if (SpeedFontSizeTextBox.Text != fontSize.ToString())
            {
                SpeedFontSizeTextBox.Text = fontSize.ToString();
            }

            if (_settings.SpeedFontSize == fontSize)
            {
                return;
            }

            _settings.SpeedFontSize = fontSize;

            SaveAndNotify();
        }

        private void SaveTaskbarEdgePaddingFromTextBox()
        {
            if (_isLoading)
            {
                return;
            }

            if (!int.TryParse(TaskbarEdgePaddingTextBox.Text, out var inputValue))
            {
                TaskbarEdgePaddingTextBox.Text = _settings.TaskbarEdgePadding.ToString();
                return;
            }

            var padding =
                Math.Clamp(
                    inputValue,
                    MinTaskbarEdgePadding,
                    MaxTaskbarEdgePadding);

            if (TaskbarEdgePaddingTextBox.Text != padding.ToString())
            {
                TaskbarEdgePaddingTextBox.Text = padding.ToString();
            }

            if (_settings.TaskbarEdgePadding == padding)
            {
                return;
            }

            _settings.TaskbarEdgePadding = padding;

            SaveAndNotify();
        }

        private void TopmostToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            _settings.TopmostEnabled = TopmostToggle.IsOn;

            SaveAndNotify();
        }

        private void LockWindowToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            _settings.LockWindowPosition = LockWindowToggle.IsOn;

            SaveAndNotify();
        }

        private void DisplayModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            _settings.DisplayMode = GetDisplayModeFromIndex(DisplayModeCombo.SelectedIndex);

            SaveAndNotify();
        }

        private void SystemStatusEnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            _settings.SystemStatus.Enabled = SystemStatusEnabledToggle.IsOn;

            SaveAndNotify();
        }

        private void SystemStatusServerToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            _settings.SystemStatus.ServerEnabled = SystemStatusServerToggle.IsOn;

            SaveAndNotify();
        }

        private void SystemStatusPasswordToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            _settings.SystemStatus.PasswordEnabled = SystemStatusPasswordToggle.IsOn;

            if (_settings.SystemStatus.PasswordEnabled &&
                string.IsNullOrWhiteSpace(_settings.SystemStatus.PasswordHash) &&
                string.IsNullOrWhiteSpace(SystemStatusPasswordBox.Password))
            {
                _settings.SystemStatus.PasswordEnabled = false;

                _isLoading = true;
                SystemStatusPasswordToggle.IsOn = false;
                _isLoading = false;

                SystemStatusServiceErrorText.Text = "请先输入访问密码";
                return;
            }

            var passwordChanged = SaveSystemStatusPasswordFromInput();
            if (passwordChanged)
            {
                return;
            }

            SaveAndNotify();
        }

        private void SystemStatusPasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _ = SaveSystemStatusPasswordFromInput();
        }

        private void SystemStatusPasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            _ = SaveSystemStatusPasswordFromInput();
            e.Handled = true;
        }

        private bool SaveSystemStatusPasswordFromInput()
        {
            if (_isLoading ||
                string.IsNullOrWhiteSpace(SystemStatusPasswordBox.Password))
            {
                return false;
            }

            _settings.SystemStatus.PasswordHash =
                SystemStatusHttpServerService.CreatePasswordHash(
                    SystemStatusPasswordBox.Password);
            SystemStatusPasswordBox.Password = string.Empty;

            SaveAndNotify();
            return true;
        }

        private void SystemStatusRefreshIntervalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            _settings.SystemStatus.RefreshIntervalSeconds =
                GetRefreshIntervalFromIndex(SystemStatusRefreshIntervalCombo.SelectedIndex);

            SaveAndNotify();
        }

        private void SystemStatusWebThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            _settings.SystemStatus.WebTheme =
                GetWebThemeFromIndex(SystemStatusWebThemeCombo.SelectedIndex);

            SaveAndNotify();
        }

        private void SystemStatusCardToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            _settings.SystemStatus.Cards.NetworkSpeedVisible = NetworkStatusCardToggle.IsOn;
            _settings.SystemStatus.Cards.CpuUsageVisible = CpuUsageCardToggle.IsOn;
            _settings.SystemStatus.Cards.CpuTemperatureVisible = CpuTemperatureCardToggle.IsOn;
            _settings.SystemStatus.Cards.GpuUsageVisible = GpuUsageCardToggle.IsOn;
            _settings.SystemStatus.Cards.GpuTemperatureVisible = GpuTemperatureCardToggle.IsOn;
            _settings.SystemStatus.Cards.MemoryUsageVisible = MemoryUsageCardToggle.IsOn;

            SaveAndNotify();
        }

        /// <summary>用户主动授权硬件采集；拒绝授权时保留网速和普通系统指标。</summary>
        private async void HardwareAuthorizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hardwareSamplingService is null) return;
            HardwareAuthorizeButton.IsEnabled = false;
            try { await _hardwareSamplingService.AuthorizeAsync(); }
            catch (Exception ex)
            {
                AppLogService.Write("Hardware authorization event failed.", ex);
                HardwareStateText.Text = "硬件授权失败：" + ex.Message;
                return;
            }
            finally
            {
                HardwareAuthorizeButton.IsEnabled = true;
            }
            HardwareStateText.Text = _hardwareSamplingService.State;
        }

        private void StartupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
            {
                return;
            }

            var previousStartupEnabled = _settings.StartupEnabled;
            _settings.StartupEnabled = StartupToggle.IsOn;

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
                StartupStateText.Text = "启动项设置失败：" + ex.Message;
                _settings.StartupEnabled = previousStartupEnabled;

                _isLoading = true;
                StartupToggle.IsOn = previousStartupEnabled;
                _isLoading = false;

                return;
            }

            StartupStateText.Text = _settings.StartupEnabled ? "已登记登录启动；系统启动应用中的禁用设置仍生效" : "未登记登录启动";
            SaveAndNotify();
        }

        private void SaveAndNotify()
        {
            _settings = _settingsService.MergeAndSave(_settings, _lastSavedSettings);
            _lastSavedSettings = SettingsService.Clone(_settings);
            _settingsChanged(_settings);
            RefreshSystemStatusServerState();
        }

        private void RefreshSystemStatusServerState()
        {
            // 1. 运行时状态可能枚举网络适配器，后台读取以免阻塞设置窗口首帧。
            var generation = ++_serverStateRefreshGeneration;
            if (_settings.SystemStatus.Enabled && _settings.SystemStatus.ServerEnabled)
                SystemStatusServiceStatusText.Text = "正在读取…";
            _ = RefreshSystemStatusServerStateAsync(generation);
        }

        /// <summary>后台获取服务状态，只允许最新一次结果更新仍打开的设置窗口。</summary>
        private async Task RefreshSystemStatusServerStateAsync(int generation)
        {
            SystemStatusHttpServerState state;
            try
            {
                state = await Task.Run(_getSystemStatusHttpServerState);
            }
            catch (Exception error)
            {
                AppLogService.Write("Failed to read system status server state.", error);
                return;
            }

            // 2. 回到 UI 线程；关闭窗口或再次刷新后丢弃过期结果。
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isClosed || generation != _serverStateRefreshGeneration) return;
                ApplySystemStatusServerState(state);
            });
        }

        /// <summary>根据最新设置和服务快照刷新系统状态页面的文字。</summary>
        private void ApplySystemStatusServerState(SystemStatusHttpServerState state)
        {
            var serverRequested =
                _settings.SystemStatus.Enabled &&
                _settings.SystemStatus.ServerEnabled;

            SystemStatusServiceStatusText.Text =
                !_settings.SystemStatus.Enabled
                    ? "系统状态未启用"
                    : !_settings.SystemStatus.ServerEnabled
                        ? "服务未启用"
                        : state.IsRunning && state.ActualPort > 0
                            ? $"运行中（端口 {state.ActualPort}）"
                            : "未启动";

            SystemStatusAccessAddressText.Text =
                state.AccessUrls.Count > 0
                    ? string.Join(Environment.NewLine, state.AccessUrls)
                    : "未启动";

            SystemStatusServiceErrorText.Text =
                !serverRequested || string.IsNullOrWhiteSpace(state.LastError)
                    ? "无"
                    : state.LastError;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void NetSpeedNavigationButton_Click(object sender, RoutedEventArgs e) => ShowSettingsPage(SettingsPage.NetSpeed);

        private void SystemStatusNavigationButton_Click(object sender, RoutedEventArgs e) => ShowSettingsPage(SettingsPage.SystemStatus);

        private void UpdateNavigationButton_Click(object sender, RoutedEventArgs e) => ShowSettingsPage(SettingsPage.Update);

        private void ShowSettingsPage(SettingsPage page)
        {
            _currentSettingsPage = page;
            NetSpeedSettingsPanel.Visibility = page == SettingsPage.NetSpeed ? Visibility.Visible : Visibility.Collapsed;
            SystemStatusSettingsPanel.Visibility = page == SettingsPage.SystemStatus ? Visibility.Visible : Visibility.Collapsed;
            UpdateSettingsPanel.Visibility = page == SettingsPage.Update ? Visibility.Visible : Visibility.Collapsed;
            ApplyNavigationSelection(page);
        }

        private void ApplyNavigationSelection(SettingsPage page)
        {
            var isLightTheme = _settings.Theme == AppThemeMode.Light;
            var selectedBackgroundBrush =
                new SolidColorBrush(
                    isLightTheme
                        ? Windows.UI.Color.FromArgb(255, 232, 232, 232)
                        : Windows.UI.Color.FromArgb(255, 49, 49, 49));
            var transparentBrush =
                new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            var accentBrush =
                new SolidColorBrush(
                    isLightTheme
                        ? Windows.UI.Color.FromArgb(255, 0, 95, 184)
                        : Windows.UI.Color.FromArgb(255, 96, 205, 255));

            var netSpeedSelected = page == SettingsPage.NetSpeed;
            var systemStatusSelected = page == SettingsPage.SystemStatus;
            var updateSelected = page == SettingsPage.Update;
            UpdateNavigationButton.Background = updateSelected ? selectedBackgroundBrush : transparentBrush;
            UpdateNavigationSelectionBar.Background = updateSelected ? accentBrush : transparentBrush;
            UpdateNavigationText.FontWeight = updateSelected ? FontWeights.SemiBold : FontWeights.Normal;

            NetSpeedNavigationButton.Background =
                netSpeedSelected ? selectedBackgroundBrush : transparentBrush;
            SystemStatusNavigationButton.Background =
                systemStatusSelected ? selectedBackgroundBrush : transparentBrush;

            NetSpeedNavigationSelectionBar.Background =
                netSpeedSelected ? accentBrush : transparentBrush;
            SystemStatusNavigationSelectionBar.Background =
                systemStatusSelected ? accentBrush : transparentBrush;

            NetSpeedNavigationText.FontWeight =
                netSpeedSelected ? FontWeights.SemiBold : FontWeights.Normal;
            SystemStatusNavigationText.FontWeight =
                systemStatusSelected ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private void ApplyTheme(AppThemeMode theme)
        {
            var palette = SettingsService.GetThemePalette(theme);
            var isLightTheme = theme == AppThemeMode.Light;
            var backgroundBrush = new SolidColorBrush(ToColor(palette.BackgroundColor));
            var foregroundBrush = new SolidColorBrush(ToColor(palette.ForegroundColor));
            var sectionForegroundBrush =
                new SolidColorBrush(
                    isLightTheme
                        ? Windows.UI.Color.FromArgb(255, 80, 80, 80)
                        : Windows.UI.Color.FromArgb(255, 207, 207, 207));
            var rowBackgroundBrush =
                new SolidColorBrush(
                    isLightTheme
                        ? Windows.UI.Color.FromArgb(255, 255, 255, 255)
                        : Windows.UI.Color.FromArgb(255, 43, 43, 43));
            var rowBorderBrush =
                new SolidColorBrush(
                    isLightTheme
                        ? Windows.UI.Color.FromArgb(255, 218, 218, 218)
                        : Windows.UI.Color.FromArgb(255, 58, 58, 58));
            var navigationBackgroundBrush =
                new SolidColorBrush(
                    isLightTheme
                        ? Windows.UI.Color.FromArgb(255, 243, 243, 243)
                        : Windows.UI.Color.FromArgb(255, 32, 32, 32));

            SettingsRoot.RequestedTheme =
                isLightTheme
                    ? ElementTheme.Light
                    : ElementTheme.Dark;

            SettingsRoot.Background = backgroundBrush;
            TitleBarHost.Background = backgroundBrush;
            SettingsNavigationRail.Background = navigationBackgroundBrush;
            ApplyTitleBarTheme(theme);
            TitleText.Foreground = foregroundBrush;
            TitleBarText.Foreground = foregroundBrush;
            UpdatePageTitle.Foreground = foregroundBrush;
            UpdateNavigationText.Foreground = foregroundBrush;
            UpdateNavigationIcon.Foreground = foregroundBrush;
            UpdateCurrentVersionText.Foreground = foregroundBrush;
            UpdateStateText.Foreground = foregroundBrush;
            NetSpeedNavigationText.Foreground = foregroundBrush;
            SystemStatusNavigationText.Foreground = foregroundBrush;
            NetSpeedNavigationIcon.Foreground = foregroundBrush;
            SystemStatusNavigationIcon.Foreground = foregroundBrush;
            AppearanceSectionTitle.Foreground = sectionForegroundBrush;
            WindowSectionTitle.Foreground = sectionForegroundBrush;
            ThemeLabel.Foreground = foregroundBrush;
            SpeedFontSizeLabel.Foreground = foregroundBrush;
            StartupLabel.Foreground = foregroundBrush;
            StartupStateText.Foreground = foregroundBrush;
            HardwareStateText.Foreground = foregroundBrush;
            TopmostLabel.Foreground = foregroundBrush;
            LockWindowLabel.Foreground = foregroundBrush;
            DisplayModeLabel.Foreground = foregroundBrush;
            TaskbarEdgePaddingLabel.Foreground = foregroundBrush;
            SpeedFontSizeUnitLabel.Foreground = foregroundBrush;
            TaskbarEdgePaddingUnitLabel.Foreground = foregroundBrush;
            DarkThemeRadio.Foreground = foregroundBrush;
            LightThemeRadio.Foreground = foregroundBrush;
            StartupToggle.Foreground = foregroundBrush;
            TopmostToggle.Foreground = foregroundBrush;
            LockWindowToggle.Foreground = foregroundBrush;
            DisplayModeCombo.Foreground = foregroundBrush;
            SpeedFontSizeTextBox.Foreground = foregroundBrush;
            TaskbarEdgePaddingTextBox.Foreground = foregroundBrush;
            SystemStatusTitleText.Foreground = foregroundBrush;
            SystemStatusServiceSectionTitle.Foreground = sectionForegroundBrush;
            SystemStatusCardsSectionTitle.Foreground = sectionForegroundBrush;
            SystemStatusEnabledLabel.Foreground = foregroundBrush;
            SystemStatusServerLabel.Foreground = foregroundBrush;
            SystemStatusPortLabel.Foreground = foregroundBrush;
            SystemStatusServiceStatusLabel.Foreground = foregroundBrush;
            SystemStatusServiceStatusText.Foreground = foregroundBrush;
            SystemStatusAccessAddressLabel.Foreground = foregroundBrush;
            SystemStatusAccessAddressText.Foreground = foregroundBrush;
            SystemStatusServiceErrorLabel.Foreground = foregroundBrush;
            SystemStatusServiceErrorText.Foreground = foregroundBrush;
            SystemStatusRefreshIntervalLabel.Foreground = foregroundBrush;
            SystemStatusPasswordLabel.Foreground = foregroundBrush;
            SystemStatusPasswordValueLabel.Foreground = foregroundBrush;
            SystemStatusWebThemeLabel.Foreground = foregroundBrush;
            NetworkStatusCardLabel.Foreground = foregroundBrush;
            CpuUsageCardLabel.Foreground = foregroundBrush;
            CpuTemperatureCardLabel.Foreground = foregroundBrush;
            GpuUsageCardLabel.Foreground = foregroundBrush;
            GpuTemperatureCardLabel.Foreground = foregroundBrush;
            MemoryUsageCardLabel.Foreground = foregroundBrush;
            SystemStatusEnabledToggle.Foreground = foregroundBrush;
            SystemStatusServerToggle.Foreground = foregroundBrush;
            SystemStatusPasswordToggle.Foreground = foregroundBrush;
            SystemStatusPasswordBox.Foreground = foregroundBrush;
            NetworkStatusCardToggle.Foreground = foregroundBrush;
            CpuUsageCardToggle.Foreground = foregroundBrush;
            CpuTemperatureCardToggle.Foreground = foregroundBrush;
            GpuUsageCardToggle.Foreground = foregroundBrush;
            GpuTemperatureCardToggle.Foreground = foregroundBrush;
            MemoryUsageCardToggle.Foreground = foregroundBrush;
            SystemStatusPortTextBox.Foreground = foregroundBrush;
            SystemStatusRefreshIntervalCombo.Foreground = foregroundBrush;
            SystemStatusWebThemeCombo.Foreground = foregroundBrush;

            foreach (var child in SettingsRoot.Children)
            {
                ApplyRowBrushes(child, rowBackgroundBrush, rowBorderBrush);
            }

            ApplyNavigationSelection(_currentSettingsPage);
        }

        private static void ApplyRowBrushes(
            DependencyObject element,
            Brush backgroundBrush,
            Brush borderBrush)
        {
            if (element is Border border &&
                border.Style is not null)
            {
                border.Background = backgroundBrush;
                border.BorderBrush = borderBrush;
            }

            var childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(element);

            for (var index = 0; index < childCount; index++)
            {
                ApplyRowBrushes(
                    Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(element, index),
                    backgroundBrush,
                    borderBrush);
            }
        }

        private void ApplyTitleBarTheme(AppThemeMode theme)
        {
            var palette = WindowTitleBarService.GetTitleBarPalette(theme);
            var backgroundColor = ToColor(palette.BackgroundColor);
            var foregroundColor = ToColor(palette.ForegroundColor);
            var buttonHoverBackground =
                theme == AppThemeMode.Light
                    ? Windows.UI.Color.FromArgb(255, 230, 230, 230)
                    : Windows.UI.Color.FromArgb(255, 45, 45, 45);
            var buttonPressedBackground =
                theme == AppThemeMode.Light
                    ? Windows.UI.Color.FromArgb(255, 210, 210, 210)
                    : Windows.UI.Color.FromArgb(255, 60, 60, 60);

            if (_appWindow is null)
            {
                return;
            }

            var titleBar = _appWindow.TitleBar;

            titleBar.BackgroundColor = backgroundColor;
            titleBar.ForegroundColor = foregroundColor;
            titleBar.InactiveBackgroundColor = backgroundColor;
            titleBar.InactiveForegroundColor = foregroundColor;
            titleBar.ButtonBackgroundColor = backgroundColor;
            titleBar.ButtonForegroundColor = foregroundColor;
            titleBar.ButtonInactiveBackgroundColor = backgroundColor;
            titleBar.ButtonInactiveForegroundColor = foregroundColor;
            titleBar.ButtonHoverBackgroundColor = buttonHoverBackground;
            titleBar.ButtonHoverForegroundColor = foregroundColor;
            titleBar.ButtonPressedBackgroundColor = buttonPressedBackground;
            titleBar.ButtonPressedForegroundColor = foregroundColor;
        }

        private static int GetDisplayModeIndex(WidgetDisplayMode displayMode)
        {
            return displayMode switch
            {
                WidgetDisplayMode.TaskbarLeft => 1,
                WidgetDisplayMode.TaskbarRight => 2,
                _ => 0
            };
        }

        private static WidgetDisplayMode GetDisplayModeFromIndex(int selectedIndex)
        {
            return selectedIndex switch
            {
                1 => WidgetDisplayMode.TaskbarLeft,
                2 => WidgetDisplayMode.TaskbarRight,
                _ => WidgetDisplayMode.Floating
            };
        }

        private static int GetRefreshIntervalIndex(int refreshIntervalSeconds)
        {
            return refreshIntervalSeconds switch
            {
                2 => 1,
                3 => 2,
                5 => 3,
                _ => 0
            };
        }

        private static int GetRefreshIntervalFromIndex(int selectedIndex)
        {
            return selectedIndex switch
            {
                1 => 2,
                2 => 3,
                3 => 5,
                _ => 1
            };
        }

        private static int GetWebThemeIndex(SystemStatusWebTheme webTheme)
        {
            return webTheme switch
            {
                SystemStatusWebTheme.Light => 1,
                SystemStatusWebTheme.Dark => 2,
                _ => 0
            };
        }

        private static SystemStatusWebTheme GetWebThemeFromIndex(int selectedIndex)
        {
            return selectedIndex switch
            {
                1 => SystemStatusWebTheme.Light,
                2 => SystemStatusWebTheme.Dark,
                _ => SystemStatusWebTheme.FollowApp
            };
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
    }
}
