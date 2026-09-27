using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetSpeedWidget.Models;
using NetSpeedWidget.Services;

namespace NetSpeedWidget.Tests;

internal static class UpdateTests
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "NetSpeedWidget-UpdateTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var bytes = Encoding.UTF8.GetBytes("download-fixture");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var name = "NetSpeedWidget-v2.10.0-win-x64-Portable.zip";
        var url = $"https://github.com/{UpdateService.Repository}/releases/download/v2.10.0/{name}";
        var metadata = JsonSerializer.Serialize(new
        {
            tag_name = "v2.10.0", draft = false, prerelease = false, body = "## 问题修复\n- 示例", published_at = "2026-09-27T00:00:00Z",
            assets = new[] { new { name, browser_download_url = url, size = bytes.Length, digest = "sha256:" + hash } }
        });
        int downloads = 0;
        using (var service = new UpdateService(new Handler(request => request.RequestUri!.Host == "api.github.com"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(metadata) }
            : DownloadResponse())))
        {
            var release = await service.CheckAsync(new Version(2, 9, 0), false, CancellationToken.None);
            Require(release?.Version == new Version(2, 10, 0), "数字版本比较失败");
            Require(release!.Notes.Contains("问题修复"), "更新内容丢失");
            Require(await service.CheckAsync(new Version(2, 10, 0), false, CancellationToken.None) is null, "重复更新");
            await RejectAsync(() => service.CheckAsync(new Version(2, 0, 0), true, CancellationToken.None));
            var path = await service.DownloadAsync(release, Path.Combine(root, "good"), null, CancellationToken.None);
            Require(File.ReadAllBytes(path).Length == bytes.Length, "下载失败");
            await service.DownloadAsync(release, Path.Combine(root, "good"), null, CancellationToken.None);
            Require(downloads == 1, "已下载的相同更新包被重复下载");
            using (var reopened = new UpdateService(new Handler(_ => throw new Exception("已有缓存不应请求网络"))))
                Require(await reopened.DownloadAsync(release, Path.Combine(root, "good"), null, default) == path, "重新打开后没有复用缓存");
            File.WriteAllText(path, "corrupted-cache");
            await service.DownloadAsync(release, Path.Combine(root, "good"), null, default);
            Require(downloads == 2, "损坏缓存未重新下载");
            await RejectAsync(() => service.DownloadAsync(release with { Sha256 = new string('0', 64) }, Path.Combine(root, "bad"), null, CancellationToken.None));
            Require(!File.Exists(Path.Combine(root, "bad", name)), "损坏包未清理");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await RejectAsync(() => service.DownloadAsync(release, Path.Combine(root, "cancel"), null, cancelled.Token));
        }
        HttpResponseMessage DownloadResponse()
        {
            downloads++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }
        using (var limited = new UpdateService(new Handler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden))))
            await RejectAsync(() => limited.CheckAsync(new Version(2, 0, 0), false, CancellationToken.None));
        Reject(() => UpdateService.ValidateAssetUrl(url.Replace("mitu810", "other"), "v2.10.0", name));
        foreach (var malicious in new[] { "../escape.exe", "settings.json", "installed.marker", "licenses/../../escape.txt", "/evil.txt" })
        {
            var zip = Path.Combine(root, Guid.NewGuid() + ".zip");
            MakeZip(zip, malicious);
            Reject(() => PortableUpdate.Prepare(zip, Path.Combine(root, Guid.NewGuid().ToString())));
        }
        var duplicate = Path.Combine(root, "duplicate.zip");
        MakeZip(duplicate, "NetSpeedWidget.exe", "netspeedwidget.exe");
        Reject(() => PortableUpdate.Prepare(duplicate, Path.Combine(root, "duplicates")));
        var target = Path.Combine(root, "target");
        var staging = Path.Combine(root, "staging");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(target, "settings.json"), "private-settings");
        File.WriteAllText(Path.Combine(target, "LICENSE"), "old-license");
        File.WriteAllText(Path.Combine(target, "NetSpeedWidget.exe"), "old-app");
        File.WriteAllText(Path.Combine(staging, "LICENSE"), "new-license");
        File.WriteAllText(Path.Combine(staging, "NetSpeedWidget.exe"), "new-app");
        // 模拟最后一步失败：EXE 被其他进程持有，前面已更新的许可证应被恢复。
        using (var locked = new FileStream(Path.Combine(target, "NetSpeedWidget.exe"), FileMode.Open, FileAccess.Read, FileShare.Read))
            Reject(() => PortableUpdate.Apply(staging, target, new[] { "LICENSE", "NetSpeedWidget.exe" }, Path.Combine(root, "backup-failed")));
        Require(File.ReadAllText(Path.Combine(target, "LICENSE")) == "old-license", "失败回滚未恢复许可证");
        Require(File.ReadAllText(Path.Combine(target, "NetSpeedWidget.exe")) == "old-app", "失败改变旧程序");
        PortableUpdate.Apply(staging, target, new[] { "LICENSE", "NetSpeedWidget.exe" }, Path.Combine(root, "backup-ok"));
        Require(File.ReadAllText(Path.Combine(target, "NetSpeedWidget.exe")) == "new-app", "替换失败");
        Require(File.ReadAllText(Path.Combine(target, "settings.json")) == "private-settings", "覆盖用户配置");
        var legacy = Path.Combine(root, "legacy");
        var migrated = Path.Combine(root, "migrated");
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(migrated);
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "legacy-config");
        AppPaths.MigrateLegacySettings(migrated, legacy);
        Require(File.ReadAllText(Path.Combine(migrated, "settings.json")) == "legacy-config", "旧位置配置未迁移");
        File.WriteAllText(Path.Combine(migrated, "settings.json"), "current-config");
        AppPaths.MigrateLegacySettings(migrated, legacy);
        Require(File.ReadAllText(Path.Combine(migrated, "settings.json")) == "current-config", "迁移覆盖已有配置");
        Require(File.Exists(Path.Combine(legacy, "settings.json")), "旧配置原件被删除");
        Console.WriteLine("Update tests passed (versions, metadata, checksum, cancel, ZIP safety, rollback, config preservation).");
    }

    private static void MakeZip(string path, params string[] names)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var name in names) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write("fixture"); }
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception) { return; }
        throw new Exception("预期失败却成功");
    }
    private static async Task RejectAsync(Func<Task> action)
    {
        try { await action(); } catch (Exception) { return; }
        throw new Exception("预期失败却成功");
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(callback(request));
        }
    }
}
