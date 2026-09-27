using System;
using System.IO;

namespace NetSpeedWidget.Services
{
    public static class AppLogService
    {
        private static readonly object WriteLock = new();
        private const long MaxLogBytes = 2 * 1024 * 1024;
        /// <summary>
        /// Writes diagnostic details for startup components that cannot report errors in UI.
        /// </summary>
        public static void Write(string message, Exception? exception = null)
        {
            try
            {
                var logPath = GetDefaultLogPath();
                var directory = Path.GetDirectoryName(logPath);
                var detail =
                    $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}{exception}{Environment.NewLine}";

                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                lock (WriteLock)
                {
                    if (File.Exists(logPath) && new FileInfo(logPath).Length > MaxLogBytes)
                        File.Move(logPath, logPath + ".previous", true);
                    File.AppendAllText(logPath, detail, new System.Text.UTF8Encoding(false));
                }
            }
            catch
            {
                // Logging must never affect app startup.
            }
        }

        public static string GetDefaultLogPath()
        {
            return Path.Combine(AppPaths.DataDirectory, "app.log");
        }
    }
}
