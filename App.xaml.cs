using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Security.Principal;
using System;

namespace NetSpeedWidget
{
    public partial class App : Application
    {
        private MainWindow? _window;

        public App()
        {
            this.InitializeComponent();
            // 记录未处理 UI 异常，保留失败上下文，不全局吞掉异常。
            UnhandledException += (_, args) => NetSpeedWidget.Services.AppLogService.Write("Unhandled UI exception: " + args.Message, args.Exception);
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--unregister-hardware") >= 0)
            {
                try
                {
                    var pipe = NetSpeedWidget.Services.HardwareSamplingProtocol.GetPipeName(WindowsIdentity.GetCurrent().User!.Value, NetSpeedWidget.Services.AppPaths.ExecutableDirectory);
                    NetSpeedWidget.Services.HardwareTaskService.Unregister(pipe);
                }
                catch (Exception ex) { NetSpeedWidget.Services.AppLogService.Write("Failed to remove hardware task.", ex); Environment.ExitCode = 1; }
                Exit();
                return;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-package") >= 0)
            {
                try { NetSpeedWidget.Services.PackageVerificationService.Verify(); }
                catch (Exception ex)
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(NetSpeedWidget.Services.AppPaths.ExecutableDirectory, "package-verification-error.txt"), ex.ToString());
                    Environment.ExitCode = 1;
                }
                Exit();
                return;
            }
            // 1. 在创建窗口前登记实例，重复启动转发给原窗口。
            var smokeKey = Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-update") >= 0 ? ".SmokeUpdate" :
                Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-dpi") >= 0 ? ".SmokeDpi" :
                Array.IndexOf(Environment.GetCommandLineArgs(), "--smoke-ui") >= 0 ? ".SmokeUi" : "";
            var instance = AppInstance.FindOrRegisterForKey("NetSpeedWidget." + WindowsIdentity.GetCurrent().User!.Value + smokeKey);
            if (!instance.IsCurrent)
            {
                await instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
                Exit();
                return;
            }
            instance.Activated += (_, _) => _window?.DispatcherQueue.TryEnqueue(() => _window.OpenSettingsWindow());
            // 2. 创建唯一的主窗口。
            _window = new MainWindow();
            _window.ActivateWidget();
        }
    }
}
