using System.Collections.Concurrent;
using System.Net;
using CowBull.Infrastructure.Networking;

namespace CowBull.Infrastructure.Tests.Networking;

public sealed class AsyncTcpTransportTests
{
    [Fact]
    public async Task ClientAndServer_ExchangeHashAndUnicodeMessages_ThenStopPromptly()
    {
        var serverConfiguration = new NetworkConfiguration(
            IPAddress.Loopback.ToString(),
            port: 0,
            maximumPayloadBytes: 4_096,
            connectTimeout: TimeSpan.FromSeconds(3),
            readTimeout: TimeSpan.FromSeconds(10),
            writeTimeout: TimeSpan.FromSeconds(3));

        await using var server = new AsyncTcpServer(serverConfiguration);
        var connectedClient = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverMessage = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientMessage = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientConnected += (_, eventArgs) => connectedClient.TrySetResult(eventArgs.ClientId);
        server.MessageReceived += (_, eventArgs) => serverMessage.TrySetResult(eventArgs.Message);

        await server.StartAsync();
        int port = Assert.IsType<IPEndPoint>(server.LocalEndpoint).Port;

        var clientConfiguration = new NetworkConfiguration(
            IPAddress.Loopback.ToString(),
            port,
            maximumPayloadBytes: 4_096,
            connectTimeout: TimeSpan.FromSeconds(3),
            readTimeout: TimeSpan.FromSeconds(10),
            writeTimeout: TimeSpan.FromSeconds(3));

        await using var client = new AsyncTcpClient(clientConfiguration);
        client.MessageReceived += (_, eventArgs) => clientMessage.TrySetResult(eventArgs.Message);

        await client.ConnectAsync();
        Guid clientId = await connectedClient.Task.WaitAsync(TimeSpan.FromSeconds(5));

        const string Request = "guess#1234 🐮";
        await client.SendAsync(Request);
        Assert.Equal(Request, await serverMessage.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        const string Response = "result#1牛2雪";
        Assert.True(await server.SendAsync(clientId, Response));
        Assert.Equal(Response, await clientMessage.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        await client.DisconnectAsync();
        await server.StopAsync();

        Assert.Equal(TcpConnectionState.Disconnected, client.State);
        Assert.False(server.IsListening);
        Assert.Equal(0, server.ConnectedClientCount);
    }

    [Fact]
    public async Task StopAsync_ClosesAnIdleConnectionWithoutWaitingForReadTimeout()
    {
        var serverConfiguration = new NetworkConfiguration(
            IPAddress.Loopback.ToString(),
            port: 0,
            readTimeout: TimeSpan.FromMinutes(1));
        await using var server = new AsyncTcpServer(serverConfiguration);
        await server.StartAsync();

        int port = Assert.IsType<IPEndPoint>(server.LocalEndpoint).Port;
        await using var client = new AsyncTcpClient(
            new NetworkConfiguration(
                IPAddress.Loopback.ToString(),
                port,
                readTimeout: TimeSpan.FromMinutes(1)));
        await client.ConnectAsync();

        await server.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));

        Assert.False(server.IsListening);
        Assert.Equal(0, server.ConnectedClientCount);
    }

    [Fact]
    public async Task ConcurrentWrites_AreDeliveredAsCompleteIndependentFramesInBothDirections()
    {
        const int MessageCount = 40;
        var serverConfiguration = new NetworkConfiguration(
            IPAddress.Loopback.ToString(),
            port: 0,
            maximumPayloadBytes: 4_096,
            readTimeout: TimeSpan.FromSeconds(10),
            writeTimeout: TimeSpan.FromSeconds(5));
        await using var server = new AsyncTcpServer(serverConfiguration);

        var connectedClient = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverReceivedAll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientReceivedAll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverMessages = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        var clientMessages = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        server.ClientConnected += (_, eventArgs) => connectedClient.TrySetResult(eventArgs.ClientId);
        server.MessageReceived += (_, eventArgs) =>
        {
            serverMessages.TryAdd(eventArgs.Message, 0);
            if (serverMessages.Count == MessageCount)
            {
                serverReceivedAll.TrySetResult();
            }
        };

        await server.StartAsync();
        int port = Assert.IsType<IPEndPoint>(server.LocalEndpoint).Port;
        await using var client = new AsyncTcpClient(
            new NetworkConfiguration(
                IPAddress.Loopback.ToString(),
                port,
                maximumPayloadBytes: 4_096,
                readTimeout: TimeSpan.FromSeconds(10),
                writeTimeout: TimeSpan.FromSeconds(5)));
        client.MessageReceived += (_, eventArgs) =>
        {
            clientMessages.TryAdd(eventArgs.Message, 0);
            if (clientMessages.Count == MessageCount)
            {
                clientReceivedAll.TrySetResult();
            }
        };

        await client.ConnectAsync();
        Guid clientId = await connectedClient.Task.WaitAsync(TimeSpan.FromSeconds(5));

        string[] requests = Enumerable.Range(0, MessageCount)
            .Select(index => "client#" + index + "雪")
            .ToArray();
        await Task.WhenAll(requests.Select(message => client.SendAsync(message).AsTask()));
        await serverReceivedAll.Task.WaitAsync(TimeSpan.FromSeconds(5));

        string[] responses = Enumerable.Range(0, MessageCount)
            .Select(index => "server#" + index + "🐮")
            .ToArray();
        bool[] sendResults = await Task.WhenAll(
            responses.Select(message => server.SendAsync(clientId, message).AsTask()));
        await clientReceivedAll.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.All(sendResults, Assert.True);
        Assert.All(requests, message => Assert.True(serverMessages.ContainsKey(message)));
        Assert.All(responses, message => Assert.True(clientMessages.ContainsKey(message)));

        await client.DisconnectAsync();
        await server.StopAsync();
    }
}
