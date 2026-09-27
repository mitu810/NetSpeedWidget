using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace NetSpeedWidget.Services
{
    public class NetworkMonitorService
    {
        private readonly NetworkSpeedCalculator _calculator = new();

        public NetworkMonitorService()
        {
            _calculator.Calculate(CreateSnapshot());
        }

        private NetworkSnapshot CreateSnapshot()
        {
            var counters = new List<NetworkTrafficCounter>();

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!IsActiveNetworkInterface(nic))
                {
                    continue;
                }

                var stats = nic.GetIPv4Statistics();

                counters.Add(
                    new NetworkTrafficCounter(
                        stats.BytesReceived,
                        stats.BytesSent));
            }

            return NetworkSpeedCalculator.CreateSnapshot(counters, DateTime.Now);
        }

        public async Task<(string Download, string Upload)> GetNetworkSpeedAsync()
        {
            return await Task.Run(() =>
            {
                var speed = _calculator.Calculate(CreateSnapshot());

                return (
                    speed.Download,
                    speed.Upload);
            });
        }

        private static bool IsActiveNetworkInterface(NetworkInterface nic)
        {
            return nic.OperationalStatus == OperationalStatus.Up &&
                   nic.NetworkInterfaceType is
                       NetworkInterfaceType.Wireless80211 or
                       NetworkInterfaceType.Ethernet or
                       NetworkInterfaceType.GigabitEthernet or
                       NetworkInterfaceType.FastEthernetFx or
                       NetworkInterfaceType.FastEthernetT;
        }

    }
}
