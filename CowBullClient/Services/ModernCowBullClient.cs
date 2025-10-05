using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CowBull.Common.Contracts;
using CowBull.Common.Infrastructure;
using CowBull.Common.Models;
using CowBull.Common.Services;

namespace CowBullClient.Services
{
    /// <summary>
    /// Modern client implementation using the new communication layer
    /// </summary>
    public class ModernCowBullClient : IDisposable
    {
        private readonly INetworkCommunication _networkClient;
        private readonly ILogger<ModernCowBullClient> _logger;
        private readonly object _lockObject = new object();
        private bool _disposed;
        private Guid? _currentGameSession;

        public event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;
        public event EventHandler<GameResponseEventArgs> GameResponseReceived;
        public event EventHandler<SystemMessageEventArgs> SystemMessageReceived;
        public event EventHandler<ErrorMessageEventArgs> ErrorMessageReceived;

        public bool IsConnected => _networkClient?.IsConnected == true;
        public Guid? CurrentGameSession => _currentGameSession;

        public ModernCowBullClient(
            NetworkConfiguration networkConfig,
            ILogger<ModernCowBullClient> logger)
        {
            if (networkConfig == null)
                throw new ArgumentNullException(nameof(networkConfig));

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _networkClient = new AsyncTcpClient(networkConfig, CreateLogger<AsyncTcpClient>());

            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            _networkClient.ConnectionStatusChanged += OnConnectionStatusChanged;
            _networkClient.MessageReceived += OnMessageReceived;
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ModernCowBullClient));

            _logger.LogInformation("Connecting to server");
            return await _networkClient.ConnectAsync(cancellationToken);
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return;

            _logger.LogInformation("Disconnecting from server");
            
            // End current game session if any
            if (_currentGameSession.HasValue)
            {
                await EndCurrentGameAsync(cancellationToken);
            }

            await _networkClient.DisconnectAsync(cancellationToken);
        }

        public async Task<bool> StartNewGameAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to server");

            try
            {
                _logger.LogInformation("Starting new game");

                var newGameMessage = new GameMessage
                {
                    ActionType = GameActionType.NewGame,
                    Context = "Request new game"
                };

                var result = await _networkClient.SendMessageAsync(newGameMessage, cancellationToken);
                
                if (result)
                {
                    lock (_lockObject)
                    {
                        _currentGameSession = Guid.NewGuid(); // Will be updated when server responds
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting new game");
                return false;
            }
        }

        public async Task<bool> SendGuessAsync(string guess, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to server");

            if (string.IsNullOrWhiteSpace(guess))
                throw new ArgumentException("Guess cannot be null or empty", nameof(guess));

            try
            {
                _logger.LogDebug("Sending guess: {Guess}", guess);

                var guessMessage = new GameMessage
                {
                    ActionType = GameActionType.Guess,
                    Number = guess,
                    Context = "Player guess"
                };

                // Also send legacy format for backward compatibility
                var legacyResult = await _networkClient.SendMessageAsync(guess, cancellationToken);
                var structuredResult = await _networkClient.SendMessageAsync(guessMessage, cancellationToken);

                return legacyResult || structuredResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending guess {Guess}", guess);
                return false;
            }
        }

        public async Task<bool> RequestGeneratedNumberAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to server");

            try
            {
                _logger.LogDebug("Requesting generated number");

                var requestMessage = new GameMessage
                {
                    ActionType = GameActionType.GenerateNumber,
                    Context = "Request generated number"
                };

                return await _networkClient.SendMessageAsync(requestMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting generated number");
                return false;
            }
        }

        public async Task<bool> SendHeartbeatAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                return false;

            try
            {
                var heartbeatMessage = new SystemMessage
                {
                    ActionType = SystemActionType.Heartbeat,
                    Content = "Client heartbeat"
                };

                return await _networkClient.SendMessageAsync(heartbeatMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending heartbeat");
                return false;
            }
        }

        private async Task EndCurrentGameAsync(CancellationToken cancellationToken)
        {
            if (!_currentGameSession.HasValue)
                return;

            try
            {
                var endGameMessage = new SystemMessage
                {
                    ActionType = SystemActionType.Disconnect,
                    Content = "Ending current game session"
                };

                await _networkClient.SendMessageAsync(endGameMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error ending current game session");
            }
            finally
            {
                lock (_lockObject)
                {
                    _currentGameSession = null;
                }
            }
        }

        private void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            _logger.LogInformation("Connection status changed: {IsConnected} - {Reason}", 
                e.IsConnected, e.Reason);

            if (!e.IsConnected)
            {
                lock (_lockObject)
                {
                    _currentGameSession = null;
                }
            }

            ConnectionStatusChanged?.Invoke(this, e);
        }

        private void OnMessageReceived(object sender, Common.Contracts.MessageReceivedEventArgs e)
        {
            try
            {
                _logger.LogDebug("Received message: {Message}", e.Message);

                // Try to parse as structured message first
                if (TryParseStructuredMessage(e.Message, out var structuredMessage))
                {
                    HandleStructuredMessage(structuredMessage);
                }
                else
                {
                    // Handle as legacy string message
                    HandleLegacyMessage(e.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing received message");
            }
        }

        private bool TryParseStructuredMessage(string message, out MessageBase parsedMessage)
        {
            parsedMessage = null;

            try
            {
                var jsonDoc = JsonDocument.Parse(message);
                if (!jsonDoc.RootElement.TryGetProperty("messageType", out var messageTypeElement))
                {
                    return false;
                }

                var messageType = messageTypeElement.GetString();

                parsedMessage = messageType switch
                {
                    "Game" => JsonSerializer.Deserialize<GameMessage>(message),
                    "System" => JsonSerializer.Deserialize<SystemMessage>(message),
                    "Error" => JsonSerializer.Deserialize<ErrorMessage>(message),
                    _ => null
                };

                return parsedMessage != null;
            }
            catch
            {
                return false;
            }
        }

        private void HandleStructuredMessage(MessageBase message)
        {
            switch (message)
            {
                case GameMessage gameMessage:
                    HandleGameMessage(gameMessage);
                    break;

                case SystemMessage systemMessage:
                    HandleSystemMessage(systemMessage);
                    break;

                case ErrorMessage errorMessage:
                    HandleErrorMessage(errorMessage);
                    break;

                default:
                    _logger.LogWarning("Unhandled message type: {MessageType}", message.GetType().Name);
                    break;
            }
        }

        private void HandleGameMessage(GameMessage gameMessage)
        {
            _logger.LogDebug("Received game message: {ActionType}", gameMessage.ActionType);

            var eventArgs = new GameResponseEventArgs
            {
                ActionType = gameMessage.ActionType,
                Number = gameMessage.Number,
                Bulls = gameMessage.Bulls,
                Cows = gameMessage.Cows,
                Context = gameMessage.Context,
                Timestamp = gameMessage.Timestamp
            };

            GameResponseReceived?.Invoke(this, eventArgs);
        }

        private void HandleSystemMessage(SystemMessage systemMessage)
        {
            _logger.LogDebug("Received system message: {ActionType}", systemMessage.ActionType);

            var eventArgs = new SystemMessageEventArgs
            {
                ActionType = systemMessage.ActionType,
                Content = systemMessage.Content,
                Data = systemMessage.Data,
                Timestamp = systemMessage.Timestamp
            };

            SystemMessageReceived?.Invoke(this, eventArgs);
        }

        private void HandleErrorMessage(ErrorMessage errorMessage)
        {
            _logger.LogWarning("Received error message: {ErrorCode} - {ErrorDescription}", 
                errorMessage.ErrorCode, errorMessage.ErrorDescription);

            var eventArgs = new ErrorMessageEventArgs
            {
                ErrorCode = errorMessage.ErrorCode,
                ErrorDescription = errorMessage.ErrorDescription,
                Details = errorMessage.Details,
                Timestamp = errorMessage.Timestamp
            };

            ErrorMessageReceived?.Invoke(this, eventArgs);
        }

        private void HandleLegacyMessage(string message)
        {
            // Handle legacy string-based messages for backward compatibility
            if (string.IsNullOrWhiteSpace(message))
                return;

            // Try to parse as "number bulls cows" format
            var parts = message.Split(' ');
            if (parts.Length == 3 && 
                int.TryParse(parts[1], out var bulls) && 
                int.TryParse(parts[2], out var cows))
            {
                var eventArgs = new GameResponseEventArgs
                {
                    ActionType = GameActionType.Response,
                    Number = parts[0],
                    Bulls = bulls,
                    Cows = cows,
                    Context = "Legacy response",
                    Timestamp = DateTime.UtcNow
                };

                GameResponseReceived?.Invoke(this, eventArgs);
            }
            else
            {
                // Treat as generic system message
                var eventArgs = new SystemMessageEventArgs
                {
                    ActionType = SystemActionType.Configuration,
                    Content = message,
                    Timestamp = DateTime.UtcNow
                };

                SystemMessageReceived?.Invoke(this, eventArgs);
            }
        }

        // Helper method to create logger for dependencies
        private ILogger<T> CreateLogger<T>()
        {
            // In a real application, this would come from DI container
            return new DelegatingLogger<T>(_logger);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                DisconnectAsync().GetAwaiter().GetResult();
                _networkClient?.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }

    /// <summary>
    /// Event arguments for game responses
    /// </summary>
    public class GameResponseEventArgs : EventArgs
    {
        public GameActionType ActionType { get; set; }
        public string Number { get; set; }
        public int Bulls { get; set; }
        public int Cows { get; set; }
        public string Context { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Event arguments for system messages
    /// </summary>
    public class SystemMessageEventArgs : EventArgs
    {
        public SystemActionType ActionType { get; set; }
        public string Content { get; set; }
        public string Data { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Event arguments for error messages
    /// </summary>
    public class ErrorMessageEventArgs : EventArgs
    {
        public string ErrorCode { get; set; }
        public string ErrorDescription { get; set; }
        public string Details { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Simple logger adapter for demonstration purposes
    /// In production, use proper DI container
    /// </summary>
    internal class DelegatingLogger<T> : ILogger<T>
    {
        private readonly ILogger _baseLogger;

        public DelegatingLogger(ILogger baseLogger)
        {
            _baseLogger = baseLogger;
        }

        public IDisposable BeginScope<TState>(TState state) => _baseLogger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => _baseLogger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => _baseLogger.Log(logLevel, eventId, state, exception, formatter);
    }
}