using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;
using System.Text.Json;

namespace NetSpeedWidget.Services;

public static class PackageVerificationService
{
    /// <summary>验证正式 EXE 的运行时和内嵌资源，不创建窗口、不注册自启或采集任务。</summary>
    public static void Verify()
    {
        using var helper = Assembly.GetExecutingAssembly().GetManifestResourceStream("NetSpeedWidget.Hardware.zip")
            ?? throw new InvalidOperationException("发布包缺少硬件采集资源。");
        using var archive = new ZipArchive(helper, ZipArchiveMode.Read);
        if (archive.GetEntry("NetSpeedWidget.Hardware.exe") is null || archive.GetEntry("coreclr.dll") is null)
            throw new InvalidOperationException("硬件采集程序或自包含运行时缺失。");
        if (!SystemStatusHttpServerService.CreateDashboardHtml().Contains("<html", StringComparison.Ordinal))
            throw new InvalidOperationException("仪表盘页面缺失。");
        var result = new
        {
            verifiedAt = DateTimeOffset.Now,
            executable = Environment.ProcessPath,
            runtime = Environment.Version.ToString(),
            administrator = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator),
            helperEntries = archive.Entries.Count,
            dashboardAvailable = true,
            dataDirectory = AppPaths.DataDirectory
        };
        File.WriteAllText(Path.Combine(AppPaths.ExecutableDirectory, "package-verification.json"),
            JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }), new System.Text.UTF8Encoding(false));
    }
}
