using System;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using NetSpeedWidget.Models;

namespace NetSpeedWidget.Services
{
    public class SystemStatusCollectorService : IDisposable
    {
        private readonly SemaphoreSlim _collectionGate = new(1, 1);
        private Func<HardwareStatusSnapshot>? _hardwareProvider;
        private SystemStatusSnapshot? _cachedSnapshot;
        private long _lastCollectionTicks;
        private bool _disposed;

        public SystemStatusCollectorService(Func<HardwareStatusSnapshot> hardwareProvider) : this()
        {
            _hardwareProvider = hardwareProvider;
        }

        private readonly NetworkMonitorService _networkMonitorService;
        private readonly CpuUsageCalculator _cpuUsageCalculator;
        private readonly MemoryStatusService _memoryStatusService;
        private readonly HardwareStatusService _hardwareStatusService;
        private readonly GpuUsageCounterService _gpuUsageCounterService;

        public SystemStatusCollectorService()
            : this(
                new NetworkMonitorService(),
                new CpuUsageCalculator(),
                new MemoryStatusService(),
                new HardwareStatusService(),
                new GpuUsageCounterService())
        {
        }

        public SystemStatusCollectorService(
            NetworkMonitorService networkMonitorService,
            CpuUsageCalculator cpuUsageCalculator,
            MemoryStatusService memoryStatusService,
            HardwareStatusService hardwareStatusService)
            : this(
                networkMonitorService,
                cpuUsageCalculator,
                memoryStatusService,
                hardwareStatusService,
                new GpuUsageCounterService())
        {
        }

        public SystemStatusCollectorService(
            NetworkMonitorService networkMonitorService,
            CpuUsageCalculator cpuUsageCalculator,
            MemoryStatusService memoryStatusService,
            HardwareStatusService hardwareStatusService,
            GpuUsageCounterService gpuUsageCounterService)
        {
            _networkMonitorService = networkMonitorService;
            _cpuUsageCalculator = cpuUsageCalculator;
            _memoryStatusService = memoryStatusService;
            _hardwareStatusService = hardwareStatusService;
            _gpuUsageCounterService = gpuUsageCounterService;
        }

        /// <summary>
        /// Collects one system-status snapshot for future LAN display.
        /// </summary>
        public async Task<SystemStatusSnapshot> CollectAsync()
        {
            // 1. 串行保护前次采样状态，多个客户端共用一秒内的快照。
            await _collectionGate.WaitAsync();
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_cachedSnapshot is not null && Stopwatch.GetElapsedTime(_lastCollectionTicks).TotalSeconds < 1)
                    return _cachedSnapshot.Value;
                // 2. 采集普通权限指标，温度由专用辅助进程提供。
                var networkSpeed = await _networkMonitorService.GetNetworkSpeedAsync();
                var cpuUsagePercent = _cpuUsageCalculator.GetUsagePercent();
                var memoryUsage = _memoryStatusService.GetMemoryUsage();
                var hardwareStatus = _hardwareProvider?.Invoke() ?? _hardwareStatusService.GetStatus();
                hardwareStatus = PreferWindowsGpuUsage(hardwareStatus, _gpuUsageCounterService.GetUsage());
                var snapshot = CreateSnapshot(networkSpeed.Download, networkSpeed.Upload, cpuUsagePercent, memoryUsage, hardwareStatus);
                _cachedSnapshot = snapshot;
                _lastCollectionTicks = Stopwatch.GetTimestamp();
                return snapshot;
            }
            finally { _collectionGate.Release(); }
        }

        /// <summary>待本次采样结束后释放传感器和性能计数器，重复释放安全。</summary>
        public void Dispose()
        {
            _collectionGate.Wait();
            try
            {
                if (_disposed) return;
                _disposed = true;
                _hardwareStatusService.Dispose();
                _gpuUsageCounterService.Dispose();
            }
            finally { _collectionGate.Release(); }
        }

        public static SystemStatusSnapshot CreateSnapshot(
            string downloadSpeed,
            string uploadSpeed,
            double cpuUsagePercent,
            SystemMetricValue memoryUsage)
        {
            return CreateSnapshot(
                downloadSpeed,
                uploadSpeed,
                cpuUsagePercent,
                memoryUsage,
                HardwareStatusSnapshot.CreateUnavailable());
        }

        public static SystemStatusSnapshot CreateSnapshot(
            string downloadSpeed,
            string uploadSpeed,
            double cpuUsagePercent,
            SystemMetricValue memoryUsage,
            HardwareStatusSnapshot hardwareStatus)
        {
            return new SystemStatusSnapshot(
                DateTime.Now,
                SystemMetricValue.Available(downloadSpeed, 0, "B/s"),
                SystemMetricValue.Available(uploadSpeed, 0, "B/s"),
                CreatePercentMetric(cpuUsagePercent),
                hardwareStatus.CpuTemperature,
                hardwareStatus.GpuUsage,
                hardwareStatus.GpuTemperature,
                memoryUsage);
        }

        public static HardwareStatusSnapshot PreferWindowsGpuUsage(
            HardwareStatusSnapshot hardwareStatus,
            SystemMetricValue windowsGpuUsage)
        {
            return windowsGpuUsage.Status == SystemMetricStatus.Available
                ? hardwareStatus with { GpuUsage = windowsGpuUsage }
                : hardwareStatus;
        }

        public static SystemMetricValue CreatePercentMetric(double percent)
        {
            var clampedPercent = Math.Clamp(percent, 0, 100);
            var roundedPercent = Math.Round(clampedPercent, 1, MidpointRounding.AwayFromZero);

            return SystemMetricValue.Available(
                $"{roundedPercent:F1}%",
                roundedPercent,
                "%");
        }
    }
}
