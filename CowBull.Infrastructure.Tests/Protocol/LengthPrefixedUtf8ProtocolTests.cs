using System.Buffers.Binary;
using CowBull.Infrastructure.Protocol;

namespace CowBull.Infrastructure.Tests.Protocol;

public sealed class LengthPrefixedUtf8ProtocolTests
{
    [Fact]
    public async Task WriteAndReadAsync_RoundTripsMessage()
    {
        var protocol = new LengthPrefixedUtf8Protocol(1_024);
        using var stream = new MemoryStream();

        await protocol.WriteAsync(stream, "1234");
        stream.Position = 0;

        string? result = await protocol.ReadAsync(stream);

        Assert.Equal("1234", result);
    }

    [Fact]
    public async Task WriteAndReadAsync_PreservesHashCharactersAndUnicode()
    {
        const string Message = "guess#42 🐮 雪 café";
        var protocol = new LengthPrefixedUtf8Protocol(1_024);
        using var stream = new MemoryStream();

        await protocol.WriteAsync(stream, Message);
        stream.Position = 0;

        string? result = await protocol.ReadAsync(stream);

        Assert.Equal(Message, result);
    }

    [Fact]
    public async Task ReadAsync_ReadsMultipleAdjacentFramesIndependently()
    {
        var protocol = new LengthPrefixedUtf8Protocol(1_024);
        using var stream = new MemoryStream();

        await protocol.WriteAsync(stream, "first#");
        await protocol.WriteAsync(stream, "second");
        await protocol.WriteAsync(stream, "第三");
        stream.Position = 0;

        Assert.Equal("first#", await protocol.ReadAsync(stream));
        Assert.Equal("second", await protocol.ReadAsync(stream));
        Assert.Equal("第三", await protocol.ReadAsync(stream));
        Assert.Null(await protocol.ReadAsync(stream));
    }

    [Fact]
    public async Task WriteAsync_RejectsOversizedUtf8Payload()
    {
        var protocol = new LengthPrefixedUtf8Protocol(4);
        using var stream = new MemoryStream();

        FrameTooLargeException exception = await Assert.ThrowsAsync<FrameTooLargeException>(
            () => protocol.WriteAsync(stream, "🐮🐮").AsTask());

        Assert.Equal((uint)8, exception.PayloadLength);
        Assert.Equal(4, exception.MaximumPayloadBytes);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task ReadAsync_RejectsOversizedLengthBeforeAllocatingPayload()
    {
        var protocol = new LengthPrefixedUtf8Protocol(16);
        byte[] frame = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(frame, 17);
        using var stream = new MemoryStream(frame);

        FrameTooLargeException exception = await Assert.ThrowsAsync<FrameTooLargeException>(
            () => protocol.ReadAsync(stream).AsTask());

        Assert.Equal((uint)17, exception.PayloadLength);
    }

    [Fact]
    public async Task ReadAsync_RejectsMalformedUtf8()
    {
        var protocol = new LengthPrefixedUtf8Protocol(16);
        byte[] frame = CreateFrame(0xC3, 0x28);
        using var stream = new MemoryStream(frame);

        FrameProtocolException exception = await Assert.ThrowsAsync<FrameProtocolException>(
            () => protocol.ReadAsync(stream).AsTask());

        Assert.Contains("UTF-8", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_DetectsTruncatedHeader()
    {
        var protocol = new LengthPrefixedUtf8Protocol(16);
        using var stream = new MemoryStream(new byte[] { 0, 0, 0 });

        TruncatedFrameException exception = await Assert.ThrowsAsync<TruncatedFrameException>(
            () => protocol.ReadAsync(stream).AsTask());

        Assert.Equal("header", exception.Section);
        Assert.Equal(4, exception.ExpectedBytes);
        Assert.Equal(3, exception.ReceivedBytes);
    }

    [Fact]
    public async Task ReadAsync_DetectsTruncatedPayload()
    {
        var protocol = new LengthPrefixedUtf8Protocol(16);
        byte[] frame = new byte[] { 0, 0, 0, 4, 1, 2 };
        using var stream = new MemoryStream(frame);

        TruncatedFrameException exception = await Assert.ThrowsAsync<TruncatedFrameException>(
            () => protocol.ReadAsync(stream).AsTask());

        Assert.Equal("payload", exception.Section);
        Assert.Equal(4, exception.ExpectedBytes);
        Assert.Equal(2, exception.ReceivedBytes);
    }

    private static byte[] CreateFrame(params byte[] payload)
    {
        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
        payload.CopyTo(frame, 4);
        return frame;
    }
}
