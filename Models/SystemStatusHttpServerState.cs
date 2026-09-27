using System;
using System.Collections.Generic;

namespace NetSpeedWidget.Models
{
    public readonly record struct SystemStatusHttpServerState(
        bool IsRunning,
        int ActualPort,
        IReadOnlyList<string> AccessUrls,
        string LastError)
    {
        public static SystemStatusHttpServerState Stopped(string lastError = "")
        {
            return new SystemStatusHttpServerState(
                false,
                0,
                Array.Empty<string>(),
                lastError ?? string.Empty);
        }
    }
}
