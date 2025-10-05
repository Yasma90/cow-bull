using System;
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
    /// Modern async TCP client implementation following best practices
    /// </summary>
    public class AsyncTcpClient : INetworkCommunication
    {
        private readonly NetworkConfiguration _config;
        private readonly ILogger<AsyncTcpClient> _logger;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _receiveTask;
        private readonly object _lockObject = new object();
        private bool _disposed;

        public event EventHandler<Common.Contracts.MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

        public bool IsConnected => _tcpClient?.Connected == true;

        public AsyncTcpClient(NetworkConfiguration config, ILogger<AsyncTcpClient> logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!_config.IsValid())
                throw new ArgumentException("Invalid network configuration", nameof(config));
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncTcpClient));

            if (IsConnected)
            {
                _logger.LogWarning("Already connected to {Address}:{Port}", _config.ServerAddress, _config.Port);
                return true;
            }

            try
            {
                _logger.LogInformation("Connecting to {Address}:{Port}", _config.ServerAddress, _config.Port);

                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = _config.ReceiveTimeoutMs;
                _tcpClient.SendTimeout = _config.SendTimeoutMs;

                using var timeoutCts = new CancellationTokenSource(_config.ConnectionTimeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await _tcpClient.ConnectAsync(_config.ServerAddress, _config.Port);

                if (_tcpClient.Connected)
                {
                    _stream = _tcpClient.GetStream();
                    _cancellationTokenSource = new CancellationTokenSource();
                    
                    // Start receiving messages
                    _receiveTask = ReceiveMessagesAsync(_cancellationTokenSource.Token);

                    _logger.LogInformation("Successfully connected to {Address}:{Port}", _config.ServerAddress, _config.Port);
                    OnConnectionStatusChanged(true, "Connected successfully");
                    
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to connect to {Address}:{Port}", _config.ServerAddress, _config.Port);
                    OnConnectionStatusChanged(false, "Connection failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to {Address}:{Port}", _config.ServerAddress, _config.Port);
                OnConnectionStatusChanged(false, $"Connection error: {ex.Message}");
                await CleanupConnection();
                return false;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return;

            _logger.LogInformation("Disconnecting from server");

            try
            {
                // Send disconnect message if connected
                if (IsConnected)
                {
                    var disconnectMessage = new SystemMessage
                    {
                        ActionType = SystemActionType.Disconnect,
                        Content = "Client disconnecting"
                    };

                    await SendMessageAsync(disconnectMessage, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending disconnect message");
            }
            finally
            {
                await CleanupConnection();
                OnConnectionStatusChanged(false, "Disconnected");
                _logger.LogInformation("Disconnected from server");
            }
        }

        public async Task<bool> SendMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty", nameof(message));

            return await SendRawMessageAsync(message, cancellationToken);
        }

        public async Task<bool> SendMessageAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            try
            {
                var jsonMessage = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                return await SendRawMessageAsync(jsonMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing message of type {Type}", typeof(T).Name);
                return false;
            }
        }

        private async Task<bool> SendRawMessageAsync(string message, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncTcpClient));

            if (!IsConnected)
            {
                _logger.LogWarning("Cannot send message: not connected");
                return false;
            }

            try
            {
                // Add message delimiter
                var messageWithDelimiter = message + "#";
                var messageBytes = Encoding.UTF8.GetBytes(messageWithDelimiter);

                if (messageBytes.Length > _config.MaxMessageSize)
                {
                    _logger.LogError("Message size {Size} exceeds maximum allowed size {MaxSize}", 
                        messageBytes.Length, _config.MaxMessageSize);
                    return false;
                }

                using var timeoutCts = new CancellationTokenSource(_config.SendTimeoutMs);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await _stream.WriteAsync(messageBytes, 0, messageBytes.Length, combinedCts.Token);
                await _stream.FlushAsync(combinedCts.Token);

                _logger.LogDebug("Sent message: {Message}", message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                await HandleConnectionError(ex);
                return false;
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
                            _logger.LogWarning("Server closed the connection");
                            break;
                        }

                        var receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(receivedData);

                        // Process complete messages (delimited by #)
                        await ProcessReceivedData(messageBuilder);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        _logger.LogError(ex, "Error receiving data");
                        await HandleConnectionError(ex);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Receive operation cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in receive loop");
                await HandleConnectionError(ex);
            }
        }

        private async Task ProcessReceivedData(StringBuilder messageBuilder)
        {
            var data = messageBuilder.ToString();
            var delimiterIndex = data.IndexOf('#');

            while (delimiterIndex >= 0)
            {
                var completeMessage = data.Substring(0, delimiterIndex);
                
                if (!string.IsNullOrEmpty(completeMessage))
                {
                    _logger.LogDebug("Received message: {Message}", completeMessage);
                    OnMessageReceived(completeMessage);
                }

                // Remove processed message from buffer
                data = data.Substring(delimiterIndex + 1);
                messageBuilder.Clear();
                messageBuilder.Append(data);

                delimiterIndex = data.IndexOf('#');
            }
        }

        private async Task HandleConnectionError(Exception ex)
        {
            _logger.LogError(ex, "Connection error occurred");
            OnConnectionStatusChanged(false, $"Connection error: {ex.Message}");
            await CleanupConnection();
        }

        private async Task CleanupConnection()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                
                if (_receiveTask != null)
                {
                    await _receiveTask;
                }

                _stream?.Close();
                _tcpClient?.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during connection cleanup");
            }
            finally
            {
                _stream?.Dispose();
                _tcpClient?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                _stream = null;
                _tcpClient = null;
                _cancellationTokenSource = null;
                _receiveTask = null;
            }
        }

        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, new Common.Contracts.MessageReceivedEventArgs(message));
        }

        protected virtual void OnConnectionStatusChanged(bool isConnected, string reason)
        {
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(isConnected, reason));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                DisconnectAsync().GetAwaiter().GetResult();
                GC.SuppressFinalize(this);
            }
        }
    }
}