using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSpeedWidget.Models;
using NetSpeedWidget.Services;

namespace NetSpeedWidget.Tests;

internal static class StabilityTests
{
    /// <summary>验证请求边界、屏幕增删和采集缓存，测试不注册启动项或计划任务。</summary>
    public static async Task RunAsync()
    {
        foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            Check(WindowDpiService.ToPixels(640, scale) == 640 * scale, "设置宽度转换保持逻辑尺寸");
            Check(WindowDpiService.ToPixels(168, scale) == 168 * scale, "悬浮网速窗口宽度转换保持逻辑尺寸");
            Check(WindowDpiService.ToPixels(32, scale) == 32 * scale, "窗口高度与圆角区域使用同一比例");
        }
        Check(WindowDpiService.ToPixels(590, 1.25) == 738, "非整数像素尺寸按统一规则舍入");
        var missingTask = HardwareSamplingProtocol.GetPipeName("missing-test", Guid.NewGuid().ToString());
        Check(!HardwareTaskService.TryRun(missingTask), "首次授权时任务不存在应返回未授权而非抛出异常");
        HardwareTaskService.Unregister(missingTask);
        Check(!File.Exists(Path.Combine(AppPaths.ExecutableDirectory, "Hardware", "NetSpeedWidget.Hardware.exe")), "测试目录不能包含会弹出 UAC 的辅助程序");
        using (var hardwareSampling = new HardwareSamplingService())
        {
            hardwareSampling.Start();
            await hardwareSampling.AuthorizeAsync();
            Check(hardwareSampling.State.Contains("未找到硬件辅助程序", StringComparison.Ordinal), "授权失败返回明确状态且不抛出 UI 异常");
            Check(hardwareSampling.GetStatus().Equals(HardwareStatusSnapshot.CreateUnavailable()), "授权失败后硬件指标不可用");
        }
        // 1. 中文、Emoji 正文跨任意 TCP 字节边界仍完整解码。
        const string body = "{\"password\":\"中文密码🔐\"}";
        var bytes = Encoding.UTF8.GetBytes(body);
        var request = Encoding.ASCII.GetBytes($"POST /api/auth/login HTTP/1.1\r\nContent-Length: {bytes.Length}\r\n\r\n").Concat(bytes).ToArray();
        using (var fragmented = new FragmentedStream(request))
            Check((await HttpRequestReader.ReadAsync(fragmented, default)).Body == body, "UTF-8 分段读取");
        await RejectAsync("POST / HTTP/1.1\r\nContent-Length: 65537\r\n\r\n", 413);
        await RejectAsync("POST / HTTP/1.1\r\nContent-Length: -1\r\n\r\n", 400);
        await RejectAsync("POST / HTTP/1.1\r\nContent-Length: 1\r\nContent-Length: 2\r\n\r\n", 400);
        await RejectAsync("POST / HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n", 400);
        await RejectAsync("POST / HTTP/1.1\r\nContent-Length: 4\r\n\r\nx", 400);
        await RejectAsync("GET / HTTP/1.1\r\nX-Large: " + new string('x', 17000) + "\r\n\r\n", 431);
        using (var stalled = new StalledStream())
        using (var cancellation = new CancellationTokenSource(30))
        {
            try { await HttpRequestReader.ReadAsync(stalled, cancellation.Token); throw new Exception("读取未取消"); }
            catch (OperationCanceledException) { }
        }
        // 2. 新屏幕插入、排序变化、删除和局部恢复都不替换健康屏幕窗口。
        using (var registry = new TaskbarOverlayRegistry<FakeWindow>(_ => new FakeWindow()))
        {
            var first = registry.Synchronize(new IntPtr[] { new(1), new(2) }).ToArray();
            var expanded = registry.Synchronize(new IntPtr[] { new(3), new(2), new(1) });
            Check(ReferenceEquals(expanded[1], first[1]) && ReferenceEquals(expanded[2], first[0]), "屏幕排序保留窗口");
            registry.Synchronize(new IntPtr[] { new(2), new(3) });
            Check(first[0].Disposed && !first[1].Disposed, "仅释放移除屏幕");
            registry.Recreate(new IntPtr(3));
            Check(!first[1].Disposed, "局部恢复不重建其他屏幕");
        }
        // 3. 本机 IPC 按字节限制消息大小并支持分段数据。
        var snapshot = HardwareStatusSnapshot.CreateUnavailable();
        using (var message = new MemoryStream())
        {
            await HardwareSamplingProtocol.WriteAsync(message, snapshot, default);
            using var fragmented = new FragmentedStream(message.ToArray());
            Check((await HardwareSamplingProtocol.ReadAsync(fragmented, default)).Equals(snapshot), "硬件快照通信");
        }
        using (var oversized = new MemoryStream(BitConverter.GetBytes(4097)))
        {
            try { await HardwareSamplingProtocol.ReadAsync(oversized, default); throw new Exception("IPC 超限未拒绝"); }
            catch (InvalidDataException) { }
        }
        Check(StartupService.QuoteExecutablePath(@"C:\Program Files\Widget\NetSpeedWidget.exe") == "\"C:\\Program Files\\Widget\\NetSpeedWidget.exe\"", "自启路径引用");
        Check(HardwareSamplingProtocol.GetPipeName("S-1", @"C:\One") != HardwareSamplingProtocol.GetPipeName("S-2", @"C:\One"), "用户管道隔离");
        // 4. 多客户端共享一次采样，释放后不继续访问硬件。
        var collections = 0;
        using (var collector = new SystemStatusCollectorService(() => { Interlocked.Increment(ref collections); return snapshot; }))
        {
            var samples = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => collector.CollectAsync()));
            Check(collections == 1 && samples.All(value => value.Timestamp == samples[0].Timestamp), "多客户端快照缓存");
            collector.Dispose();
            try { await collector.CollectAsync(); throw new Exception("释放后仍采样"); }
            catch (ObjectDisposedException) { }
        }
        var firstHash = PasswordHashService.Create("same-password");
        Check(firstHash != PasswordHashService.Create("same-password"), "每次密码生成独立随机盐");
        var legacy = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("legacy")));
        Check(PasswordHashService.Verify("legacy", legacy) && !PasswordHashService.Verify("wrong", legacy), "旧密码配置兼容");
        Check(!PasswordHashService.Verify("test", "PBKDF2-SHA256$9999999999$x$x"), "异常迭代数拒绝");
        var settingsFile = Path.Combine(Path.GetTempPath(), "NetSpeedWidget-merge-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var settingsService = new SettingsService(settingsFile);
            var baseline = new AppSettings();
            settingsService.Save(baseline);
            var webEdit = SettingsService.Clone(baseline);
            webEdit.SystemStatus.WebTheme = SystemStatusWebTheme.Light;
            settingsService.MergeAndSave(webEdit, baseline);
            var windowEdit = SettingsService.Clone(baseline);
            windowEdit.SpeedFontSize = 16;
            var merged = settingsService.MergeAndSave(windowEdit, baseline);
            Check(merged.SpeedFontSize == 16 && merged.SystemStatus.WebTheme == SystemStatusWebTheme.Light, "窗口保存不覆盖网页设置");
        }
        finally { if (File.Exists(settingsFile)) File.Delete(settingsFile); }
        await VerifyHttpLoginAsync(body);
    }

    private static async Task VerifyHttpLoginAsync(string body)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), "NetSpeedWidget-test-" + Guid.NewGuid().ToString("N") + ".json");
        using var server = new SystemStatusHttpServerService(new SystemStatusCollectorService(() => HardwareStatusSnapshot.CreateUnavailable()), new SettingsService(settingsPath));
        server.ApplySettings(new AppSettings { SystemStatus = new SystemStatusSettings { Enabled = true, ServerEnabled = true, PreferredPort = 49180, PasswordEnabled = true, PasswordHash = SystemStatusHttpServerService.CreatePasswordHash("中文密码🔐") } });
        Check(server.IsRunning, "测试 HTTP 服务启动");
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.ActualPort);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes($"POST /api/auth/login HTTP/1.1\r\nHost: localhost\r\nContent-Length: {bodyBytes.Length}\r\n\r\n"));
        await client.GetStream().WriteAsync(bodyBytes);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
        var response = await reader.ReadToEndAsync(timeout.Token);
        Check(response.StartsWith("HTTP/1.1 200", StringComparison.Ordinal) && response.Contains("token"), "真实 TCP 中文密码登录");
    }

    private static async Task RejectAsync(string request, int status)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(request));
        try { await HttpRequestReader.ReadAsync(stream, default); throw new Exception("非法请求未拒绝"); }
        catch (LocalHttpRequestException ex) { Check(ex.StatusCode == status, "HTTP 请求错误状态"); }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("行为测试失败：" + name);
    }

    private sealed class FakeWindow : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class FragmentedStream(byte[] data) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(2, buffer.Length)], cancellationToken);
    }

    private sealed class StalledStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }
}
