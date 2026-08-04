namespace CowBull.Infrastructure.Protocol;

public class FrameProtocolException : IOException
{
    public FrameProtocolException(string message)
        : base(message)
    {
    }

    public FrameProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class FrameTooLargeException : FrameProtocolException
{
    public FrameTooLargeException(uint payloadLength, int maximumPayloadBytes)
        : base($"The frame payload length {payloadLength} exceeds the configured maximum of {maximumPayloadBytes} bytes.")
    {
        PayloadLength = payloadLength;
        MaximumPayloadBytes = maximumPayloadBytes;
    }

    public uint PayloadLength { get; }

    public int MaximumPayloadBytes { get; }
}

public sealed class TruncatedFrameException : FrameProtocolException
{
    public TruncatedFrameException(string section, int expectedBytes, int receivedBytes)
        : base($"The frame {section} was truncated: expected {expectedBytes} bytes but received {receivedBytes}.")
    {
        Section = section;
        ExpectedBytes = expectedBytes;
        ReceivedBytes = receivedBytes;
    }

    public string Section { get; }

    public int ExpectedBytes { get; }

    public int ReceivedBytes { get; }
}
