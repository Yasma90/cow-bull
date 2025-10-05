using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CowBull.Common.Contracts;
using CowBull.Common.Models;
using CowBull.Common.Services;

namespace CowBull.Common.Infrastructure
{
    /// <summary>
    /// Modern async TCP server implementation following best practices
    /// </summary>
    public class AsyncTcpServer : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly NetworkConfiguration _config;
        private readonly ILogger<AsyncTcpServer> _logger;
        private TcpListener _tcpListener;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _listenTask;
        private readonly ConcurrentDictionary<Guid, ConnectedClient> _clients;
        private bool _disposed;
        private bool _isListening;

        public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
        public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;
        public event EventHandler<Infrastructure.MessageReceivedEventArgs>? MessageReceived;

        public bool IsListening => _isListening;
        public int ConnectedClientsCount => _clients.Count;

        public AsyncTcpServer(NetworkConfiguration config, ILogger<AsyncTcpServer> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clients = new ConcurrentDictionary<Guid, ConnectedClient>();

            if (!_config.IsValid())
                throw new ArgumentException("Invalid network configuration", nameof(config));
        }

        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncTcpServer));

            if (_isListening)
            {
                _logger.LogWarning("Server is already listening on {Address}:{Port}", _config.ServerAddress, _config.Port);
                return true;
            }

            try
            {
                var ipAddress = IPAddress.Parse(_config.ServerAddress);
                _tcpListener = new TcpListener(ipAddress, _config.Port);
                _tcpListener.Start();

                _cancellationTokenSource = new CancellationTokenSource();
                _listenTask = ListenForClientsAsync(_cancellationTokenSource.Token);
                _isListening = true;

                _logger.LogInformation("Server started listening on {Address}:{Port}", _config.ServerAddress, _config.Port);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start server on {Address}:{Port}", _config.ServerAddress, _config.Port);
                await CleanupServer();
                return false;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed || !_isListening)
                return;

            _logger.LogInformation("Stopping server");

            try
            {
                // Send disconnect messages to all clients
                await DisconnectAllClientsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disconnecting clients during server stop");
            }
            finally
            {
                await CleanupServer();
                _logger.LogInformation("Server stopped");
            }
        }

        public async Task<bool> SendMessageToClientAsync(Guid clientId, string message, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            if (_clients.TryGetValue(clientId, out var client))
            {
                return await client.SendMessageAsync(message, cancellationToken);
            }

            _logger.LogWarning("Client {ClientId} not found", clientId);
            return false;
        }

        public async Task<bool> SendMessageToClientAsync<T>(Guid clientId, T message, CancellationToken cancellationToken = default) where T : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            try
            {
                var jsonMessage = JsonSerializer.Serialize(message, JsonOptions);
                return await SendMessageToClientAsync(clientId, jsonMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing message of type {Type} for client {ClientId}", typeof(T).Name, clientId);
                return false;
            }
        }

        public async Task BroadcastMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            var tasks = new List<Task>();
            foreach (var client in _clients.Values)
            {
                tasks.Add(client.SendMessageAsync(message, cancellationToken));
            }

            await Task.WhenAll(tasks);
            _logger.LogDebug("Broadcasted message to {ClientCount} clients", _clients.Count);
        }

        public async Task BroadcastMessageAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            try
            {
                var jsonMessage = JsonSerializer.Serialize(message, JsonOptions);
                await BroadcastMessageAsync(jsonMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing broadcast message of type {Type}", typeof(T).Name);
            }
        }

        private async Task ListenForClientsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                        
                        // Handle client connection on a separate task
                        _ = Task.Run(async () => await HandleClientAsync(tcpClient, cancellationToken), cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Error accepting client connection");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Listen operation cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in listen loop");
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            var clientId = Guid.NewGuid();
            var clientEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
            ConnectedClient connectedClient = null;

            try
            {
                _logger.LogInformation("Client {ClientId} connected from {Endpoint}", clientId, clientEndpoint);

                connectedClient = new ConnectedClient(clientId, tcpClient, _config, _logger);
                connectedClient.MessageReceived += (sender, args) => OnMessageReceived(clientId, args.Message);
                connectedClient.Disconnected += (sender, args) => OnClientDisconnected(clientId, args.Reason);

                _clients.TryAdd(clientId, connectedClient);
                OnClientConnected(clientId, clientEndpoint);

                await connectedClient.StartReceivingAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling client {ClientId}", clientId);
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                connectedClient?.Dispose();
                OnClientDisconnected(clientId, "Connection ended");
            }
        }

        private async Task DisconnectAllClientsAsync(CancellationToken cancellationToken)
        {
            var disconnectTasks = new List<Task>();
            
            foreach (var client in _clients.Values)
            {
                disconnectTasks.Add(client.DisconnectAsync(cancellationToken));
            }

            await Task.WhenAll(disconnectTasks);
            _clients.Clear();
        }

        private async Task CleanupServer()
        {
            try
            {
                _isListening = false;
                _cancellationTokenSource?.Cancel();

                if (_listenTask != null)
                {
                    await _listenTask;
                }

                _tcpListener?.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during server cleanup");
            }
            finally
            {
                _tcpListener = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _listenTask = null;
            }
        }

        protected virtual void OnClientConnected(Guid clientId, string endpoint)
        {
            ClientConnected?.Invoke(this, new ClientConnectedEventArgs(clientId, endpoint));
        }

        protected virtual void OnClientDisconnected(Guid clientId, string reason)
        {
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(clientId, reason));
        }

        protected virtual void OnMessageReceived(Guid clientId, string message)
        {
            MessageReceived?.Invoke(this, new Infrastructure.MessageReceivedEventArgs(message) { ClientId = clientId });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                StopAsync().GetAwaiter().GetResult();
                GC.SuppressFinalize(this);
            }
        }
    }

    /// <summary>
    /// Represents a connected client
    /// </summary>
    internal class ConnectedClient : IDisposable
    {
        private readonly Guid _clientId;
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly NetworkConfiguration _config;
        private readonly ILogger _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _receiveTask;
        private bool _disposed;

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler<ClientDisconnectedEventArgs> Disconnected;

        public bool IsConnected => _tcpClient?.Connected == true;

        public ConnectedClient(Guid clientId, TcpClient tcpClient, NetworkConfiguration config, ILogger logger)
        {
            _clientId = clientId;
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _stream = _tcpClient.GetStream();
            _tcpClient.ReceiveTimeout = _config.ReceiveTimeoutMs;
            _tcpClient.SendTimeout = _config.SendTimeoutMs;
        }

        public async Task StartReceivingAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _receiveTask = ReceiveMessagesAsync(_cancellationTokenSource.Token);
            await _receiveTask;
        }

        public async Task<bool> SendMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            if (_disposed || !IsConnected)
                return false;

            try
            {
                var messageWithDelimiter = message + "#";
                var messageBytes = Encoding.UTF8.GetBytes(messageWithDelimiter);

                using var timeoutCts = new CancellationTokenSource(_config.SendTimeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await _stream.WriteAsync(messageBytes, 0, messageBytes.Length, combinedCts.Token);
                await _stream.FlushAsync(combinedCts.Token);

                _logger.LogDebug("Sent message to client {ClientId}: {Message}", _clientId, message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to client {ClientId}", _clientId);
                OnDisconnected($"Send error: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return;

            try
            {
                var disconnectMessage = new SystemMessage
                {
                    ActionType = SystemActionType.Disconnect,
                    Content = "Server disconnecting"
                };

                await SendMessageAsync(JsonSerializer.Serialize(disconnectMessage), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending disconnect message to client {ClientId}", _clientId);
            }
            finally
            {
                Dispose();
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[_config.BufferSize];
            var messageBuilder = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        
                        if (bytesRead == 0)
                        {
                            _logger.LogInformation("Client {ClientId} disconnected", _clientId);
                            break;
                        }

                        var receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(receivedData);

                        ProcessReceivedData(messageBuilder);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger.LogError(ex, "Error receiving data from client {ClientId}", _clientId);
                        OnDisconnected($"Receive error: {ex.Message}");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Receive operation cancelled for client {ClientId}", _clientId);
            }
        }

        private void ProcessReceivedData(StringBuilder messageBuilder)
        {
            var data = messageBuilder.ToString();
            var delimiterIndex = data.IndexOf('#');

            while (delimiterIndex >= 0)
            {
                var completeMessage = data.Substring(0, delimiterIndex);
                
                if (!string.IsNullOrEmpty(completeMessage))
                {
                    _logger.LogDebug("Received message from client {ClientId}: {Message}", _clientId, completeMessage);
                    OnMessageReceived(completeMessage);
                }

                data = data.Substring(delimiterIndex + 1);
                messageBuilder.Clear();
                messageBuilder.Append(data);

                delimiterIndex = data.IndexOf('#');
            }
        }

        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
        }

        protected virtual void OnDisconnected(string reason)
        {
            Disconnected?.Invoke(this, new ClientDisconnectedEventArgs(_clientId, reason));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _cancellationTokenSource?.Cancel();
                _stream?.Close();
                _tcpClient?.Close();
                _stream?.Dispose();
                _tcpClient?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
        }
    }

    /// <summary>
    /// Event arguments for client connected events
    /// </summary>
    public class ClientConnectedEventArgs : EventArgs
    {
        public Guid ClientId { get; }
        public string Endpoint { get; }
        public DateTime Timestamp { get; }

        public ClientConnectedEventArgs(Guid clientId, string endpoint)
        {
            ClientId = clientId;
            Endpoint = endpoint ?? string.Empty;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event arguments for client disconnected events
    /// </summary>
    public class ClientDisconnectedEventArgs : EventArgs
    {
        public Guid ClientId { get; }
        public string Reason { get; }
        public DateTime Timestamp { get; }

        public ClientDisconnectedEventArgs(Guid clientId, string reason)
        {
            ClientId = clientId;
            Reason = reason ?? string.Empty;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Extended message received event args with client information
    /// </summary>
    public class MessageReceivedEventArgs : Common.Contracts.MessageReceivedEventArgs
    {
        public Guid ClientId { get; set; }

        public MessageReceivedEventArgs(string message) : base(message)
        {
        }
    }
}