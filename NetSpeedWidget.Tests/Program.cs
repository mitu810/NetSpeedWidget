using System;
using System.Collections.Generic;
using System.IO;
using NetSpeedWidget.Models;
using NetSpeedWidget.Services;

namespace NetSpeedWidget.Tests
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--check-release")
            {
                using var updater = new UpdateService();
                var release = updater.CheckAsync(Version.Parse(args[1]), Array.Exists(args, arg => arg == "--installed"), default).GetAwaiter().GetResult();
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(release));
                if (args.Length >= 4 && args[2] == "--download" && release is not null)
                    Console.WriteLine(updater.DownloadAsync(release, args[3], null, default).GetAwaiter().GetResult());
                return;
            }
            if (Array.Exists(
                args,
                argument => string.Equals(argument, "--hardware-diagnostics", StringComparison.OrdinalIgnoreCase)))
            {
                using var hardwareStatusService = new HardwareStatusService();
                Console.WriteLine(hardwareStatusService.GetDiagnosticsReport());
                return;
            }

            UpdateTests.RunAsync().GetAwaiter().GetResult();
            StabilityTests.RunAsync().GetAwaiter().GetResult();
            TestFirstSnapshotReturnsZeroSpeed();
            TestSecondSnapshotCalculatesDownloadAndUploadSpeed();
            TestSnapshotSumsMultipleInterfaces();
            TestFormatSpeedUsesLargerUnits();
            TestSettingsServiceReturnsDefaultsWhenFileDoesNotExist();
            TestSettingsServiceReturnsDefaultsWhenFileIsInvalid();
            TestSettingsServiceLoadsDocumentedCamelCaseSettings();
            TestSettingsServiceSavesAndLoadsSettings();
            TestSettingsServiceLoadsLockWindowPosition();
            TestSettingsServiceLoadsTaskbarEdgePadding();
            TestSettingsServiceClampsTaskbarEdgePadding();
            TestSettingsServiceLoadsSpeedFontSize();
            TestSettingsServiceClampsSpeedFontSize();
            TestSettingsServiceDefaultsSystemStatusSettings();
            TestSettingsServiceLoadsSystemStatusSettings();
            TestSettingsServiceNormalizesSystemStatusSettings();
            TestSettingsServiceSavesSystemStatusSettingsAsCamelCase();
            TestSystemMetricValueCreatesAvailableMetric();
            TestSystemStatusSnapshotDefaultsUnavailableHardwareMetrics();
            TestCpuUsageCalculatorComputesUsageFromTicks();
            TestCpuUsageCalculatorClampsInvalidDeltas();
            TestMemoryStatusServiceCalculatesUsagePercent();
            TestMemoryStatusServiceFormatsUsageText();
            TestHardwareStatusServiceFormatsTemperatureMetric();
            TestHardwareStatusServiceSelectsHardwareMetrics();
            TestHardwareStatusServicePrefersPrimaryGpuUsageSensor();
            TestHardwareStatusServicePrefersPrimaryGpuTemperatureSensor();
            TestHardwareStatusServiceIgnoresMotherboardCpuTemperature();
            TestHardwareStatusServiceRequiresCoreAverageCpuTemperature();
            TestHardwareStatusServiceSelectsAmdCpuTemperature();
            TestHardwareStatusServiceRejectsZeroCpuTemperature();
            TestHardwareStatusServiceIgnoresInvalidSensorValues();
            TestHardwareStatusServiceCreatesDiagnosticsReport();
            TestHardwareStatusServiceReportsCoreAverageCpuSource();
            TestGpuUsageCounterSumsEngineValues();
            TestGpuUsageCounterIgnoresInvalidEngineValues();
            TestGpuUsageCounterReturnsNullWithoutValidEngineValues();
            TestSystemStatusCollectorFormatsPercentMetric();
            TestSystemStatusCollectorCreatesSnapshotWithUnavailableHardwareMetrics();
            TestSystemStatusCollectorCreatesSnapshotWithHardwareMetrics();
            TestSystemStatusCollectorPrefersWindowsGpuUsage();
            TestSystemStatusHttpServerShouldRunOnlyWhenEnabled();
            TestSystemStatusHttpServerBuildsCandidatePorts();
            TestSystemStatusHttpServerBuildsAccessUrl();
            TestSystemStatusHttpServerBuildsIPv6AccessUrl();
            TestSystemStatusHttpServerUsesDualStackListenerFactory();
            TestSystemStatusHttpServerExposesIPv4AndIPv6LanAddresses();
            TestSystemStatusHttpServerHashesAndVerifiesPassword();
            TestSystemStatusHttpServerParsesLoginJson();
            TestSystemStatusHttpServerIdentifiesProtectedPaths();
            TestSystemStatusHttpServerTreatsEmptyPasswordHashAsDisabled();
            TestSystemStatusHttpServerReadsBearerToken();
            TestSystemStatusHttpServerCreatesAuthJson();
            TestSystemStatusHttpServerSerializesStatusJson();
            TestSystemStatusHttpServerSerializesDashboardSettings();
            TestSystemStatusHttpServerParsesAndNormalizesLayout();
            TestSystemStatusHttpServerParsesThemeUpdate();
            TestSystemStatusHttpServerParsesRequestPath();
            TestSystemStatusHttpServerCreatesDashboardHtml();
            TestSystemStatusHttpServerAllowsPostCorsMethod();
            TestSettingsServiceDefaultPathUsesExecutableDirectory();
            TestAppLogServiceDefaultPathUsesExecutableDirectory();
            TestThemePaletteMapsLightAndDarkColors();
            TestTitleBarPaletteMapsLightAndDarkColors();
            TestTrayIconTreatsContextMenuAsMenuRequest();
            TestTrayIconTreatsRightButtonUpAsMenuRequest();
            TestTrayIconExtractsMessageFromLowWord();
            TestTrayIconUsesVisibleTooltipFlags();
            TestApplicationUsesCustomIconAsset();
            TestTrayIconLoadsCustomApplicationIcon();
            TestProjectExcludesPublishArtifactsFromBuildItems();
            TestWindowStyleHidesTaskbarButton();
            TestWindowStyleSelectsTopmostInsertAfter();
            TestWindowStyleSelectsNoTopmostInsertAfter();
            TestSettingsServiceDefaultsToFloatingDisplayMode();
            TestSettingsServiceLoadsTaskbarDisplayMode();
            TestWidgetPlacementAlignsToBottomTaskbarLeft();
            TestWidgetPlacementAlignsToBottomTaskbarRight();
            TestWidgetPlacementAlignsInsideExplicitTaskbarBounds();
            TestWidgetPlacementUsesCustomTaskbarEdgePadding();
            TestWidgetPlacementAvoidsRightTrayReservedBounds();
            TestWidgetPlacementIdentifiesTaskbarModes();
            TestForegroundServiceDetectsFullscreenWindow();
            TestForegroundServiceDoesNotTreatMaximizedWorkAreaAsFullscreen();
            TestForegroundServiceDetectsShellOverlayProcesses();
            TestForegroundServiceDetectsDesktopShellClasses();
            TestForegroundServiceCanDescribeForegroundState();
            TestForegroundServiceChoosesTaskbarOverlayActions();
            TestAppBarContentAlignsLeft();
            TestAppBarContentAlignsRight();
            TestTaskbarOverlayUsesSystemTextRenderingSettings();
            TestTaskbarOverlayConvertsSpeedFontSizeToGdiFontHeight();
            TestTaskbarOverlayCalculatesSeparatedTextBaselines();
            TestTaskbarOverlayConvertsTextMaskToAlphaPixels();
            TestNativeTextOverlayWindowAttachesToTaskbarParent();
            TestNativeTextOverlayWindowCanEnumerateTaskbarBounds();
            TestNativeTextOverlayWindowCanEnumerateTaskbarAvailableBounds();
            TestNativeTextOverlayWindowFiltersUnavailableTaskbars();
            TestNativeTextOverlayWindowFiltersFallbackTaskbarLookup();
            TestNativeTextOverlayWindowCanResetTaskbarParent();
            TestNativeTextOverlayReattachesWhenCachedTaskbarParentIsLost();
            TestApplicationManifestEnablesPerMonitorDpiAwareness();
            TestMainWindowAppliesSystemStatusHttpServerLifecycle();
            TestMainWindowDoesNotResetTaskbarOverlayForSystemStatusOnlySettings();
            TestMainWindowMovesTaskbarOverlayForPaddingOnlySettings();
            TestMainWindowUsesCachedTaskbarPlacementsWhenTaskbarEnumerationFails();
            TestMainWindowRefreshesTaskbarPlacementWithoutResetWhenParentUnchanged();
            TestMainWindowMovesTaskbarOverlayForTaskbarSideSwitch();
            TestMainWindowDisposesTaskbarOverlaysWhenSwitchingToFloating();
            TestMainWindowWritesDiagnosticLogForStartupAndDisplayModeChanges();
            TestSettingsWindowUsesStableTextBoxForTaskbarPadding();
            TestSettingsWindowUsesGroupedSettingRows();
            TestSettingsWindowUsesLeftNavigationPages();
            TestSettingsWindowSwitchesSettingsPages();
            TestSettingsWindowContainsSystemStatusControls();
            TestSettingsWindowHandlesSystemStatusSettings();
            TestSettingsWindowThemesSystemStatusControls();
            TestSettingsWindowExtendsContentIntoCustomTitleBar();
            TestSettingsWindowUsesScrollableContentArea();
            TestSettingsWindowCentersOnCurrentWorkArea();
            TestNativeTextOverlayWindowCanForceTopmostDisplay();
            TestMainWindowUsesForegroundServiceForTaskbarOverlay();
            TestMainWindowRefreshesTaskbarOverlayPlacement();
            TestMainWindowRefreshesTaskbarOverlayOnDisplaySettingsChanged();
            TestMainWindowRefreshesTaskbarOverlayOnTaskbarCreated();
            TestMainWindowUsesAllCurrentTaskbarOverlays();
            TestMainWindowShowsEachTaskbarOverlay();
            TestMainWindowPassesExplicitTaskbarWindowToOverlay();
            TestNativeTextOverlayDoesNotFallbackWhenExplicitTaskbarIsStale();
            TestMainWindowRebuildsOverlaysAfterTaskbarCreated();
            TestMainWindowRecreatesInvalidTaskbarOverlays();
            TestMainWindowRefreshesPlacementAfterInvalidTaskbarOverlay();

            Console.WriteLine("All tests passed.");
        }

        private static void TestFirstSnapshotReturnsZeroSpeed()
        {
            var calculator = new NetworkSpeedCalculator();

            var result = calculator.Calculate(
                new NetworkSnapshot(
                    BytesReceived: 1000,
                    BytesSent: 500,
                    Timestamp: new DateTime(2026, 5, 28, 10, 0, 0)));

            AssertEqual("0 B/s", result.Download, nameof(result.Download));
            AssertEqual("0 B/s", result.Upload, nameof(result.Upload));
        }

        private static void TestSecondSnapshotCalculatesDownloadAndUploadSpeed()
        {
            var calculator = new NetworkSpeedCalculator();

            calculator.Calculate(
                new NetworkSnapshot(
                    BytesReceived: 1024,
                    BytesSent: 512,
                    Timestamp: new DateTime(2026, 5, 28, 10, 0, 0)));

            var result = calculator.Calculate(
                new NetworkSnapshot(
                    BytesReceived: 3 * 1024,
                    BytesSent: 1536,
                    Timestamp: new DateTime(2026, 5, 28, 10, 0, 1)));

            AssertEqual("2.0 KB/s", result.Download, nameof(result.Download));
            AssertEqual("1.0 KB/s", result.Upload, nameof(result.Upload));
        }

        private static void TestFormatSpeedUsesLargerUnits()
        {
            AssertEqual(
                "1.5 MB/s",
                NetworkSpeedCalculator.FormatSpeed(1.5 * 1024 * 1024),
                nameof(NetworkSpeedCalculator.FormatSpeed));
        }

        private static void TestSnapshotSumsMultipleInterfaces()
        {
            var timestamp = new DateTime(2026, 5, 28, 10, 0, 0);

            var snapshot = NetworkSpeedCalculator.CreateSnapshot(
                new[]
                {
                    new NetworkTrafficCounter(BytesReceived: 100, BytesSent: 20),
                    new NetworkTrafficCounter(BytesReceived: 300, BytesSent: 40)
                },
                timestamp);

            AssertEqual("400", snapshot.BytesReceived.ToString(), nameof(snapshot.BytesReceived));
            AssertEqual("60", snapshot.BytesSent.ToString(), nameof(snapshot.BytesSent));
            AssertEqual(timestamp.ToString("O"), snapshot.Timestamp.ToString("O"), nameof(snapshot.Timestamp));
        }

        private static void TestSettingsServiceReturnsDefaultsWhenFileDoesNotExist()
        {
            var settingsPath = CreateTempSettingsPath();
            var service = new SettingsService(settingsPath);

            var settings = service.Load();

            AssertEqual(AppThemeMode.Dark.ToString(), settings.Theme.ToString(), nameof(settings.Theme));
            AssertEqual("True", settings.StartupEnabled.ToString(), nameof(settings.StartupEnabled));
            AssertEqual("True", settings.TopmostEnabled.ToString(), nameof(settings.TopmostEnabled));
            AssertEqual(WidgetDisplayMode.Floating.ToString(), settings.DisplayMode.ToString(), nameof(settings.DisplayMode));
            AssertEqual("False", settings.LockWindowPosition.ToString(), nameof(settings.LockWindowPosition));
            AssertEqual("4", settings.TaskbarEdgePadding.ToString(), nameof(settings.TaskbarEdgePadding));
            AssertEqual("12", settings.SpeedFontSize.ToString(), nameof(settings.SpeedFontSize));
        }

        private static void TestSettingsServiceReturnsDefaultsWhenFileIsInvalid()
        {
            var settingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, "{ invalid json");

            var service = new SettingsService(settingsPath);

            var settings = service.Load();

            AssertEqual(AppThemeMode.Dark.ToString(), settings.Theme.ToString(), nameof(settings.Theme));
            AssertEqual("True", settings.StartupEnabled.ToString(), nameof(settings.StartupEnabled));
            AssertEqual("True", settings.TopmostEnabled.ToString(), nameof(settings.TopmostEnabled));
            AssertEqual(WidgetDisplayMode.Floating.ToString(), settings.DisplayMode.ToString(), nameof(settings.DisplayMode));
            AssertEqual("False", settings.LockWindowPosition.ToString(), nameof(settings.LockWindowPosition));
            AssertEqual("4", settings.TaskbarEdgePadding.ToString(), nameof(settings.TaskbarEdgePadding));
            AssertEqual("12", settings.SpeedFontSize.ToString(), nameof(settings.SpeedFontSize));
        }

        private static void TestSettingsServiceLoadsDocumentedCamelCaseSettings()
        {
            var settingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                """
                {
                  "theme": "Light",
                  "startupEnabled": false,
                  "topmostEnabled": false,
                  "displayMode": "TaskbarRight",
                  "lockWindowPosition": true,
                  "taskbarEdgePadding": 24,
                  "speedFontSize": 14
                }
                """);

            var service = new SettingsService(settingsPath);

            var settings = service.Load();

            AssertEqual(AppThemeMode.Light.ToString(), settings.Theme.ToString(), nameof(settings.Theme));
            AssertEqual("False", settings.StartupEnabled.ToString(), nameof(settings.StartupEnabled));
            AssertEqual("False", settings.TopmostEnabled.ToString(), nameof(settings.TopmostEnabled));
            AssertEqual(WidgetDisplayMode.TaskbarRight.ToString(), settings.DisplayMode.ToString(), nameof(settings.DisplayMode));
            AssertEqual("True", settings.LockWindowPosition.ToString(), nameof(settings.LockWindowPosition));
            AssertEqual("24", settings.TaskbarEdgePadding.ToString(), nameof(settings.TaskbarEdgePadding));
            AssertEqual("14", settings.SpeedFontSize.ToString(), nameof(settings.SpeedFontSize));
        }

        private static void TestSettingsServiceSavesAndLoadsSettings()
        {
            var settingsPath = CreateTempSettingsPath();
            var service = new SettingsService(settingsPath);

            service.Save(
                new AppSettings
                {
                    Theme = AppThemeMode.Light,
                    StartupEnabled = false,
                    TopmostEnabled = false,
                    DisplayMode = WidgetDisplayMode.TaskbarLeft,
                    LockWindowPosition = true,
                    TaskbarEdgePadding = 32,
                    SpeedFontSize = 16
                });

            var settings = service.Load();

            AssertEqual(AppThemeMode.Light.ToString(), settings.Theme.ToString(), nameof(settings.Theme));
            AssertEqual("False", settings.StartupEnabled.ToString(), nameof(settings.StartupEnabled));
            AssertEqual("False", settings.TopmostEnabled.ToString(), nameof(settings.TopmostEnabled));
            AssertEqual(WidgetDisplayMode.TaskbarLeft.ToString(), settings.DisplayMode.ToString(), nameof(settings.DisplayMode));
            AssertEqual("True", settings.LockWindowPosition.ToString(), nameof(settings.LockWindowPosition));
            AssertEqual("32", settings.TaskbarEdgePadding.ToString(), nameof(settings.TaskbarEdgePadding));
            AssertEqual("16", settings.SpeedFontSize.ToString(), nameof(settings.SpeedFontSize));
        }

        private static void TestSettingsServiceDefaultsToFloatingDisplayMode()
        {
            var settings = new AppSettings();

            AssertEqual(WidgetDisplayMode.Floating.ToString(), settings.DisplayMode.ToString(), nameof(settings.DisplayMode));
            AssertEqual("False", settings.LockWindowPosition.ToString(), nameof(settings.LockWindowPosition));
            AssertEqual("4", settings.TaskbarEdgePadding.ToString(), nameof(settings.TaskbarEdgePadding));
            AssertEqual("12", settings.SpeedFontSize.ToString(), nameof(settings.SpeedFontSize));
        }

        private static void TestSettingsServiceLoadsLockWindowPosition()
        {
            var settingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                """
                {
                  "lockWindowPosition": true
                }
                """);

            var service = new SettingsService(settingsPath);
            var settings = service.Load();

            AssertEqual("True", settings.LockWindowPosition.ToString(), nameof(settings.LockWindowPosition));
        }

        private static void TestSettingsServiceLoadsTaskbarDisplayMode()
        {
            var settingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                """
                {
                  "displayMode": "TaskbarLeft"
                }
                """);

            var service = new SettingsService(settingsPath);
            var settings = service.Load();

            AssertEqual(WidgetDisplayMode.TaskbarLeft.ToString(), settings.DisplayMode.ToString(), nameof(settings.DisplayMode));
        }

        private static void TestSettingsServiceLoadsTaskbarEdgePadding()
        {
            var settingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                """
                {
                  "taskbarEdgePadding": 16
                }
                """);

            var service = new SettingsService(settingsPath);
            var settings = service.Load();

            AssertEqual("16", settings.TaskbarEdgePadding.ToString(), nameof(settings.TaskbarEdgePadding));
        }

        private static void TestSettingsServiceClampsTaskbarEdgePadding()
        {
            var negativeSettingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(negativeSettingsPath)!);
            File.WriteAllText(
                negativeSettingsPath,
                """
                {
                  "taskbarEdgePadding": -10
                }
                """);

            var negativeService = new SettingsService(negativeSettingsPath);
            var negativeSettings = negativeService.Load();

            AssertEqual("0", negativeSettings.TaskbarEdgePadding.ToString(), nameof(negativeSettings.TaskbarEdgePadding));

            var oversizedSettingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(oversizedSettingsPath)!);
            File.WriteAllText(
                oversizedSettingsPath,
                """
                {
                  "taskbarEdgePadding": 250
                }
                """);

            var oversizedService = new SettingsService(oversizedSettingsPath);
            var oversizedSettings = oversizedService.Load();

            AssertEqual("200", oversizedSettings.TaskbarEdgePadding.ToString(), nameof(oversizedSettings.TaskbarEdgePadding));
        }

        private static void TestSettingsServiceLoadsSpeedFontSize()
        {
            var settingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                """
                {
                  "speedFontSize": 15
                }
                """);

            var service = new SettingsService(settingsPath);
            var settings = service.Load();

            AssertEqual("15", settings.SpeedFontSize.ToString(), nameof(settings.SpeedFontSize));
        }

        private static void TestSettingsServiceClampsSpeedFontSize()
        {
            var smallSettingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(smallSettingsPath)!);
            File.WriteAllText(
                smallSettingsPath,
                """
                {
                  "speedFontSize": 6
                }
                """);

            var smallService = new SettingsService(smallSettingsPath);
            var smallSettings = smallService.Load();

            AssertEqual("10", smallSettings.SpeedFontSize.ToString(), nameof(smallSettings.SpeedFontSize));

            var largeSettingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(largeSettingsPath)!);
            File.WriteAllText(
                largeSettingsPath,
                """
                {
                  "speedFontSize": 30
                }
                """);

            var largeService = new SettingsService(largeSettingsPath);
            var largeSettings = largeService.Load();

            AssertEqual("18", largeSettings.SpeedFontSize.ToString(), nameof(largeSettings.SpeedFontSize));
        }

        private static void TestSettingsServiceDefaultsSystemStatusSettings()
        {
            var settings = new AppSettings();

            AssertEqual("False", settings.SystemStatus.Enabled.ToString(), "system status enabled default");
            AssertEqual("True", settings.SystemStatus.ServerEnabled.ToString(), "system status server enabled default");
            AssertEqual("17890", settings.SystemStatus.PreferredPort.ToString(), "system status preferred port default");
            AssertEqual("False", settings.SystemStatus.PasswordEnabled.ToString(), "system status password enabled default");
            AssertEqual(string.Empty, settings.SystemStatus.PasswordHash, "system status password hash default");
            AssertEqual("1", settings.SystemStatus.RefreshIntervalSeconds.ToString(), "system status refresh interval default");
            AssertEqual(SystemStatusWebTheme.FollowApp.ToString(), settings.SystemStatus.WebTheme.ToString(), "system status web theme default");
            AssertEqual("True", settings.SystemStatus.Cards.NetworkSpeedVisible.ToString(), "network card visible default");
            AssertEqual("True", settings.SystemStatus.Cards.CpuUsageVisible.ToString(), "cpu usage card visible default");
            AssertEqual("True", settings.SystemStatus.Cards.CpuTemperatureVisible.ToString(), "cpu temperature card visible default");
            AssertEqual("True", settings.SystemStatus.Cards.GpuUsageVisible.ToString(), "gpu usage card visible default");
            AssertEqual("True", settings.SystemStatus.Cards.GpuTemperatureVisible.ToString(), "gpu temperature card visible default");
            AssertEqual("True", settings.SystemStatus.Cards.MemoryUsageVisible.ToString(), "memory usage card visible default");
            AssertEqual("6", settings.SystemStatus.Layout.Count.ToString(), "system status layout default count");
        }

        private static void TestSettingsServiceLoadsSystemStatusSettings()
        {
            var settingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                """
                {
                  "systemStatus": {
                    "enabled": true,
                    "serverEnabled": false,
                    "preferredPort": 19000,
                    "passwordEnabled": true,
                    "passwordHash": "hash-value",
                    "refreshIntervalSeconds": 3,
                    "webTheme": "Dark",
                    "cards": {
                      "networkSpeedVisible": false,
                      "cpuUsageVisible": true,
                      "cpuTemperatureVisible": false,
                      "gpuUsageVisible": true,
                      "gpuTemperatureVisible": false,
                      "memoryUsageVisible": true
                    },
                    "layout": [
                      { "cardKey": "memory", "order": 0 },
                      { "cardKey": "network", "order": 1 }
                    ]
                  }
                }
                """);

            var service = new SettingsService(settingsPath);
            var settings = service.Load();

            AssertEqual("True", settings.SystemStatus.Enabled.ToString(), "loaded system status enabled");
            AssertEqual("False", settings.SystemStatus.ServerEnabled.ToString(), "loaded system status server enabled");
            AssertEqual("19000", settings.SystemStatus.PreferredPort.ToString(), "loaded system status preferred port");
            AssertEqual("True", settings.SystemStatus.PasswordEnabled.ToString(), "loaded system status password enabled");
            AssertEqual("hash-value", settings.SystemStatus.PasswordHash, "loaded system status password hash");
            AssertEqual("3", settings.SystemStatus.RefreshIntervalSeconds.ToString(), "loaded system status refresh interval");
            AssertEqual(SystemStatusWebTheme.Dark.ToString(), settings.SystemStatus.WebTheme.ToString(), "loaded system status web theme");
            AssertEqual("False", settings.SystemStatus.Cards.NetworkSpeedVisible.ToString(), "loaded network card visible");
            AssertEqual("False", settings.SystemStatus.Cards.CpuTemperatureVisible.ToString(), "loaded cpu temp card visible");
            AssertEqual("False", settings.SystemStatus.Cards.GpuTemperatureVisible.ToString(), "loaded gpu temp card visible");
            AssertEqual("2", settings.SystemStatus.Layout.Count.ToString(), "loaded layout count");
            AssertEqual("memory", settings.SystemStatus.Layout[0].CardKey, "loaded layout first card");
        }

        private static void TestSettingsServiceNormalizesSystemStatusSettings()
        {
            var settingsPath = CreateTempSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(
                settingsPath,
                """
                {
                  "systemStatus": {
                    "preferredPort": 70000,
                    "passwordEnabled": true,
                    "passwordHash": "",
                    "refreshIntervalSeconds": 9,
                    "cards": null,
                    "layout": []
                  }
                }
                """);

            var service = new SettingsService(settingsPath);
            var settings = service.Load();

            AssertEqual("65535", settings.SystemStatus.PreferredPort.ToString(), "normalized max preferred port");
            AssertEqual("False", settings.SystemStatus.PasswordEnabled.ToString(), "empty password hash disables auth");
            AssertEqual("5", settings.SystemStatus.RefreshIntervalSeconds.ToString(), "normalized refresh interval");
            AssertEqual("True", (settings.SystemStatus.Cards is not null).ToString(), "normalized card settings");
            AssertEqual("6", settings.SystemStatus.Layout.Count.ToString(), "normalized empty layout");
        }

        private static void TestSettingsServiceSavesSystemStatusSettingsAsCamelCase()
        {
            var settingsPath = CreateTempSettingsPath();
            var service = new SettingsService(settingsPath);

            service.Save(
                new AppSettings
                {
                    SystemStatus = new SystemStatusSettings
                    {
                        Enabled = true,
                        ServerEnabled = true,
                        PreferredPort = 19001,
                        PasswordEnabled = false,
                        RefreshIntervalSeconds = 2,
                        WebTheme = SystemStatusWebTheme.Light,
                        Cards = new SystemStatusCardSettings
                        {
                            NetworkSpeedVisible = true,
                            CpuUsageVisible = false,
                            CpuTemperatureVisible = true,
                            GpuUsageVisible = false,
                            GpuTemperatureVisible = true,
                            MemoryUsageVisible = false
                        }
                    }
                });

            var json = File.ReadAllText(settingsPath);

            AssertEqual("True", json.Contains("\"systemStatus\"", StringComparison.Ordinal).ToString(), "systemStatus camel case");
            AssertEqual("True", json.Contains("\"preferredPort\": 19001", StringComparison.Ordinal).ToString(), "preferredPort camel case");
            AssertEqual("True", json.Contains("\"webTheme\": \"Light\"", StringComparison.Ordinal).ToString(), "webTheme string enum");
            AssertEqual("True", json.Contains("\"cpuUsageVisible\": false", StringComparison.Ordinal).ToString(), "cpuUsageVisible camel case");
        }

        private static void TestSystemMetricValueCreatesAvailableMetric()
        {
            var value = SystemMetricValue.Available(
                DisplayText: "42.5%",
                NumericValue: 42.5,
                Unit: "%");

            AssertEqual(SystemMetricStatus.Available.ToString(), value.Status.ToString(), "metric available status");
            AssertEqual("42.5%", value.DisplayText, "metric available display text");
            AssertEqual("42.5", value.NumericValue?.ToString("F1") ?? string.Empty, "metric available numeric value");
            AssertEqual("%", value.Unit, "metric available unit");
        }

        private static void TestSystemStatusSnapshotDefaultsUnavailableHardwareMetrics()
        {
            var snapshot = SystemStatusSnapshot.CreateUnavailable();

            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.CpuTemperature.Status.ToString(), "cpu temperature unavailable");
            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.GpuUsage.Status.ToString(), "gpu usage unavailable");
            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.GpuTemperature.Status.ToString(), "gpu temperature unavailable");
            AssertEqual("不可用", snapshot.CpuTemperature.DisplayText, "cpu temperature unavailable text");
        }

        private static void TestCpuUsageCalculatorComputesUsageFromTicks()
        {
            var usage = CpuUsageCalculator.CalculateUsagePercent(
                previousIdleTicks: 100,
                previousTotalTicks: 1000,
                currentIdleTicks: 150,
                currentTotalTicks: 1200);

            AssertEqual("75.0", usage.ToString("F1"), "cpu usage percent");
        }

        private static void TestCpuUsageCalculatorClampsInvalidDeltas()
        {
            var zeroTotal = CpuUsageCalculator.CalculateUsagePercent(100, 1000, 120, 1000);
            var negativeIdle = CpuUsageCalculator.CalculateUsagePercent(100, 1000, 90, 1100);
            var oversizedIdle = CpuUsageCalculator.CalculateUsagePercent(100, 1000, 300, 1100);

            AssertEqual("0.0", zeroTotal.ToString("F1"), "cpu zero total delta");
            AssertEqual("100.0", negativeIdle.ToString("F1"), "cpu negative idle delta");
            AssertEqual("0.0", oversizedIdle.ToString("F1"), "cpu oversized idle delta");
        }

        private static void TestMemoryStatusServiceCalculatesUsagePercent()
        {
            var usage = MemoryStatusService.CalculateUsagePercent(
                totalBytes: 16L * 1024 * 1024 * 1024,
                availableBytes: 4L * 1024 * 1024 * 1024);

            AssertEqual("75.0", usage.ToString("F1"), "memory usage percent");
        }

        private static void TestMemoryStatusServiceFormatsUsageText()
        {
            var text = MemoryStatusService.FormatUsageText(
                usagePercent: 75,
                usedBytes: 12L * 1024 * 1024 * 1024,
                totalBytes: 16L * 1024 * 1024 * 1024);

            AssertEqual("75.0% (12.0 GB / 16.0 GB)", text, "memory usage display");
        }

        private static void TestHardwareStatusServiceFormatsTemperatureMetric()
        {
            var metric = HardwareStatusService.CreateTemperatureMetric(66.66);
            var invalidMetric = HardwareStatusService.CreateTemperatureMetric(200);

            AssertEqual(SystemMetricStatus.Available.ToString(), metric.Status.ToString(), "temperature metric status");
            AssertEqual("66.7 °C", metric.DisplayText, "temperature metric text");
            AssertEqual("66.7", metric.NumericValue?.ToString("F1") ?? string.Empty, "temperature metric value");
            AssertEqual("°C", metric.Unit, "temperature metric unit");
            AssertEqual(SystemMetricStatus.Unavailable.ToString(), invalidMetric.Status.ToString(), "invalid temperature unavailable");
        }

        private static void TestHardwareStatusServiceSelectsHardwareMetrics()
        {
            var snapshot =
                HardwareStatusService.CreateSnapshot(
                    new[]
                    {
                        new HardwareSensorReading("Cpu", "Temperature", "CPU Core #1", 54.2),
                        new HardwareSensorReading("Cpu", "Temperature", "CPU Package", 61.8),
                        new HardwareSensorReading("Cpu", "Temperature", "Core Average", 56.4),
                        new HardwareSensorReading("GpuNvidia", "Load", "GPU Core", 42.25),
                        new HardwareSensorReading("GpuNvidia", "Load", "Memory Controller", 15),
                        new HardwareSensorReading("GpuNvidia", "Temperature", "GPU Core", 70.25)
                    });

            AssertEqual("56.4 °C", snapshot.CpuTemperature.DisplayText, "cpu temperature prefers core average");
            AssertEqual("42.3%", snapshot.GpuUsage.DisplayText, "gpu usage from core load");
            AssertEqual("70.3 °C", snapshot.GpuTemperature.DisplayText, "gpu temperature from gpu sensor");
        }

        private static void TestHardwareStatusServicePrefersPrimaryGpuUsageSensor()
        {
            var snapshot =
                HardwareStatusService.CreateSnapshot(
                    new[]
                    {
                        new HardwareSensorReading("GpuNvidia", "Load", "Video Engine", 92),
                        new HardwareSensorReading("GpuNvidia", "Load", "GPU Memory", 81),
                        new HardwareSensorReading("GpuNvidia", "Load", "GPU Core", 14.6)
                    });

            AssertEqual("14.6%", snapshot.GpuUsage.DisplayText, "gpu usage prefers primary core load");
        }

        private static void TestHardwareStatusServicePrefersPrimaryGpuTemperatureSensor()
        {
            var snapshot =
                HardwareStatusService.CreateSnapshot(
                    new[]
                    {
                        new HardwareSensorReading("GpuAmd", "Temperature", "GPU Hot Spot", 96),
                        new HardwareSensorReading("GpuAmd", "Temperature", "GPU Memory Junction", 88),
                        new HardwareSensorReading("GpuAmd", "Temperature", "GPU Core", 62.4)
                    });

            AssertEqual("62.4 °C", snapshot.GpuTemperature.DisplayText, "gpu temperature prefers primary core temperature");
        }

        private static void TestHardwareStatusServiceIgnoresMotherboardCpuTemperature()
        {
            var snapshot =
                HardwareStatusService.CreateSnapshot(
                    new[]
                    {
                        new HardwareSensorReading("Motherboard", "Temperature", "PCH", 44),
                        new HardwareSensorReading("SuperIO", "Temperature", "CPU", 58.4),
                        new HardwareSensorReading("SuperIO", "Temperature", "System", 35)
                    });

            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.CpuTemperature.Status.ToString(), "motherboard cpu temperature is not used");
        }

        private static void TestHardwareStatusServiceRequiresCoreAverageCpuTemperature()
        {
            var snapshot =
                HardwareStatusService.CreateSnapshot(
                    new[]
                    {
                        new HardwareSensorReading("Cpu", "Temperature", "CPU Package", 61.8),
                        new HardwareSensorReading("SuperIO", "Temperature", "CPU", 58.4)
                    });

            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.CpuTemperature.Status.ToString(), "cpu package is not used without core average");
        }

        private static void TestHardwareStatusServiceSelectsAmdCpuTemperature()
        {
            // 1. Ryzen 的 Tdie 优先于控制温度 Tctl，且忽略主板上的同名传感器。
            var snapshot = HardwareStatusService.CreateSnapshot(new[]
            {
                new HardwareSensorReading("SuperIO", "Temperature", "Core (Tdie)", 99, "AMD Ryzen", "/lpc/temperature/0"),
                new HardwareSensorReading("Cpu", "Temperature", "Core (Tctl)", 61.8, "AMD Ryzen 5 5600GT", "/amdcpu/0/temperature/0"),
                new HardwareSensorReading("Cpu", "Temperature", "Core (Tctl/Tdie)", 57.2, "AMD Ryzen 5 5600GT", "/amdcpu/0/temperature/2"),
                new HardwareSensorReading("Cpu", "Temperature", "Core (Tdie)", 54.3, "AMD Ryzen 5 5600GT", "/amdcpu/0/temperature/1")
            });
            AssertEqual("54.3 °C", snapshot.CpuTemperature.DisplayText, "AMD CPU prefers Tdie");

            // 2. 这台机器只提供组合传感器，管理员读数有效时应直接显示。
            var combined = HardwareStatusService.CreateSnapshot(new[]
            {
                new HardwareSensorReading("Cpu", "Temperature", "Core (Tctl/Tdie)", 66.625, "AMD Ryzen 5 5600GT with Radeon Graphics", "/amdcpu/0/temperature/2")
            });
            AssertEqual("66.6 °C", combined.CpuTemperature.DisplayText, "AMD combined temperature is used");
        }

        private static void TestHardwareStatusServiceRejectsZeroCpuTemperature()
        {
            // 1. 驱动读取失败时可能留下 0 °C；此时不能向用户报告虚假温度。
            var zero = HardwareStatusService.CreateSnapshot(new[]
            {
                new HardwareSensorReading("Cpu", "Temperature", "Core (Tctl/Tdie)", 0, "AMD Ryzen 5 5600GT", "/amdcpu/0/temperature/2")
            });
            AssertEqual(SystemMetricStatus.Unavailable.ToString(), zero.CpuTemperature.Status.ToString(), "zero AMD temperature is unavailable");

            // 2. Intel 的 Core Average 优先规则保留，零值时不降级为 Package。
            var intel = HardwareStatusService.CreateSnapshot(new[]
            {
                new HardwareSensorReading("Cpu", "Temperature", "Core Average", 0, "Intel Core", "/intelcpu/0/temperature/1"),
                new HardwareSensorReading("Cpu", "Temperature", "CPU Package", 62, "Intel Core", "/intelcpu/0/temperature/2")
            });
            AssertEqual(SystemMetricStatus.Unavailable.ToString(), intel.CpuTemperature.Status.ToString(), "zero Intel temperature is unavailable");
        }

        private static void TestHardwareStatusServiceIgnoresInvalidSensorValues()
        {
            var snapshot =
                HardwareStatusService.CreateSnapshot(
                    new[]
                    {
                        new HardwareSensorReading("Cpu", "Temperature", "CPU Package", 200),
                        new HardwareSensorReading("GpuAmd", "Load", "GPU Core", -1),
                        new HardwareSensorReading("GpuAmd", "Temperature", "GPU Core", double.NaN)
                    });

            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.CpuTemperature.Status.ToString(), "invalid cpu temperature unavailable");
            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.GpuUsage.Status.ToString(), "invalid gpu usage unavailable");
            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.GpuTemperature.Status.ToString(), "invalid gpu temperature unavailable");
        }

        private static void TestHardwareStatusServiceCreatesDiagnosticsReport()
        {
            var snapshot =
                HardwareStatusService.CreateSnapshot(
                    new[]
                    {
                        new HardwareSensorReading(
                            "Cpu",
                            "Temperature",
                            "CPU Package",
                            61.8,
                            "AMD Ryzen",
                            "/amdcpu/0/temperature/0"),
                        new HardwareSensorReading(
                            "SuperIO",
                            "Temperature",
                            "System",
                            35,
                            "Nuvoton",
                            "/lpc/nct/temperature/1")
                    });

            var report =
                HardwareStatusService.CreateDiagnosticsReport(
                    new[]
                    {
                        new HardwareSensorReading(
                            "Cpu",
                            "Temperature",
                            "CPU Package",
                            61.8,
                            "AMD Ryzen",
                            "/amdcpu/0/temperature/0")
                    },
                    snapshot,
                    "PawnIO: installed=True, loaded=True, version=1.0");

            AssertEqual("True", report.Contains("PawnIO: installed=True", StringComparison.Ordinal).ToString(), "diagnostics pawnio status");
            AssertEqual("True", report.Contains("Selected CPU temperature: 不可用", StringComparison.Ordinal).ToString(), "diagnostics selected cpu temp");
            AssertEqual("True", report.Contains("Selected CPU temperature source: Unavailable", StringComparison.Ordinal).ToString(), "diagnostics selected cpu source");
            AssertEqual("True", report.Contains("[Cpu] AMD Ryzen :: CPU Package = 61.8 °C", StringComparison.Ordinal).ToString(), "diagnostics sensor line");
        }

        private static void TestHardwareStatusServiceReportsCoreAverageCpuSource()
        {
            var readings =
                new[]
                {
                    new HardwareSensorReading(
                        "Cpu",
                        "Temperature",
                        "Core Average",
                        56.4,
                        "AMD Ryzen",
                        "/amdcpu/0/temperature/1"),
                    new HardwareSensorReading(
                        "Cpu",
                        "Control",
                        "CPU Fan",
                        72,
                        "AMD Ryzen",
                        "/amdcpu/0/control/0")
                };

            var report =
                HardwareStatusService.CreateDiagnosticsReport(
                    readings,
                    HardwareStatusService.CreateSnapshot(readings),
                    "PawnIO: installed=True, loaded=True, version=1.0");

            AssertEqual("True", report.Contains("Selected CPU temperature: 56.4 °C", StringComparison.Ordinal).ToString(), "diagnostics core average temp");
            AssertEqual(
                "True",
                report.Contains("Selected CPU temperature source: [Cpu] AMD Ryzen :: Temperature :: Core Average = 56.4 (/amdcpu/0/temperature/1)", StringComparison.Ordinal).ToString(),
                "diagnostics core average source");
        }

        private static void TestGpuUsageCounterSumsEngineValues()
        {
            var usage =
                GpuUsageCounterService.CalculateUsagePercent(
                    new[]
                    {
                        new GpuEngineCounterSample("pid_100_luid_0x00000000_0x00000000_engtype_3D", 38.4),
                        new GpuEngineCounterSample("pid_101_luid_0x00000000_0x00000000_engtype_Copy", 24.2),
                        new GpuEngineCounterSample("_Total", 99)
                    });

            AssertEqual("62.6", usage?.ToString("F1") ?? string.Empty, "gpu engine usage sums non total counters");
        }

        private static void TestGpuUsageCounterIgnoresInvalidEngineValues()
        {
            var usage =
                GpuUsageCounterService.CalculateUsagePercent(
                    new[]
                    {
                        new GpuEngineCounterSample("pid_100_luid_0x00000000_0x00000000_engtype_3D", 80),
                        new GpuEngineCounterSample("pid_101_luid_0x00000000_0x00000000_engtype_Copy", double.NaN),
                        new GpuEngineCounterSample("pid_102_luid_0x00000000_0x00000000_engtype_VideoDecode", -1),
                        new GpuEngineCounterSample("pid_103_luid_0x00000000_0x00000000_engtype_Compute", 50)
                    });

            AssertEqual("100.0", usage?.ToString("F1") ?? string.Empty, "gpu engine usage clamps total");
        }

        private static void TestGpuUsageCounterReturnsNullWithoutValidEngineValues()
        {
            var usage =
                GpuUsageCounterService.CalculateUsagePercent(
                    new[]
                    {
                        new GpuEngineCounterSample("_Total", 42),
                        new GpuEngineCounterSample("pid_100_luid_0x00000000_0x00000000_engtype_3D", double.NaN)
                    });

            AssertEqual("True", (usage is null).ToString(), "gpu engine usage returns null without valid counters");
        }

        private static void TestSystemStatusCollectorFormatsPercentMetric()
        {
            var metric = SystemStatusCollectorService.CreatePercentMetric(12.345);

            AssertEqual(SystemMetricStatus.Available.ToString(), metric.Status.ToString(), "percent metric status");
            AssertEqual("12.3%", metric.DisplayText, "percent metric display text");
            AssertEqual("12.3", metric.NumericValue?.ToString("F1") ?? string.Empty, "percent metric numeric value");
            AssertEqual("%", metric.Unit, "percent metric unit");
        }

        private static void TestSystemStatusCollectorCreatesSnapshotWithUnavailableHardwareMetrics()
        {
            var snapshot = SystemStatusCollectorService.CreateSnapshot(
                downloadSpeed: "1.0 KB/s",
                uploadSpeed: "2.0 KB/s",
                cpuUsagePercent: 35.25,
                memoryUsage: SystemMetricValue.Available("60.0% (6.0 GB / 10.0 GB)", 60, "%"));

            AssertEqual("1.0 KB/s", snapshot.DownloadSpeed.DisplayText, "collector download speed");
            AssertEqual("2.0 KB/s", snapshot.UploadSpeed.DisplayText, "collector upload speed");
            AssertEqual("35.3%", snapshot.CpuUsage.DisplayText, "collector cpu usage");
            AssertEqual("60.0% (6.0 GB / 10.0 GB)", snapshot.MemoryUsage.DisplayText, "collector memory usage");
            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.CpuTemperature.Status.ToString(), "collector cpu temp unavailable");
            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.GpuUsage.Status.ToString(), "collector gpu usage unavailable");
            AssertEqual(SystemMetricStatus.Unavailable.ToString(), snapshot.GpuTemperature.Status.ToString(), "collector gpu temp unavailable");
        }

        private static void TestSystemStatusCollectorCreatesSnapshotWithHardwareMetrics()
        {
            var hardwareStatus = new HardwareStatusSnapshot(
                SystemMetricValue.Available("61.8 °C", 61.8, "°C"),
                SystemMetricValue.Available("42.3%", 42.3, "%"),
                SystemMetricValue.Available("70.3 °C", 70.3, "°C"));

            var snapshot = SystemStatusCollectorService.CreateSnapshot(
                downloadSpeed: "1.0 KB/s",
                uploadSpeed: "2.0 KB/s",
                cpuUsagePercent: 35.25,
                memoryUsage: SystemMetricValue.Available("60.0% (6.0 GB / 10.0 GB)", 60, "%"),
                hardwareStatus: hardwareStatus);

            AssertEqual("61.8 °C", snapshot.CpuTemperature.DisplayText, "collector cpu temp available");
            AssertEqual("42.3%", snapshot.GpuUsage.DisplayText, "collector gpu usage available");
            AssertEqual("70.3 °C", snapshot.GpuTemperature.DisplayText, "collector gpu temp available");
        }

        private static void TestSystemStatusCollectorPrefersWindowsGpuUsage()
        {
            var hardwareStatus = new HardwareStatusSnapshot(
                SystemMetricValue.Available("61.8 °C", 61.8, "°C"),
                SystemMetricValue.Available("8.0%", 8, "%"),
                SystemMetricValue.Available("70.3 °C", 70.3, "°C"));

            var merged =
                SystemStatusCollectorService.PreferWindowsGpuUsage(
                    hardwareStatus,
                    SystemMetricValue.Available("62.6%", 62.6, "%"));

            AssertEqual("62.6%", merged.GpuUsage.DisplayText, "collector prefers windows gpu usage");
            AssertEqual("70.3 °C", merged.GpuTemperature.DisplayText, "collector keeps hardware gpu temperature");
        }

        private static void TestSystemStatusHttpServerShouldRunOnlyWhenEnabled()
        {
            AssertEqual(
                "False",
                SystemStatusHttpServerService.ShouldRun(new SystemStatusSettings()).ToString(),
                "disabled system status server should not run");
            AssertEqual(
                "False",
                SystemStatusHttpServerService.ShouldRun(
                    new SystemStatusSettings
                    {
                        Enabled = true,
                        ServerEnabled = false
                    }).ToString(),
                "disabled http server should not run");
            AssertEqual(
                "True",
                SystemStatusHttpServerService.ShouldRun(
                    new SystemStatusSettings
                    {
                        Enabled = true,
                        ServerEnabled = true
                    }).ToString(),
                "enabled http server should run");
        }

        private static void TestSystemStatusHttpServerBuildsCandidatePorts()
        {
            var ports = SystemStatusHttpServerService.GetCandidatePorts(65534, 4);

            AssertEqual("4", ports.Count.ToString(), "candidate port count");
            AssertEqual("65534", ports[0].ToString(), "first candidate port");
            AssertEqual("65535", ports[1].ToString(), "second candidate port");
            AssertEqual("1", ports[2].ToString(), "wrapped candidate port");
            AssertEqual("2", ports[3].ToString(), "next wrapped candidate port");
        }

        private static void TestSystemStatusHttpServerBuildsAccessUrl()
        {
            AssertEqual(
                "http://localhost:17890/",
                SystemStatusHttpServerService.CreateAccessUrl(" localhost ", 17890),
                "localhost access url");
            AssertEqual(
                "http://192.168.1.8:1/",
                SystemStatusHttpServerService.CreateAccessUrl("192.168.1.8", -10),
                "normalized lan access url");
        }

        private static void TestSystemStatusHttpServerBuildsIPv6AccessUrl()
        {
            AssertEqual(
                "http://[::1]:17890/",
                SystemStatusHttpServerService.CreateAccessUrl("::1", 17890),
                "IPv6 loopback access url");

            AssertEqual(
                "http://[240e:1234::5678]:17890/",
                SystemStatusHttpServerService.CreateAccessUrl("240e:1234::5678", 17890),
                "IPv6 LAN access url");

            AssertEqual(
                "http://[fe80::1234%25]:17890/",
                SystemStatusHttpServerService.CreateAccessUrl("fe80::1234%25", 17890),
                "IPv6 scoped access url keeps escaped scope id");
        }

        private static void TestSystemStatusHttpServerUsesDualStackListenerFactory()
        {
            if (!System.Net.Sockets.Socket.OSSupportsIPv6)
            {
                return;
            }

            using var listener = SystemStatusHttpServerService.CreatePreferredListener(0);

            AssertEqual(
                System.Net.Sockets.AddressFamily.InterNetworkV6.ToString(),
                listener.Server.AddressFamily.ToString(),
                "preferred listener address family");
            AssertEqual(
                "True",
                listener.Server.DualMode.ToString(),
                "preferred listener dual mode");
        }

        private static void TestSystemStatusHttpServerExposesIPv4AndIPv6LanAddresses()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "SystemStatusHttpServerService.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("GetLanIPAddresses()", StringComparison.Ordinal).ToString(), "LAN address helper renamed");
            AssertEqual("True", code.Contains("AddressFamily.InterNetworkV6", StringComparison.Ordinal).ToString(), "LAN address helper includes IPv6");
            AssertEqual("True", code.Contains("IsUsableLanAddress", StringComparison.Ordinal).ToString(), "LAN address helper filters usable addresses");
        }

        private static void TestSystemStatusHttpServerHashesAndVerifiesPassword()
        {
            var hash = SystemStatusHttpServerService.CreatePasswordHash("secret");

            AssertEqual("True", hash.StartsWith("PBKDF2-SHA256$", StringComparison.Ordinal).ToString(), "salted password hash format");
            AssertEqual("True", SystemStatusHttpServerService.VerifyPassword("secret", hash).ToString(), "password verified");
            AssertEqual("False", SystemStatusHttpServerService.VerifyPassword("bad", hash).ToString(), "wrong password rejected");
            AssertEqual("False", hash.Contains("secret", StringComparison.Ordinal).ToString(), "hash does not contain password");
        }

        private static void TestSystemStatusHttpServerParsesLoginJson()
        {
            var parsed =
                SystemStatusHttpServerService.TryParseLoginJson(
                    "{\"password\":\"secret\"}",
                    out var password);
            var empty =
                SystemStatusHttpServerService.TryParseLoginJson(
                    "{\"password\":\"\"}",
                    out _);

            AssertEqual("True", parsed.ToString(), "login json parsed");
            AssertEqual("secret", password, "login json password");
            AssertEqual("False", empty.ToString(), "empty login password rejected");
        }

        private static void TestSystemStatusHttpServerIdentifiesProtectedPaths()
        {
            var protectedSettings =
                new SystemStatusSettings
                {
                    PasswordEnabled = true,
                    PasswordHash = SystemStatusHttpServerService.CreatePasswordHash("secret")
                };
            var openSettings =
                new SystemStatusSettings
                {
                    PasswordEnabled = false
                };

            AssertEqual("True", SystemStatusHttpServerService.RequiresAuthentication(protectedSettings, "GET", "/api/status").ToString(), "status protected");
            AssertEqual("True", SystemStatusHttpServerService.RequiresAuthentication(protectedSettings, "GET", "/api/settings").ToString(), "settings protected");
            AssertEqual("True", SystemStatusHttpServerService.RequiresAuthentication(protectedSettings, "POST", "/api/layout").ToString(), "layout protected");
            AssertEqual("True", SystemStatusHttpServerService.RequiresAuthentication(protectedSettings, "POST", "/api/theme").ToString(), "theme protected");
            AssertEqual("False", SystemStatusHttpServerService.RequiresAuthentication(protectedSettings, "GET", "/api/auth/state").ToString(), "auth state open");
            AssertEqual("False", SystemStatusHttpServerService.RequiresAuthentication(protectedSettings, "POST", "/api/auth/login").ToString(), "auth login open");
            AssertEqual("False", SystemStatusHttpServerService.RequiresAuthentication(openSettings, "GET", "/api/status").ToString(), "status open when password disabled");
        }

        private static void TestSystemStatusHttpServerTreatsEmptyPasswordHashAsDisabled()
        {
            var settings =
                new SystemStatusSettings
                {
                    PasswordEnabled = true,
                    PasswordHash = string.Empty
                };
            var authState = SystemStatusHttpServerService.CreateAuthStateJson(settings);

            AssertEqual("False", SystemStatusHttpServerService.IsPasswordAuthenticationConfigured(settings).ToString(), "empty password hash not configured");
            AssertEqual("False", SystemStatusHttpServerService.RequiresAuthentication(settings, "GET", "/api/status").ToString(), "empty password hash does not protect route");
            AssertEqual("True", authState.Contains("\"passwordEnabled\":false", StringComparison.Ordinal).ToString(), "empty password hash reports disabled auth");
        }

        private static void TestSystemStatusHttpServerReadsBearerToken()
        {
            var headers =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Authorization"] = "Bearer token-value"
                };

            AssertEqual("token-value", SystemStatusHttpServerService.GetBearerToken(headers) ?? string.Empty, "bearer token");
            AssertEqual(string.Empty, SystemStatusHttpServerService.GetBearerToken(new Dictionary<string, string>()) ?? string.Empty, "missing bearer token");
        }

        private static void TestSystemStatusHttpServerCreatesAuthJson()
        {
            var stateJson =
                SystemStatusHttpServerService.CreateAuthStateJson(
                    new SystemStatusSettings
                    {
                        PasswordEnabled = true,
                        PasswordHash = "hash-value"
                    });
            var loginJson =
                SystemStatusHttpServerService.CreateLoginResponseJson(
                    "token-value",
                    new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
                    true);

            AssertEqual("True", stateJson.Contains("\"passwordEnabled\":true", StringComparison.Ordinal).ToString(), "auth state password enabled");
            AssertEqual("False", stateJson.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase).ToString(), "auth state hides password hash");
            AssertEqual("True", loginJson.Contains("\"token\":\"token-value\"", StringComparison.Ordinal).ToString(), "login response token");
            AssertEqual("True", loginJson.Contains("\"expiresAt\"", StringComparison.Ordinal).ToString(), "login response expiry");
        }

        private static void TestSystemStatusHttpServerSerializesStatusJson()
        {
            var snapshot = new SystemStatusSnapshot(
                new DateTime(2026, 6, 1, 10, 30, 0),
                SystemMetricValue.Available("1.0 KB/s", 1024, "B/s"),
                SystemMetricValue.Available("2.0 KB/s", 2048, "B/s"),
                SystemMetricValue.Available("35.5%", 35.5, "%"),
                SystemMetricValue.Unavailable("°C"),
                SystemMetricValue.Unavailable("%"),
                SystemMetricValue.Unavailable("°C"),
                SystemMetricValue.Available("50.0% (8.0 GB / 16.0 GB)", 50, "%"));

            var json = SystemStatusHttpServerService.CreateStatusJson(snapshot);

            AssertEqual("True", json.Contains("\"timestamp\"", StringComparison.Ordinal).ToString(), "status json timestamp camel case");
            AssertEqual("True", json.Contains("\"downloadSpeed\"", StringComparison.Ordinal).ToString(), "status json downloadSpeed camel case");
            AssertEqual("True", json.Contains("\"displayText\":\"1.0 KB/s\"", StringComparison.Ordinal).ToString(), "status json display text");
            AssertEqual("True", json.Contains("\"status\":\"Available\"", StringComparison.Ordinal).ToString(), "status json enum string");
            AssertEqual("False", json.Contains("\"source\"", StringComparison.Ordinal).ToString(), "status json no public metric source");
            AssertEqual("False", json.Contains("\"DownloadSpeed\"", StringComparison.Ordinal).ToString(), "status json no pascal case");
        }

        private static void TestSystemStatusHttpServerSerializesDashboardSettings()
        {
            var settings = new SystemStatusSettings
            {
                RefreshIntervalSeconds = 3,
                WebTheme = SystemStatusWebTheme.FollowApp,
                Cards = new SystemStatusCardSettings
                {
                    CpuTemperatureVisible = false
                }
            };

            var json = SystemStatusHttpServerService.CreateSettingsJson(settings, AppThemeMode.Light);

            AssertEqual("True", json.Contains("\"refreshIntervalSeconds\":3", StringComparison.Ordinal).ToString(), "dashboard settings refresh interval");
            AssertEqual("True", json.Contains("\"webTheme\":\"FollowApp\"", StringComparison.Ordinal).ToString(), "dashboard settings web theme");
            AssertEqual("True", json.Contains("\"effectiveTheme\":\"Light\"", StringComparison.Ordinal).ToString(), "dashboard settings effective theme");
            AssertEqual("True", json.Contains("\"cpuTemperatureVisible\":false", StringComparison.Ordinal).ToString(), "dashboard settings card visibility");
            AssertEqual("True", json.Contains("\"layout\"", StringComparison.Ordinal).ToString(), "dashboard settings layout");
        }

        private static void TestSystemStatusHttpServerParsesAndNormalizesLayout()
        {
            var json = """
                {
                  "layout": [
                    { "cardKey": "memory", "order": 9 },
                    { "cardKey": "network", "order": 3 },
                    { "cardKey": "memory", "order": 1 },
                    { "cardKey": "unknown", "order": 2 }
                  ]
                }
                """;

            var parsed = SystemStatusHttpServerService.TryParseLayoutJson(json, out var layout);

            AssertEqual("True", parsed.ToString(), "layout parsed");
            AssertEqual("6", layout.Count.ToString(), "layout fills missing cards");
            AssertEqual("memory", layout[0].CardKey, "layout first normalized card");
            AssertEqual("0", layout[0].Order.ToString(), "layout first normalized order");
            AssertEqual("network", layout[1].CardKey, "layout second normalized card");
            AssertEqual("cpuUsage", layout[2].CardKey, "layout appends missing card");
        }

        private static void TestSystemStatusHttpServerParsesThemeUpdate()
        {
            var parsedLight =
                SystemStatusHttpServerService.TryParseThemeJson(
                    "{\"webTheme\":\"Light\"}",
                    out var lightTheme);
            var parsedDark =
                SystemStatusHttpServerService.TryParseThemeJson(
                    "{\"webTheme\":\"dark\"}",
                    out var darkTheme);
            var parsedInvalid =
                SystemStatusHttpServerService.TryParseThemeJson(
                    "{\"webTheme\":\"Blue\"}",
                    out _);

            AssertEqual("True", parsedLight.ToString(), "light theme parsed");
            AssertEqual(SystemStatusWebTheme.Light.ToString(), lightTheme.ToString(), "light theme value");
            AssertEqual("True", parsedDark.ToString(), "dark theme parsed case insensitive");
            AssertEqual(SystemStatusWebTheme.Dark.ToString(), darkTheme.ToString(), "dark theme value");
            AssertEqual("False", parsedInvalid.ToString(), "invalid theme rejected");
        }

        private static void TestSystemStatusHttpServerParsesRequestPath()
        {
            AssertEqual("/api/status", SystemStatusHttpServerService.GetRequestPath("GET /api/status HTTP/1.1"), "api status path");
            AssertEqual("/", SystemStatusHttpServerService.GetRequestPath("GET / HTTP/1.1"), "root path");
            AssertEqual("/api/status", SystemStatusHttpServerService.GetRequestPath("GET /api/status?refresh=1 HTTP/1.1"), "query string stripped");
            AssertEqual("/api/status", SystemStatusHttpServerService.GetRequestPath("GET http://localhost:17890/api/status HTTP/1.1"), "absolute uri path");
        }

        private static void TestSystemStatusHttpServerCreatesDashboardHtml()
        {
            var html = SystemStatusHttpServerService.CreateDashboardHtml();

            AssertEqual("True", html.Contains("<title>系统状态</title>", StringComparison.Ordinal).ToString(), "dashboard html title");
            AssertEqual("True", html.Contains("\"/api/settings\"", StringComparison.Ordinal).ToString(), "dashboard html settings fetch");
            AssertEqual("True", html.Contains("\"/api/status\"", StringComparison.Ordinal).ToString(), "dashboard html status fetch");
            AssertEqual("True", html.Contains("fetch(\"/api/layout\"", StringComparison.Ordinal).ToString(), "dashboard html layout fetch");
            AssertEqual("True", html.Contains("fetch(\"/api/theme\"", StringComparison.Ordinal).ToString(), "dashboard html theme fetch");
            AssertEqual("True", html.Contains("fetch(\"/api/auth/login\"", StringComparison.Ordinal).ToString(), "dashboard html login fetch");
            AssertEqual("True", html.Contains("fetch(\"/api/auth/state\"", StringComparison.Ordinal).ToString(), "dashboard html auth state fetch");
            AssertEqual("True", html.Contains("Authorization = `Bearer ${authToken}`", StringComparison.Ordinal).ToString(), "dashboard html authorization header");
            AssertEqual("True", html.Contains("id=\"loginPanel\"", StringComparison.Ordinal).ToString(), "dashboard html login panel");
            AssertEqual("True", html.Contains("id=\"themeButton\"", StringComparison.Ordinal).ToString(), "dashboard html theme button");
            AssertEqual("True", html.Contains("class=\"title-row\"", StringComparison.Ordinal).ToString(), "dashboard html title status row");
            AssertEqual("True", IsDashboardErrorPlacedNearTitle(html).ToString(), "dashboard html error status near title");
            AssertEqual("True", html.Contains("toggleTheme", StringComparison.Ordinal).ToString(), "dashboard html theme toggle");
            AssertEqual("True", html.Contains("draggable = true", StringComparison.Ordinal).ToString(), "dashboard html draggable cards");
            AssertEqual("True", html.Contains("handleDrop", StringComparison.Ordinal).ToString(), "dashboard html drop handler");
            AssertEqual("True", html.Contains("CPU 占用率", StringComparison.Ordinal).ToString(), "dashboard html cpu card");
            AssertEqual("False", html.Contains("appendMetricSource", StringComparison.Ordinal).ToString(), "dashboard html no metric source renderer");
            AssertEqual("True", html.Contains("GPU 温度", StringComparison.Ordinal).ToString(), "dashboard html gpu temperature card");
            AssertEqual("True", html.Contains("grid-template-columns", StringComparison.Ordinal).ToString(), "dashboard html responsive grid");
            AssertEqual("True", html.Contains("height: 206px", StringComparison.Ordinal).ToString(), "dashboard html fixed card height");
            AssertEqual("True", html.Contains("grid-template-rows: minmax(0, auto) minmax(0, auto)", StringComparison.Ordinal).ToString(), "dashboard html fixed network rows");
        }

        private static bool IsDashboardErrorPlacedNearTitle(string html)
        {
            var titleIndex = html.IndexOf("<h1>系统状态</h1>", StringComparison.Ordinal);
            var errorIndex = html.IndexOf("id=\"error\"", StringComparison.Ordinal);
            var cardsIndex = html.IndexOf("id=\"cards\"", StringComparison.Ordinal);

            return titleIndex >= 0
                && errorIndex > titleIndex
                && cardsIndex > errorIndex;
        }

        private static void TestSystemStatusHttpServerAllowsPostCorsMethod()
        {
            var response = SystemStatusHttpServerService.CreateHttpResponse(
                200,
                "OK",
                "application/json; charset=utf-8",
                "{}");

            AssertEqual("True", response.Contains("Access-Control-Allow-Methods: GET, POST, OPTIONS", StringComparison.Ordinal).ToString(), "http response allows post cors method");
            AssertEqual("True", response.Contains("Access-Control-Allow-Headers: Content-Type, Authorization", StringComparison.Ordinal).ToString(), "http response allows authorization cors header");
        }

        private static void TestSettingsServiceDefaultPathUsesExecutableDirectory()
        {
            var expectedPath =
                Path.Combine(AppContext.BaseDirectory, "settings.json");

            AssertEqual(expectedPath, SettingsService.GetDefaultSettingsPath(), nameof(SettingsService.GetDefaultSettingsPath));
        }

        private static void TestAppLogServiceDefaultPathUsesExecutableDirectory()
        {
            var expectedPath =
                Path.Combine(AppContext.BaseDirectory, "app.log");

            AssertEqual(expectedPath, AppLogService.GetDefaultLogPath(), nameof(AppLogService.GetDefaultLogPath));
        }

        private static void TestThemePaletteMapsLightAndDarkColors()
        {
            var darkPalette = SettingsService.GetThemePalette(AppThemeMode.Dark);
            var lightPalette = SettingsService.GetThemePalette(AppThemeMode.Light);

            AssertEqual("#202020", darkPalette.BackgroundColor, nameof(darkPalette.BackgroundColor));
            AssertEqual("#FFFFFF", darkPalette.ForegroundColor, nameof(darkPalette.ForegroundColor));
            AssertEqual("#F2F2F2", lightPalette.BackgroundColor, nameof(lightPalette.BackgroundColor));
            AssertEqual("#202020", lightPalette.ForegroundColor, nameof(lightPalette.ForegroundColor));
        }

        private static void TestTitleBarPaletteMapsLightAndDarkColors()
        {
            var darkPalette = WindowTitleBarService.GetTitleBarPalette(AppThemeMode.Dark);
            var lightPalette = WindowTitleBarService.GetTitleBarPalette(AppThemeMode.Light);

            AssertEqual("#202020", darkPalette.BackgroundColor, nameof(darkPalette.BackgroundColor));
            AssertEqual("#FFFFFF", darkPalette.ForegroundColor, nameof(darkPalette.ForegroundColor));
            AssertEqual("#F2F2F2", lightPalette.BackgroundColor, nameof(lightPalette.BackgroundColor));
            AssertEqual("#202020", lightPalette.ForegroundColor, nameof(lightPalette.ForegroundColor));
        }

        private static void TestTrayIconTreatsContextMenuAsMenuRequest()
        {
            AssertEqual(
                "True",
                TrayIconService.IsContextMenuMessage(0x007B).ToString(),
                nameof(TrayIconService.IsContextMenuMessage));
        }

        private static void TestTrayIconTreatsRightButtonUpAsMenuRequest()
        {
            AssertEqual(
                "True",
                TrayIconService.IsContextMenuMessage(0x0205).ToString(),
                nameof(TrayIconService.IsContextMenuMessage));
        }

        private static void TestTrayIconExtractsMessageFromLowWord()
        {
            var packedMessage = (1 << 16) | 0x007B;

            AssertEqual(
                "True",
                TrayIconService.IsContextMenuMessage(packedMessage).ToString(),
                nameof(TrayIconService.IsContextMenuMessage));
        }

        private static void TestTrayIconUsesVisibleTooltipFlags()
        {
            const int nifTip = 0x00000004;
            const int nifShowTip = 0x00000080;
            var flags = TrayIconService.GetNotifyIconFlags();

            AssertEqual("True", ((flags & nifTip) == nifTip).ToString(), "tray tooltip text flag");
            AssertEqual("True", ((flags & nifShowTip) == nifShowTip).ToString(), "tray standard tooltip display flag");
            AssertEqual("NetSpeedWidget", TrayIconService.GetTooltipText(), "tray tooltip text");
        }

        private static void TestApplicationUsesCustomIconAsset()
        {
            var project = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NetSpeedWidget.csproj"));

            AssertEqual("True", project.Contains("<ApplicationIcon>Assets\\AppIcon.ico</ApplicationIcon>", StringComparison.Ordinal).ToString(), "project custom application icon");
        }

        private static void TestTrayIconLoadsCustomApplicationIcon()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "TrayIconService.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("LoadCustomIcon()", StringComparison.Ordinal).ToString(), "tray icon custom loader");
            AssertEqual("True", code.Contains("_iconHandle = LoadCustomIcon();", StringComparison.Ordinal).ToString(), "tray icon constructor uses custom loader");
            AssertEqual("True", code.Contains("LoadImage(", StringComparison.Ordinal).ToString(), "tray icon loads application icon resource");
            AssertEqual("False", code.Contains("_iconHandle = LoadIcon(IntPtr.Zero, IdiApplication);", StringComparison.Ordinal).ToString(), "tray icon constructor must not use default application icon directly");
        }

        private static void TestProjectExcludesPublishArtifactsFromBuildItems()
        {
            var project = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NetSpeedWidget.csproj"));

            AssertEqual("True", project.Contains("<None Remove=\"publish\\**\" />", StringComparison.Ordinal).ToString(), "project excludes publish none items");
            AssertEqual("True", project.Contains("<Content Remove=\"publish\\**\" />", StringComparison.Ordinal).ToString(), "project excludes publish content items");
        }

        private static void TestWindowStyleHidesTaskbarButton()
        {
            const int appWindowStyle = 0x00040000;
            const int toolWindowStyle = 0x00000080;

            var style = WindowStyleService.HideFromTaskbar(appWindowStyle);

            AssertEqual("False", ((style & appWindowStyle) == appWindowStyle).ToString(), nameof(appWindowStyle));
            AssertEqual("True", ((style & toolWindowStyle) == toolWindowStyle).ToString(), nameof(toolWindowStyle));
        }

        private static void TestWindowStyleSelectsTopmostInsertAfter()
        {
            AssertEqual(
                "-1",
                WindowStyleService.GetTopmostInsertAfter(true).ToInt64().ToString(),
                nameof(WindowStyleService.GetTopmostInsertAfter));
        }

        private static void TestWindowStyleSelectsNoTopmostInsertAfter()
        {
            AssertEqual(
                "-2",
                WindowStyleService.GetTopmostInsertAfter(false).ToInt64().ToString(),
                nameof(WindowStyleService.GetTopmostInsertAfter));
        }

        private static void TestWidgetPlacementAlignsToBottomTaskbarLeft()
        {
            var point = WidgetPlacementService.CalculateTaskbarPosition(
                WidgetDisplayMode.TaskbarLeft,
                new RectBounds(0, 0, 1920, 1080),
                new RectBounds(0, 0, 1920, 1040),
                168,
                32,
                4);

            AssertEqual("4", point.X.ToString(), nameof(point.X));
            AssertEqual("1044", point.Y.ToString(), nameof(point.Y));
        }

        private static void TestWidgetPlacementAlignsToBottomTaskbarRight()
        {
            var point = WidgetPlacementService.CalculateTaskbarPosition(
                WidgetDisplayMode.TaskbarRight,
                new RectBounds(0, 0, 1920, 1080),
                new RectBounds(0, 0, 1920, 1040),
                168,
                32,
                4);

            AssertEqual("1748", point.X.ToString(), nameof(point.X));
            AssertEqual("1044", point.Y.ToString(), nameof(point.Y));
        }

        private static void TestWidgetPlacementUsesCustomTaskbarEdgePadding()
        {
            var leftPoint = WidgetPlacementService.CalculateTaskbarPosition(
                WidgetDisplayMode.TaskbarLeft,
                new RectBounds(0, 0, 1920, 1080),
                new RectBounds(0, 0, 1920, 1040),
                168,
                32,
                20);
            var rightPoint = WidgetPlacementService.CalculateTaskbarPosition(
                WidgetDisplayMode.TaskbarRight,
                new RectBounds(0, 0, 1920, 1080),
                new RectBounds(0, 0, 1920, 1040),
                168,
                32,
                20);

            AssertEqual("20", leftPoint.X.ToString(), nameof(leftPoint.X));
            AssertEqual("1732", rightPoint.X.ToString(), nameof(rightPoint.X));
        }

        private static void TestWidgetPlacementAvoidsRightTrayReservedBounds()
        {
            var point = WidgetPlacementService.CalculateTaskbarPositionFromBounds(
                WidgetDisplayMode.TaskbarRight,
                new RectBounds(0, 1040, 1452, 40),
                168,
                32,
                12);

            AssertEqual("1272", point.X.ToString(), "right x before tray area");
            AssertEqual("1044", point.Y.ToString(), "right y before tray area");
        }

        private static void TestWidgetPlacementIdentifiesTaskbarModes()
        {
            AssertEqual("False", WidgetPlacementService.IsTaskbarMode(WidgetDisplayMode.Floating).ToString(), nameof(WidgetPlacementService.IsTaskbarMode));
            AssertEqual("True", WidgetPlacementService.IsTaskbarMode(WidgetDisplayMode.TaskbarLeft).ToString(), nameof(WidgetPlacementService.IsTaskbarMode));
            AssertEqual("True", WidgetPlacementService.IsTaskbarMode(WidgetDisplayMode.TaskbarRight).ToString(), nameof(WidgetPlacementService.IsTaskbarMode));
        }

        private static void TestForegroundServiceDetectsFullscreenWindow()
        {
            var result = WindowForegroundService.IsFullscreenWindow(
                new RectBounds(0, 0, 1920, 1080),
                new RectBounds(0, 0, 1920, 1080));

            AssertEqual("True", result.ToString(), nameof(WindowForegroundService.IsFullscreenWindow));
        }

        private static void TestForegroundServiceDoesNotTreatMaximizedWorkAreaAsFullscreen()
        {
            var result = WindowForegroundService.IsFullscreenWindow(
                new RectBounds(0, 0, 1920, 1040),
                new RectBounds(0, 0, 1920, 1080));

            AssertEqual("False", result.ToString(), nameof(WindowForegroundService.IsFullscreenWindow));
        }

        private static void TestForegroundServiceDetectsShellOverlayProcesses()
        {
            AssertEqual("True", WindowForegroundService.IsShellOverlayProcess("StartMenuExperienceHost").ToString(), "start menu process");
            AssertEqual("True", WindowForegroundService.IsShellOverlayProcess("SearchHost").ToString(), "search process");
            AssertEqual("True", WindowForegroundService.IsShellOverlayProcess("SearchApp").ToString(), "legacy search process");
            AssertEqual("False", WindowForegroundService.IsShellOverlayProcess("ApplicationFrameHost").ToString(), "hosted app process");
            AssertEqual("False", WindowForegroundService.IsShellOverlayProcess("notepad").ToString(), "normal process");
        }

        private static void TestForegroundServiceDetectsDesktopShellClasses()
        {
            AssertEqual("True", WindowForegroundService.IsDesktopShellWindow("explorer", "Progman").ToString(), "desktop shell window");
            AssertEqual("True", WindowForegroundService.IsDesktopShellWindow("explorer", "WorkerW").ToString(), "desktop worker window");
            AssertEqual("True", WindowForegroundService.IsDesktopShellWindow("explorer", "Shell_TrayWnd").ToString(), "taskbar shell window");
            AssertEqual("False", WindowForegroundService.IsDesktopShellWindow("explorer", "CabinetWClass").ToString(), "file explorer window");
            AssertEqual("False", WindowForegroundService.IsDesktopShellWindow("notepad", "Progman").ToString(), "non explorer class");
        }

        private static void TestForegroundServiceCanDescribeForegroundState()
        {
            var description = WindowForegroundService.DescribeForegroundState(
                new ForegroundWindowInfo(
                    ProcessName: "StartMenuExperienceHost",
                    ClassName: "Windows.UI.Core.CoreWindow",
                    WindowTitle: "Start",
                    Bounds: new RectBounds(0, 0, 1920, 1080),
                    MonitorBounds: new RectBounds(0, 0, 1920, 1080)),
                new ForegroundWindowState(
                    IsFullscreenApplication: false,
                    IsShellOverlayWindow: true),
                TaskbarOverlayAction.ForceShowTopmost);

            AssertEqual("True", description.Contains("process=StartMenuExperienceHost", StringComparison.Ordinal).ToString(), "foreground description process");
            AssertEqual("True", description.Contains("class=Windows.UI.Core.CoreWindow", StringComparison.Ordinal).ToString(), "foreground description class");
            AssertEqual("True", description.Contains("action=ForceShowTopmost", StringComparison.Ordinal).ToString(), "foreground description action");
        }

        private static void TestForegroundServiceChoosesTaskbarOverlayActions()
        {
            var normal = WindowForegroundService.GetTaskbarOverlayAction(
                new ForegroundWindowState(IsFullscreenApplication: false, IsShellOverlayWindow: false));
            var shell = WindowForegroundService.GetTaskbarOverlayAction(
                new ForegroundWindowState(IsFullscreenApplication: false, IsShellOverlayWindow: true));
            var fullscreen = WindowForegroundService.GetTaskbarOverlayAction(
                new ForegroundWindowState(IsFullscreenApplication: true, IsShellOverlayWindow: false));

            AssertEqual(TaskbarOverlayAction.ShowTopmost.ToString(), normal.ToString(), "normal overlay action");
            AssertEqual(TaskbarOverlayAction.ForceShowTopmost.ToString(), shell.ToString(), "shell overlay action");
            AssertEqual(TaskbarOverlayAction.Hide.ToString(), fullscreen.ToString(), "fullscreen overlay action");
        }

        private static void TestAppBarContentAlignsLeft()
        {
            var point = WidgetPlacementService.CalculateAppBarContentPosition(
                WidgetDisplayMode.TaskbarLeft,
                new RectBounds(0, 1008, 1920, 32),
                168,
                32,
                4);

            AssertEqual("4", point.X.ToString(), nameof(point.X));
            AssertEqual("1008", point.Y.ToString(), nameof(point.Y));
        }

        private static void TestAppBarContentAlignsRight()
        {
            var point = WidgetPlacementService.CalculateAppBarContentPosition(
                WidgetDisplayMode.TaskbarRight,
                new RectBounds(0, 1008, 1920, 32),
                168,
                32,
                4);

            AssertEqual("1748", point.X.ToString(), nameof(point.X));
            AssertEqual("1008", point.Y.ToString(), nameof(point.Y));
        }

        private static void TestWidgetPlacementAlignsInsideExplicitTaskbarBounds()
        {
            var left = WidgetPlacementService.CalculateTaskbarPositionFromBounds(
                WidgetDisplayMode.TaskbarLeft,
                new RectBounds(1920, 1040, 1920, 40),
                168,
                32,
                8);
            var right = WidgetPlacementService.CalculateTaskbarPositionFromBounds(
                WidgetDisplayMode.TaskbarRight,
                new RectBounds(1920, 1040, 1920, 40),
                168,
                32,
                8);

            AssertEqual("1928", left.X.ToString(), "left x inside explicit taskbar");
            AssertEqual("1044", left.Y.ToString(), "left y inside explicit taskbar");
            AssertEqual("3664", right.X.ToString(), "right x inside explicit taskbar");
            AssertEqual("1044", right.Y.ToString(), "right y inside explicit taskbar");
        }

        private static void TestTaskbarOverlayUsesSystemTextRenderingSettings()
        {
            AssertEqual("Segoe UI", NativeTextOverlayRenderService.FontFaceName, nameof(NativeTextOverlayRenderService.FontFaceName));
            AssertEqual("5", NativeTextOverlayRenderService.FontQuality.ToString(), nameof(NativeTextOverlayRenderService.FontQuality));
            AssertEqual("400", NativeTextOverlayRenderService.FontWeight.ToString(), nameof(NativeTextOverlayRenderService.FontWeight));
        }

        private static void TestTaskbarOverlayConvertsSpeedFontSizeToGdiFontHeight()
        {
            AssertEqual("-10", NativeTextOverlayRenderService.GetFontHeight(10).ToString(), "font height 10");
            AssertEqual("-12", NativeTextOverlayRenderService.GetFontHeight(12).ToString(), "font height 12");
            AssertEqual("-18", NativeTextOverlayRenderService.GetFontHeight(18).ToString(), "font height 18");
        }

        private static void TestTaskbarOverlayCalculatesSeparatedTextBaselines()
        {
            var baselines = NativeTextOverlayRenderService.CalculateTextBaselines(32, 16);

            AssertEqual("True", (baselines.DownloadBaseline > baselines.UploadBaseline).ToString(), "download baseline below upload");
            AssertEqual("True", ((baselines.DownloadBaseline - baselines.UploadBaseline) >= 18).ToString(), "baseline spacing");
            AssertEqual("True", (baselines.UploadBaseline > 0).ToString(), "upload baseline inside window");
            AssertEqual("True", (baselines.DownloadBaseline <= 31).ToString(), "download baseline inside window");
        }

        private static void TestTaskbarOverlayConvertsTextMaskToAlphaPixels()
        {
            var pixels =
                new byte[]
                {
                    0, 0, 0, 0,
                    64, 64, 64, 0,
                    255, 255, 255, 0
                };
            var color = NativeTextOverlayRenderService.GetTextColor(AppThemeMode.Dark);

            NativeTextOverlayRenderService.ApplyTextMaskToPixels(pixels, color);

            AssertEqual("0", pixels[3].ToString(), "transparent alpha");
            AssertEqual("64", pixels[7].ToString(), "edge alpha");
            AssertEqual("255", pixels[11].ToString(), "solid alpha");
            AssertEqual("64", pixels[4].ToString(), "edge blue");
            AssertEqual("64", pixels[5].ToString(), "edge green");
            AssertEqual("64", pixels[6].ToString(), "edge red");
        }

        private static void TestNativeTextOverlayWindowAttachesToTaskbarParent()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("SetParent(_hwnd, taskbarWindow)", StringComparison.Ordinal).ToString(), "taskbar parent SetParent");
            AssertEqual("True", code.Contains("Shell_TrayWnd", StringComparison.Ordinal).ToString(), "primary taskbar class");
            AssertEqual("True", code.Contains("Shell_SecondaryTrayWnd", StringComparison.Ordinal).ToString(), "secondary taskbar class");
            AssertEqual("True", code.Contains("CalculateParentedPoint", StringComparison.Ordinal).ToString(), "taskbar parent coordinate conversion");
        }

        private static void TestNativeTextOverlayWindowCanEnumerateTaskbarBounds()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("public readonly record struct TaskbarWindowInfo", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow taskbar window info record");
            AssertEqual("True", code.Contains("IntPtr WindowHandle", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow taskbar window handle");
            AssertEqual("True", code.Contains("public static IReadOnlyList<TaskbarWindowInfo> GetTaskbarWindows()", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow taskbar window enumeration method");
            AssertEqual("True", code.Contains("new TaskbarWindowInfo(", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow adds each taskbar window");
            AssertEqual("True", code.Contains("taskbarWindows.Sort", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow sorts taskbar windows");
        }

        private static void TestNativeTextOverlayWindowCanEnumerateTaskbarAvailableBounds()
        {
            var overlayCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");
            var mainWindowCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", overlayCode.Contains("RectBounds AvailableBounds", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow taskbar available bounds");
            AssertEqual("True", overlayCode.Contains("TrayNotifyWnd", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow detects tray notify area");
            AssertEqual("True", overlayCode.Contains("EnumChildWindows", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow enumerates taskbar child windows");
            AssertEqual("True", overlayCode.Contains("GetTaskbarAvailableBounds", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow computes available taskbar bounds");
            AssertEqual("True", overlayCode.Contains("bounds = GetTaskbarAvailableBounds(taskbarWindow, bounds)", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow cached taskbar bounds use available bounds");
            AssertEqual("True", mainWindowCode.Contains("taskbarWindow.AvailableBounds", StringComparison.Ordinal).ToString(), "MainWindow positions overlay inside available taskbar bounds");
        }

        private static void TestNativeTextOverlayWindowFiltersUnavailableTaskbars()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("IsWindowVisible(hwnd)", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow skips hidden taskbar windows");
            AssertEqual("True", code.Contains("IsTaskbarOnCurrentDisplay(bounds)", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow skips off-screen taskbar windows");
            AssertEqual("True", code.Contains("MonitorFromRect", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow checks current monitor coverage");
            AssertEqual("True", code.Contains("MonitorDefaultToNull", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow rejects taskbars outside current monitors");
        }

        private static void TestNativeTextOverlayWindowFiltersFallbackTaskbarLookup()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");
            var methodStart = code.IndexOf("private static IntPtr FindTaskbarWindowForPoint", StringComparison.Ordinal);
            var nextMethodStart = code.IndexOf("private static bool IsTaskbarClass", StringComparison.Ordinal);

            if (methodStart < 0 || nextMethodStart < 0 || nextMethodStart <= methodStart)
            {
                throw new InvalidOperationException("Could not locate FindTaskbarWindowForPoint method body.");
            }

            var methodCode = code[methodStart..nextMethodStart];

            AssertEqual("True", code.Contains("private static bool IsAvailableTaskbarWindow", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow has shared available-taskbar filter");
            AssertEqual("True", methodCode.Contains("IsAvailableTaskbarWindow(hwnd, out var bounds)", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow filters point-based taskbar lookup");
            AssertEqual("False", methodCode.Contains("FindWindow(PrimaryTaskbarClassName, null)", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow does not attach to primary taskbar when point lookup misses");
        }

        private static void TestNativeTextOverlayWindowCanResetTaskbarParent()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");
            var normalizedCode = code.Replace("\r\n", "\n", StringComparison.Ordinal);

            AssertEqual("True", code.Contains("public void ResetTaskbarParent()", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow ResetTaskbarParent method");
            AssertEqual("True", normalizedCode.Contains("public void ResetTaskbarParent()\n        {\n            DetachFromTaskbar();\n        }", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow reset detaches taskbar parent");
        }

        private static void TestNativeTextOverlayReattachesWhenCachedTaskbarParentIsLost()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");
            var methodStart = code.IndexOf("private WidgetPoint AttachToTaskbarIfPossible(", StringComparison.Ordinal);
            var nextMethodStart = code.IndexOf("private void DetachFromTaskbar()", StringComparison.Ordinal);

            if (methodStart < 0 || nextMethodStart < 0 || nextMethodStart <= methodStart)
            {
                throw new InvalidOperationException("Could not locate AttachToTaskbarIfPossible method body.");
            }

            var methodCode = code[methodStart..nextMethodStart];

            AssertEqual("True", methodCode.Contains("GetParent(_hwnd) != taskbarWindow", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow detects lost real taskbar parent");
            AssertEqual("True", methodCode.Contains("_parentWindow = IntPtr.Zero", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow clears stale cached taskbar parent");
        }

        private static void TestApplicationManifestEnablesPerMonitorDpiAwareness()
        {
            var projectDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
            var projectFile = File.ReadAllText(Path.Combine(projectDirectory, "NetSpeedWidget.csproj"));
            var manifestFile = File.ReadAllText(Path.Combine(projectDirectory, "app.manifest"));

            AssertEqual("True", projectFile.Contains("<ApplicationManifest>app.manifest</ApplicationManifest>", StringComparison.Ordinal).ToString(), "project app manifest reference");
            AssertEqual("True", manifestFile.Contains("<dpiAwareness ", StringComparison.Ordinal).ToString(), "app manifest dpi awareness element");
            AssertEqual("True", manifestFile.Contains("PerMonitorV2</dpiAwareness>", StringComparison.Ordinal).ToString(), "app manifest dpi awareness value");
            AssertEqual("True", manifestFile.Contains("<dpiAware ", StringComparison.Ordinal).ToString(), "app manifest legacy dpi awareness element");
            AssertEqual("True", manifestFile.Contains("true/pm</dpiAware>", StringComparison.Ordinal).ToString(), "app manifest legacy dpi awareness value");
            AssertEqual("True", manifestFile.Contains("level=\"asInvoker\"", StringComparison.Ordinal).ToString(), "app manifest standard user privilege");
        }

        private static void TestMainWindowAppliesSystemStatusHttpServerLifecycle()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("SystemStatusHttpServerService _systemStatusHttpServerService", StringComparison.Ordinal).ToString(), "main window system status http field");
            AssertEqual("True", code.Contains("_systemStatusHttpServerService = new SystemStatusHttpServerService(", StringComparison.Ordinal).ToString(), "main window system status http constructor");
            AssertEqual("True", code.Contains("ApplySystemStatusHttpServer();", StringComparison.Ordinal).ToString(), "main window applies system status http");
            AssertEqual("True", code.Contains("_systemStatusHttpServerService.Dispose();", StringComparison.Ordinal).ToString(), "main window disposes system status http");
            AssertEqual("True", code.Contains("_systemStatusHttpServerService.ApplySettings(_settings)", StringComparison.Ordinal).ToString(), "main window applies system status settings");
            AssertEqual("True", code.Contains("_systemStatusHttpServerService.GetState", StringComparison.Ordinal).ToString(), "main window passes system status runtime state");
        }

        private static void TestMainWindowDoesNotResetTaskbarOverlayForSystemStatusOnlySettings()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("ShouldApplyWidgetDisplaySettings", StringComparison.Ordinal).ToString(), "main window compares widget display settings before resetting taskbar overlay");
            AssertEqual("True", code.Contains("WidgetDisplaySettingsSnapshot", StringComparison.Ordinal).ToString(), "main window stores independent widget display settings snapshot");
            AssertEqual("True", code.Contains("_appliedWidgetDisplaySettings", StringComparison.Ordinal).ToString(), "main window does not compare shared AppSettings references");
            AssertEqual("True", code.Contains("ApplySystemStatusHttpServer();", StringComparison.Ordinal).ToString(), "main window still applies system status service changes");
            AssertEqual("True", code.Contains("if (ShouldApplyWidgetDisplaySettings(_settings))", StringComparison.Ordinal).ToString(), "main window gates widget display reset behind widget setting changes");
        }

        private static void TestMainWindowMovesTaskbarOverlayForPaddingOnlySettings()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("ApplyWidgetDisplaySettings(", StringComparison.Ordinal).ToString(), "main window centralizes widget display setting application");
            AssertEqual("True", code.Contains("HasTaskbarPlacementSettingChanged", StringComparison.Ordinal).ToString(), "main window detects taskbar placement-only setting changes");
            AssertEqual("True", code.Contains("MoveTaskbarOverlayForCurrentSettings()", StringComparison.Ordinal).ToString(), "main window moves taskbar overlay without display mode reset");
            AssertEqual("True", code.Contains("_overlayRegistry.Synchronize(handles)", StringComparison.Ordinal).ToString(), "main window preserves taskbar parent when only placement changed");
        }

        private static void TestMainWindowUsesCachedTaskbarPlacementsWhenTaskbarEnumerationFails()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");
            var methodStart = code.IndexOf("private IReadOnlyList<TaskbarOverlayPlacement> CalculateTaskbarOverlayPlacements", StringComparison.Ordinal);
            var nextMethodStart = code.IndexOf("private void RefreshTaskbarOverlayPlacementIfNeeded()", StringComparison.Ordinal);

            if (methodStart < 0 || nextMethodStart < 0 || nextMethodStart <= methodStart)
            {
                throw new InvalidOperationException("Could not locate CalculateTaskbarOverlayPlacements method body.");
            }

            var methodCode = code[methodStart..nextMethodStart];

            AssertEqual("True", code.Contains("TryCalculateCachedTaskbarOverlayPlacements", StringComparison.Ordinal).ToString(), "MainWindow has cached taskbar placement fallback");
            AssertEqual("True", methodCode.Contains("TryCalculateCachedTaskbarOverlayPlacements(displayMode, out var cachedPlacements)", StringComparison.Ordinal).ToString(), "MainWindow tries cached placement before display-area fallback");
            AssertEqual("True", code.Contains("Taskbar overlay taskbar lookup returned no windows; using cached taskbar placement.", StringComparison.Ordinal).ToString(), "MainWindow logs cached placement fallback");
            AssertEqual("True", code.Contains("placement.TaskbarWindow == IntPtr.Zero", StringComparison.Ordinal).ToString(), "MainWindow keeps only real cached taskbar windows");
        }

        private static void TestMainWindowRefreshesTaskbarPlacementWithoutResetWhenParentUnchanged()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");
            var methodStart = code.IndexOf("private void RefreshTaskbarOverlayPlacement(bool force)", StringComparison.Ordinal);
            var nextMethodStart = code.IndexOf("private void MoveTaskbarOverlayForCurrentSettings()", StringComparison.Ordinal);

            if (methodStart < 0 || nextMethodStart < 0 || nextMethodStart <= methodStart)
            {
                throw new InvalidOperationException("Could not locate RefreshTaskbarOverlayPlacement method body.");
            }

            var methodCode = code[methodStart..nextMethodStart];

            AssertEqual("True", methodCode.Contains("var topologyChanged = HasTaskbarSetChanged(placements);", StringComparison.Ordinal).ToString(), "MainWindow detects parent changes during taskbar refresh");
            AssertEqual("True", methodCode.Contains("ApplyTaskbarOverlayPlacements(placements)", StringComparison.Ordinal).ToString(), "MainWindow does not always reset parents during taskbar refresh");
            AssertEqual("False", methodCode.Contains("ResetTaskbarParents(", StringComparison.Ordinal).ToString(), "MainWindow avoids unconditional parent reset during taskbar refresh");
        }

        private static void TestMainWindowMovesTaskbarOverlayForTaskbarSideSwitch()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");
            var methodStart = code.IndexOf("private void ApplyWidgetDisplaySettings(", StringComparison.Ordinal);
            var nextMethodStart = code.IndexOf("private static bool HasTaskbarPlacementSettingChanged(", StringComparison.Ordinal);

            if (methodStart < 0 || nextMethodStart < 0 || nextMethodStart <= methodStart)
            {
                throw new InvalidOperationException("Could not locate ApplyWidgetDisplaySettings method body.");
            }

            var methodCode = code[methodStart..nextMethodStart];

            AssertEqual("True", code.Contains("IsTaskbarSideSwitch(previousSettings, currentSettings)", StringComparison.Ordinal).ToString(), "MainWindow detects taskbar side switch");
            AssertEqual("True", methodCode.Contains("if (IsTaskbarSideSwitch(previousSettings, currentSettings))", StringComparison.Ordinal).ToString(), "MainWindow handles taskbar side switch before full display reset");
            AssertEqual("True", methodCode.Contains("MoveTaskbarOverlayForCurrentSettings();", StringComparison.Ordinal).ToString(), "MainWindow moves overlay for taskbar side switch");
            AssertEqual("True", methodCode.Contains("ApplyDisplayMode(currentSettings.DisplayMode);", StringComparison.Ordinal).ToString(), "MainWindow still fully applies floating/taskbar switches");
        }

        private static void TestMainWindowDisposesTaskbarOverlaysWhenSwitchingToFloating()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");
            var methodStart = code.IndexOf("private void ApplyDisplayMode(WidgetDisplayMode displayMode)", StringComparison.Ordinal);
            var nextMethodStart = code.IndexOf("private void ShowTaskbarOverlay()", StringComparison.Ordinal);

            if (methodStart < 0 || nextMethodStart < 0 || nextMethodStart <= methodStart)
            {
                throw new InvalidOperationException("Could not locate ApplyDisplayMode method body.");
            }

            var methodCode = code[methodStart..nextMethodStart];

            AssertEqual("True", methodCode.Contains("Taskbar overlay placement applied for display mode.", StringComparison.Ordinal).ToString(), "MainWindow logs taskbar placement when entering taskbar mode");
            AssertEqual("True", methodCode.Contains("DisposeTaskbarOverlays();", StringComparison.Ordinal).ToString(), "MainWindow destroys taskbar overlays when leaving taskbar mode");
            AssertEqual("True", methodCode.Contains("_taskbarOverlayPlacements = [];", StringComparison.Ordinal).ToString(), "MainWindow clears taskbar placement cache when switching to floating");
        }

        private static void TestMainWindowWritesDiagnosticLogForStartupAndDisplayModeChanges()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("NetSpeedWidget started.", StringComparison.Ordinal).ToString(), "MainWindow writes startup diagnostic log");
            AssertEqual("True", code.Contains("Taskbar overlay display mode changed.", StringComparison.Ordinal).ToString(), "MainWindow logs display mode changes");
        }

        private static void TestSettingsWindowUsesStableTextBoxForTaskbarPadding()
        {
            var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SettingsWindow.xaml"));

            AssertEqual("False", xaml.Contains("<NumberBox", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml NumberBox usage");
            AssertEqual("True", xaml.Contains("TaskbarEdgePaddingTextBox", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml taskbar padding TextBox");
            AssertEqual("True", xaml.Contains("SpeedFontSizeTextBox", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml speed font size TextBox");
            AssertEqual("True", xaml.Contains("SpeedFontSizeUnitLabel", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml speed font size unit label");
        }

        private static void TestSettingsWindowUsesGroupedSettingRows()
        {
            var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SettingsWindow.xaml"));

            AssertEqual("True", xaml.Contains("AppearanceSectionTitle", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml appearance section");
            AssertEqual("True", xaml.Contains("WindowSectionTitle", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml window section");
            AssertEqual("True", xaml.Contains("Style=\"{StaticResource SettingRowBorderStyle}\"", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml setting row style");
        }

        private static void TestSettingsWindowUsesLeftNavigationPages()
        {
            var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SettingsWindow.xaml"));

            AssertEqual("True", xaml.Contains("SettingsNavigationColumn", StringComparison.Ordinal).ToString(), "settings navigation column");
            AssertEqual("True", xaml.Contains("SettingsNavigationRail", StringComparison.Ordinal).ToString(), "settings navigation rail");
            AssertEqual("True", xaml.Contains("SettingsNavigationButtonStyle", StringComparison.Ordinal).ToString(), "settings navigation button style");
            AssertEqual("True", xaml.Contains("NetSpeedNavigationSelectionBar", StringComparison.Ordinal).ToString(), "net speed navigation selection bar");
            AssertEqual("True", xaml.Contains("SystemStatusNavigationSelectionBar", StringComparison.Ordinal).ToString(), "system status navigation selection bar");
            AssertEqual("True", xaml.Contains("NetSpeedNavigationButton", StringComparison.Ordinal).ToString(), "net speed navigation button");
            AssertEqual("True", xaml.Contains("SystemStatusNavigationButton", StringComparison.Ordinal).ToString(), "system status navigation button");
            AssertEqual("True", xaml.Contains("NetSpeedSettingsPanel", StringComparison.Ordinal).ToString(), "net speed settings panel");
            AssertEqual("True", xaml.Contains("SystemStatusSettingsPanel", StringComparison.Ordinal).ToString(), "system status settings panel");
        }

        private static void TestSettingsWindowSwitchesSettingsPages()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SettingsWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("private enum SettingsPage", StringComparison.Ordinal).ToString(), "settings page enum");
            AssertEqual("True", code.Contains("_currentSettingsPage", StringComparison.Ordinal).ToString(), "current settings page tracking");
            AssertEqual("True", code.Contains("ShowSettingsPage(SettingsPage.NetSpeed)", StringComparison.Ordinal).ToString(), "net speed page switch");
            AssertEqual("True", code.Contains("ShowSettingsPage(SettingsPage.SystemStatus)", StringComparison.Ordinal).ToString(), "system status page switch");
            AssertEqual("True", code.Contains("ApplyNavigationSelection(page)", StringComparison.Ordinal).ToString(), "navigation selection refresh");
            AssertEqual("True", code.Contains("SystemStatusSettingsPanel.Visibility", StringComparison.Ordinal).ToString(), "system status panel visibility switch");
        }

        private static void TestSettingsWindowContainsSystemStatusControls()
        {
            var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SettingsWindow.xaml"));

            AssertEqual("True", xaml.Contains("SystemStatusEnabledToggle", StringComparison.Ordinal).ToString(), "system status enabled toggle");
            AssertEqual("True", xaml.Contains("SystemStatusServerToggle", StringComparison.Ordinal).ToString(), "system status server toggle");
            AssertEqual("True", xaml.Contains("SystemStatusPortTextBox", StringComparison.Ordinal).ToString(), "system status port textbox");
            AssertEqual("True", xaml.Contains("SystemStatusServiceStatusText", StringComparison.Ordinal).ToString(), "system status runtime status text");
            AssertEqual("True", xaml.Contains("SystemStatusAccessAddressText", StringComparison.Ordinal).ToString(), "system status access address text");
            AssertEqual("True", xaml.Contains("SystemStatusServiceErrorText", StringComparison.Ordinal).ToString(), "system status service error text");
            AssertEqual("True", xaml.Contains("SystemStatusRefreshIntervalCombo", StringComparison.Ordinal).ToString(), "system status refresh interval combo");
            AssertEqual("True", xaml.Contains("SystemStatusPasswordToggle", StringComparison.Ordinal).ToString(), "system status password toggle");
            AssertEqual("True", xaml.Contains("SystemStatusPasswordBox", StringComparison.Ordinal).ToString(), "system status password input");
            AssertEqual("True", xaml.Contains("SystemStatusWebThemeCombo", StringComparison.Ordinal).ToString(), "system status web theme combo");
            AssertEqual("True", xaml.Contains("NetworkStatusCardToggle", StringComparison.Ordinal).ToString(), "network status card toggle");
            AssertEqual("True", xaml.Contains("CpuUsageCardToggle", StringComparison.Ordinal).ToString(), "cpu usage card toggle");
            AssertEqual("True", xaml.Contains("CpuTemperatureCardToggle", StringComparison.Ordinal).ToString(), "cpu temperature card toggle");
            AssertEqual("True", xaml.Contains("GpuUsageCardToggle", StringComparison.Ordinal).ToString(), "gpu usage card toggle");
            AssertEqual("True", xaml.Contains("GpuTemperatureCardToggle", StringComparison.Ordinal).ToString(), "gpu temperature card toggle");
            AssertEqual("True", xaml.Contains("MemoryUsageCardToggle", StringComparison.Ordinal).ToString(), "memory usage card toggle");
        }

        private static void TestSettingsWindowHandlesSystemStatusSettings()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SettingsWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("LoadSystemStatusSettings()", StringComparison.Ordinal).ToString(), "load system status settings");
            AssertEqual("True", code.Contains("SaveSystemStatusPortFromTextBox()", StringComparison.Ordinal).ToString(), "save system status port");
            AssertEqual("True", code.Contains("SystemStatusEnabledToggle_Toggled", StringComparison.Ordinal).ToString(), "system status enabled toggled");
            AssertEqual("True", code.Contains("SystemStatusRefreshIntervalCombo_SelectionChanged", StringComparison.Ordinal).ToString(), "refresh interval selection changed");
            AssertEqual("True", code.Contains("SystemStatusWebThemeCombo_SelectionChanged", StringComparison.Ordinal).ToString(), "web theme selection changed");
            AssertEqual("True", code.Contains("Func<SystemStatusHttpServerState>", StringComparison.Ordinal).ToString(), "system status runtime state provider");
            AssertEqual("True", code.Contains("RefreshSystemStatusServerState()", StringComparison.Ordinal).ToString(), "system status runtime state refresh");
            AssertEqual("True", code.Contains("SaveSystemStatusPasswordFromInput()", StringComparison.Ordinal).ToString(), "system status password save");
            AssertEqual("True", code.Contains("CreatePasswordHash", StringComparison.Ordinal).ToString(), "system status password hash");
        }

        private static void TestSettingsWindowThemesSystemStatusControls()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SettingsWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("SystemStatusTitleText.Foreground = foregroundBrush", StringComparison.Ordinal).ToString(), "system status title themed");
            AssertEqual("True", code.Contains("SystemStatusServiceSectionTitle.Foreground = sectionForegroundBrush", StringComparison.Ordinal).ToString(), "system status service section themed");
            AssertEqual("True", code.Contains("SystemStatusCardsSectionTitle.Foreground = sectionForegroundBrush", StringComparison.Ordinal).ToString(), "system status cards section themed");
            AssertEqual("True", code.Contains("SystemStatusEnabledLabel.Foreground = foregroundBrush", StringComparison.Ordinal).ToString(), "system status label themed");
            AssertEqual("True", code.Contains("SystemStatusAccessAddressText.Foreground = foregroundBrush", StringComparison.Ordinal).ToString(), "system status access address themed");
            AssertEqual("True", code.Contains("SystemStatusServiceErrorText.Foreground = foregroundBrush", StringComparison.Ordinal).ToString(), "system status service error themed");
            AssertEqual("True", code.Contains("SystemStatusPasswordBox.Foreground = foregroundBrush", StringComparison.Ordinal).ToString(), "system status password input themed");
            AssertEqual("True", code.Contains("NetworkStatusCardLabel.Foreground = foregroundBrush", StringComparison.Ordinal).ToString(), "network status card label themed");
        }

        private static void TestSettingsWindowExtendsContentIntoCustomTitleBar()
        {
            var projectDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
            var xaml = File.ReadAllText(Path.Combine(projectDirectory, "SettingsWindow.xaml"));
            var code = File.ReadAllText(Path.Combine(projectDirectory, "SettingsWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", xaml.Contains("x:Name=\"TitleBarHost\"", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml custom title bar host");
            AssertEqual("True", code.Contains("ExtendsContentIntoTitleBar = true", StringComparison.Ordinal).ToString(), "SettingsWindow custom title bar extension");
            AssertEqual("True", code.Contains("SetTitleBar(TitleBarHost)", StringComparison.Ordinal).ToString(), "SettingsWindow custom title bar drag region");
        }

        private static void TestSettingsWindowUsesScrollableContentArea()
        {
            var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SettingsWindow.xaml"));

            AssertEqual("True", xaml.Contains("<ScrollViewer", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml scroll viewer");
            AssertEqual("True", xaml.Contains("VerticalScrollBarVisibility=\"Auto\"", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml vertical scroll auto");
            AssertEqual("True", xaml.Contains("HorizontalScrollBarVisibility=\"Disabled\"", StringComparison.Ordinal).ToString(), "SettingsWindow.xaml horizontal scroll disabled");
        }

        private static void TestSettingsWindowCentersOnCurrentWorkArea()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SettingsWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("DisplayArea.GetFromWindowId", StringComparison.Ordinal).ToString(), "SettingsWindow display area lookup");
            AssertEqual("True", code.Contains(".WorkArea", StringComparison.Ordinal).ToString(), "SettingsWindow work area centering");
            AssertEqual("True", code.Contains("_appWindow.Move(", StringComparison.Ordinal).ToString(), "SettingsWindow move to center");
        }

        private static void TestNativeTextOverlayWindowCanForceTopmostDisplay()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("public void ForceTopmost()", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow ForceTopmost method");
            AssertEqual("True", code.Contains("SwpShowWindow", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow topmost show flag");
        }

        private static void TestMainWindowUsesForegroundServiceForTaskbarOverlay()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("RefreshTaskbarOverlayVisibility()", StringComparison.Ordinal).ToString(), "MainWindow taskbar overlay refresh");
            AssertEqual("True", code.Contains("WindowForegroundService.GetForegroundWindowState(_hwnd)", StringComparison.Ordinal).ToString(), "MainWindow foreground state");
            AssertEqual("True", code.Contains("TaskbarOverlayAction.ForceShowTopmost", StringComparison.Ordinal).ToString(), "MainWindow shell overlay action");
            AssertEqual("True", code.Contains("TaskbarOverlayAction.Hide", StringComparison.Ordinal).ToString(), "MainWindow fullscreen hide action");
            AssertEqual("True", code.Contains("_taskbarOverlayHiddenByForeground", StringComparison.Ordinal).ToString(), "MainWindow foreground hidden state");
        }

        private static void TestMainWindowRefreshesTaskbarOverlayPlacement()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("TaskbarPlacementRefreshIntervalMilliseconds", StringComparison.Ordinal).ToString(), "MainWindow taskbar placement refresh interval");
            AssertEqual("True", code.Contains("RefreshTaskbarOverlayPlacementIfNeeded()", StringComparison.Ordinal).ToString(), "MainWindow taskbar placement refresh call");
            AssertEqual("True", code.Contains("CalculateTaskbarOverlayPlacements", StringComparison.Ordinal).ToString(), "MainWindow recalculates taskbar placements");
            AssertEqual("True", code.Contains("_overlayRegistry.Synchronize", StringComparison.Ordinal).ToString(), "MainWindow synchronizes taskbar windows by handle");
        }

        private static void TestMainWindowRefreshesTaskbarOverlayOnDisplaySettingsChanged()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("WmDisplayChange = 0x007E", StringComparison.Ordinal).ToString(), "MainWindow declares WM_DISPLAYCHANGE");
            AssertEqual("True", code.Contains("SetWindowLongPtr", StringComparison.Ordinal).ToString(), "MainWindow subclasses native window procedure");
            AssertEqual("True", code.Contains("CallWindowProc", StringComparison.Ordinal).ToString(), "MainWindow forwards native window messages");
            AssertEqual("True", code.Contains("MainWindow_DisplaySettingsChanged", StringComparison.Ordinal).ToString(), "MainWindow display settings changed handler");
            AssertEqual("True", code.Contains("RefreshTaskbarOverlayPlacement(force: true)", StringComparison.Ordinal).ToString(), "MainWindow forces taskbar placement refresh after display change");
            AssertEqual("True", code.Contains("_lastTaskbarPlacementRefreshUtc = DateTime.MinValue", StringComparison.Ordinal).ToString(), "MainWindow bypasses placement refresh throttle after display change");
            AssertEqual("True", code.Contains("DisplayChangeForcedRefreshTicks", StringComparison.Ordinal).ToString(), "MainWindow display change forced refresh tick count");
            AssertEqual("True", code.Contains("_remainingDisplayChangeForcedRefreshTicks", StringComparison.Ordinal).ToString(), "MainWindow tracks delayed display change refreshes");
            AssertEqual("True", code.Contains("RefreshTaskbarOverlayPlacement(force: forcePlacementRefresh)", StringComparison.Ordinal).ToString(), "MainWindow keepalive forces repeated placement refreshes after display change");
            AssertEqual("True", code.Contains("Taskbar overlay display settings changed.", StringComparison.Ordinal).ToString(), "MainWindow logs display setting changes");
            AssertEqual("True", code.Contains("Taskbar overlay placement recalculated.", StringComparison.Ordinal).ToString(), "MainWindow logs taskbar placement recalculation");
            AssertEqual("True", code.Contains("FormatTaskbarOverlayPlacements", StringComparison.Ordinal).ToString(), "MainWindow formats taskbar placement diagnostics");
        }

        private static void TestMainWindowRefreshesTaskbarOverlayOnTaskbarCreated()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("TaskbarCreated", StringComparison.Ordinal).ToString(), "MainWindow registers TaskbarCreated message");
            AssertEqual("True", code.Contains("RegisterWindowMessage", StringComparison.Ordinal).ToString(), "MainWindow uses RegisterWindowMessage");
            AssertEqual("True", code.Contains("message == _taskbarCreatedMessage", StringComparison.Ordinal).ToString(), "MainWindow handles TaskbarCreated message");
            AssertEqual("True", code.Contains("MainWindow_TaskbarCreated()", StringComparison.Ordinal).ToString(), "MainWindow TaskbarCreated handler");
            AssertEqual("True", code.Contains("Taskbar overlay taskbar created.", StringComparison.Ordinal).ToString(), "MainWindow logs taskbar recreation message");
        }

        private static void TestMainWindowUsesAllCurrentTaskbarOverlays()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("List<NativeTextOverlayWindow> _taskbarOverlayWindows", StringComparison.Ordinal).ToString(), "MainWindow keeps multiple taskbar overlay windows");
            AssertEqual("True", code.Contains("NativeTextOverlayWindow.GetTaskbarWindows()", StringComparison.Ordinal).ToString(), "MainWindow gets current taskbar windows");
            AssertEqual("True", code.Contains("EnsureTaskbarOverlayWindowCount", StringComparison.Ordinal).ToString(), "MainWindow syncs overlay count");
            AssertEqual("True", code.Contains("foreach (var taskbarWindow in taskbarWindows)", StringComparison.Ordinal).ToString(), "MainWindow creates placement for each taskbar");
            AssertEqual("True", code.Contains("IntPtr TaskbarWindow", StringComparison.Ordinal).ToString(), "MainWindow keeps taskbar handle in placement");
        }

        private static void TestMainWindowShowsEachTaskbarOverlay()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("for (var index = 0; index < _taskbarOverlayPlacements.Count; index++)", StringComparison.Ordinal).ToString(), "MainWindow loops through every taskbar placement");
            AssertEqual("True", code.Contains("_taskbarOverlayWindows[index].Show(", StringComparison.Ordinal).ToString(), "MainWindow shows the matching overlay window for each placement");
            AssertEqual("True", code.Contains("placement.Point.X", StringComparison.Ordinal).ToString(), "MainWindow uses each placement X coordinate");
            AssertEqual("True", code.Contains("placement.Point.Y", StringComparison.Ordinal).ToString(), "MainWindow uses each placement Y coordinate");
        }

        private static void TestMainWindowPassesExplicitTaskbarWindowToOverlay()
        {
            var mainWindowCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");
            var overlayCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");

            AssertEqual("True", overlayCode.Contains("IntPtr taskbarWindow", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow accepts explicit taskbar window");
            AssertEqual("True", overlayCode.Contains("AttachToTaskbarIfPossible(x, y, taskbarWindow)", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow uses explicit taskbar window during show");
            AssertEqual("True", mainWindowCode.Contains("placement.TaskbarWindow", StringComparison.Ordinal).ToString(), "MainWindow passes explicit taskbar window");
        }

        private static void TestNativeTextOverlayDoesNotFallbackWhenExplicitTaskbarIsStale()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("if (taskbarWindow != IntPtr.Zero && !IsWindow(taskbarWindow))", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow detects stale explicit taskbar window");
            AssertEqual("True", code.Contains("DetachFromTaskbar();\n                return new WidgetPoint(screenX, screenY);", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow detaches instead of falling back from stale explicit taskbar");
            AssertEqual("True", code.Contains("if (taskbarWindow == IntPtr.Zero)\n            {\n                taskbarWindow = FindTaskbarWindowForPoint(screenX, screenY);\n            }", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow only searches by point when no explicit taskbar was provided");
        }

        private static void TestMainWindowRebuildsOverlaysAfterTaskbarCreated()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");

            AssertEqual("True", code.Contains("_taskbarCreatedMessage != 0 && message == _taskbarCreatedMessage", StringComparison.Ordinal).ToString(), "MainWindow ignores TaskbarCreated when registration failed");
            AssertEqual("True", code.Contains("DisposeTaskbarOverlays();", StringComparison.Ordinal).ToString(), "MainWindow destroys stale overlays after TaskbarCreated");
            AssertEqual("True", code.Contains("_taskbarOverlayPlacements = [];", StringComparison.Ordinal).ToString(), "MainWindow clears stale placements after TaskbarCreated");
            AssertEqual("True", code.Contains("RefreshTaskbarOverlayPlacement(force: true);", StringComparison.Ordinal).ToString(), "MainWindow forces placement refresh after TaskbarCreated");
        }

        private static void TestMainWindowRecreatesInvalidTaskbarOverlays()
        {
            var mainWindowCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");
            var overlayCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Services", "NativeTextOverlayWindow.cs")).Replace("\r\n", "\n");

            AssertEqual("True", overlayCode.Contains("IsAttachedToTaskbar", StringComparison.Ordinal).ToString(), "NativeTextOverlayWindow exposes taskbar attachment health");
            AssertEqual("True", mainWindowCode.Contains("RecreateInvalidTaskbarOverlays", StringComparison.Ordinal).ToString(), "MainWindow recreates invalid taskbar overlays");
            AssertEqual("True", mainWindowCode.Contains("_overlayRegistry.Recreate(expectedTaskbarWindow)", StringComparison.Ordinal).ToString(), "MainWindow delegates disposal and recreation to taskbar registry");
            AssertEqual("True", mainWindowCode.Contains("Taskbar overlay window recreated.", StringComparison.Ordinal).ToString(), "MainWindow logs overlay recreation");
        }

        private static void TestMainWindowRefreshesPlacementAfterInvalidTaskbarOverlay()
        {
            var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MainWindow.xaml.cs")).Replace("\r\n", "\n");
            var methodStart = code.IndexOf("private void RecreateInvalidTaskbarOverlays()", StringComparison.Ordinal);
            var nextMethodStart = code.IndexOf("private void ForceTaskbarOverlaysTopmost()", StringComparison.Ordinal);

            if (methodStart < 0 || nextMethodStart < 0 || nextMethodStart <= methodStart)
            {
                throw new InvalidOperationException("Could not locate RecreateInvalidTaskbarOverlays method body.");
            }

            var methodCode = code[methodStart..nextMethodStart];

            AssertEqual("True", methodCode.Contains("_lastTaskbarPlacementRefreshUtc = DateTime.MinValue", StringComparison.Ordinal).ToString(), "MainWindow invalid overlay bypasses placement throttle");
            AssertEqual("True", methodCode.Contains("RefreshTaskbarOverlayPlacement(force: true);", StringComparison.Ordinal).ToString(), "MainWindow invalid overlay forces taskbar placement refresh");
        }

        private static string CreateTempSettingsPath()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "NetSpeedWidget.Tests",
                Guid.NewGuid().ToString("N"));

            return Path.Combine(directory, "settings.json");
        }

        private static void AssertEqual(string expected, string actual, string name)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{name} expected '{expected}', actual '{actual}'.");
            }
        }

    }
}
