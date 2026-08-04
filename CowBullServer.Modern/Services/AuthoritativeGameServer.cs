using System.Text.Json;
using CowBull.Infrastructure.Networking;
using CowBull.Infrastructure.Protocol;

namespace CowBullServer.Modern.Services;

/// <summary>
/// Owns transport lifecycle and processes each client's requests in receive order
/// through the authoritative request handler.
/// </summary>
public sealed class AuthoritativeGameServer : IGameServerHost
{
    private readonly AsyncTcpServer _transport;
    private readonly GameRequestHandler _handler;
    private int _disposeStarted;

    public AuthoritativeGameServer(
        AsyncTcpServer transport,
        GameRequestHandler handler)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(handler);

        _transport = transport;
        _handler = handler;
        _transport.ClientConnected += OnClientConnected;
        _transport.ClientDisconnected += OnClientDisconnected;
        _transport.MessageReceived += OnMessageReceived;
    }

    public event EventHandler<ServerActivityEventArgs>? ActivityOccurred;

    public bool IsRunning => _transport.IsListening;

    public int ConnectedClientCount => _transport.ConnectedClientCount;

    public string Endpoint =>
        _transport.LocalEndpoint?.ToString() ?? "127.0.0.1:4510";

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _transport.StartAsync(cancellationToken);
        PublishActivity($"Server listening on {Endpoint}.");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _transport.StopAsync(cancellationToken);
        PublishActivity("Server stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        try
        {
            // Keep handlers attached during Stop so client disconnects abandon
            // their owned sessions before transport resources disappear.
            await StopAsync();
        }
        finally
        {
            _transport.ClientConnected -= OnClientConnected;
            _transport.ClientDisconnected -= OnClientDisconnected;
            _transport.MessageReceived -= OnMessageReceived;
            await _transport.DisposeAsync();
        }
    }

    private void OnClientConnected(object? sender, ClientConnectedEventArgs eventArgs) =>
        PublishActivity($"Client {eventArgs.ClientId} connected from {eventArgs.RemoteEndpoint}.");

    private void OnClientDisconnected(
        object? sender,
        ClientDisconnectedEventArgs eventArgs)
    {
        _handler.Disconnect(eventArgs.ClientId);
        PublishActivity($"Client {eventArgs.ClientId} disconnected: {eventArgs.Reason}");
    }

    private void OnMessageReceived(
        object? sender,
        ClientMessageReceivedEventArgs eventArgs)
    {
        // Transport invokes this callback on the owning client's receive loop.
        // Waiting here gives that client ordering and natural backpressure while
        // independent client loops continue in parallel.
        ProcessMessageSafelyAsync(eventArgs).GetAwaiter().GetResult();
    }

    private async Task ProcessMessageSafelyAsync(ClientMessageReceivedEventArgs eventArgs)
    {
        try
        {
            ProtocolMessage request = ProtocolSerializer.Deserialize(eventArgs.Message);
            IReadOnlyList<ProtocolMessage> responses =
                _handler.Handle(eventArgs.ClientId, request);

            foreach (ProtocolMessage response in responses)
            {
                bool sent = await _transport.SendAsync(eventArgs.ClientId, response);
                if (!sent)
                {
                    PublishActivity(
                        $"Could not deliver a response to client {eventArgs.ClientId}.");
                    return;
                }
            }

            PublishActivity(
                $"Processed {request.GetType().Name} for client {eventArgs.ClientId}.");
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException)
        {
            await SendErrorAsync(
                eventArgs.ClientId,
                "invalidPayload",
                "The request payload is invalid.");
            PublishActivity($"Rejected an invalid payload from client {eventArgs.ClientId}.");
        }
        catch (Exception)
        {
            await SendErrorAsync(
                eventArgs.ClientId,
                "serverError",
                "The server could not process the request.");
            PublishActivity($"A request from client {eventArgs.ClientId} failed.");
        }
    }

    private async Task SendErrorAsync(
        Guid clientId,
        string code,
        string description) =>
        await _transport.SendAsync(
            clientId,
            new ErrorResponse(Guid.NewGuid(), null, code, description));

    private void PublishActivity(string message)
    {
        try
        {
            ActivityOccurred?.Invoke(
                this,
                new ServerActivityEventArgs(message, ConnectedClientCount));
        }
        catch
        {
            // Presentation subscribers cannot compromise server operation.
        }
    }
}
