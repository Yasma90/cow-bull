using CowBull.Infrastructure.Protocol;

namespace CowBullClient.Modern.Services;

public interface IGameClient : IAsyncDisposable
{
    event EventHandler<GameClientMessageEventArgs>? MessageReceived;
    event EventHandler<GameClientStatusEventArgs>? StatusChanged;
    event EventHandler<GameClientFaultEventArgs>? Faulted;

    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    ValueTask SendAsync(ProtocolMessage message, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

public sealed class GameClientMessageEventArgs : EventArgs
{
    public GameClientMessageEventArgs(ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Message = message;
    }

    public ProtocolMessage Message { get; }
}

public sealed class GameClientStatusEventArgs : EventArgs
{
    public GameClientStatusEventArgs(bool isConnected, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        IsConnected = isConnected;
        Reason = reason;
    }

    public bool IsConnected { get; }
    public string Reason { get; }
}

public sealed class GameClientFaultEventArgs : EventArgs
{
    public GameClientFaultEventArgs(string description, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(exception);
        Description = description;
        Exception = exception;
    }

    public string Description { get; }
    public Exception Exception { get; }
}
