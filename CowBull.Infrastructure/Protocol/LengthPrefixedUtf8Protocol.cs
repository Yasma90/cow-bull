using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace CowBull.Infrastructure.Protocol;

/// <summary>
/// Reads and writes UTF-8 messages framed by an unsigned four-byte big-endian payload length.
/// </summary>
public sealed class LengthPrefixedUtf8Protocol
{
    private const int HeaderLength = sizeof(uint);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly int _maximumPayloadBytes;

    public LengthPrefixedUtf8Protocol(int maximumPayloadBytes)
    {
        if (maximumPayloadBytes is <= 0 or > Networking.NetworkConfiguration.MaximumSupportedPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPayloadBytes));
        }

        _maximumPayloadBytes = maximumPayloadBytes;
    }

    public int MaximumPayloadBytes => _maximumPayloadBytes;

    public async ValueTask WriteAsync(Stream stream, string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);

        int payloadLength = StrictUtf8.GetByteCount(message);
        if (payloadLength > _maximumPayloadBytes)
        {
            throw new FrameTooLargeException((uint)payloadLength, _maximumPayloadBytes);
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(HeaderLength + payloadLength);
        try
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, HeaderLength), (uint)payloadLength);
            _ = StrictUtf8.GetBytes(message.AsSpan(), buffer.AsSpan(HeaderLength, payloadLength));
            await stream.WriteAsync(buffer.AsMemory(0, HeaderLength + payloadLength), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Reads one frame, or returns <see langword="null"/> when the peer closes cleanly between frames.
    /// </summary>
    public async ValueTask<string?> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] header = ArrayPool<byte>.Shared.Rent(HeaderLength);
        try
        {
            int headerBytes = await ReadAtMostAsync(stream, header.AsMemory(0, HeaderLength), cancellationToken).ConfigureAwait(false);
            if (headerBytes == 0)
            {
                return null;
            }

            if (headerBytes != HeaderLength)
            {
                throw new TruncatedFrameException("header", HeaderLength, headerBytes);
            }

            uint payloadLength = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, HeaderLength));
            if (payloadLength > _maximumPayloadBytes)
            {
                throw new FrameTooLargeException(payloadLength, _maximumPayloadBytes);
            }

            if (payloadLength == 0)
            {
                return string.Empty;
            }

            int length = checked((int)payloadLength);
            byte[] payload = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                int payloadBytes = await ReadAtMostAsync(stream, payload.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
                if (payloadBytes != length)
                {
                    throw new TruncatedFrameException("payload", length, payloadBytes);
                }

                try
                {
                    return StrictUtf8.GetString(payload, 0, length);
                }
                catch (DecoderFallbackException exception)
                {
                    throw new FrameProtocolException("The frame payload is not valid UTF-8.", exception);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(payload);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);
        }
    }

    private static async ValueTask<int> ReadAtMostAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        int totalBytesRead = 0;
        while (totalBytesRead < destination.Length)
        {
            int bytesRead = await stream.ReadAsync(destination[totalBytesRead..], cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }
}
