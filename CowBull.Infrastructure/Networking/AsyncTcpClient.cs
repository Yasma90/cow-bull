using System.Net.Sockets;
using CowBull.Infrastructure.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CowBull.Infrastructure.Networking;

/// <summary>
/// Cancellation-aware TCP client for length-prefixed UTF-8 messages.
/// </summary>
public sealed partial class AsyncTcpClient : IAsyncDisposable
{
    private readonly NetworkConfiguration _configuration;
    private readonly LengthPrefixedUtf8Protocol _protocol;
    private readonly ILogger<AsyncTcpClient> _logger;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _connectionSync = new();

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _connectionCancellation;
    private Task? _receiveTask;
    private int _state = (int)TcpConnectionState.Disconnected;
    private int _disposeStarted;

    public AsyncTcpClient(
        NetworkConfiguration configuration,
        ILogger<AsyncTcpClient>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _protocol = new LengthPrefixedUtf8Protocol(configuration.MaximumPayloadBytes);
        _logger = logger ?? NullLogger<AsyncTcpClient>.Instance;
    }

    /// <summary>
    /// Raised synchronously on the receive loop. Handlers must return promptly.
    /// Handler exceptions are isolated from the transport.
    /// </summary>
    public event EventHandler<NetworkMessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Raised synchronously by the operation that changes state. Handlers must return promptly.
    /// Handler exceptions are isolated from the transport.
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public TcpConnectionState State => (TcpConnectionState)Volatile.Read(ref _state);

    public bool IsConnected => State == TcpConnectionState.Connected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_configuration.Port == 0)
        {
            throw new InvalidOperationException("A client cannot connect to port zero.");
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (IsConnected)
            {
                return;
            }

            if (State != TcpConnectionState.Disconnected)
            {
                throw new InvalidOperationException($"The client cannot connect while it is {State}.");
            }

            ChangeState(TcpConnectionState.Connecting, "Connecting.");

            var tcpClient = new TcpClient { NoDelay = true };
            var connectionCancellation = new CancellationTokenSource();
            lock (_connectionSync)
            {
                _tcpClient = tcpClient;
                _connectionCancellation = connectionCancellation;
            }

            using var timeoutCancellation = new CancellationTokenSource(_configuration.ConnectTimeout);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token,
                connectionCancellation.Token);

            try
            {
                await tcpClient.ConnectAsync(
                    _configuration.Host,
                    _configuration.Port,
                    linkedCancellation.Token).ConfigureAwait(false);
                connectionCancellation.Token.ThrowIfCancellationRequested();

                NetworkStream stream = tcpClient.GetStream();
                lock (_connectionSync)
                {
                    if (!ReferenceEquals(_tcpClient, tcpClient))
                    {
                        throw new OperationCanceledException("The connection was stopped.", connectionCancellation.Token);
                    }

                    _stream = stream;
                }

                ChangeState(TcpConnectionState.Connected, "Connected.");
                Task receiveTask = Task.Run(
                    () => ReceiveLoopAsync(tcpClient, stream, connectionCancellation),
                    CancellationToken.None);
                lock (_connectionSync)
                {
                    if (ReferenceEquals(_tcpClient, tcpClient))
                    {
                        _receiveTask = receiveTask;
                    }
                }
            }
            catch (OperationCanceledException exception)
                when (timeoutCancellation.IsCancellationRequested &&
                      !cancellationToken.IsCancellationRequested &&
                      !connectionCancellation.IsCancellationRequested)
            {
                ClearConnection(tcpClient, connectionCancellation);
                var timeoutException = new TimeoutException(
                    $"Connecting to {_configuration.Host}:{_configuration.Port} timed out.",
                    exception);
                ChangeState(TcpConnectionState.Disconnected, timeoutException.Message, timeoutException);
                throw timeoutException;
            }
            catch (Exception exception)
            {
                ClearConnection(tcpClient, connectionCancellation);
                ChangeState(TcpConnectionState.Disconnected, "Connection failed.", exception);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public ValueTask SendAsync(ProtocolMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return SendAsync(ProtocolSerializer.Serialize(message), cancellationToken);
    }

    public async ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);

        NetworkStream stream;
        CancellationToken connectionToken;
        lock (_connectionSync)
        {
            if (State != TcpConnectionState.Connected || _stream is null || _connectionCancellation is null)
            {
                throw new InvalidOperationException("The client is not connected.");
            }

            stream = _stream;
            connectionToken = _connectionCancellation.Token;
        }

        using var timeoutCancellation = new CancellationTokenSource(_configuration.WriteTimeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            connectionToken,
            timeoutCancellation.Token);

        bool enteredWriteGate = false;
        try
        {
            await _writeGate.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
            enteredWriteGate = true;

            lock (_connectionSync)
            {
                if (!ReferenceEquals(stream, _stream) || State != TcpConnectionState.Connected)
                {
                    throw new InvalidOperationException("The connection ended before the message could be sent.");
                }
            }

            await _protocol.WriteAsync(stream, message, linkedCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (timeoutCancellation.IsCancellationRequested &&
                  !cancellationToken.IsCancellationRequested &&
                  !connectionToken.IsCancellationRequested)
        {
            if (enteredWriteGate)
            {
                AbortConnection();
            }

            throw new TimeoutException("Sending the message timed out.", exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (enteredWriteGate)
            {
                AbortConnection();
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (OperationCanceledException)
        {
            if (enteredWriteGate)
            {
                AbortConnection();
            }

            throw;
        }
        catch (Exception exception) when (exception is IOException or SocketException)
        {
            AbortConnection();
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

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return DisconnectCoreAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
        ChangeState(TcpConnectionState.Disposed, "Disposed.");
        GC.SuppressFinalize(this);
    }

    private async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TcpConnectionState state = State;
        if (state is TcpConnectionState.Disconnected or TcpConnectionState.Disposed)
        {
            return;
        }

        ChangeState(TcpConnectionState.Disconnecting, "Disconnecting.");
        AbortConnection();

        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            Task? receiveTask;
            lock (_connectionSync)
            {
                receiveTask = _receiveTask;
            }

            if (receiveTask is not null)
            {
                await receiveTask.ConfigureAwait(false);
            }

            ClearCurrentConnection();
            ChangeState(TcpConnectionState.Disconnected, "Disconnected.");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task ReceiveLoopAsync(
        TcpClient tcpClient,
        NetworkStream stream,
        CancellationTokenSource connectionCancellation)
    {
        string disconnectReason = "The remote endpoint closed the connection.";
        Exception? disconnectException = null;

        try
        {
            while (!connectionCancellation.IsCancellationRequested)
            {
                using var timeoutCancellation = new CancellationTokenSource(_configuration.ReadTimeout);
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    connectionCancellation.Token,
                    timeoutCancellation.Token);

                string? message;
                try
                {
                    message = await _protocol.ReadAsync(stream, linkedCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception)
                    when (timeoutCancellation.IsCancellationRequested && !connectionCancellation.IsCancellationRequested)
                {
                    disconnectReason = "Receiving a message timed out.";
                    disconnectException = new TimeoutException(disconnectReason, exception);
                    break;
                }

                if (message is null)
                {
                    break;
                }

                InvokeEventHandler(MessageReceived, new NetworkMessageReceivedEventArgs(message));
            }
        }
        catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
        {
            disconnectReason = "Disconnected locally.";
        }
        catch (Exception exception) when (exception is IOException or SocketException or FrameProtocolException)
        {
            disconnectReason = "The connection ended because incoming data could not be read.";
            disconnectException = exception;
            LogReceiveFailure(exception);
        }
        finally
        {
            ClearConnection(tcpClient, connectionCancellation);
            if (State is TcpConnectionState.Connected or TcpConnectionState.Connecting)
            {
                ChangeState(TcpConnectionState.Disconnected, disconnectReason, disconnectException);
            }
        }
    }

    private void AbortConnection()
    {
        lock (_connectionSync)
        {
            TryCancel(_connectionCancellation);
            _tcpClient?.Dispose();
        }
    }

    private void ClearConnection(TcpClient tcpClient, CancellationTokenSource connectionCancellation)
    {
        bool ownsConnection;
        lock (_connectionSync)
        {
            ownsConnection = ReferenceEquals(_tcpClient, tcpClient);
            if (ownsConnection)
            {
                _tcpClient = null;
                _stream = null;
                _connectionCancellation = null;
                _receiveTask = null;
            }
        }

        TryCancel(connectionCancellation);
        tcpClient.Dispose();
        if (ownsConnection)
        {
            connectionCancellation.Dispose();
        }
    }

    private void ClearCurrentConnection()
    {
        TcpClient? tcpClient;
        CancellationTokenSource? connectionCancellation;
        lock (_connectionSync)
        {
            tcpClient = _tcpClient;
            connectionCancellation = _connectionCancellation;
            _tcpClient = null;
            _stream = null;
            _connectionCancellation = null;
            _receiveTask = null;
        }

        TryCancel(connectionCancellation);
        tcpClient?.Dispose();
        connectionCancellation?.Dispose();
    }

    private void ChangeState(TcpConnectionState newState, string reason, Exception? exception = null)
    {
        var previousState = (TcpConnectionState)Interlocked.Exchange(ref _state, (int)newState);
        if (previousState == newState)
        {
            return;
        }

        var eventArgs = new ConnectionStateChangedEventArgs(previousState, newState, reason, exception);
        InvokeEventHandler(ConnectionStateChanged, eventArgs);
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "The receive loop stopped with an error.")]
    private partial void LogReceiveFailure(Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "A network event handler threw an exception.")]
    private partial void LogEventHandlerFailure(Exception exception);
}
