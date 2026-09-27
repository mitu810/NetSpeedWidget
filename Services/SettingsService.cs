using NetSpeedWidget.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;

namespace NetSpeedWidget.Services
{
    public readonly record struct ThemePalette(
        string BackgroundColor,
        string ForegroundColor);

    public class SettingsService
    {
        private const int MinTaskbarEdgePadding = 0;
        private const int MaxTaskbarEdgePadding = 200;
        private const int MinSpeedFontSize = 10;
        private const int MaxSpeedFontSize = 18;
        private const int MinSystemStatusPort = 1;
        private const int MaxSystemStatusPort = 65535;

        private static readonly int[] AllowedSystemStatusRefreshIntervals = { 1, 2, 3, 5 };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        static SettingsService()
        {
            JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        private static readonly object SaveLock = new();
        private readonly string _settingsPath;

        public SettingsService()
            : this(GetDefaultSettingsPath())
        {
            if (!File.Exists(Path.Combine(AppPaths.ExecutableDirectory, "installed.marker"))) return;
            try
            {
                AppPaths.MigrateLegacySettings(AppPaths.ExecutableDirectory,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetSpeedWidget"));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                AppLogService.Write("Legacy settings migration failed; original preserved.", error);
            }
        }

        public SettingsService(string settingsPath)
        {
            _settingsPath = settingsPath;
        }

        /// <summary>
        /// Loads app settings from disk, or returns defaults when unavailable.
        /// </summary>
        public AppSettings Load()
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

                return Normalize(settings ?? new AppSettings());
            }
            catch (JsonException)
            {
                return new AppSettings();
            }
            catch (IOException)
            {
                return new AppSettings();
            }
        }

        /// <summary>
        /// Saves app settings to disk.
        /// </summary>
        public void Save(AppSettings settings)
        {
            var directory = Path.GetDirectoryName(_settingsPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 1. 写入同目录临时文件，再替换完整配置，避免中断留下半份 JSON。
            lock (SaveLock)
            {
                var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
                var temporaryPath = _settingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporaryPath, json, new System.Text.UTF8Encoding(false));
                    File.Move(temporaryPath, _settingsPath, true);
                }
                finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            }
        }

        /// <summary>复制设置，避免 UI 与 HTTP 在不同线程修改同一对象。</summary>
        public static AppSettings Clone(AppSettings settings) =>
            JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, JsonOptions), JsonOptions)!;

        /// <summary>仅合并本次编辑字段，保留网页和设置窗口各自的其他修改。</summary>
        public AppSettings MergeAndSave(AppSettings edited, AppSettings baseline)
        {
            // 1. 在持久化锁中读取最新配置。
            lock (SaveLock)
            {
                var latest = JsonSerializer.SerializeToNode(Load(), JsonOptions)!.AsObject();
                var before = JsonSerializer.SerializeToNode(baseline, JsonOptions)!.AsObject();
                var after = JsonSerializer.SerializeToNode(edited, JsonOptions)!.AsObject();
                // 2. 只覆盖与基线不同的字段，再原子保存。
                MergeChanged(latest, before, after);
                var result = latest.Deserialize<AppSettings>(JsonOptions)!;
                Save(result);
                return result;
            }
        }

        private static void MergeChanged(JsonObject latest, JsonObject before, JsonObject after)
        {
            foreach (var property in after)
            {
                var previous = before[property.Key];
                if (JsonNode.DeepEquals(previous, property.Value)) continue;
                if (property.Value is JsonObject afterObject && previous is JsonObject beforeObject &&
                    latest[property.Key] is JsonObject latestObject)
                    MergeChanged(latestObject, beforeObject, afterObject);
                else latest[property.Key] = property.Value?.DeepClone();
            }
        }

        /// <summary>
        /// Gets display colors for a theme mode.
        /// </summary>
        public static ThemePalette GetThemePalette(AppThemeMode theme)
        {
            return theme switch
            {
                AppThemeMode.Light => new ThemePalette("#F2F2F2", "#202020"),
                _ => new ThemePalette("#202020", "#FFFFFF")
            };
        }

        public static string GetDefaultSettingsPath()
        {
            return Path.Combine(AppPaths.DataDirectory, "settings.json");
        }

        private static AppSettings Normalize(AppSettings settings)
        {
            settings.TaskbarEdgePadding =
                Math.Clamp(
                    settings.TaskbarEdgePadding,
                    MinTaskbarEdgePadding,
                    MaxTaskbarEdgePadding);

            settings.SpeedFontSize =
                Math.Clamp(
                    settings.SpeedFontSize,
                    MinSpeedFontSize,
                    MaxSpeedFontSize);

            settings.SystemStatus = NormalizeSystemStatusSettings(settings.SystemStatus);

            return settings;
        }

        private static SystemStatusSettings NormalizeSystemStatusSettings(
            SystemStatusSettings? settings)
        {
            settings ??= new SystemStatusSettings();

            settings.PreferredPort =
                Math.Clamp(
                    settings.PreferredPort,
                    MinSystemStatusPort,
                    MaxSystemStatusPort);

            if (Array.IndexOf(
                    AllowedSystemStatusRefreshIntervals,
                    settings.RefreshIntervalSeconds) < 0)
            {
                settings.RefreshIntervalSeconds =
                    settings.RefreshIntervalSeconds <= 1
                        ? 1
                        : settings.RefreshIntervalSeconds <= 2
                            ? 2
                            : settings.RefreshIntervalSeconds <= 3
                                ? 3
                                : 5;
            }

            settings.Cards ??= new SystemStatusCardSettings();

            if (settings.PasswordEnabled &&
                string.IsNullOrWhiteSpace(settings.PasswordHash))
            {
                settings.PasswordEnabled = false;
            }

            if (settings.Layout is null ||
                settings.Layout.Count == 0)
            {
                settings.Layout = new SystemStatusSettings().Layout;
            }

            return settings;
        }
    }
}
