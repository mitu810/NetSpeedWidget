using System;

namespace NetSpeedWidget.Models
{
    public readonly record struct SystemStatusSnapshot(
        DateTime Timestamp,
        SystemMetricValue DownloadSpeed,
        SystemMetricValue UploadSpeed,
        SystemMetricValue CpuUsage,
        SystemMetricValue CpuTemperature,
        SystemMetricValue GpuUsage,
        SystemMetricValue GpuTemperature,
        SystemMetricValue MemoryUsage)
    {
        public static SystemStatusSnapshot CreateUnavailable()
        {
            var unavailable = SystemMetricValue.Unavailable();

            return new SystemStatusSnapshot(
                DateTime.Now,
                unavailable,
                unavailable,
                unavailable,
                SystemMetricValue.Unavailable("°C"),
                SystemMetricValue.Unavailable("%"),
                SystemMetricValue.Unavailable("°C"),
                unavailable);
        }
    }
}
