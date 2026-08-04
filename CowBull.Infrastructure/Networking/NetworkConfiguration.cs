namespace CowBull.Infrastructure.Networking;

/// <summary>
/// Immutable limits and endpoint settings used by the TCP transports.
/// </summary>
public sealed record NetworkConfiguration
{
    public const int DefaultPort = 4510;
    public const int DefaultMaximumPayloadBytes = 64 * 1024;
    public const int MaximumSupportedPayloadBytes = 16 * 1024 * 1024;

    public NetworkConfiguration(
        string host = "127.0.0.1",
        int port = DefaultPort,
        int maximumPayloadBytes = DefaultMaximumPayloadBytes,
        TimeSpan? connectTimeout = null,
        TimeSpan? readTimeout = null,
        TimeSpan? writeTimeout = null)
    {
        Host = ValidateHost(host);
        Port = ValidatePort(port);
        MaximumPayloadBytes = ValidateMaximumPayloadBytes(maximumPayloadBytes);
        ConnectTimeout = ValidateTimeout(connectTimeout ?? TimeSpan.FromSeconds(10), nameof(connectTimeout));
        ReadTimeout = ValidateTimeout(readTimeout ?? TimeSpan.FromSeconds(30), nameof(readTimeout));
        WriteTimeout = ValidateTimeout(writeTimeout ?? TimeSpan.FromSeconds(10), nameof(writeTimeout));
    }

    public string Host { get; }

    /// <summary>
    /// Gets the TCP port. Port zero is accepted so a server can request an ephemeral port.
    /// A client must use a non-zero port.
    /// </summary>
    public int Port { get; }

    public int MaximumPayloadBytes { get; }

    public TimeSpan ConnectTimeout { get; }

    public TimeSpan ReadTimeout { get; }

    public TimeSpan WriteTimeout { get; }

    private static string ValidateHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        string trimmedHost = host.Trim();
        if (trimmedHost.Length > 253)
        {
            throw new ArgumentOutOfRangeException(nameof(host), "The host name cannot exceed 253 characters.");
        }

        return trimmedHost;
    }

    private static int ValidatePort(int port)
    {
        if (port is < 0 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "The port must be between 0 and 65535.");
        }

        return port;
    }

    private static int ValidateMaximumPayloadBytes(int maximumPayloadBytes)
    {
        if (maximumPayloadBytes is <= 0 or > MaximumSupportedPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayloadBytes),
                $"The maximum payload must be between 1 and {MaximumSupportedPayloadBytes} bytes.");
        }

        return maximumPayloadBytes;
    }

    private static TimeSpan ValidateTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Timeouts must be greater than zero and no longer than five minutes.");
        }

        return timeout;
    }
}
