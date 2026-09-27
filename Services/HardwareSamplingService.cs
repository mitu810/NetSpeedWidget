using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using NetSpeedWidget.Models;

namespace NetSpeedWidget.Services;

public sealed class HardwareSamplingService : IDisposable
{
    private readonly string _userSid = WindowsIdentity.GetCurrent().User!.Value;
    private readonly string _pipeName;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _cancellation;
    private Task? _receiver;
    private HardwareStatusSnapshot _snapshot = HardwareStatusSnapshot.CreateUnavailable();
    private DateTime _lastReceivedUtc;
    public string State { get; private set; } = "尚未授权硬件采集";

    public HardwareSamplingService() => _pipeName = HardwareSamplingProtocol.GetPipeName(_userSid, AppPaths.ExecutableDirectory);

    /// <summary>启动当前用户专用管道并尝试运行已授权任务，启动时不会弹出 UAC。</summary>
    public void Start()
    {
        lock (_syncRoot)
        {
            if (_cancellation is not null) return;
            _cancellation = new CancellationTokenSource();
            _receiver = ReceiveAsync(_cancellation.Token);
        }
        State = HardwareTaskService.TryRun(_pipeName) ? "等待硬件采集连接" : "请点击授权硬件采集";
    }

    /// <summary>仅从用户主动点击的入口请求 UAC，注册受保护的采集程序和登录任务。</summary>
    public async Task AuthorizeAsync()
    {
        try
        {
            // 1. 初次没有任务时仍启动接收端；全部准备操作均在授权错误边界内。
            Start();
            State = "等待硬件采集授权";
            var helper = ExtractHelper();
            var start = new ProcessStartInfo(helper) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            start.ArgumentList.Add("--authorize");
            start.ArgumentList.Add("--pipe"); start.ArgumentList.Add(_pipeName);
            start.ArgumentList.Add("--user"); start.ArgumentList.Add(_userSid);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动硬件采集授权程序。");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new InvalidOperationException("授权失败：请使用当前登录用户的管理员权限。详见开发文档。");
            State = "已授权，等待采集连接";
        }
        catch (Exception ex)
        {
            State = ex is Win32Exception win32 && win32.NativeErrorCode == 1223 ? "已取消授权，网速仍正常运行" : ex.Message;
            AppLogService.Write("Hardware sampling authorization failed.", ex);
        }
    }

    /// <summary>获取最近的硬件快照，连接中断后不继续显示旧温度。</summary>
    public HardwareStatusSnapshot GetStatus()
    {
        lock (_syncRoot)
            return DateTime.UtcNow - _lastReceivedUtc < TimeSpan.FromSeconds(5) ? _snapshot : HardwareStatusSnapshot.CreateUnavailable();
    }

    /// <summary>停止管道接收，关闭管道后辅助进程自行退出。</summary>
    public void Stop()
    {
        lock (_syncRoot)
        {
            _cancellation?.Cancel();
            _cancellation = null;
            _lastReceivedUtc = default;
        }
    }

    public void Dispose() => Stop();

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        // 1. ACL 只授予当前 SID，允许同一用户的普通进程与提升进程通信。
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(_userSid), PipeAccessRights.FullControl, AccessControlType.Allow));
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var pipe = NamedPipeServerStreamAcl.Create(_pipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security);
                await pipe.WaitForConnectionAsync(cancellationToken);
                var peer = string.Empty;
                pipe.RunAsClient(() => peer = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty);
                if (peer != _userSid) continue;
                // 2. 接收有界快照；IPC 只传指标，不接受执行命令。
                while (!cancellationToken.IsCancellationRequested)
                {
                    var snapshot = await HardwareSamplingProtocol.ReadAsync(pipe, cancellationToken);
                    lock (_syncRoot) { _snapshot = snapshot; _lastReceivedUtc = DateTime.UtcNow; }
                    State = "硬件采集已连接";
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                State = "硬件采集连接中断";
                AppLogService.Write("Hardware sampling connection ended.", ex);
                try { await Task.Delay(1000, cancellationToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private static string ExtractHelper()
    {
        // 1. 单文件版将辅助程序完整自包含目录作为资源，按内容哈希解压到版本目录。
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("NetSpeedWidget.Hardware.zip");
        if (resource is null)
        {
            var adjacent = Path.Combine(AppPaths.ExecutableDirectory, "Hardware", "NetSpeedWidget.Hardware.exe");
            if (File.Exists(adjacent)) return adjacent;
            throw new FileNotFoundException("未找到硬件辅助程序，请使用正式发布包。");
        }
        var hash = Convert.ToHexString(SHA256.HashData(resource))[..16];
        resource.Position = 0;
        var directory = Path.Combine(AppPaths.CacheDirectory, "HelperPackages", hash);
        Directory.CreateDirectory(directory);
        // 2. 每次授权都覆盖资源内容，避免使用不完整的旧解压结果。
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read);
        archive.ExtractToDirectory(directory, true);
        return Path.Combine(directory, "NetSpeedWidget.Hardware.exe");
    }
}
