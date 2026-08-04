namespace CowBull.Infrastructure.Networking;

public enum TcpConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Disposed,
}

public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    public ConnectionStateChangedEventArgs(
        TcpConnectionState previousState,
        TcpConnectionState currentState,
        string reason,
        Exception? exception = null)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        Reason = reason;
        Exception = exception;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public TcpConnectionState PreviousState { get; }

    public TcpConnectionState CurrentState { get; }

    public string Reason { get; }

    public Exception? Exception { get; }

    public DateTimeOffset Timestamp { get; }
}

public sealed class NetworkMessageReceivedEventArgs : EventArgs
{
    public NetworkMessageReceivedEventArgs(string message)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Timestamp = DateTimeOffset.UtcNow;
    }

    public string Message { get; }

    public DateTimeOffset Timestamp { get; }
}

public sealed class ClientConnectedEventArgs : EventArgs
{
    public ClientConnectedEventArgs(Guid clientId, string remoteEndpoint)
    {
        ClientId = clientId;
        RemoteEndpoint = remoteEndpoint ?? throw new ArgumentNullException(nameof(remoteEndpoint));
        Timestamp = DateTimeOffset.UtcNow;
    }

    public Guid ClientId { get; }

    public string RemoteEndpoint { get; }

    public DateTimeOffset Timestamp { get; }
}

public sealed class ClientDisconnectedEventArgs : EventArgs
{
    public ClientDisconnectedEventArgs(Guid clientId, string reason, Exception? exception = null)
    {
        ClientId = clientId;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        Exception = exception;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public Guid ClientId { get; }

    public string Reason { get; }

    public Exception? Exception { get; }

    public DateTimeOffset Timestamp { get; }
}

public sealed class ClientMessageReceivedEventArgs : EventArgs
{
    public ClientMessageReceivedEventArgs(Guid clientId, string message)
    {
        ClientId = clientId;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Timestamp = DateTimeOffset.UtcNow;
    }

    public Guid ClientId { get; }

    public string Message { get; }

    public DateTimeOffset Timestamp { get; }
}
