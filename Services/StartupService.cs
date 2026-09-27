using Microsoft.Win32;
using System;

namespace NetSpeedWidget.Services
{
    public static class StartupService
    {
        private const string AppName = "NetSpeedWidget";
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// Checks whether startup is enabled for the current user.
        /// </summary>
        public static bool IsStartupEnabled()
        {
            using var key =
                Registry.CurrentUser.OpenSubKey(
                    RunKeyPath,
                    false);

            return key?.GetValue(AppName) is string value &&
                   string.Equals(value.Trim().Trim('"'), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Enables startup for the current user.
        /// </summary>
        public static void EnableStartup()
        {
            using var key =
                Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

            string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定启动程序路径。");
            if (key is null) throw new InvalidOperationException("无法创建当前用户启动项。");
            key.SetValue(AppName, QuoteExecutablePath(exePath));
        }

        /// <summary>将可执行文件路径作为完整命令引用，兼容包含空格的安装目录。</summary>
        public static string QuoteExecutablePath(string path) => "\"" + path + "\"";

        /// <summary>
        /// Disables startup for the current user.
        /// </summary>
        public static void DisableStartup()
        {
            using var key =
                Registry.CurrentUser.OpenSubKey(
                    RunKeyPath,
                    true);

            key?.DeleteValue(AppName, false);
        }
    }
}
