using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private UpdateRelease? _updateRelease;
    private string? _cachedPackage;
    private Action? _exitForUpdate;
    private bool _updatesClosed;
    private bool _updateBusy;
    private bool _updatePopupOpen;
    private bool _updateVerificationOnly;

    /// <summary>初始化发行类型、上次更新结果及关闭时的取消处理。</summary>
    private void InitializeUpdates(Action? exitForUpdate)
    {
        _exitForUpdate = exitForUpdate;
        UpdateCurrentVersionText.Text = $"当前版本：v{UpdateService.CurrentVersion} · {(UpdateService.IsInstalled ? "安装版" : "便携版")}";
        UpdateInstallHintText.Text = "下载和安装分开操作。下载成功后保存在软件目录，下次检查无需重复下载。安装时软件退出并在完成后重启，配置和日志保留。";
        if (File.Exists(UpdateLauncher.ResultPath))
        {
            try { UpdateStateText.Text = File.ReadAllText(UpdateLauncher.ResultPath); }
            catch (IOException) { UpdateStateText.Text = "上次更新结果无法读取，请重新检查。"; }
        }
        Closed += (_, _) =>
        {
            _updatesClosed = true;
            _updateCancellation?.Cancel();
            _updateService.Dispose();
        };
    }

    private void UpdateNavigationButton_Click(object sender, RoutedEventArgs e) => ShowSettingsPage(SettingsPage.Update);

    /// <summary>启动检查或手动检查发现新版本时显示弹窗，复用已校验缓存。</summary>
    public async Task ShowDiscoveredReleaseAsync(UpdateRelease release)
    {
        if (_updatesClosed || _updatePopupOpen || _updateBusy) return;
        ShowSettingsPage(SettingsPage.Update);
        _updateRelease = release;
        UpdateNotesText.Text = release.Notes;
        UpdateStateText.Text = $"发现新版本 {release.Tag} · {release.PublishedAt.ToLocalTime():yyyy-MM-dd} · {release.Size / 1048576.0:F1} MB";
        _cachedPackage = await UpdateCache.FindAsync(release, UpdateCache.GetDirectory(release), default);
        if (_updatesClosed) return;
        if (_cachedPackage is not null) UpdateStateText.Text += "\n已下载，可直接安装。";
        RefreshUpdateButtons();
        await ShowUpdatePopupAsync(release);
    }

    /// <summary>显示更新内容；稍后安装不会删除缓存，重新检查后直接提供安装按钮。</summary>
    private async Task ShowUpdatePopupAsync(UpdateRelease release)
    {
        if (_updatesClosed || _updatePopupOpen) return;
        for (var attempt = 0; SettingsRoot.XamlRoot is null && attempt < 100 && !_updatesClosed; attempt++) await Task.Delay(50);
        if (_updatesClosed || SettingsRoot.XamlRoot is null) return;
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = $"发布时间：{release.PublishedAt.ToLocalTime():yyyy-MM-dd} · {release.Size / 1048576.0:F1} MB", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = _cachedPackage is null ? "下载后可以选择稍后安装。" : "更新包已下载并校验，无需重复下载。", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new ScrollViewer
        {
            MaxHeight = 280, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new TextBlock { Text = release.Notes, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true }
        });
        var dialog = new ContentDialog
        {
            XamlRoot = SettingsRoot.XamlRoot, RequestedTheme = SettingsRoot.RequestedTheme,
            Title = "发现新版本 " + release.Tag, Content = content,
            PrimaryButtonText = _cachedPackage is null ? "下载更新" : "安装更新",
            IsPrimaryButtonEnabled = !_updateVerificationOnly,
            CloseButtonText = "稍后", DefaultButton = ContentDialogButton.Close
        };
        _updatePopupOpen = true;
        ContentDialogResult result;
        try { result = await dialog.ShowAsync(); }
        finally { _updatePopupOpen = false; }
        if (result == ContentDialogResult.Primary && !_updatesClosed) await PerformUpdateActionAsync();
    }

    /// <summary>手动查询稳定版本，失败时保留当前软件并展示原因。</summary>
    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updateBusy) return;
        using var cancellation = new CancellationTokenSource();
        _updateCancellation = cancellation;
        _updateBusy = true;
        RefreshUpdateButtons();
        UpdateProgress.Visibility = Visibility.Visible;
        UpdateProgress.IsIndeterminate = true;
        UpdateStateText.Text = "正在检查更新…";
        try
        {
            var release = await _updateService.CheckAsync(UpdateService.CurrentVersion, UpdateService.IsInstalled, cancellation.Token);
            if (_updatesClosed) return;
            _updateBusy = false;
            UpdateProgress.Visibility = Visibility.Collapsed;
            if (release is not null) await ShowDiscoveredReleaseAsync(release);
            else
            {
                _updateRelease = null;
                _cachedPackage = null;
                UpdateStateText.Text = "当前已是最新稳定版本。";
                UpdateNotesText.Text = "暂无更新。";
            }
        }
        catch (Exception error)
        {
            if (!_updatesClosed) UpdateStateText.Text = "检查失败：" + FriendlyUpdateError(error);
        }
        finally
        {
            _updateCancellation = null;
            _updateBusy = false;
            if (!_updatesClosed) { UpdateProgress.Visibility = Visibility.Collapsed; RefreshUpdateButtons(); }
        }
    }

    private async void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        try { if (!_updateBusy) await PerformUpdateActionAsync(); }
        catch (Exception error) { if (!_updatesClosed) UpdateStateText.Text = "更新提示无法显示：" + FriendlyUpdateError(error); }
    }

    /// <summary>未缓存时只下载，已有完整缓存时才准备安装并正常退出。</summary>
    private async Task PerformUpdateActionAsync()
    {
        if (_updateRelease is not { } release || _exitForUpdate is null) return;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        _updateCancellation = cancellation;
        _updateBusy = true;
        RefreshUpdateButtons();
        UpdateProgress.Visibility = Visibility.Visible;
        bool downloading = false;
        bool downloaded = false;
        try
        {
            // 1. 安装前重新校验缓存，缓存损坏时重新下载但不自动安装。
            _cachedPackage = await UpdateCache.FindAsync(release, UpdateCache.GetDirectory(release), cancellation.Token);
            if (_cachedPackage is null)
            {
                downloading = true;
                CancelUpdateButton.Visibility = Visibility.Visible;
                UpdateProgress.IsIndeterminate = false;
                UpdateProgress.Value = 0;
                UpdateStateText.Text = "正在下载…";
                var progress = new Progress<double>(value =>
                {
                    if (_updatesClosed || !downloading) return;
                    UpdateProgress.Value = value;
                    UpdateStateText.Text = $"正在下载：{value:F0}%";
                });
                _cachedPackage = await _updateService.DownloadAsync(release, UpdateCache.GetDirectory(release), progress, cancellation.Token);
                downloading = false;
                downloaded = true;
                if (!_updatesClosed) UpdateStateText.Text = "下载完成并通过校验。可以现在安装，也可以稍后安装。";
            }
            else
            {
                // 2. 仅收到辅助程序的准备信号后退出；安装过程使用独立进度窗口或安装向导。
                cancellation.Token.ThrowIfCancellationRequested();
                if (_updatesClosed) return;
                CancelUpdateButton.Visibility = Visibility.Collapsed;
                UpdateProgress.IsIndeterminate = true;
                UpdateStateText.Text = "正在准备安装，请稍候…";
                await UpdateLauncher.PrepareAsync(release, _cachedPackage);
                _exitForUpdate();
            }
        }
        catch (Exception error)
        {
            if (!_updatesClosed) UpdateStateText.Text = "操作未完成：" + FriendlyUpdateError(error);
        }
        finally
        {
            downloading = false;
            _updateCancellation = null;
            _updateBusy = false;
            if (!_updatesClosed)
            {
                CancelUpdateButton.Visibility = UpdateProgress.Visibility = Visibility.Collapsed;
                RefreshUpdateButtons();
            }
        }
        if (downloaded && !_updatesClosed) await ShowUpdatePopupAsync(release);
    }

    private void RefreshUpdateButtons()
    {
        CheckUpdateButton.IsEnabled = !_updateBusy;
        InstallUpdateButton.IsEnabled = !_updateBusy && _updateRelease is not null && _exitForUpdate is not null;
        InstallUpdateButton.Content = _cachedPackage is null ? "下载更新" : "安装更新";
    }

    private void CancelUpdateButton_Click(object sender, RoutedEventArgs e) => _updateCancellation?.Cancel();
    private static string FriendlyUpdateError(Exception error) => error is OperationCanceledException
        ? "请求已取消或超时，当前软件未被替换。" : error.Message;

    /// <summary>隔离冒烟模式仅展示更新页面，不修改自启或执行下载安装。</summary>
    public void ShowUpdateVerificationPage()
    {
        ShowSettingsPage(SettingsPage.Update);
        UpdateStateText.Text = "布局验证：发现新版本 v2.2.0。下载和安装分开操作。";
        UpdateNotesText.Text = "## 新增功能\n- 新版本弹窗、下载、安装。\n\n## 优化改进\n- 复用已校验下载包，保留用户配置。\n\n## 问题修复\n- 无。\n\n## 更新说明\n- 数据和缓存保存在实际 EXE 目录。";
        _updateVerificationOnly = true;
        _ = ShowVerificationPopupAsync();
    }
    private async Task ShowVerificationPopupAsync()
    {
        try
        {
            var release = new UpdateRelease(new Version(2, 2, 0), "v2.2.0", UpdateNotesText.Text, DateTimeOffset.Now,
                new Uri("https://github.com/" + UpdateService.Repository), "verification-only", 10485760, "", false);
            await ShowUpdatePopupAsync(release);
        }
        catch (Exception error) { AppLogService.Write("Update popup verification failed.", error); }
    }
    public object GetUpdateSizingVerification() => new
    {
        visible = UpdateSettingsPanel.Visibility == Visibility.Visible, panelWidth = UpdateSettingsPanel.ActualWidth,
        notesWidth = UpdateNotesText.ActualWidth, navigationWidth = UpdateNavigationButton.ActualWidth,
        currentVersion = UpdateCurrentVersionText.Text
        , popupOpen = _updatePopupOpen
    };
}
