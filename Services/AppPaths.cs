using System;
using System.IO;

namespace NetSpeedWidget.Services;

public static class AppPaths
{
    /// <summary>获取用户实际启动的 EXE 目录，单文件依赖解压目录不作为配置位置。</summary>
    public static string ExecutableDirectory => Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>安装版写入当前用户目录，便携版保留 EXE 旁的配置位置。</summary>
    public static string DataDirectory => File.Exists(Path.Combine(ExecutableDirectory, "installed.marker"))
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetSpeedWidget")
        : ExecutableDirectory;
}
