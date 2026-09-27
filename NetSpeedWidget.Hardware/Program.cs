using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using NetSpeedWidget.Services;

internal static class Program
{
    /// <summary>执行硬件授权或仅通过本机管道提供指标，进程不开放 HTTP 接口。</summary>
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var pipeIndex = Array.IndexOf(args, "--pipe");
            if (pipeIndex < 0 || pipeIndex + 1 >= args.Length) return 2;
            var pipeName = args[pipeIndex + 1];
            if (!pipeName.StartsWith("NetSpeedWidget.Hardware.", StringComparison.Ordinal) || pipeName.Length != 48) return 2;
            if (Array.IndexOf(args, "--authorize") >= 0)
            {
                var userIndex = Array.IndexOf(args, "--user");
                if (userIndex < 0 || userIndex + 1 >= args.Length) return 2;
                HardwareTaskService.Install(pipeName, args[userIndex + 1]);
                return 0;
            }
            if (Array.IndexOf(args, "--serve") < 0) return 2;
            // 1. 每份安装标识只启动一份采集进程。
            using var mutex = new Mutex(false, "Local\\" + pipeName, out var created);
            if (!created) return 0;
            {
                using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await pipe.ConnectAsync(timeout.Token);
                // 2. 周期发送硬件快照，主程序退出后管道断开，辅助进程自然退出。
                using var hardware = new HardwareStatusService();
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                do { await HardwareSamplingProtocol.WriteAsync(pipe, hardware.GetStatus(), CancellationToken.None); }
                while (await timer.WaitForNextTickAsync());
            }
            return 0;
        }
        catch (Exception ex) when (Array.IndexOf(args, "--serve") >= 0 &&
            ex is IOException or OperationCanceledException) { return 0; }
        catch (Exception ex)
        {
            AppLogService.Write("Hardware helper authorization or initialization failed.", ex);
            return 1;
        }
    }
}
