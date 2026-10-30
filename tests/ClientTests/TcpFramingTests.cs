using System.Buffers.Binary;
using Rpg.Net;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-v2-hosting 的长度边界和完整消息分帧。</summary>
public sealed class TcpFramingTests
{
    /// <summary>连续帧逐个还原，末尾正常关闭不产生空业务包。</summary>
    [Fact]
    public async Task ConsecutiveFramesRoundTripAndRejectTruncation()
    {
        using var stream = new MemoryStream();
        await TcpFraming.WriteAsync(stream, [1, 2, 3], default);
        await TcpFraming.WriteAsync(stream, [4, 5], default);
        stream.Position = 0;
        Assert.Equal(new byte[] { 1, 2, 3 }, await TcpFraming.ReadAsync(stream, default));
        Assert.Equal(new byte[] { 4, 5 }, await TcpFraming.ReadAsync(stream, default));
        Assert.Null(await TcpFraming.ReadAsync(stream, default));
        using var truncated = new MemoryStream([0, 0, 0, 3, 1]);
        await Assert.ThrowsAsync<EndOfStreamException>(() => TcpFraming.ReadAsync(truncated, default));
    }
    /// <summary>拒绝零、负数和超大长度，分配载荷前失败。</summary>
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(4 * 1024 * 1024 + 1)]
    public async Task InvalidLengthsFailBeforePayloadAllocation(int length)
    {
        var header = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(header, length);
        using var stream = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidDataException>(() => TcpFraming.ReadAsync(stream, default));
    }
}
