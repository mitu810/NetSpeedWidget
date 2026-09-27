using NetSpeedWidget.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace NetSpeedWidget.Services;

public static class UpdateLauncher
{
    public static string ResultPath => Path.Combine(AppPaths.DataDirectory, "update-result.txt");

    /// <summary>启动嵌入的普通权限更新程序，确认它已准备完成后再允许主程序退出。</summary>
    public static async Task PrepareAsync(UpdateRelease release, string package)
    {
        // 1. 从正式发行 EXE 提取辅助程序；开发构建不执行原地更新。
        var directory = Path.Combine(AppPaths.CacheDirectory, "updates", "jobs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var helper = Path.Combine(directory, "NetSpeedWidget.Updater.exe");
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("NetSpeedWidget.Updater.exe")
            ?? throw new InvalidOperationException("当前为开发构建，未包含更新程序。请使用正式安装版或便携版。");
        await using (var output = File.Create(helper)) await resource.CopyToAsync(output);
        using var current = Process.GetCurrentProcess();
        var job = new UpdateJob(current.Id, current.StartTime.ToUniversalTime().Ticks, AppPaths.ExecutableDirectory,
            package, release.Sha256, release.Size, release.Installed, release.Version.ToString(), ResultPath);
        var jobPath = Path.Combine(directory, "job.json");
        await File.WriteAllTextAsync(jobPath, JsonSerializer.Serialize(job));
        var info = new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(jobPath);
        using var process = Process.Start(info) ?? throw new IOException("无法启动更新程序。");
        // 2. 等待明确的准备信号，失败时保留主界面与本次错误。
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(Path.Combine(directory, "ready"))) return;
            if (process.HasExited)
                throw new IOException("更新准备失败：" + (File.Exists(Path.Combine(directory, "error.txt"))
                    ? await File.ReadAllTextAsync(Path.Combine(directory, "error.txt")) : "更新程序已退出。"));
            await Task.Delay(100);
        }
        throw new TimeoutException("更新准备超时，软件保持运行；请稍后重试。");
    }
}
