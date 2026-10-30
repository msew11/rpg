using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Rpg.Net;

/// <summary>客户端和服务器共用的有界大端长度分帧。</summary>
internal static class TcpFraming
{
    /// <summary>单条信封允许的最大字节数。</summary>
    internal const int MaximumPacket = 4 * 1024 * 1024;
    /// <summary>读满一条消息；正常流结束返回空。</summary>
    internal static async Task<byte[]?> ReadAsync(Stream stream, CancellationToken cancellation)
    {
        var header = new byte[4];
        var first = await stream.ReadAsync(header.AsMemory(0, 1), cancellation).ConfigureAwait(false);
        if (first == 0) return null;
        await stream.ReadExactlyAsync(header.AsMemory(1), cancellation).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length <= 0 || length > MaximumPacket) throw new InvalidDataException("TCP 消息长度超限");
        var packet = new byte[length];
        await stream.ReadExactlyAsync(packet, cancellation).ConfigureAwait(false);
        return packet;
    }
    /// <summary>顺序写入长度与不可变消息字节。</summary>
    internal static async Task WriteAsync(Stream stream, byte[] packet, CancellationToken cancellation)
    {
        if (packet.Length <= 0 || packet.Length > MaximumPacket) throw new InvalidDataException("TCP 消息长度超限");
        var header = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(header, packet.Length);
        await stream.WriteAsync(header, cancellation).ConfigureAwait(false);
        await stream.WriteAsync(packet, cancellation).ConfigureAwait(false);
    }
}
