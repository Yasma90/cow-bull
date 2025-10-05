using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CowBull.Common.Infrastructure;
using CowBull.Common.Models;
using CowBull.Common.Services;
using CowBull.Common.Configuration;

namespace CowBullServer.Services
{
    /// <summary>
    /// Modern server implementation using the new communication layer
    /// </summary>
    public class ModernCowBullServer : IDisposable
    {
        private readonly AsyncTcpServer _tcpServer;
        private readonly IGameService _gameService;
        private readonly ILogger<ModernCowBullServer> _logger;
        private readonly Dictionary<Guid, Guid> _clientSessionMap; // clientId -> gameSessionId
        private readonly object _lockObject = new object();
        private bool _disposed;

        public event EventHandler<ServerStatusChangedEventArgs> StatusChanged;

        public bool IsRunning => _tcpServer?.IsListening == true;
        public int ConnectedClients => _tcpServer?.ConnectedClientsCount ?? 0;

        public ModernCowBullServer(
            NetworkConfiguration networkConfig,
            IGameService gameService,
            ILogger<ModernCowBullServer> logger)
        {
            if (networkConfig == null)
                throw new ArgumentNullException(nameof(networkConfig));

            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _tcpServer = new AsyncTcpServer(networkConfig, CreateLogger<AsyncTcpServer>());
            _clientSessionMap = new Dictionary<Guid, Guid>();

            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            _tcpServer.ClientConnected += OnClientConnected;
            _tcpServer.ClientDisconnected += OnClientDisconnected;
            _tcpServer.MessageReceived += OnMessageReceived;
        }

        public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ModernCowBullServer));

            _logger.LogInformation("Starting CowBull server");

            var result = await _tcpServer.StartAsync(cancellationToken);
            
            if (result)
            {
                OnStatusChanged(true, "Server started successfully");
                _logger.LogInformation("CowBull server started successfully");
            }
            else
            {
                OnStatusChanged(false, "Failed to start server");
                _logger.LogError("Failed to start CowBull server");
            }

            return result;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed || !IsRunning)
                return;

            _logger.LogInformation("Stopping CowBull server");

            // End all active game sessions
            lock (_lockObject)
            {
                foreach (var sessionId in _clientSessionMap.Values)
                {
                    _ = Task.Run(async () => await _gameService.EndGameAsync(sessionId));
                }
                _clientSessionMap.Clear();
            }

            await _tcpServer.StopAsync(cancellationToken);
            OnStatusChanged(false, "Server stopped");
            _logger.LogInformation("CowBull server stopped");
        }

        private async void OnClientConnected(object sender, ClientConnectedEventArgs e)
        {
            _logger.LogInformation("Client {ClientId} connected from {Endpoint}", e.ClientId, e.Endpoint);

            // Send welcome message
            var welcomeMessage = new SystemMessage
            {
                ActionType = SystemActionType.Connect,
                Content = "Welcome to CowBull Server!",
                Data = JsonSerializer.Serialize(new { serverId = Guid.NewGuid(), timestamp = DateTime.UtcNow })
            };

            await _tcpServer.SendMessageToClientAsync(e.ClientId, welcomeMessage);
        }

        private async void OnClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            _logger.LogInformation("Client {ClientId} disconnected: {Reason}", e.ClientId, e.Reason);

            // End the client's game session
            lock (_lockObject)
            {
                if (_clientSessionMap.TryGetValue(e.ClientId, out var sessionId))
                {
                    _clientSessionMap.Remove(e.ClientId);
                    _ = Task.Run(async () => await _gameService.EndGameAsync(sessionId));
                    _logger.LogInformation("Ended game session {SessionId} for disconnected client {ClientId}", 
                        sessionId, e.ClientId);
                }
            }
        }

        private async void OnMessageReceived(object sender, Infrastructure.MessageReceivedEventArgs e)
        {
            try
            {
                _logger.LogDebug("Received message from client {ClientId}: {Message}", e.ClientId, e.Message);

                // Try to parse as structured message first
                if (TryParseStructuredMessage(e.Message, out var structuredMessage))
                {
                    await HandleStructuredMessage(e.ClientId, structuredMessage);
                }
                else
                {
                    // Handle as legacy string message (for backward compatibility)
                    await HandleLegacyMessage(e.ClientId, e.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from client {ClientId}", e.ClientId);
                
                var errorMessage = new ErrorMessage
                {
                    ErrorCode = "MESSAGE_PROCESSING_ERROR",
                    ErrorDescription = "Failed to process your message",
                    Details = ex.Message
                };

                await _tcpServer.SendMessageToClientAsync(e.ClientId, errorMessage);
            }
        }

        private bool TryParseStructuredMessage(string message, out MessageBase parsedMessage)
        {
            parsedMessage = null;

            try
            {
                var jsonDoc = JsonDocument.Parse(message);
                var messageType = jsonDoc.RootElement.GetProperty("messageType").GetString();

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

        private async Task HandleStructuredMessage(Guid clientId, MessageBase message)
        {
            switch (message)
            {
                case GameMessage gameMessage:
                    await HandleGameMessage(clientId, gameMessage);
                    break;

                case SystemMessage systemMessage:
                    await HandleSystemMessage(clientId, systemMessage);
                    break;

                default:
                    _logger.LogWarning("Unhandled message type: {MessageType}", message.GetType().Name);
                    break;
            }
        }

        private async Task HandleGameMessage(Guid clientId, GameMessage gameMessage)
        {
            switch (gameMessage.ActionType)
            {
                case GameActionType.NewGame:
                    await StartNewGame(clientId, gameMessage);
                    break;

                case GameActionType.Guess:
                    await ProcessGuess(clientId, gameMessage);
                    break;

                case GameActionType.GenerateNumber:
                    await SendGeneratedNumber(clientId);
                    break;

                default:
                    _logger.LogWarning("Unhandled game action: {ActionType}", gameMessage.ActionType);
                    break;
            }
        }

        private async Task HandleSystemMessage(Guid clientId, SystemMessage systemMessage)
        {
            switch (systemMessage.ActionType)
            {
                case SystemActionType.Disconnect:
                    _logger.LogInformation("Client {ClientId} requested disconnect", clientId);
                    // Client will handle disconnection
                    break;

                case SystemActionType.Heartbeat:
                    // Respond to heartbeat
                    var heartbeatResponse = new SystemMessage
                    {
                        ActionType = SystemActionType.Heartbeat,
                        Content = "Heartbeat acknowledged"
                    };
                    await _tcpServer.SendMessageToClientAsync(clientId, heartbeatResponse);
                    break;

                default:
                    _logger.LogDebug("Received system message: {ActionType}", systemMessage.ActionType);
                    break;
            }
        }

        private async Task HandleLegacyMessage(Guid clientId, string message)
        {
            // Handle legacy string-based messages for backward compatibility
            if (string.IsNullOrWhiteSpace(message))
                return;

            // Assume it's a guess if it's all digits
            if (message.All(char.IsDigit))
            {
                await ProcessLegacyGuess(clientId, message);
            }
            else
            {
                _logger.LogWarning("Unrecognized legacy message format from client {ClientId}: {Message}", 
                    clientId, message);
            }
        }

        private async Task StartNewGame(Guid clientId, GameMessage gameMessage)
        {
            try
            {
                var gameConfig = new GameConfiguration
                {
                    NumberLength = 4, // Default for CowBull
                    MaxAttempts = 10,
                    AllowDuplicateDigits = false
                };

                var session = await _gameService.CreateGameAsync(gameConfig);

                lock (_lockObject)
                {
                    _clientSessionMap[clientId] = session.SessionId;
                }

                var response = new GameMessage
                {
                    ActionType = GameActionType.NewGame,
                    Number = session.SecretNumber, // For server-side reference
                    Context = "New game started. Start guessing!"
                };

                await _tcpServer.SendMessageToClientAsync(clientId, response);
                _logger.LogInformation("Started new game session {SessionId} for client {ClientId}", 
                    session.SessionId, clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting new game for client {ClientId}", clientId);
                await SendErrorToClient(clientId, "GAME_START_ERROR", "Failed to start new game");
            }
        }

        private async Task ProcessGuess(Guid clientId, GameMessage gameMessage)
        {
            await ProcessGuessInternal(clientId, gameMessage.Number);
        }

        private async Task ProcessLegacyGuess(Guid clientId, string guess)
        {
            await ProcessGuessInternal(clientId, guess);
        }

        private async Task ProcessGuessInternal(Guid clientId, string guess)
        {
            try
            {
                Guid sessionId;
                lock (_lockObject)
                {
                    if (!_clientSessionMap.TryGetValue(clientId, out sessionId))
                    {
                        // No active session, start a new one
                        await StartNewGame(clientId, new GameMessage { ActionType = GameActionType.NewGame });
                        if (!_clientSessionMap.TryGetValue(clientId, out sessionId))
                        {
                            await SendErrorToClient(clientId, "NO_GAME_SESSION", "No active game session");
                            return;
                        }
                    }
                }

                var result = await _gameService.ProcessGuessAsync(sessionId, guess);

                var response = new GameMessage
                {
                    ActionType = GameActionType.Response,
                    Number = guess,
                    Bulls = result.Bulls,
                    Cows = result.Cows,
                    Context = result.Message
                };

                await _tcpServer.SendMessageToClientAsync(clientId, response);

                // For legacy compatibility, also send simple string response
                var legacyResponse = $"{guess} {result.Bulls} {result.Cows}";
                await _tcpServer.SendMessageToClientAsync(clientId, legacyResponse);

                if (result.IsGameOver)
                {
                    lock (_lockObject)
                    {
                        _clientSessionMap.Remove(clientId);
                    }

                    var gameOverMessage = new GameMessage
                    {
                        ActionType = GameActionType.GameOver,
                        Number = result.SecretNumber,
                        Context = result.IsGameWon ? "Congratulations!" : "Game Over!"
                    };

                    await _tcpServer.SendMessageToClientAsync(clientId, gameOverMessage);
                    _logger.LogInformation("Game session {SessionId} ended for client {ClientId}: {Result}", 
                        sessionId, clientId, result.IsGameWon ? "Won" : "Lost");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing guess {Guess} for client {ClientId}", guess, clientId);
                await SendErrorToClient(clientId, "GUESS_PROCESSING_ERROR", "Failed to process guess");
            }
        }

        private async Task SendGeneratedNumber(Guid clientId)
        {
            try
            {
                Guid sessionId;
                lock (_lockObject)
                {
                    if (!_clientSessionMap.TryGetValue(clientId, out sessionId))
                    {
                        await SendErrorToClient(clientId, "NO_GAME_SESSION", "No active game session");
                        return;
                    }
                }

                var session = await _gameService.GetGameSessionAsync(sessionId);
                if (session == null)
                {
                    await SendErrorToClient(clientId, "INVALID_SESSION", "Game session not found");
                    return;
                }

                var response = new GameMessage
                {
                    ActionType = GameActionType.GenerateNumber,
                    Number = session.SecretNumber,
                    Context = "Here's the secret number for this session"
                };

                await _tcpServer.SendMessageToClientAsync(clientId, response);
                _logger.LogDebug("Sent generated number to client {ClientId}", clientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending generated number to client {ClientId}", clientId);
                await SendErrorToClient(clientId, "NUMBER_GENERATION_ERROR", "Failed to get generated number");
            }
        }

        private async Task SendErrorToClient(Guid clientId, string errorCode, string description)
        {
            var errorMessage = new ErrorMessage
            {
                ErrorCode = errorCode,
                ErrorDescription = description
            };

            await _tcpServer.SendMessageToClientAsync(clientId, errorMessage);
        }

        protected virtual void OnStatusChanged(bool isRunning, string message)
        {
            StatusChanged?.Invoke(this, new ServerStatusChangedEventArgs(isRunning, message));
        }

        // Helper method to create logger for dependencies
        private ILogger<T> CreateLogger<T>()
        {
            // In a real application, this would come from DI container
            // For now, create a basic logger that delegates to our logger
            return new DelegatingLogger<T>(_logger);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                StopAsync().GetAwaiter().GetResult();
                _tcpServer?.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }

    /// <summary>
    /// Event arguments for server status changes
    /// </summary>
    public class ServerStatusChangedEventArgs : EventArgs
    {
        public bool IsRunning { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public ServerStatusChangedEventArgs(bool isRunning, string message)
        {
            IsRunning = isRunning;
            Message = message ?? string.Empty;
            Timestamp = DateTime.UtcNow;
        }
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