using NetSpeedWidget.Models;
using System;

namespace NetSpeedWidget.Services
{
    public readonly record struct OverlayTextColor(
        byte Red,
        byte Green,
        byte Blue);

    public readonly record struct OverlayTextBaselines(
        int UploadBaseline,
        int DownloadBaseline);

    public static class NativeTextOverlayRenderService
    {
        public const int FontHeight = -11;
        public const int FontWeight = 400;
        public const uint FontQuality = 5;
        public const string FontFaceName = "Segoe UI";

        /// <summary>
        /// Converts the configured speed font size to a GDI font height.
        /// </summary>
        public static int GetFontHeight(int speedFontSize)
        {
            return -Math.Abs(speedFontSize);
        }

        /// <summary>
        /// Calculates readable baselines for the two taskbar overlay text rows.
        /// </summary>
        public static OverlayTextBaselines CalculateTextBaselines(
            int windowHeight,
            int speedFontSize)
        {
            const int bottomPadding = 2;
            var spacing = Math.Max(18, speedFontSize + 2);
            var downloadBaseline = Math.Max(0, windowHeight - bottomPadding);
            var uploadBaseline = Math.Max(1, downloadBaseline - spacing);

            return new OverlayTextBaselines(uploadBaseline, downloadBaseline);
        }

        /// <summary>
        /// Gets the taskbar overlay text color for the selected app theme.
        /// </summary>
        public static OverlayTextColor GetTextColor(AppThemeMode theme)
        {
            return theme == AppThemeMode.Light
                ? new OverlayTextColor(32, 32, 32)
                : new OverlayTextColor(255, 255, 255);
        }

        /// <summary>
        /// Converts a white text mask into premultiplied-looking alpha pixels for a layered window.
        /// </summary>
        public static void ApplyTextMaskToPixels(
            byte[] pixels,
            OverlayTextColor textColor)
        {
            for (var index = 0; index < pixels.Length; index += 4)
            {
                var alpha =
                    (byte)((pixels[index] + pixels[index + 1] + pixels[index + 2]) / 3);

                if (alpha == 0)
                {
                    pixels[index + 3] = 0;
                    continue;
                }

                pixels[index] = Premultiply(textColor.Blue, alpha);
                pixels[index + 1] = Premultiply(textColor.Green, alpha);
                pixels[index + 2] = Premultiply(textColor.Red, alpha);
                pixels[index + 3] = alpha;
            }
        }

        private static byte Premultiply(byte color, byte alpha)
        {
            return (byte)((color * alpha + 127) / 255);
        }
    }
}
