using NetSpeedWidget.Services;
using System;
using System.Threading.Tasks;

namespace NetSpeedWidget;

public sealed partial class MainWindow
{
    /// <summary>正常启动后后台检查一次，新版本出现才打开设置及内容弹窗。</summary>
    private async Task CheckStartupUpdateAsync()
    {
        try
        {
            await Task.Delay(2000);
            if (_isExiting || _isSmokeTest) return;
            using var updates = new UpdateService();
            var release = await updates.CheckAsync(UpdateService.CurrentVersion, UpdateService.IsInstalled, default);
            if (_isExiting || release is null) return;
            OpenSettingsWindow();
            if (_settingsWindow is not null) await _settingsWindow.ShowDiscoveredReleaseAsync(release);
        }
        catch (Exception error) { AppLogService.Write("Startup update check failed; widget continues running.", error); }
    }
}
