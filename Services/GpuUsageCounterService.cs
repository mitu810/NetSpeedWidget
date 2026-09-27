using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NetSpeedWidget.Models;

namespace NetSpeedWidget.Services
{
    public sealed class GpuUsageCounterService : IDisposable
    {
        private const string GpuEngineCounterPath = @"\GPU Engine(*)\Utilization Percentage";
        private const uint PdhFmtDouble = 0x00000200;
        private const uint PdhCStatusValidData = 0x00000000;
        private const uint PdhCStatusNewData = 0x00000001;
        private const int ErrorSuccess = 0;
        private const int PdhMoreData = unchecked((int)0x800007D2);

        private readonly object _syncRoot = new();
        private IntPtr _queryHandle;
        private IntPtr _counterHandle;
        private bool _initialized;
        private bool _disposed;

        /// <summary>
        /// Reads Windows GPU Engine counters and formats the total GPU usage.
        /// </summary>
        public SystemMetricValue GetUsage()
        {
            return HardwareStatusService.CreatePercentMetric(GetUsagePercent());
        }

        public double? GetUsagePercent()
        {
            try
            {
                lock (_syncRoot)
                {
                    if (_disposed)
                    {
                        return null;
                    }

                    EnsureInitialized();
                    if (!_initialized)
                    {
                        return null;
                    }

                    var collectStatus = PdhCollectQueryData(_queryHandle);
                    if (collectStatus != ErrorSuccess)
                    {
                        ResetQuery();
                        return null;
                    }

                    return CalculateUsagePercent(ReadCounterSamples());
                }
            }
            catch (Exception)
            {
                ResetQuery();
                return null;
            }
        }

        public static double? CalculateUsagePercent(
            IEnumerable<GpuEngineCounterSample>? samples)
        {
            var validValues =
                samples?
                    .Where(sample => !IsTotalInstance(sample.InstanceName))
                    .Select(sample => sample.Value)
                    .Where(IsValidCounterValue)
                    .ToArray();

            if (validValues is null || validValues.Length == 0)
            {
                return null;
            }

            return Math.Clamp(validValues.Sum(), 0, 100);
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                _disposed = true;
                ResetQuery();
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            var openStatus = PdhOpenQuery(null, IntPtr.Zero, out _queryHandle);
            if (openStatus != ErrorSuccess)
            {
                ResetQuery();
                return;
            }

            var addStatus =
                PdhAddEnglishCounter(
                    _queryHandle,
                    GpuEngineCounterPath,
                    IntPtr.Zero,
                    out _counterHandle);

            if (addStatus != ErrorSuccess)
            {
                ResetQuery();
                return;
            }

            _initialized = true;
            _ = PdhCollectQueryData(_queryHandle);
        }

        private IReadOnlyList<GpuEngineCounterSample> ReadCounterSamples()
        {
            uint bufferSize = 0;
            uint itemCount = 0;
            var status =
                PdhGetFormattedCounterArray(
                    _counterHandle,
                    PdhFmtDouble,
                    ref bufferSize,
                    ref itemCount,
                    IntPtr.Zero);

            if (status != PdhMoreData || bufferSize == 0 || itemCount == 0)
            {
                return Array.Empty<GpuEngineCounterSample>();
            }

            var buffer = Marshal.AllocHGlobal((int)bufferSize);
            try
            {
                status =
                    PdhGetFormattedCounterArray(
                        _counterHandle,
                        PdhFmtDouble,
                        ref bufferSize,
                        ref itemCount,
                        buffer);

                if (status != ErrorSuccess)
                {
                    return Array.Empty<GpuEngineCounterSample>();
                }

                var samples = new List<GpuEngineCounterSample>((int)itemCount);
                var itemSize = Marshal.SizeOf<PdhFmtCounterValueItem>();

                for (var index = 0; index < itemCount; index++)
                {
                    var itemPointer = IntPtr.Add(buffer, index * itemSize);
                    var item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(itemPointer);
                    if (item.Value.CStatus != PdhCStatusValidData &&
                        item.Value.CStatus != PdhCStatusNewData)
                    {
                        continue;
                    }

                    var instanceName = Marshal.PtrToStringUni(item.Name) ?? string.Empty;
                    samples.Add(new GpuEngineCounterSample(instanceName, item.Value.DoubleValue));
                }

                return samples;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void ResetQuery()
        {
            if (_queryHandle != IntPtr.Zero)
            {
                _ = PdhCloseQuery(_queryHandle);
            }

            _queryHandle = IntPtr.Zero;
            _counterHandle = IntPtr.Zero;
            _initialized = false;
        }

        private static bool IsTotalInstance(string instanceName)
        {
            return string.Equals(instanceName, "_Total", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidCounterValue(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= 0;
        }

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern int PdhOpenQuery(
            string? dataSource,
            IntPtr userData,
            out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern int PdhAddEnglishCounter(
            IntPtr query,
            string fullCounterPath,
            IntPtr userData,
            out IntPtr counter);

        [DllImport("pdh.dll")]
        private static extern int PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll")]
        private static extern int PdhGetFormattedCounterArray(
            IntPtr counter,
            uint format,
            ref uint bufferSize,
            ref uint itemCount,
            IntPtr itemBuffer);

        [DllImport("pdh.dll")]
        private static extern int PdhCloseQuery(IntPtr query);

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PdhFmtCounterValue
        {
            public readonly uint CStatus;
            public readonly double DoubleValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct PdhFmtCounterValueItem
        {
            public readonly IntPtr Name;
            public readonly PdhFmtCounterValue Value;
        }
    }

    public readonly record struct GpuEngineCounterSample(
        string InstanceName,
        double Value);
}
