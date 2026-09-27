using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NetSpeedWidget.Services;

public static class PortableUpdate
{
    /// <summary>解压发行白名单，拒绝目录穿越、重复路径、配置覆盖和超大内容。</summary>
    public static IReadOnlyList<string> Prepare(string archive, string staging)
    {
        Directory.CreateDirectory(staging);
        using var zip = ZipFile.OpenRead(archive);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (entry.Name.Length == 0) continue;
            if (name.Split('/').Any(p => p is "" or "." or ".." || p.Contains(':')) || name.StartsWith('/') || !names.Add(name))
                throw new InvalidDataException("更新包存在非法或重复路径。");
            if (!(name is "NetSpeedWidget.exe" or "LICENSE" or "THIRD_PARTY_NOTICES.md" or "使用说明.md" ||
                name.StartsWith("licenses/", StringComparison.Ordinal) && name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("更新包包含非发行文件：" + name);
            total += entry.Length;
            if (names.Count > 64 || total > 512L * 1024 * 1024) throw new InvalidDataException("解压内容超出允许范围。");
            var path = Path.Combine(staging, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, false);
        }
        if (!names.Contains("NetSpeedWidget.exe")) throw new InvalidDataException("更新包缺少主程序。");
        return names.OrderBy(n => n == "NetSpeedWidget.exe" ? 1 : 0).ToArray();
    }

    /// <summary>先备份再替换白名单文件；替换失败时恢复，备份保留供人工恢复。</summary>
    public static void Apply(string staging, string target, IReadOnlyList<string> names, string backup, Action<double>? progress = null)
    {
        // 1. 验证目标父路径，拒绝符号链接或目录联接造成的目录逃逸。
        foreach (var name in names)
        {
            var path = Path.GetFullPath(Path.Combine(target, name));
            if (!path.StartsWith(Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("更新目标越过程序目录。");
            for (var cursor = Path.GetDirectoryName(path); cursor is not null; cursor = Path.GetDirectoryName(cursor))
                if (Directory.Exists(cursor) && File.GetAttributes(cursor).HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException("更新目录包含符号链接或目录联接，请手动更新。");
            if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("更新目标包含符号链接，请手动更新。");
        }
        var changed = new List<(string Name, bool Existed)>();
        var totalBytes = names.Sum(name => new FileInfo(Path.Combine(staging, name)).Length);
        long copiedBytes = 0;
        try
        {
            // 2. 保留旧版本；不触碰 settings.json、日志、安装标记或其他用户文件。
            foreach (var name in names)
            {
                var destination = Path.Combine(target, name);
                var saved = Path.Combine(backup, name);
                var existed = File.Exists(destination);
                if (existed) { Directory.CreateDirectory(Path.GetDirectoryName(saved)!); File.Copy(destination, saved, false); }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                changed.Add((name, existed));
                using var input = File.OpenRead(Path.Combine(staging, name));
                using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    copiedBytes += read;
                    progress?.Invoke(totalBytes == 0 ? 100 : copiedBytes * 100.0 / totalBytes);
                }
            }
        }
        catch (Exception failure)
        {
            // 3. 尽可能恢复全部已修改文件，任何恢复失败都保留原始错误和备份。
            var errors = new List<Exception> { failure };
            foreach (var change in changed.AsEnumerable().Reverse())
            {
                try
                {
                    var destination = Path.Combine(target, change.Name);
                    if (change.Existed) File.Copy(Path.Combine(backup, change.Name), destination, true);
                    else if (File.Exists(destination)) File.Delete(destination);
                }
                catch (Exception error) { errors.Add(error); }
            }
            throw new AggregateException("更新失败，已尝试恢复旧文件。备份：" + backup, errors);
        }
    }
}
