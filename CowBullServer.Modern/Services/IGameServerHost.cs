namespace CowBullServer.Modern.Services;

public interface IGameServerHost : IAsyncDisposable
{
    event EventHandler<ServerActivityEventArgs>? ActivityOccurred;

    event EventHandler<ServerFaultEventArgs>? Faulted;

    bool IsRunning { get; }
    int ConnectedClientCount { get; }
    string Endpoint { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed class ServerActivityEventArgs : EventArgs
{
    public ServerActivityEventArgs(string message, int connectedClientCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentOutOfRangeException.ThrowIfNegative(connectedClientCount);

        Message = message;
        ConnectedClientCount = connectedClientCount;
    }

    public string Message { get; }
    public int ConnectedClientCount { get; }
}

public sealed class ServerFaultEventArgs : EventArgs
{
    public ServerFaultEventArgs(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Message = message;
    }

    public string Message { get; }
}
