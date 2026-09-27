using System;
using System.Runtime.InteropServices;
using NetSpeedWidget.Models;

namespace NetSpeedWidget.Services
{
    public class MemoryStatusService
    {
        /// <summary>
        /// Reads current Windows memory usage.
        /// </summary>
        public SystemMetricValue GetMemoryUsage()
        {
            if (!TryGetMemoryStatus(out var memoryStatus))
            {
                return SystemMetricValue.Unavailable("%");
            }

            var totalBytes = (long)memoryStatus.TotalPhysical;
            var availableBytes = (long)memoryStatus.AvailablePhysical;
            var usedBytes = totalBytes - availableBytes;
            var usagePercent = CalculateUsagePercent(totalBytes, availableBytes);

            return SystemMetricValue.Available(
                FormatUsageText(usagePercent, usedBytes, totalBytes),
                usagePercent,
                "%");
        }

        public static double CalculateUsagePercent(
            long totalBytes,
            long availableBytes)
        {
            if (totalBytes <= 0)
            {
                return 0;
            }

            var usedBytes = totalBytes - availableBytes;
            var usagePercent = usedBytes * 100.0 / totalBytes;

            return Math.Clamp(usagePercent, 0, 100);
        }

        public static string FormatUsageText(
            double usagePercent,
            long usedBytes,
            long totalBytes)
        {
            return $"{usagePercent:F1}% ({FormatBytes(usedBytes)} / {FormatBytes(totalBytes)})";
        }

        private static string FormatBytes(long bytes)
        {
            var gigabytes = bytes / 1024.0 / 1024.0 / 1024.0;

            return $"{gigabytes:F1} GB";
        }

        private static bool TryGetMemoryStatus(out MemoryStatus memoryStatus)
        {
            memoryStatus = new MemoryStatus
            {
                Length = (uint)Marshal.SizeOf<MemoryStatus>()
            };

            return GlobalMemoryStatusEx(ref memoryStatus);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memoryStatus);

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length;

            public uint MemoryLoad;

            public ulong TotalPhysical;

            public ulong AvailablePhysical;

            public ulong TotalPageFile;

            public ulong AvailablePageFile;

            public ulong TotalVirtual;

            public ulong AvailableVirtual;

            public ulong AvailableExtendedVirtual;
        }
    }
}
