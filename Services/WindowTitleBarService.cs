using NetSpeedWidget.Models;

namespace NetSpeedWidget.Services
{
    public readonly record struct TitleBarPalette(
        string BackgroundColor,
        string ForegroundColor);

    public static class WindowTitleBarService
    {
        /// <summary>
        /// Gets title bar colors that match the selected settings window theme.
        /// </summary>
        public static TitleBarPalette GetTitleBarPalette(AppThemeMode theme)
        {
            return theme switch
            {
                AppThemeMode.Light => new TitleBarPalette("#F2F2F2", "#202020"),
                _ => new TitleBarPalette("#202020", "#FFFFFF")
            };
        }
    }
}
