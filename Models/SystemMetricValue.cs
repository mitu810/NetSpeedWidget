namespace NetSpeedWidget.Models
{
    public readonly record struct SystemMetricValue(
        SystemMetricStatus Status,
        string DisplayText,
        double? NumericValue,
        string Unit)
    {
        public static SystemMetricValue Available(
            string DisplayText,
            double NumericValue,
            string Unit)
        {
            return new SystemMetricValue(
                SystemMetricStatus.Available,
                DisplayText,
                NumericValue,
                Unit);
        }

        public static SystemMetricValue Unavailable(string Unit = "")
        {
            return new SystemMetricValue(
                SystemMetricStatus.Unavailable,
                "不可用",
                null,
                Unit);
        }
    }
}
