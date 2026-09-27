namespace NetSpeedWidget.Models
{
    public readonly record struct HardwareStatusSnapshot(
        SystemMetricValue CpuTemperature,
        SystemMetricValue GpuUsage,
        SystemMetricValue GpuTemperature)
    {
        public static HardwareStatusSnapshot CreateUnavailable()
        {
            return new HardwareStatusSnapshot(
                SystemMetricValue.Unavailable("°C"),
                SystemMetricValue.Unavailable("%"),
                SystemMetricValue.Unavailable("°C"));
        }
    }
}
