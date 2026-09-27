namespace NetSpeedWidget.Models
{
    public class AppSettings
    {
        public AppThemeMode Theme { get; set; } = AppThemeMode.Dark;

        public bool StartupEnabled { get; set; } = true;

        public bool TopmostEnabled { get; set; } = true;

        public bool LockWindowPosition { get; set; } = false;

        public WidgetDisplayMode DisplayMode { get; set; } = WidgetDisplayMode.Floating;

        public int TaskbarEdgePadding { get; set; } = 4;

        public int SpeedFontSize { get; set; } = 12;

        public SystemStatusSettings SystemStatus { get; set; } = new();
    }
}
