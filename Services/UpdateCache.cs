using NetSpeedWidget.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace NetSpeedWidget.Services;

public static class UpdateCache
{
    public static string GetDirectory(UpdateRelease release) => Path.Combine(AppPaths.CacheDirectory, "updates", release.Tag,
        release.Installed ? "installed" : "portable");

    /// <summary>按版本、发行类型、长度和 SHA-256 复用已完成下载，损坏缓存不会用于安装。</summary>
    public static async Task<string?> FindAsync(UpdateRelease release, string directory, CancellationToken token)
    {
        var path = Path.Combine(directory, release.FileName);
        if (!File.Exists(path) || new FileInfo(path).Length != release.Size) return null;
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hash = await SHA256.HashDataAsync(input, token);
        return Convert.ToHexString(hash).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}
