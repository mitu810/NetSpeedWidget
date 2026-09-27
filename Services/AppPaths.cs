using System;
using System.IO;

namespace NetSpeedWidget.Services;

public static class AppPaths
{
    /// <summary>获取用户实际启动的 EXE 目录，单文件依赖解压目录不作为配置位置。</summary>
    public static string ExecutableDirectory => Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>安装版与便携版都将配置、日志和应用缓存放在实际 EXE 目录。</summary>
    public static string DataDirectory => ExecutableDirectory;
    public static string CacheDirectory => Path.Combine(DataDirectory, "cache");

    /// <summary>仅在新位置没有配置时复制旧安装版配置，保留原件供回退。</summary>
    public static void MigrateLegacySettings(string target, string legacy)
    {
        var destination = Path.Combine(target, "settings.json");
        var source = Path.Combine(legacy, "settings.json");
        if (!File.Exists(destination) && File.Exists(source)) File.Copy(source, destination, false);
    }
}
