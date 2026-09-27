using System;

namespace NetSpeedWidget.Models;

public sealed record UpdateRelease(Version Version, string Tag, string Notes, DateTimeOffset PublishedAt,
    Uri DownloadUrl, string FileName, long Size, string Sha256, bool Installed);

public sealed record UpdateJob(int ProcessId, long ProcessStartTicks, string TargetDirectory,
    string PackagePath, string Sha256, long Size, bool Installed, string Version, string ResultPath);
