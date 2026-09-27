using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Media;
using NetSpeedWidget.Models;
using NetSpeedWidget.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetSpeedWidget;

public sealed partial class SettingsWindow
{
    private readonly UpdateService _updateService = new();
    private CancellationTokenSource? _updateCancellation;
    private Action? _exitForUpdate;
    private bool _updatesClosed;
    private bool _updateBusy;
    private bool _updatePopupOpen;
    private bool _updateVerificationOnly;
    private bool _verifyPopupDownload;
    public bool UpdateDownloadVerificationFinished { get; private set; }
    private bool _downloadVerificationPassed;
    private ContentDialog? _updateDialog;
    private TextBlock? _dialogStatus;
    private ProgressBar? _dialogProgress;

    /// <summary>始终显示版本和检查按钮，关闭设置时取消本窗口的下载或检查。</summary>
    private void InitializeUpdates(Action? exitForUpdate)
    {
        _exitForUpdate = exitForUpdate;
        UpdateCurrentVersionText.Text = $"当前版本 v{UpdateService.CurrentVersion}";
        TitleBarText.Text = $"设置 · v{UpdateService.CurrentVersion}";
        if (File.Exists(UpdateLauncher.ResultPath))
        {
            try { ToolTipService.SetToolTip(UpdateCurrentVersionText, File.ReadAllText(UpdateLauncher.ResultPath)); }
            catch (IOException) { }
        }
        Closed += (_, _) =>
        {
            _updatesClosed = true;
            _updateCancellation?.Cancel();
            _updateService.Dispose();
        };
    }

    /// <summary>手动检查，没有新版本时仅在按钮旁显示结果，有新版本时弹窗。</summary>
    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updateBusy || _updatePopupOpen) return;
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        _updateBusy = true;
        CheckUpdateButton.IsEnabled = false;
        ToolTipService.SetToolTip(UpdateStateText, null);
        UpdateStateText.Text = "检查中…";
        try
        {
            // 1. 查询稳定版本，普通检查结果不打开新的页面或对话框。
            var release = await _updateService.CheckAsync(UpdateService.CurrentVersion, UpdateService.IsInstalled, cancellation.Token);
            if (_updatesClosed) return;
            _updateBusy = false;
            if (release is null) UpdateStateText.Text = "没有新版本";
            else await ShowDiscoveredReleaseAsync(release);
        }
        catch (Exception error)
        {
            if (!_updatesClosed)
            {
                UpdateStateText.Text = "检查失败";
                ToolTipService.SetToolTip(UpdateStateText, FriendlyUpdateError(error));
            }
        }
        finally
        {
            if (ReferenceEquals(_updateCancellation, cancellation)) _updateCancellation = null;
            _updateBusy = false;
            if (!_updatesClosed) CheckUpdateButton.IsEnabled = true;
        }
    }

    /// <summary>启动或手动检查发现新版本后，复用缓存并在当前设置窗口显示更新弹窗。</summary>
    public async Task ShowDiscoveredReleaseAsync(UpdateRelease release)
    {
        if (_updatesClosed || _updatePopupOpen || _updateBusy) return;
        _updateBusy = true;
        CheckUpdateButton.IsEnabled = false;
        UpdateStateText.Text = "发现新版本";
        try
        {
            var cached = await UpdateCache.FindAsync(release, UpdateCache.GetDirectory(release), default);
            await ShowUpdatePopupAsync(release, cached);
        }
        finally
        {
            _updateBusy = false;
            if (!_updatesClosed) CheckUpdateButton.IsEnabled = true;
        }
    }

    /// <summary>在同一个弹窗内展示更新内容、下载进度和安装按钮，不跳转设置页面。</summary>
    private async Task ShowUpdatePopupAsync(UpdateRelease release, string? cached)
    {
        if (_updatesClosed || _updatePopupOpen) return;
        for (var attempt = 0; SettingsRoot.XamlRoot is null && attempt < 100 && !_updatesClosed; attempt++) await Task.Delay(50);
        if (_updatesClosed || SettingsRoot.XamlRoot is null) return;
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = $"发布时间：{release.PublishedAt.ToLocalTime():yyyy-MM-dd} · {release.Size / 1048576.0:F1} MB", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new ScrollViewer
        {
            MaxHeight = 230, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new TextBlock { Text = release.Notes, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true }
        });
        var status = new TextBlock { Text = cached is null ? "下载完成后，可直接点击安装。" : "更新包已下载并校验，可直接安装。", TextWrapping = TextWrapping.Wrap };
        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
        content.Children.Add(status);
        content.Children.Add(progress);
        var dialog = new ContentDialog
        {
            XamlRoot = SettingsRoot.XamlRoot, RequestedTheme = SettingsRoot.RequestedTheme,
            Title = "发现新版本 " + release.Tag, Content = content,
            PrimaryButtonText = cached is null ? "下载更新" : "安装更新",
            IsPrimaryButtonEnabled = (!_updateVerificationOnly || _verifyPopupDownload) && _exitForUpdate is not null,
            CloseButtonText = "稍后", DefaultButton = ContentDialogButton.Close
        };
        bool preparing = false;
        bool downloading = false;
        bool active = true;
        string? package = cached;
        dialog.CloseButtonClick += (_, _) => _updateCancellation?.Cancel();
        dialog.Closing += (_, args) =>
        {
            if (preparing && !_updatesClosed) args.Cancel = true;
            else _updateCancellation?.Cancel();
        };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            // 1. 保持弹窗打开，异步操作期间禁用主按钮，避免重复下载或重复启动安装。
            args.Cancel = true;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            _updateCancellation = cancellation;
            dialog.IsPrimaryButtonEnabled = false;
            progress.Visibility = Visibility.Visible;
            try
            {
                package = await UpdateCache.FindAsync(release, UpdateCache.GetDirectory(release), cancellation.Token);
                if (package is null)
                {
                    downloading = true;
                    dialog.CloseButtonText = "取消下载";
                    progress.IsIndeterminate = false;
                    progress.Value = 0;
                    status.Text = "正在下载…";
                    var reporter = new Progress<double>(value =>
                    {
                        if (!active || !downloading || _updatesClosed) return;
                        progress.Value = value;
                        status.Text = $"正在下载：{value:F0}%";
                    });
                    package = await _updateService.DownloadAsync(release, UpdateCache.GetDirectory(release), reporter, cancellation.Token);
                    downloading = false;
                    // 2. 下载成功后原弹窗直接切换为安装，不重新打开弹窗，不自动退出软件。
                    if (active && !_updatesClosed)
                    {
                        progress.Value = 100;
                        status.Text = "下载完成，校验通过。点击“安装更新”即可安装。";
                        dialog.PrimaryButtonText = "安装更新";
                        dialog.CloseButtonText = "稍后安装";
                    }
                }
                else
                {
                    if (_updateVerificationOnly) { status.Text = "仅验证下载安装交互，不执行安装。"; return; }
                    // 3. 安装前重新校验；收到独立更新程序准备信号后正常退出。
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (_updatesClosed || !active) return;
                    preparing = true;
                    dialog.CloseButtonText = "";
                    progress.IsIndeterminate = true;
                    status.Text = "正在准备安装；随后显示安装进度，并在完成后重新启动软件…";
                    await UpdateLauncher.PrepareAsync(release, package);
                    _exitForUpdate?.Invoke();
                }
            }
            catch (Exception error)
            {
                if (active && !_updatesClosed) status.Text = "操作未完成：" + FriendlyUpdateError(error);
                AppLogService.Write("Update dialog operation failed.", error);
            }
            finally
            {
                downloading = preparing = false;
                if (ReferenceEquals(_updateCancellation, cancellation)) _updateCancellation = null;
                if (active && !_updatesClosed)
                {
                    dialog.IsPrimaryButtonEnabled = !_updateVerificationOnly || _verifyPopupDownload;
                    dialog.CloseButtonText = package is null ? "稍后" : "稍后安装";
                    dialog.PrimaryButtonText = package is null ? "下载更新" : "安装更新";
                    progress.IsIndeterminate = false;
                    if (_verifyPopupDownload)
                    {
                        _downloadVerificationPassed = package is not null && progress.Value == 100 && dialog.PrimaryButtonText == "安装更新";
                        UpdateDownloadVerificationFinished = true;
                    }
                }
            }
        };
        _updatePopupOpen = true;
        _updateDialog = dialog;
        _dialogProgress = progress;
        _dialogStatus = status;
        if (_verifyPopupDownload)
        {
            dialog.Opened += (_, _) =>
            {
                try
                {
                    var button = FindPrimaryButton(dialog) ?? throw new InvalidOperationException("未找到弹窗下载按钮。");
                    var invoke = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke);
                    invoke.Invoke();
                }
                catch (Exception error)
                {
                    status.Text = error.Message;
                    UpdateDownloadVerificationFinished = true;
                    AppLogService.Write("Popup download verification failed.", error);
                }
            };
        }
        try { await dialog.ShowAsync(); }
        finally
        {
            active = false;
            _updateCancellation?.Cancel();
            _updatePopupOpen = false;
            _updateDialog = null;
            _dialogProgress = null;
            _dialogStatus = null;
        }
    }

    private static string FriendlyUpdateError(Exception error) => error is OperationCanceledException
        ? "请求已取消或超时，当前软件未被替换。" : error.Message;

    /// <summary>隔离验证版本入口、无更新结果或新版本弹窗，不执行下载安装。</summary>
    public void ShowUpdateVerificationPage()
    {
        _updateVerificationOnly = true;
        _verifyPopupDownload = Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-download-popup") >= 0;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-update-current") >= 0)
            CheckUpdateButton_Click(CheckUpdateButton, new RoutedEventArgs());
        else _ = ShowVerificationPopupAsync();
    }

    private async Task ShowVerificationPopupAsync()
    {
        try
        {
            if (_verifyPopupDownload)
            {
                var actual = await _updateService.CheckAsync(new Version(0, 0, 0), UpdateService.IsInstalled, default)
                    ?? throw new InvalidOperationException("真实 Release 不存在。");
                await ShowUpdatePopupAsync(actual, null);
                return;
            }
            var release = new UpdateRelease(new Version(2, 2, 0), "v2.2.0",
                "## 新增功能\n- 下载和安装都在同一弹窗操作。\n\n## 优化改进\n- 复用下载缓存。\n\n## 问题修复\n- 移除独立更新页面。\n\n## 更新说明\n- 保留用户配置。",
                DateTimeOffset.Now, new Uri("https://github.com/" + UpdateService.Repository), "verification-only", 10485760, "", false);
            await ShowUpdatePopupAsync(release, null);
        }
        catch (Exception error) { UpdateDownloadVerificationFinished = true; AppLogService.Write("Update popup verification failed.", error); }
    }

    /// <summary>只在隔离验证模式通过控件自动化触发真实下载按钮，不执行安装。</summary>
    private static Button? FindPrimaryButton(DependencyObject root)
    {
        if (root is Button button && button.Name == "PrimaryButton") return button;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            if (FindPrimaryButton(VisualTreeHelper.GetChild(root, index)) is { } found) return found;
        return null;
    }

    public object GetUpdateSizingVerification() => new
    {
        buttonWidth = CheckUpdateButton.ActualWidth, statusWidth = UpdateStateText.ActualWidth,
        versionWidth = UpdateCurrentVersionText.ActualWidth, currentVersion = UpdateCurrentVersionText.Text,
        status = UpdateStateText.Text, popupOpen = _updatePopupOpen,
        primaryButton = _updateDialog?.PrimaryButtonText,
        progressInPopup = _dialogProgress is not null, popupStatus = _dialogStatus?.Text,
        progressValue = _dialogProgress?.Value, downloadVerificationPassed = _downloadVerificationPassed
    };
}
