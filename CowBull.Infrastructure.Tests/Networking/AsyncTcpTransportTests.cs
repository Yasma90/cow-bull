using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
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

    [Fact]
    public async Task ClientEvents_AreDeliveredInConnectedMessageDisconnectedOrder()
    {
        var serverConfiguration = new NetworkConfiguration(
            IPAddress.Loopback.ToString(),
            port: 0,
            readTimeout: TimeSpan.FromSeconds(10));
        await using var server = new AsyncTcpServer(serverConfiguration);
        var eventOrder = new ConcurrentQueue<string>();
        var messageReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        server.ClientConnected += (_, _) => eventOrder.Enqueue("connected");
        server.MessageReceived += (_, _) =>
        {
            eventOrder.Enqueue("message");
            messageReceived.TrySetResult();
        };
        server.ClientDisconnected += (_, _) =>
        {
            eventOrder.Enqueue("disconnected");
            disconnected.TrySetResult();
        };

        await server.StartAsync();
        int port = Assert.IsType<IPEndPoint>(server.LocalEndpoint).Port;
        await using var client = new AsyncTcpClient(
            new NetworkConfiguration(
                IPAddress.Loopback.ToString(),
                port,
                readTimeout: TimeSpan.FromSeconds(10)));

        await client.ConnectAsync();
        await client.SendAsync("sent-immediately#雪");
        await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await client.DisconnectAsync();
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

        string[] observedOrder = eventOrder.ToArray();
        Assert.Equal(3, observedOrder.Length);
        Assert.Equal("connected", observedOrder[0]);
        Assert.Equal("message", observedOrder[1]);
        Assert.Equal("disconnected", observedOrder[2]);

        await server.StopAsync();
    }

    [Fact]
    public async Task ClientSend_CancelledAfterWriteAdmission_AbortsConnectionAndPreservesCallerToken()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Server.ReceiveBufferSize = 1_024;
        listener.Start();

        try
        {
            int port = Assert.IsType<IPEndPoint>(listener.LocalEndpoint).Port;
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
            await using var client = new AsyncTcpClient(
                new NetworkConfiguration(
                    IPAddress.Loopback.ToString(),
                    port,
                    maximumPayloadBytes: NetworkConfiguration.MaximumSupportedPayloadBytes,
                    readTimeout: TimeSpan.FromSeconds(30),
                    writeTimeout: TimeSpan.FromSeconds(30)));
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.ConnectionStateChanged += (_, eventArgs) =>
            {
                if (eventArgs.CurrentState == TcpConnectionState.Disconnected)
                {
                    disconnected.TrySetResult();
                }
            };

            await client.ConnectAsync();
            using TcpClient peer = await acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
            peer.ReceiveBufferSize = 1_024;

            using var cancellation = new CancellationTokenSource();
            string payload = new('x', NetworkConfiguration.MaximumSupportedPayloadBytes);
            Task sendTask = SendContinuouslyAsync(client, payload, cancellation.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            cancellation.Cancel();

            OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sendTask);
            await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.False(client.IsConnected);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task StateNotifications_ReentrantDisconnectRacingRemoteClose_RemainInTransitionOrder()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            int port = Assert.IsType<IPEndPoint>(listener.LocalEndpoint).Port;
            Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();
            var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var observedStates = new ConcurrentQueue<TcpConnectionState>();
            Task? localDisconnect = null;

            await using var client = new AsyncTcpClient(
                new NetworkConfiguration(
                    IPAddress.Loopback.ToString(),
                    port,
                    readTimeout: TimeSpan.FromSeconds(10)));
            client.ConnectionStateChanged += (_, eventArgs) =>
            {
                observedStates.Enqueue(eventArgs.CurrentState);
                if (eventArgs.CurrentState == TcpConnectionState.Connected)
                {
                    connected.TrySetResult();
                    localDisconnect = client.DisconnectAsync();
                }
            };

            Task remoteClose = Task.Run(async () =>
            {
                using TcpClient peer = await acceptTask;
                await connected.Task;
            });

            await client.ConnectAsync();
            Assert.NotNull(localDisconnect);
            await localDisconnect.WaitAsync(TimeSpan.FromSeconds(5));
            await remoteClose.WaitAsync(TimeSpan.FromSeconds(5));

            TcpConnectionState[] states = observedStates.ToArray();
            Assert.Equal(4, states.Length);
            Assert.Equal(TcpConnectionState.Connecting, states[0]);
            Assert.Equal(TcpConnectionState.Connected, states[1]);
            Assert.Equal(TcpConnectionState.Disconnecting, states[2]);
            Assert.Equal(TcpConnectionState.Disconnected, states[3]);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task SendContinuouslyAsync(
        AsyncTcpClient client,
        string payload,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            await client.SendAsync(payload, cancellationToken);
        }
    }
}
