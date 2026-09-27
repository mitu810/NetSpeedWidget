using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSpeedWidget.Services;

public sealed record LocalHttpRequest(string RequestLine, IReadOnlyDictionary<string, string> Headers, string Body);
public sealed class LocalHttpRequestException : IOException
{
    public int StatusCode { get; }
    public LocalHttpRequestException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

public static class HttpRequestReader
{
    public const int MaxHeaderBytes = 16 * 1024;
    public const int MaxBodyBytes = 64 * 1024;

    /// <summary>读取有大小边界的 HTTP 请求，Content-Length 按字节计算，JSON 使用 UTF-8。</summary>
    public static async Task<LocalHttpRequest> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        // 1. 有界读取头部，同时保留本次读取中已经收到的正文。
        var buffer = new byte[MaxHeaderBytes + 4096];
        var count = 0;
        var headerEnd = -1;
        while (headerEnd < 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(count, Math.Min(4096, buffer.Length - count)), cancellationToken);
            if (read == 0) throw new LocalHttpRequestException(400, "请求头不完整。");
            var searchStart = Math.Max(0, count - 3);
            count += read;
            for (var i = searchStart; i + 3 < count; i++)
                if (buffer[i] == 13 && buffer[i + 1] == 10 && buffer[i + 2] == 13 && buffer[i + 3] == 10)
                { headerEnd = i + 4; break; }
            if ((headerEnd < 0 && count >= MaxHeaderBytes) || headerEnd > MaxHeaderBytes)
                throw new LocalHttpRequestException(431, "请求头过大。");
        }
        // 2. 校验长度和传输格式，避免字符长度与字节长度混淆。
        var lines = Encoding.ASCII.GetString(buffer, 0, headerEnd - 4).Split("\r\n");
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var separator = lines[i].IndexOf(':');
            if (separator <= 0 || !headers.TryAdd(lines[i][..separator].Trim(), lines[i][(separator + 1)..].Trim()))
                throw new LocalHttpRequestException(400, "请求头格式无效或重复。");
        }
        if (headers.ContainsKey("Transfer-Encoding")) throw new LocalHttpRequestException(400, "不支持分块请求正文。");
        var length = 0;
        if (headers.TryGetValue("Content-Length", out var text) && (!int.TryParse(text, out length) || length < 0))
            throw new LocalHttpRequestException(400, "正文长度无效。");
        if (length > MaxBodyBytes) throw new LocalHttpRequestException(413, "请求正文过大。");
        var body = new byte[length];
        var copied = Math.Min(length, count - headerEnd);
        buffer.AsSpan(headerEnd, copied).CopyTo(body);
        // 3. 完整读取正文并严格按 UTF-8 解码。
        try
        {
            await stream.ReadExactlyAsync(body.AsMemory(copied), cancellationToken);
            return new LocalHttpRequest(lines[0], headers, new UTF8Encoding(false, true).GetString(body));
        }
        catch (EndOfStreamException) { throw new LocalHttpRequestException(400, "请求正文不完整。"); }
        catch (DecoderFallbackException) { throw new LocalHttpRequestException(400, "请求正文不是有效的 UTF-8。"); }
    }
}
