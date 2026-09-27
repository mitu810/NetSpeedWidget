using System.Collections.Generic;

namespace NetSpeedWidget.Models
{
    public class SystemStatusSettings
    {
        public const int DefaultPreferredPort = 17890;

        public bool Enabled { get; set; } = false;

        public bool ServerEnabled { get; set; } = true;

        public int PreferredPort { get; set; } = DefaultPreferredPort;

        public bool PasswordEnabled { get; set; } = false;

        public string PasswordHash { get; set; } = string.Empty;

        public int RefreshIntervalSeconds { get; set; } = 1;

        public SystemStatusWebTheme WebTheme { get; set; } = SystemStatusWebTheme.FollowApp;

        public SystemStatusCardSettings Cards { get; set; } = new();

        public List<SystemStatusCardLayoutItem> Layout { get; set; } =
            new()
            {
                new SystemStatusCardLayoutItem { CardKey = "network", Order = 0 },
                new SystemStatusCardLayoutItem { CardKey = "cpuUsage", Order = 1 },
                new SystemStatusCardLayoutItem { CardKey = "cpuTemperature", Order = 2 },
                new SystemStatusCardLayoutItem { CardKey = "gpuUsage", Order = 3 },
                new SystemStatusCardLayoutItem { CardKey = "gpuTemperature", Order = 4 },
                new SystemStatusCardLayoutItem { CardKey = "memory", Order = 5 }
            };
    }
}
