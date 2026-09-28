using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace NetSpeedWidget.Services;

public static class HardwareTaskService
{
    /// <summary>卸载时移除当前安装标识的采集任务，不操作其他安装或用户任务。</summary>
    public static void Unregister(string pipeName)
    {
        object? scheduler = null;
        try
        {
            scheduler = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
            dynamic service = scheduler!;
            service.Connect();
            dynamic folder = service.GetFolder("\\");
            dynamic task = folder.GetTask(pipeName);
            task.Stop(0);
            folder.DeleteTask(pipeName, 0);
        }
        catch (Exception ex) when ((ex is COMException or FileNotFoundException) &&
            ex.HResult == unchecked((int)0x80070002)) { }
        finally { if (scheduler is not null) Marshal.FinalReleaseComObject(scheduler); }
    }

    /// <summary>按安装标识查询或启动当前用户的硬件采集任务。</summary>
    public static bool TryRun(string pipeName)
    {
        object? scheduler = null;
        try
        {
            scheduler = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
            dynamic service = scheduler!;
            service.Connect();
            dynamic task = service.GetFolder("\\").GetTask(pipeName);
            if (!task.Enabled) return false;
            task.Run(null);
            return true;
        }
        // 任务不存在是首次授权的正常状态，COM 互操作会将该 HRESULT 转为 FileNotFoundException。
        catch (Exception ex) when (ex is COMException or FileNotFoundException or UnauthorizedAccessException)
        {
            if (ex.HResult != unchecked((int)0x80070002))
                AppLogService.Write("Failed to start existing hardware task.", ex);
            return false;
        }
        finally { if (scheduler is not null) Marshal.FinalReleaseComObject(scheduler); }
    }

    /// <summary>从已提升权限的采集程序注册登录任务，持久程序保存在管理员保护目录。</summary>
    public static void Install(string pipeName, string userSid)
    {
        // 1. 只允许同一用户提升权限，避免把任务注册到另一个账户。
        var identity = WindowsIdentity.GetCurrent();
        if (identity.User?.Value != userSid || !new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new UnauthorizedAccessException("硬件采集授权需由当前登录用户提升权限。");
        if (!pipeName.StartsWith("NetSpeedWidget.Hardware.", StringComparison.Ordinal) || pipeName.Length != 48)
            throw new ArgumentException("采集任务标识无效。");
        var source = AppContext.BaseDirectory;
        var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NetSpeedWidget", "Hardware", pipeName, new DirectoryInfo(source).Name);
        Directory.CreateDirectory(target);
        // 2. 将自包含辅助程序及依赖复制到受保护目录，计划任务不执行用户可改写的文件。
        var ready = Path.Combine(target, "ready.marker");
        if (!File.Exists(ready))
        {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
        File.WriteAllText(ready, "NetSpeedWidget hardware package installed.");
        }
        object? scheduler = null;
        try
        {
            // 3. 使用交互登录和最高可用权限，不保存密码、不使用 SYSTEM。
            scheduler = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
            dynamic service = scheduler!;
            service.Connect();
            dynamic definition = service.NewTask(0);
            definition.RegistrationInfo.Description = "NetSpeedWidget：仅通过当前用户本机管道提供硬件指标。";
            definition.Principal.UserId = userSid;
            definition.Principal.LogonType = 3;
            definition.Principal.RunLevel = 1;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = "PT0S";
            definition.Settings.MultipleInstances = 2;
            dynamic trigger = definition.Triggers.Create(9);
            trigger.UserId = userSid;
            trigger.Delay = "PT10S";
            dynamic action = definition.Actions.Create(0);
            action.Path = Path.Combine(target, "NetSpeedWidget.Hardware.exe");
            action.WorkingDirectory = target;
            action.Arguments = "--serve --pipe " + pipeName;
            dynamic task = service.GetFolder("\\").RegisterTaskDefinition(pipeName, definition, 6, userSid, null, 3, null);
            // 4. 重新授权可能替换旧版采集程序；先停掉旧实例，再启动新路径。
            task.Stop(0);
            task.Run(null);
        }
        finally { if (scheduler is not null) Marshal.FinalReleaseComObject(scheduler); }
    }
}
