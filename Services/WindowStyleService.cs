namespace NetSpeedWidget.Services
{
    using System;

    public static class WindowStyleService
    {
        private const int WsExToolWindow = 0x00000080;
        private const int WsExAppWindow = 0x00040000;
        private static readonly IntPtr HwndTopmost = new(-1);
        private static readonly IntPtr HwndNoTopmost = new(-2);

        /// <summary>
        /// Converts a normal app window style into a floating tool window style.
        /// </summary>
        public static int HideFromTaskbar(int exStyle)
        {
            exStyle &= ~WsExAppWindow;
            exStyle |= WsExToolWindow;

            return exStyle;
        }

        /// <summary>
        /// Gets the SetWindowPos insert-after handle for topmost state.
        /// </summary>
        public static IntPtr GetTopmostInsertAfter(bool topmostEnabled)
        {
            return topmostEnabled
                ? HwndTopmost
                : HwndNoTopmost;
        }
    }
}
