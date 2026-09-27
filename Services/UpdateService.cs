using NetSpeedWidget.Models;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NetSpeedWidget.Services;

public sealed class UpdateService : IDisposable
{
    public const string Repository = "mitu810/NetSpeedWidget";
    private const long MaxPackageSize = 512L * 1024 * 1024;
    private readonly HttpClient _client;
    public static Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version is { } v
        ? new Version(v.Major, v.Minor, v.Build) : new Version(0, 0, 0);
    public static bool IsInstalled => File.Exists(Path.Combine(AppPaths.ExecutableDirectory, "installed.marker"));

    public UpdateService(HttpMessageHandler? handler = null)
    {
        _client = handler is null ? new HttpClient() : new HttpClient(handler);
        _client.Timeout = TimeSpan.FromMinutes(15);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("NetSpeedWidget/" + CurrentVersion);
    }

    /// <summary>读取公开稳定 Release，按数字版本比较并选取当前发行类型的附件。</summary>
    public async Task<UpdateRelease?> CheckAsync(Version current, bool installed, CancellationToken cancellationToken)
    {
        // 1. 查询稳定版本；元数据请求单独限制时间，不需要 GitHub 令牌。
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await _client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", deadline.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidOperationException("仓库暂时没有公开的稳定版本。");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("GitHub 请求频率受限，请稍后重试。");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(deadline.Token);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$") || !Version.TryParse(tag[1..], out var version))
            throw new InvalidDataException("Release 版本号格式不正确，应为 v主版本.次版本.修订号。");
        if (version <= current) return null;
        // 2. 仅使用本仓库固定命名的 x64 资产，避免误下载源码或其他平台附件。
        var name = $"NetSpeedWidget-{tag}-win-x64-" + (installed ? "Setup.exe" : "Portable.zip");
        JsonElement? selected = null;
        JsonElement? sums = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() == name) selected = asset;
            if (asset.GetProperty("name").GetString() == "SHA256SUMS.txt") sums = asset;
        }
        if (selected is not { } file) throw new InvalidDataException("新版本缺少当前发行类型的 x64 下载包。");
        var url = ValidateAssetUrl(file.GetProperty("browser_download_url").GetString()!, tag, name);
        var size = file.GetProperty("size").GetInt64();
        if (size <= 0 || size > MaxPackageSize) throw new InvalidDataException("下载包大小超出允许范围。");
        var digest = file.TryGetProperty("digest", out var value) ? value.GetString() : null;
        var hash = digest?.StartsWith("sha256:", StringComparison.Ordinal) == true ? digest[7..] : "";
        if (!Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$") && sums is { } checksum)
        {
            var sumUrl = ValidateAssetUrl(checksum.GetProperty("browser_download_url").GetString()!, tag, "SHA256SUMS.txt");
            if (checksum.GetProperty("size").GetInt64() > 16384) throw new InvalidDataException("校验文件过大。");
            var content = await _client.GetStringAsync(sumUrl, deadline.Token);
            foreach (var line in content.Split('\n'))
            {
                var match = Regex.Match(line.Trim(), @"^([a-fA-F0-9]{64})\s+\*?(.+)$");
                if (match.Success && match.Groups[2].Value == name) hash = match.Groups[1].Value;
            }
        }
        if (!Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("新版本缺少有效 SHA-256，已停止更新。");
        return new UpdateRelease(version, tag, root.GetProperty("body").GetString() ?? "此版本未提供更新内容。",
            root.GetProperty("published_at").GetDateTimeOffset(), url, name, size, hash, installed);
    }

    /// <summary>流式下载并校验长度、哈希，取消或失败时清理本次未完成文件。</summary>
    public async Task<string> DownloadAsync(UpdateRelease release, string directory, IProgress<double>? progress, CancellationToken token)
    {
        ValidateAssetUrl(release.DownloadUrl.AbsoluteUri, release.Tag, release.FileName);
        if (release.Size <= 0 || release.Size > MaxPackageSize) throw new InvalidDataException("下载包大小不正确。");
        if (await UpdateCache.FindAsync(release, directory, token) is { } cached) { progress?.Report(100); return cached; }
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, release.FileName);
        var partial = path + ".partial-" + Guid.NewGuid().ToString("N");
        bool created = false;
        try
        {
            // 1. 分块写入临时文件，避免将整包加载到内存。
            using var response = await _client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } length && length != release.Size)
                throw new InvalidDataException("下载文件长度与 Release 不一致。");
            await using var input = await response.Content.ReadAsStreamAsync(token);
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                created = true;
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += read;
                    if (total > release.Size) throw new InvalidDataException("下载文件超出声明大小。");
                    hasher.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), token);
                    progress?.Report(total * 100.0 / release.Size);
                }
                // 2. 校验全部字节；校验成功才允许交给更新程序。
                if (total != release.Size || !Convert.ToHexString(hasher.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("下载包完整性校验失败，请重新检查更新。");
            }
            File.Move(partial, path, true);
            return path;
        }
        catch { if (created && File.Exists(partial)) File.Delete(partial); throw; }
    }

    public static Uri ValidateAssetUrl(string text, string tag, string name)
    {
        var expected = $"https://github.com/{Repository}/releases/download/{tag}/{name}";
        if (!string.Equals(text, expected, StringComparison.Ordinal)) throw new InvalidDataException("下载地址不是本项目的 Release 附件。");
        return new Uri(expected);
    }

    public void Dispose() => _client.Dispose();
}
