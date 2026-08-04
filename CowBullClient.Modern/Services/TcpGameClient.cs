using CowBull.Infrastructure.Networking;
using CowBull.Infrastructure.Protocol;

namespace CowBullClient.Modern.Services;

public sealed class TcpGameClient : IGameClient
{
    private readonly AsyncTcpClient _transport;
    private int _disposeStarted;

    public TcpGameClient(AsyncTcpClient transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
        _transport.MessageReceived += OnMessageReceived;
        _transport.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public event EventHandler<GameClientMessageEventArgs>? MessageReceived;
    public event EventHandler<GameClientStatusEventArgs>? StatusChanged;
    public event EventHandler<GameClientFaultEventArgs>? Faulted;

    public bool IsConnected => _transport.IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _transport.ConnectAsync(cancellationToken);

    public ValueTask SendAsync(
        ProtocolMessage message,
        CancellationToken cancellationToken = default) =>
        _transport.SendAsync(message, cancellationToken);

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        _transport.DisconnectAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        _transport.MessageReceived -= OnMessageReceived;
        _transport.ConnectionStateChanged -= OnConnectionStateChanged;
        await _transport.DisposeAsync();
    }

    private void OnMessageReceived(object? sender, NetworkMessageReceivedEventArgs eventArgs)
    {
        try
        {
            var message = ProtocolSerializer.Deserialize(eventArgs.Message);
            MessageReceived?.Invoke(this, new GameClientMessageEventArgs(message));
        }
        catch (Exception exception) when (
            exception is System.Text.Json.JsonException or ArgumentException)
        {
            Faulted?.Invoke(
                this,
                new GameClientFaultEventArgs("The server sent an invalid message.", exception));
        }
    }

    private void OnConnectionStateChanged(
        object? sender,
        ConnectionStateChangedEventArgs eventArgs) =>
        StatusChanged?.Invoke(
            this,
            new GameClientStatusEventArgs(
                eventArgs.CurrentState == TcpConnectionState.Connected,
                eventArgs.Reason));
}
