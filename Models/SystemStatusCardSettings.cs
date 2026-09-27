namespace NetSpeedWidget.Models
{
    public class SystemStatusCardSettings
    {
        public bool NetworkSpeedVisible { get; set; } = true;

        public bool CpuUsageVisible { get; set; } = true;

        public bool CpuTemperatureVisible { get; set; } = true;

        public bool GpuUsageVisible { get; set; } = true;

        public bool GpuTemperatureVisible { get; set; } = true;

        public bool MemoryUsageVisible { get; set; } = true;
    }
}
