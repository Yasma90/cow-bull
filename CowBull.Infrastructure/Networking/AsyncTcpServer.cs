using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CowBull.Infrastructure.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CowBull.Infrastructure.Networking;

/// <summary>
/// Multi-client TCP server for bounded, length-prefixed UTF-8 messages.
/// </summary>
public sealed partial class AsyncTcpServer : IAsyncDisposable
{
    private readonly NetworkConfiguration _configuration;
    private readonly LengthPrefixedUtf8Protocol _protocol;
    private readonly ILogger<AsyncTcpServer> _logger;
    private readonly ConcurrentDictionary<Guid, ClientConnection> _clients = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _serverSync = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _serverCancellation;
    private Task? _acceptTask;
    private int _isListening;
    private int _disposeStarted;

    public AsyncTcpServer(
        NetworkConfiguration configuration,
        ILogger<AsyncTcpServer>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _protocol = new LengthPrefixedUtf8Protocol(configuration.MaximumPayloadBytes);
        _logger = logger ?? NullLogger<AsyncTcpServer>.Instance;
    }

    /// <summary>
    /// Raised synchronously on the accept loop before receiving starts for that client.
    /// Handlers must return promptly. Handler exceptions are isolated from the transport.
    /// </summary>
    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;

    /// <summary>
    /// Raised synchronously on the client receive loop. Handlers must return promptly.
    /// Handler exceptions are isolated from the transport.
    /// </summary>
    public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

    /// <summary>
    /// Raised synchronously on the client receive loop. Handlers must return promptly.
    /// Handler exceptions are isolated from the transport.
    /// </summary>
    public event EventHandler<ClientMessageReceivedEventArgs>? MessageReceived;

    public bool IsListening => Volatile.Read(ref _isListening) != 0;

    public int ConnectedClientCount => _clients.Count;

    public IPEndPoint? LocalEndpoint
    {
        get
        {
            lock (_serverSync)
            {
                return _listener?.LocalEndpoint as IPEndPoint;
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (IsListening)
            {
                return;
            }

            IPAddress address = await ResolveAddressAsync(_configuration.Host, cancellationToken).ConfigureAwait(false);
            var listener = new TcpListener(address, _configuration.Port);
            var serverCancellation = new CancellationTokenSource();

            try
            {
                listener.Start();
                lock (_serverSync)
                {
                    _listener = listener;
                    _serverCancellation = serverCancellation;
                    Volatile.Write(ref _isListening, 1);
                    _acceptTask = AcceptLoopAsync(listener, serverCancellation);
                }
            }
            catch
            {
                listener.Stop();
                serverCancellation.Dispose();
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public ValueTask<bool> SendAsync(
        Guid clientId,
        ProtocolMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SendAsync(clientId, ProtocolSerializer.Serialize(message), cancellationToken);
    }

    public async ValueTask<bool> SendAsync(
        Guid clientId,
        string message,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        if (!_clients.TryGetValue(clientId, out ClientConnection? connection))
        {
            return false;
        }

        try
        {
            await connection.SendAsync(message, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException exception)
        {
            LogSendFailure(clientId, exception);
            return false;
        }
        catch (Exception exception)
            when (exception is IOException or SocketException or ObjectDisposedException)
        {
            connection.Abort("The client connection failed while sending.");
            LogSendFailure(clientId, exception);
            return false;
        }
    }

    public Task BroadcastAsync(ProtocolMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return BroadcastAsync(ProtocolSerializer.Serialize(message), cancellationToken);
    }

    public async Task BroadcastAsync(string message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        ClientConnection[] connections = _clients.Values.ToArray();
        await Task.WhenAll(connections.Select(connection => SendToConnectionAsync(connection, message, cancellationToken)))
            .ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return StopCoreAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestStop();

        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            // StartAsync may have owned the lifecycle gate when the first stop request ran.
            // Repeat it here so a listener created during that race is always interrupted.
            RequestStop();

            Task? acceptTask;
            lock (_serverSync)
            {
                acceptTask = _acceptTask;
            }

            if (acceptTask is not null)
            {
                await acceptTask.ConfigureAwait(false);
            }

            ClientConnection[] remainingConnections = _clients.Values.ToArray();
            foreach (ClientConnection connection in remainingConnections)
            {
                connection.Abort("The server is stopping.");
            }

            Task[] receiveTasks = remainingConnections
                .Select(connection => connection.ReceiveTask)
                .Where(task => task is not null)
                .Cast<Task>()
                .ToArray();

            if (receiveTasks.Length > 0)
            {
                await Task.WhenAll(receiveTasks).ConfigureAwait(false);
            }

            ClearServerResources();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationTokenSource serverCancellation)
    {
        try
        {
            while (!serverCancellation.IsCancellationRequested)
            {
                TcpClient tcpClient = await listener.AcceptTcpClientAsync(serverCancellation.Token).ConfigureAwait(false);
                if (serverCancellation.IsCancellationRequested)
                {
                    tcpClient.Dispose();
                    break;
                }

                tcpClient.NoDelay = true;
                var connection = new ClientConnection(
                    Guid.NewGuid(),
                    tcpClient,
                    _protocol,
                    _configuration,
                    serverCancellation.Token);

                if (!_clients.TryAdd(connection.Id, connection))
                {
                    connection.Abort("A duplicate client identifier was generated.");
                    await connection.DisposeAsync().ConfigureAwait(false);
                    continue;
                }

                var eventArgs = new ClientConnectedEventArgs(connection.Id, connection.RemoteEndpoint);
                InvokeEventHandler(ClientConnected, eventArgs);
                connection.ReceiveTask = HandleClientAsync(connection);
            }
        }
        catch (OperationCanceledException) when (serverCancellation.IsCancellationRequested)
        {
            // Expected server shutdown.
        }
        catch (SocketException) when (serverCancellation.IsCancellationRequested)
        {
            // TcpListener.Stop interrupts AcceptTcpClientAsync on all supported runtimes.
        }
        catch (ObjectDisposedException) when (serverCancellation.IsCancellationRequested)
        {
            // Expected when listener shutdown wins a race with accept.
        }
        catch (Exception exception)
        {
            LogAcceptFailure(exception);
            RequestStop();
        }
        finally
        {
            Volatile.Write(ref _isListening, 0);
        }
    }

    private async Task HandleClientAsync(ClientConnection connection)
    {
        string disconnectReason = "The remote endpoint closed the connection.";
        Exception? disconnectException = null;

        try
        {
            while (!connection.CancellationToken.IsCancellationRequested)
            {
                string? message = await connection.ReadAsync().ConfigureAwait(false);
                if (message is null)
                {
                    break;
                }

                var eventArgs = new ClientMessageReceivedEventArgs(connection.Id, message);
                InvokeEventHandler(MessageReceived, eventArgs);
            }
        }
        catch (OperationCanceledException) when (connection.CancellationToken.IsCancellationRequested)
        {
            disconnectReason = connection.CloseReason;
        }
        catch (Exception exception)
            when (exception is IOException or SocketException or TimeoutException or FrameProtocolException)
        {
            disconnectReason = "The client connection ended because incoming data could not be read.";
            disconnectException = exception;
            LogClientReadFailure(connection.Id, exception);
        }
        finally
        {
            if (_clients.TryRemove(connection.Id, out _))
            {
                connection.Abort(disconnectReason);
                var eventArgs = new ClientDisconnectedEventArgs(
                    connection.Id,
                    connection.CloseReason,
                    disconnectException);
                InvokeEventHandler(ClientDisconnected, eventArgs);
            }

            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task SendToConnectionAsync(
        ClientConnection connection,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            LogSendFailure(connection.Id, exception);
        }
        catch (Exception exception)
            when (exception is IOException or SocketException or ObjectDisposedException)
        {
            connection.Abort("The client connection failed while broadcasting.");
            LogSendFailure(connection.Id, exception);
        }
    }

    private void RequestStop()
    {
        TcpListener? listener;
        CancellationTokenSource? serverCancellation;
        lock (_serverSync)
        {
            listener = _listener;
            serverCancellation = _serverCancellation;
            Volatile.Write(ref _isListening, 0);
        }

        TryCancel(serverCancellation);
        listener?.Stop();

        foreach (ClientConnection connection in _clients.Values)
        {
            connection.Abort("The server is stopping.");
        }
    }

    private void ClearServerResources()
    {
        TcpListener? listener;
        CancellationTokenSource? serverCancellation;
        lock (_serverSync)
        {
            listener = _listener;
            serverCancellation = _serverCancellation;
            _listener = null;
            _serverCancellation = null;
            _acceptTask = null;
            Volatile.Write(ref _isListening, 0);
        }

        listener?.Stop();
        TryCancel(serverCancellation);
        serverCancellation?.Dispose();
        _clients.Clear();
    }

    private void InvokeEventHandler<TEventArgs>(
        EventHandler<TEventArgs>? eventHandler,
        TEventArgs eventArgs)
        where TEventArgs : EventArgs
    {
        if (eventHandler is null)
        {
            return;
        }

        foreach (Delegate subscriber in eventHandler.GetInvocationList())
        {
            try
            {
                ((EventHandler<TEventArgs>)subscriber).Invoke(this, eventArgs);
            }
            catch (Exception exception)
            {
                LogEventHandlerFailure(exception);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);
    }

    private static async Task<IPAddress> ResolveAddressAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out IPAddress? address))
        {
            return address;
        }

        IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        return addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault()
            ?? throw new SocketException((int)SocketError.HostNotFound);
    }

    private static void TryCancel(CancellationTokenSource? cancellationTokenSource)
    {
        try
        {
            cancellationTokenSource?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Another shutdown path completed first.
        }
    }

    [LoggerMessage(EventId = 10, Level = LogLevel.Error, Message = "The TCP accept loop stopped unexpectedly.")]
    private partial void LogAcceptFailure(Exception exception);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Reading from client {ClientId} failed.")]
    private partial void LogClientReadFailure(Guid clientId, Exception exception);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "Sending to client {ClientId} failed.")]
    private partial void LogSendFailure(Guid clientId, Exception exception);

    [LoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "A server event handler threw an exception.")]
    private partial void LogEventHandlerFailure(Exception exception);

    private sealed class ClientConnection : IAsyncDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly LengthPrefixedUtf8Protocol _protocol;
        private readonly NetworkConfiguration _configuration;
        private readonly CancellationTokenSource _connectionCancellation;
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        private readonly object _closeSync = new();

        private string _closeReason = "The remote endpoint closed the connection.";
        private int _aborted;
        private int _disposeStarted;

        public ClientConnection(
            Guid id,
            TcpClient tcpClient,
            LengthPrefixedUtf8Protocol protocol,
            NetworkConfiguration configuration,
            CancellationToken serverCancellationToken)
        {
            Id = id;
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            _protocol = protocol;
            _configuration = configuration;
            _connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken);
            RemoteEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown";
        }

        public Guid Id { get; }

        public string RemoteEndpoint { get; }

        public CancellationToken CancellationToken => _connectionCancellation.Token;

        public Task? ReceiveTask { get; set; }

        public string CloseReason
        {
            get
            {
                lock (_closeSync)
                {
                    return _closeReason;
                }
            }
        }

        public async ValueTask<string?> ReadAsync()
        {
            using var timeoutCancellation = new CancellationTokenSource(_configuration.ReadTimeout);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _connectionCancellation.Token,
                timeoutCancellation.Token);

            try
            {
                return await _protocol.ReadAsync(_stream, linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
                when (timeoutCancellation.IsCancellationRequested && !_connectionCancellation.IsCancellationRequested)
            {
                throw new TimeoutException("Receiving a client message timed out.", exception);
            }
        }

        public async ValueTask SendAsync(string message, CancellationToken cancellationToken)
        {
            using var timeoutCancellation = new CancellationTokenSource(_configuration.WriteTimeout);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _connectionCancellation.Token,
                timeoutCancellation.Token);

            bool enteredWriteGate = false;
            try
            {
                await _writeGate.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
                enteredWriteGate = true;
                await _protocol.WriteAsync(_stream, message, linkedCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
                when (timeoutCancellation.IsCancellationRequested &&
                      !cancellationToken.IsCancellationRequested &&
                      !_connectionCancellation.IsCancellationRequested)
            {
                if (enteredWriteGate)
                {
                    Abort("The framed write timed out.");
                }

                throw new TimeoutException("Sending the client message timed out.", exception);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (enteredWriteGate)
                {
                    Abort("The framed write was cancelled by its caller.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
            catch (OperationCanceledException)
            {
                if (enteredWriteGate)
                {
                    Abort("The framed write was cancelled.");
                }

                throw;
            }
            finally
            {
                if (enteredWriteGate)
                {
                    _writeGate.Release();
                }
            }
        }

        public void Abort(string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            lock (_closeSync)
            {
                _closeReason = reason;
            }

            if (Interlocked.Exchange(ref _aborted, 1) != 0)
            {
                return;
            }

            TryCancel(_connectionCancellation);
            _tcpClient.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            {
                return;
            }

            Abort("The client connection was disposed.");
            await _writeGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            _writeGate.Release();
            _writeGate.Dispose();
            _connectionCancellation.Dispose();
        }
    }
}
