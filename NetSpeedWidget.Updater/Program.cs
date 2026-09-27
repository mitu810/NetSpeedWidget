using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NetSpeedWidget.Models;
using NetSpeedWidget.Services;

internal static class Program
{
    /// <summary>校验并准备更新，等待原进程退出后安装，最后重启原路径的程序。</summary>
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1) return 2;
        UpdateJob? job = null;
        bool ready = false;
        bool exited = false;
        var workspace = Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
        try
        {
            // 1. 再次校验载荷，准备完成之前不通知主程序退出。
            job = JsonSerializer.Deserialize<UpdateJob>(await File.ReadAllTextAsync(args[0])) ?? throw new InvalidDataException("更新任务为空。");
            if (!Path.IsPathFullyQualified(job.TargetDirectory) || !File.Exists(Path.Combine(job.TargetDirectory, "NetSpeedWidget.exe")))
                throw new InvalidDataException("找不到原程序目录。");
            if (File.Exists(Path.Combine(job.TargetDirectory, "installed.marker")) != job.Installed)
                throw new InvalidDataException("发行类型已改变，请重新检查更新。");
            using (var input = File.OpenRead(job.PackagePath))
                if (input.Length != job.Size || !Convert.ToHexString(SHA256.HashData(input)).Equals(job.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("更新包校验失败。");
            var staging = Path.Combine(workspace, "staging");
            var files = job.Installed ? null : PortableUpdate.Prepare(job.PackagePath, staging);
            if (!job.Installed)
            {
                var stagedVersion = FileVersionInfo.GetVersionInfo(Path.Combine(staging, "NetSpeedWidget.exe"));
                if ($"{stagedVersion.FileMajorPart}.{stagedVersion.FileMinorPart}.{stagedVersion.FileBuildPart}" != job.Version)
                    throw new InvalidDataException("便携包程序版本与 Release 不一致。");
                var probe = Path.Combine(job.TargetDirectory, ".update-write-" + Guid.NewGuid().ToString("N"));
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
            }
            using var parent = Process.GetProcessById(job.ProcessId);
            if (parent.StartTime.ToUniversalTime().Ticks != job.ProcessStartTicks ||
                !string.Equals(parent.MainModule?.FileName, Path.Combine(job.TargetDirectory, "NetSpeedWidget.exe"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("原程序身份不一致。");
            await File.WriteAllTextAsync(Path.Combine(workspace, "ready"), "ready");
            ready = true;
            // 2. 仅等待，不强制结束用户进程；超时不覆盖正在使用的程序。
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await parent.WaitForExitAsync(deadline.Token);
            exited = true;
            if (job.Installed)
            {
                var info = new ProcessStartInfo(job.PackagePath) { UseShellExecute = true };
                info.ArgumentList.Add("/DIR=" + job.TargetDirectory);
                info.ArgumentList.Add("/NORESTART");
                info.ArgumentList.Add("/UPDATEFROMAPP=1");
                using var installer = Process.Start(info) ?? throw new IOException("无法启动安装向导。");
                await installer.WaitForExitAsync();
                if (installer.ExitCode != 0) throw new IOException("安装已取消或失败，退出码：" + installer.ExitCode);
                var installedVersion = FileVersionInfo.GetVersionInfo(Path.Combine(job.TargetDirectory, "NetSpeedWidget.exe"));
                if ($"{installedVersion.FileMajorPart}.{installedVersion.FileMinorPart}.{installedVersion.FileBuildPart}" != job.Version)
                    throw new IOException("安装结束，但原目录程序版本未更新，请检查安装结果。");
            }
            else
            {
                using var progress = new UpdateProgressWindow();
                PortableUpdate.Apply(staging, job.TargetDirectory, files!, Path.Combine(workspace, "backup"), progress.Report);
            }
            await File.WriteAllTextAsync(job.ResultPath, "更新完成：v" + job.Version);
            return 0;
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync(Path.Combine(workspace, "error.txt"), error.ToString());
            if (job is not null)
                try { await File.WriteAllTextAsync(job.ResultPath, "更新未完成：" + error.Message + "\n工作目录：" + workspace); } catch { }
            return 1;
        }
        finally
        {
            // 3. 安装成功、取消或回滚后都恢复软件；准备阶段失败时原程序仍在运行。
            if (ready && exited && job is not null)
                Process.Start(new ProcessStartInfo(Path.Combine(job.TargetDirectory, "NetSpeedWidget.exe")) { UseShellExecute = true, WorkingDirectory = job.TargetDirectory });
        }
    }
}
