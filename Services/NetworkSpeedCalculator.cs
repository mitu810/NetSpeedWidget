using System;
using System.Collections.Generic;

namespace NetSpeedWidget.Services
{
    public readonly record struct NetworkTrafficCounter(
        long BytesReceived,
        long BytesSent);

    public readonly record struct NetworkSnapshot(
        long BytesReceived,
        long BytesSent,
        DateTime Timestamp);

    public readonly record struct NetworkSpeedResult(
        string Download,
        string Upload);

    public class NetworkSpeedCalculator
    {
        private NetworkSnapshot? _lastSnapshot;

        /// <summary>
        /// Calculates download and upload speed from two traffic snapshots.
        /// </summary>
        public NetworkSpeedResult Calculate(NetworkSnapshot currentSnapshot)
        {
            if (_lastSnapshot is null)
            {
                _lastSnapshot = currentSnapshot;
                return new NetworkSpeedResult("0 B/s", "0 B/s");
            }

            var previousSnapshot = _lastSnapshot.Value;
            var interval = (currentSnapshot.Timestamp - previousSnapshot.Timestamp).TotalSeconds;

            _lastSnapshot = currentSnapshot;

            if (interval <= 0)
            {
                return new NetworkSpeedResult("0 B/s", "0 B/s");
            }

            var downloadBytesPerSecond =
                Math.Max(0, currentSnapshot.BytesReceived - previousSnapshot.BytesReceived) / interval;

            var uploadBytesPerSecond =
                Math.Max(0, currentSnapshot.BytesSent - previousSnapshot.BytesSent) / interval;

            return new NetworkSpeedResult(
                FormatSpeed(downloadBytesPerSecond),
                FormatSpeed(uploadBytesPerSecond));
        }

        /// <summary>
        /// Combines traffic counters from multiple interfaces into one snapshot.
        /// </summary>
        public static NetworkSnapshot CreateSnapshot(
            IEnumerable<NetworkTrafficCounter> counters,
            DateTime timestamp)
        {
            long bytesReceived = 0;
            long bytesSent = 0;

            foreach (var counter in counters)
            {
                bytesReceived += counter.BytesReceived;
                bytesSent += counter.BytesSent;
            }

            return new NetworkSnapshot(bytesReceived, bytesSent, timestamp);
        }

        /// <summary>
        /// Formats bytes per second for display.
        /// </summary>
        public static string FormatSpeed(double bytesPerSecond)
        {
            string[] units =
            {
                "B/s",
                "KB/s",
                "MB/s",
                "GB/s"
            };

            var value = Math.Max(0, bytesPerSecond);
            var unitIndex = 0;

            while (value >= 1024 &&
                   unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return $"{value:F1} {units[unitIndex]}";
        }
    }
}
