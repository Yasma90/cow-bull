using System.Threading.Channels;
using CowBull.Application.Games;
using CowBull.Application.Ports;
using CowBull.Domain.Games;
using CowBull.Infrastructure.Networking;
using CowBull.Infrastructure.Persistence;
using CowBull.Infrastructure.Protocol;
using CowBullServer.Modern.Services;

namespace CowBull.Presentation.Tests.Server;

public sealed class AuthoritativeGameServerTests
{
    [Fact]
    public async Task Same_client_requests_are_ordered_and_stop_completes_disconnect_cleanup()
    {
        var sessionId = Guid.NewGuid();
        var service = new GameService(
            new InMemoryGameRepository(),
            new StubSecretNumberGenerator("0123"),
            new StubGameIdGenerator(sessionId),
            TimeProvider.System);
        var handler = new GameRequestHandler(service);
        var serverTransport = new AsyncTcpServer(
            new NetworkConfiguration(
                port: 0,
                connectTimeout: TimeSpan.FromSeconds(5),
                readTimeout: TimeSpan.FromSeconds(10),
                writeTimeout: TimeSpan.FromSeconds(5)));
        var connectedClient = new TaskCompletionSource<Guid>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        serverTransport.ClientConnected += (_, eventArgs) =>
            connectedClient.TrySetResult(eventArgs.ClientId);
        await using var server = new AuthoritativeGameServer(serverTransport, handler);
        await server.StartAsync();

        int port = serverTransport.LocalEndpoint?.Port
            ?? throw new InvalidOperationException("The server did not publish an endpoint.");
        var clientTransport = new AsyncTcpClient(
            new NetworkConfiguration(
                port: port,
                connectTimeout: TimeSpan.FromSeconds(5),
                readTimeout: TimeSpan.FromSeconds(10),
                writeTimeout: TimeSpan.FromSeconds(5)));
        await using var client = clientTransport;
        var responses = Channel.CreateUnbounded<ProtocolMessage>();
        client.MessageReceived += (_, eventArgs) =>
            responses.Writer.TryWrite(ProtocolSerializer.Deserialize(eventArgs.Message));

        await client.ConnectAsync();
        Guid connectedClientId = await connectedClient.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var newGameId = Guid.NewGuid();
        await client.SendAsync(new NewGameRequest(newGameId, 4, 10));
        var newGame = Assert.IsType<NewGameResponse>(
            await ReadResponseAsync(responses.Reader));
        Assert.Equal(newGameId, newGame.MessageId);

        await client.SendAsync(new GuessRequest(Guid.NewGuid(), sessionId, "4567"));
        await client.SendAsync(new GuessRequest(Guid.NewGuid(), sessionId, "8901"));

        var first = Assert.IsType<GuessResponse>(
            await ReadResponseAsync(responses.Reader));
        var second = Assert.IsType<GuessResponse>(
            await ReadResponseAsync(responses.Reader));
        Assert.Equal(1, first.AttemptNumber);
        Assert.Equal(2, second.AttemptNumber);

        await server.StopAsync();

        Assert.False(server.IsRunning);
        ProtocolMessage afterStop = Assert.Single(
            handler.Handle(
                connectedClientId,
                new GuessRequest(Guid.NewGuid(), sessionId, "0123")));
        Assert.IsType<ErrorResponse>(afterStop);
    }

    private static async Task<ProtocolMessage> ReadResponseAsync(
        ChannelReader<ProtocolMessage> responses) =>
        await responses.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

    private sealed class StubSecretNumberGenerator(string secret) : ISecretNumberGenerator
    {
        public string Generate(GameConfiguration configuration) => secret;
    }

    private sealed class StubGameIdGenerator(Guid gameId) : IGameIdGenerator
    {
        public Guid Create() => gameId;
    }
}
