using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using LibreHardwareMonitor.Hardware;
using NetSpeedWidget.Models;

namespace NetSpeedWidget.Services
{
    public sealed class HardwareStatusService : IDisposable
    {
        private const string CpuHardwareType = "Cpu";
        private const string TemperatureSensorType = "Temperature";
        private const string LoadSensorType = "Load";
        private const string CpuCoreAverageTemperatureSensorName = "Core Average";
        private static readonly string[] AmdCpuTemperatureNames =
        {
            "Core (Tdie)",
            "Core (Tctl/Tdie)",
            "Core (Tctl)"
        };

        private static readonly string[] GpuUsageNamePriorities =
        {
            "GPU Core",
            "GPU Total",
            "GPU 3D",
            "D3D 3D",
            "3D",
            "Graphics"
        };

        private static readonly string[] GpuTemperatureNamePriorities =
        {
            "GPU Core",
            "GPU Temperature",
            "GPU Edge",
            "GPU Diode",
            "GPU"
        };

        private static readonly string[] GpuAuxiliaryNameParts =
        {
            "Memory",
            "VRAM",
            "Hot Spot",
            "Hotspot",
            "Junction",
            "VRM",
            "Video",
            "Decode",
            "Encode",
            "Bus",
            "PCIe",
            "Controller",
            "Fan",
            "Power"
        };

        private static readonly HashSet<string> GpuHardwareTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "GpuNvidia",
                "GpuAmd",
                "GpuIntel"
            };

        private readonly object _syncRoot = new();
        private Computer? _computer;

        /// <summary>
        /// Reads CPU/GPU hardware sensors and returns unavailable metrics when sensors are unsupported.
        /// </summary>
        public HardwareStatusSnapshot GetStatus()
        {
            try
            {
                lock (_syncRoot)
                {
                    var computer = GetOrCreateComputer();
                    var readings = new List<HardwareSensorReading>();

                    foreach (var hardware in computer.Hardware)
                    {
                        UpdateAndCollectReadings(hardware, readings);
                    }

                    return CreateSnapshot(readings);
                }
            }
            catch (Exception)
            {
                // Hardware sensor drivers vary by machine; failures must not break the dashboard.
                return HardwareStatusSnapshot.CreateUnavailable();
            }
        }

        public string GetDiagnosticsReport()
        {
            try
            {
                lock (_syncRoot)
                {
                    var computer = GetOrCreateComputer();
                    var readings = new List<HardwareSensorReading>();

                    foreach (var hardware in computer.Hardware)
                    {
                        UpdateAndCollectReadings(hardware, readings);
                    }

                    return CreateDiagnosticsReport(
                        readings,
                        CreateSnapshot(readings),
                        CreatePawnIoStatusText());
                }
            }
            catch (Exception ex)
            {
                return $"Hardware diagnostics unavailable: {ex.GetType().Name}: {ex.Message}";
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                _computer?.Close();
                _computer = null;
            }
        }

        public static HardwareStatusSnapshot CreateSnapshot(
            IEnumerable<HardwareSensorReading>? readings)
        {
            var sensorReadings = readings?.ToArray() ?? Array.Empty<HardwareSensorReading>();
            var cpuTemperatureReading = SelectCpuTemperatureReading(sensorReadings);

            return new HardwareStatusSnapshot(
                CreateTemperatureMetric(
                    cpuTemperatureReading?.Value),
                CreatePercentMetric(
                    SelectGpuUsage(sensorReadings)),
                CreateTemperatureMetric(
                    SelectGpuTemperature(sensorReadings)));
        }

        public static SystemMetricValue CreateTemperatureMetric(double? celsius)
        {
            if (celsius is null || !IsValidTemperature(celsius.Value))
            {
                return SystemMetricValue.Unavailable("°C");
            }

            var rounded = Math.Round(celsius.Value, 1, MidpointRounding.AwayFromZero);

            return SystemMetricValue.Available(
                $"{rounded:F1} °C",
                rounded,
                "°C");
        }

        public static string CreateDiagnosticsReport(
            IEnumerable<HardwareSensorReading>? readings,
            HardwareStatusSnapshot snapshot,
            string pawnIoStatus)
        {
            var sensorReadings = readings?.ToArray() ?? Array.Empty<HardwareSensorReading>();
            var builder = new StringBuilder();

            builder.AppendLine("Hardware diagnostics");
            builder.AppendLine($"Windows administrator: {IsRunningAsAdministrator()}");
            builder.AppendLine(pawnIoStatus);
            builder.AppendLine($"Selected CPU temperature: {snapshot.CpuTemperature.DisplayText}");
            builder.AppendLine($"Selected CPU temperature source: {CreateSensorSourceText(SelectCpuTemperatureReading(sensorReadings))}");
            builder.AppendLine($"Selected GPU usage: {snapshot.GpuUsage.DisplayText}");
            builder.AppendLine($"Selected GPU temperature: {snapshot.GpuTemperature.DisplayText}");
            builder.AppendLine();
            builder.AppendLine("Temperature sensors:");

            foreach (var reading in sensorReadings
                .Where(reading => string.Equals(reading.SensorType, TemperatureSensorType, StringComparison.OrdinalIgnoreCase))
                .OrderBy(reading => reading.HardwareType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(reading => reading.HardwareName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(reading => reading.Name, StringComparer.OrdinalIgnoreCase))
            {
                var valueText = reading.Value is null ? "null" : $"{reading.Value.Value:F1} °C";
                builder.AppendLine(
                    $"- [{reading.HardwareType}] {reading.HardwareName} :: {reading.Name} = {valueText} ({reading.Identifier})");
            }

            builder.AppendLine();
            builder.AppendLine("All sensor identifiers:");

            foreach (var reading in sensorReadings
                .OrderBy(reading => reading.HardwareType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(reading => reading.HardwareName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(reading => reading.SensorType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(reading => reading.Name, StringComparer.OrdinalIgnoreCase))
            {
                var valueText = reading.Value is null ? "null" : $"{reading.Value.Value:F1}";
                builder.AppendLine(
                    $"- [{reading.HardwareType}] {reading.HardwareName} :: {reading.SensorType} :: {reading.Name} = {valueText} ({reading.Identifier})");
            }

            return builder.ToString().TrimEnd();
        }

        public static SystemMetricValue CreatePercentMetric(double? percent)
        {
            if (percent is null || double.IsNaN(percent.Value) || double.IsInfinity(percent.Value))
            {
                return SystemMetricValue.Unavailable("%");
            }

            var clampedPercent = Math.Clamp(percent.Value, 0, 100);
            var roundedPercent = Math.Round(clampedPercent, 1, MidpointRounding.AwayFromZero);

            return SystemMetricValue.Available(
                $"{roundedPercent:F1}%",
                roundedPercent,
                "%");
        }

        private Computer GetOrCreateComputer()
        {
            if (_computer is not null)
            {
                return _computer;
            }

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true
            };
            _computer.Open();

            return _computer;
        }

        private static void UpdateAndCollectReadings(
            IHardware hardware,
            List<HardwareSensorReading> readings)
        {
            try
            {
                hardware.Update();
                CollectReadings(hardware, readings);
            }
            catch (Exception)
            {
                return;
            }

            foreach (var subHardware in hardware.SubHardware)
            {
                UpdateAndCollectReadings(subHardware, readings);
            }
        }

        private static void CollectReadings(
            IHardware hardware,
            List<HardwareSensorReading> readings)
        {
            foreach (var sensor in hardware.Sensors)
            {
                readings.Add(
                    new HardwareSensorReading(
                        hardware.HardwareType.ToString(),
                        sensor.SensorType.ToString(),
                        sensor.Name,
                        sensor.Value.HasValue ? sensor.Value.Value : null,
                        hardware.Name,
                        sensor.Identifier.ToString()));
            }
        }

        private static HardwareSensorReading? SelectCpuTemperatureReading(IReadOnlyCollection<HardwareSensorReading> readings)
        {
            // 1. 保留 Core Average 优先级，且不把驱动未读到数据时的 0 °C 当作真实温度。
            foreach (var reading in readings)
            {
                if (IsCpuCoreAverageTemperatureReading(reading) &&
                    reading.Value is not null &&
                    IsValidCpuTemperature(reading.Value.Value))
                {
                    return reading;
                }
            }

            // 2. AMD Ryzen 使用 Tdie/Tctl 命名；只在 CPU 硬件节点选取已知传感器。
            foreach (var name in AmdCpuTemperatureNames)
            {
                foreach (var reading in readings)
                {
                    if (IsAmdCpuTemperatureReading(reading, name) &&
                        reading.Value is not null &&
                        IsValidCpuTemperature(reading.Value.Value))
                    {
                        return reading;
                    }
                }
            }

            return null;
        }

        private static bool IsAmdCpuTemperatureReading(HardwareSensorReading reading, string name)
        {
            return string.Equals(reading.HardwareType, CpuHardwareType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(reading.SensorType, TemperatureSensorType, StringComparison.OrdinalIgnoreCase) &&
                (reading.HardwareName.StartsWith("AMD ", StringComparison.OrdinalIgnoreCase) ||
                    reading.Identifier.StartsWith("/amdcpu/", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(reading.Name, name, StringComparison.OrdinalIgnoreCase);
        }

        private static double? SelectGpuUsage(IReadOnlyCollection<HardwareSensorReading> readings)
        {
            var gpuLoadReadings =
                readings
                    .Where(IsGpuLoadReading)
                    .Where(IsPrimaryGpuMetricReading)
                    .Where(reading => reading.Value is not null && IsValidPercent(reading.Value.Value))
                    .ToArray();

            return SelectPreferredGpuValue(
                gpuLoadReadings,
                GpuUsageNamePriorities,
                IsValidPercent);
        }

        private static double? SelectGpuTemperature(IReadOnlyCollection<HardwareSensorReading> readings)
        {
            var gpuTemperatureReadings =
                readings
                    .Where(IsGpuTemperatureReading)
                    .Where(IsPrimaryGpuMetricReading)
                    .Where(reading => reading.Value is not null && IsValidTemperature(reading.Value.Value))
                    .ToArray();

            return SelectPreferredGpuValue(
                gpuTemperatureReadings,
                GpuTemperatureNamePriorities,
                IsValidTemperature);
        }

        private static double? SelectPreferredGpuValue(
            IReadOnlyCollection<HardwareSensorReading> readings,
            IReadOnlyCollection<string> namePriorities,
            Func<double, bool> valueValidator)
        {
            foreach (var namePart in namePriorities)
            {
                var preferredValue =
                    SelectMaxValidValue(
                        readings,
                        reading => ContainsNamePart(reading, namePart),
                        valueValidator);

                if (preferredValue is not null)
                {
                    return preferredValue;
                }
            }

            return SelectMaxValidValue(readings, _ => true, valueValidator);
        }

        private static double? SelectMaxValidValue(
            IEnumerable<HardwareSensorReading> readings,
            Func<HardwareSensorReading, bool> predicate,
            Func<double, bool> valueValidator)
        {
            var validValues =
                readings
                    .Where(predicate)
                    .Select(reading => reading.Value)
                    .Where(value => value is not null && valueValidator(value.Value))
                    .Select(value => value!.Value)
                    .ToArray();

            return validValues.Length == 0 ? null : validValues.Max();
        }

        private static bool IsCpuCoreAverageTemperatureReading(HardwareSensorReading reading)
        {
            return string.Equals(reading.HardwareType, CpuHardwareType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(reading.SensorType, TemperatureSensorType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(reading.Name, CpuCoreAverageTemperatureSensorName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGpuTemperatureReading(HardwareSensorReading reading)
        {
            return IsGpuHardware(reading.HardwareType) &&
                string.Equals(reading.SensorType, TemperatureSensorType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGpuLoadReading(HardwareSensorReading reading)
        {
            return IsGpuHardware(reading.HardwareType) &&
                string.Equals(reading.SensorType, LoadSensorType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPrimaryGpuMetricReading(HardwareSensorReading reading)
        {
            return !GpuAuxiliaryNameParts.Any(namePart => ContainsNamePart(reading, namePart));
        }

        private static bool IsGpuHardware(string hardwareType)
        {
            return GpuHardwareTypes.Contains(hardwareType);
        }

        private static bool ContainsNamePart(
            HardwareSensorReading reading,
            string namePart)
        {
            return reading.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidTemperature(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= -50 &&
                value <= 150;
        }

        private static bool IsValidCpuTemperature(double value) => value > 0 && IsValidTemperature(value);

        private static bool IsValidPercent(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= 0 &&
                value <= 100;
        }

        private static string CreateSensorSourceText(HardwareSensorReading? reading)
        {
            if (!reading.HasValue)
            {
                return "Unavailable";
            }

            var source = reading.Value;
            var valueText = source.Value is null ? "null" : $"{source.Value.Value:F1}";

            return
                $"[{source.HardwareType}] {source.HardwareName} :: {source.SensorType} :: {source.Name} = {valueText} ({source.Identifier})";
        }

        private static string CreatePawnIoStatusText()
        {
            try
            {
                return "PawnIO: " +
                    $"installed={LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled}, " +
                    $"version={LibreHardwareMonitor.PawnIo.PawnIo.Version}";
            }
            catch (Exception ex)
            {
                return $"PawnIO: unavailable ({ex.GetType().Name})";
            }
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);

                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public readonly record struct HardwareSensorReading(
        string HardwareType,
        string SensorType,
        string Name,
        double? Value,
        string HardwareName = "",
        string Identifier = "");
}
