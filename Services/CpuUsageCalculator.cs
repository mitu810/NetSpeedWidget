using System;
using System.Runtime.InteropServices;

namespace NetSpeedWidget.Services
{
    public class CpuUsageCalculator
    {
        private CpuTimes? _previousTimes;

        /// <summary>
        /// Gets total CPU usage percent from Windows system times.
        /// </summary>
        public double GetUsagePercent()
        {
            var currentTimes = GetCurrentCpuTimes();

            if (_previousTimes is null)
            {
                _previousTimes = currentTimes;
                return 0;
            }

            var previousTimes = _previousTimes.Value;
            _previousTimes = currentTimes;

            return CalculateUsagePercent(
                previousTimes.IdleTicks,
                previousTimes.TotalTicks,
                currentTimes.IdleTicks,
                currentTimes.TotalTicks);
        }

        public static double CalculateUsagePercent(
            ulong previousIdleTicks,
            ulong previousTotalTicks,
            ulong currentIdleTicks,
            ulong currentTotalTicks)
        {
            if (currentTotalTicks <= previousTotalTicks)
            {
                return 0;
            }

            var totalDelta = currentTotalTicks - previousTotalTicks;
            var idleDelta =
                currentIdleTicks >= previousIdleTicks
                    ? currentIdleTicks - previousIdleTicks
                    : 0;

            var usedPercent =
                100.0 * (1.0 - Math.Min(idleDelta, totalDelta) / (double)totalDelta);

            return Math.Clamp(usedPercent, 0, 100);
        }

        private static CpuTimes GetCurrentCpuTimes()
        {
            if (!GetSystemTimes(
                    out var idleTime,
                    out var kernelTime,
                    out var userTime))
            {
                return new CpuTimes(0, 0);
            }

            var idleTicks = ToUInt64(idleTime);
            var kernelTicks = ToUInt64(kernelTime);
            var userTicks = ToUInt64(userTime);

            return new CpuTimes(
                idleTicks,
                kernelTicks + userTicks);
        }

        private static ulong ToUInt64(FileTime fileTime)
        {
            return ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(
            out FileTime idleTime,
            out FileTime kernelTime,
            out FileTime userTime);

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct FileTime
        {
            public readonly uint LowDateTime;

            public readonly uint HighDateTime;
        }

        private readonly record struct CpuTimes(
            ulong IdleTicks,
            ulong TotalTicks);
    }
}
