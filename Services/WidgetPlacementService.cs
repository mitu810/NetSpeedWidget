using NetSpeedWidget.Models;

namespace NetSpeedWidget.Services
{
    public readonly record struct RectBounds(
        int X,
        int Y,
        int Width,
        int Height);

    public readonly record struct WidgetPoint(
        int X,
        int Y);

    public static class WidgetPlacementService
    {
        /// <summary>
        /// Calculates a taskbar-aligned widget position for the selected display mode.
        /// </summary>
        public static WidgetPoint CalculateTaskbarPosition(
            WidgetDisplayMode displayMode,
            RectBounds displayBounds,
            RectBounds workArea,
            int widgetWidth,
            int widgetHeight,
            int edgePadding)
        {
            var taskbar = GetTaskbarBounds(displayBounds, workArea);

            return CalculateTaskbarPositionFromBounds(
                displayMode,
                taskbar,
                widgetWidth,
                widgetHeight,
                edgePadding);
        }

        /// <summary>
        /// Calculates a widget position inside an explicit taskbar rectangle.
        /// </summary>
        public static WidgetPoint CalculateTaskbarPositionFromBounds(
            WidgetDisplayMode displayMode,
            RectBounds taskbar,
            int widgetWidth,
            int widgetHeight,
            int edgePadding)
        {
            return displayMode == WidgetDisplayMode.TaskbarLeft
                ? new WidgetPoint(
                    taskbar.X + edgePadding,
                    taskbar.Y + (taskbar.Height - widgetHeight) / 2)
                : new WidgetPoint(
                    taskbar.X + taskbar.Width - widgetWidth - edgePadding,
                    taskbar.Y + (taskbar.Height - widgetHeight) / 2);
        }

        /// <summary>
        /// Determines whether the selected mode should be rendered as taskbar-aligned.
        /// </summary>
        public static bool IsTaskbarMode(WidgetDisplayMode displayMode)
        {
            return displayMode is WidgetDisplayMode.TaskbarLeft or WidgetDisplayMode.TaskbarRight;
        }

        /// <summary>
        /// Calculates the visible widget content position inside a full-width native appbar.
        /// </summary>
        public static WidgetPoint CalculateAppBarContentPosition(
            WidgetDisplayMode displayMode,
            RectBounds appBarBounds,
            int widgetWidth,
            int widgetHeight,
            int edgePadding)
        {
            var x = displayMode == WidgetDisplayMode.TaskbarLeft
                ? appBarBounds.X + edgePadding
                : appBarBounds.X + appBarBounds.Width - widgetWidth - edgePadding;
            var y = appBarBounds.Y + (appBarBounds.Height - widgetHeight) / 2;

            return new WidgetPoint(x, y);
        }

        /// <summary>
        /// Calculates the taskbar rectangle from a display rectangle and its work area.
        /// </summary>
        public static RectBounds GetTaskbarBounds(
            RectBounds displayBounds,
            RectBounds workArea)
        {
            var displayRight = displayBounds.X + displayBounds.Width;
            var displayBottom = displayBounds.Y + displayBounds.Height;
            var workRight = workArea.X + workArea.Width;
            var workBottom = workArea.Y + workArea.Height;

            if (workArea.Y > displayBounds.Y)
            {
                return new RectBounds(
                    displayBounds.X,
                    displayBounds.Y,
                    displayBounds.Width,
                    workArea.Y - displayBounds.Y);
            }

            if (workBottom < displayBottom)
            {
                return new RectBounds(
                    displayBounds.X,
                    workBottom,
                    displayBounds.Width,
                    displayBottom - workBottom);
            }

            if (workArea.X > displayBounds.X)
            {
                return new RectBounds(
                    displayBounds.X,
                    displayBounds.Y,
                    workArea.X - displayBounds.X,
                    displayBounds.Height);
            }

            return new RectBounds(
                workRight,
                displayBounds.Y,
                displayRight - workRight,
                displayBounds.Height);
        }
    }
}
