using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NetSpeedWidget.Models;

namespace NetSpeedWidget.Services;

public static class HardwareSamplingProtocol
{
    public const int MaxMessageBytes = 4096;

    /// <summary>根据用户和安装目录生成互不冲突的本机通信标识。</summary>
    public static string GetPipeName(string userSid, string executableDirectory)
    {
        var identity = userSid + "|" + Path.GetFullPath(executableDirectory).ToUpperInvariant();
        return "NetSpeedWidget.Hardware." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
    }

    /// <summary>按字节边界接收一份受长度限制的硬件快照。</summary>
    public static async Task<HardwareStatusSnapshot> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        // 1. 校验长度，避免损坏的进程消息触发任意大小分配。
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var length = BitConverter.ToInt32(header);
        if (length <= 0 || length > MaxMessageBytes)
            throw new InvalidDataException("硬件快照长度无效。");
        // 2. 完整读取 UTF-8 JSON。
        var data = new byte[length];
        await stream.ReadExactlyAsync(data, cancellationToken);
        return JsonSerializer.Deserialize<HardwareStatusSnapshot>(data);
    }

    /// <summary>向本机主程序发送一份硬件快照。</summary>
    public static async Task WriteAsync(Stream stream, HardwareStatusSnapshot snapshot, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(snapshot);
        if (data.Length > MaxMessageBytes) throw new InvalidDataException("硬件快照过大。");
        await stream.WriteAsync(BitConverter.GetBytes(data.Length), cancellationToken);
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
