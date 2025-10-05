using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using CowBullClient.Model;
using CowBullClient.Services;
using CowBull.Common.Configuration;
using CowBull.Common.Services;
using CowBull.Common.Contracts;

namespace CowBullClient.ViewModel
{
    /// <summary>
    /// Modern ViewModel for the client using the new architecture
    /// </summary>
    public class ModernVModelPlay : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        private readonly ModernCowBullClient _modernClient;
        private readonly ILogger<ModernVModelPlay> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private ObservableCollection<NumeroJugado> _listShow;
        private bool _isConnected = false;
        private string _connectionStatus = "Disconnected";
        private string _currentGuess = string.Empty;
        private string _serverResponse = string.Empty;
        private bool _enableGuessButton = false;
        private bool _gameInProgress = false;
        private int _attemptsRemaining = 0;
        private string _gameMessage = "Click Connect to start playing";
        private bool _disposed = false;

        #endregion

        #region Constructor

        public ModernVModelPlay()
        {
            // Initialize services
            _logger = CreateLogger<ModernVModelPlay>();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Configure network settings
            var networkConfig = new NetworkConfiguration
            {
                ServerAddress = "127.0.0.1",
                Port = 4510,
                ConnectionTimeoutMs = 30000,
                ReceiveTimeoutMs = 30000,
                SendTimeoutMs = 10000,
                BufferSize = 8192,
                MaxMessageSize = 1024 * 1024,
                KeepAlive = true,
                HeartbeatIntervalMs = 30000,
                RetryAttempts = 3,
                RetryDelayMs = 1000
            };

            _modernClient = new ModernCowBullClient(networkConfig, _logger);

            // Initialize collections
            ListShow = new ObservableCollection<NumeroJugado>();

            // Setup event handlers
            SetupEventHandlers();

            _logger.LogInformation("ModernVModelPlay (Client) initialized");
        }

        #endregion

        #region Commands

        private AsyncRelayCommand _connectCommand;
        public ICommand ConnectCommand
        {
            get
            {
                return _connectCommand ??= new AsyncRelayCommand(
                    async () => await ConnectAsync(),
                    () => !_isConnected
                );
            }
        }

        private AsyncRelayCommand _disconnectCommand;
        public ICommand DisconnectCommand
        {
            get
            {
                return _disconnectCommand ??= new AsyncRelayCommand(
                    async () => await DisconnectAsync(),
                    () => _isConnected
                );
            }
        }

        private AsyncRelayCommand _newGameCommand;
        public ICommand NewGameCommand
        {
            get
            {
                return _newGameCommand ??= new AsyncRelayCommand(
                    async () => await StartNewGameAsync(),
                    () => _isConnected
                );
            }
        }

        private AsyncRelayCommand _sendGuessCommand;
        public ICommand SendGuessCommand
        {
            get
            {
                return _sendGuessCommand ??= new AsyncRelayCommand(
                    async () => await SendGuessAsync(),
                    () => _enableGuessButton && !string.IsNullOrWhiteSpace(_currentGuess)
                );
            }
        }

        private RelayCommand _clearListCommand;
        public ICommand ClearListCommand
        {
            get
            {
                return _clearListCommand ??= new RelayCommand(
                    () => ListShow.Clear(),
                    () => ListShow.Count > 0
                );
            }
        }

        #endregion

        #region Properties

        public ObservableCollection<NumeroJugado> ListShow
        {
            get => _listShow;
            set
            {
                _listShow = value;
                RaisePropertyChanged(nameof(ListShow));
                ((RelayCommand)ClearListCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                _isConnected = value;
                RaisePropertyChanged(nameof(IsConnected));
                ((AsyncRelayCommand)ConnectCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)DisconnectCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)NewGameCommand).RaiseCanExecuteChanged();
                UpdateGuessButtonState();
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            private set
            {
                _connectionStatus = value;
                RaisePropertyChanged(nameof(ConnectionStatus));
            }
        }

        public string CurrentGuess
        {
            get => _currentGuess;
            set
            {
                _currentGuess = value;
                RaisePropertyChanged(nameof(CurrentGuess));
                UpdateGuessButtonState();
            }
        }

        public string ServerResponse
        {
            get => _serverResponse;
            private set
            {
                _serverResponse = value;
                RaisePropertyChanged(nameof(ServerResponse));
            }
        }

        public bool EnableGuessButton
        {
            get => _enableGuessButton;
            private set
            {
                _enableGuessButton = value;
                RaisePropertyChanged(nameof(EnableGuessButton));
                ((AsyncRelayCommand)SendGuessCommand).RaiseCanExecuteChanged();
            }
        }

        public bool GameInProgress
        {
            get => _gameInProgress;
            private set
            {
                _gameInProgress = value;
                RaisePropertyChanged(nameof(GameInProgress));
                UpdateGuessButtonState();
            }
        }

        public int AttemptsRemaining
        {
            get => _attemptsRemaining;
            private set
            {
                _attemptsRemaining = value;
                RaisePropertyChanged(nameof(AttemptsRemaining));
            }
        }

        public string GameMessage
        {
            get => _gameMessage;
            private set
            {
                _gameMessage = value;
                RaisePropertyChanged(nameof(GameMessage));
            }
        }

        #endregion

        #region Private Methods

        private void SetupEventHandlers()
        {
            _modernClient.ConnectionStatusChanged += OnConnectionStatusChanged;
            _modernClient.GameResponseReceived += OnGameResponseReceived;
            _modernClient.SystemMessageReceived += OnSystemMessageReceived;
            _modernClient.ErrorMessageReceived += OnErrorMessageReceived;
        }

        private async Task ConnectAsync()
        {
            try
            {
                _logger.LogInformation("Connecting to server...");
                ConnectionStatus = "Connecting...";

                var result = await _modernClient.ConnectAsync(_cancellationTokenSource.Token);
                
                if (result)
                {
                    _logger.LogInformation("Connected to server successfully");
                }
                else
                {
                    ConnectionStatus = "Failed to connect";
                    _logger.LogError("Failed to connect to server");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to server");
                ConnectionStatus = $"Error: {ex.Message}";
            }
        }

        private async Task DisconnectAsync()
        {
            try
            {
                _logger.LogInformation("Disconnecting from server...");
                ConnectionStatus = "Disconnecting...";

                await _modernClient.DisconnectAsync(_cancellationTokenSource.Token);
                
                _logger.LogInformation("Disconnected from server");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from server");
            }
        }

        private async Task StartNewGameAsync()
        {
            try
            {
                _logger.LogInformation("Starting new game...");
                GameMessage = "Starting new game...";

                var result = await _modernClient.StartNewGameAsync(_cancellationTokenSource.Token);
                
                if (result)
                {
                    GameInProgress = true;
                    AttemptsRemaining = 10; // Default
                    CurrentGuess = string.Empty;
                    ListShow.Clear();
                    GameMessage = "New game started! Make your guess.";
                    _logger.LogInformation("New game started successfully");
                }
                else
                {
                    GameMessage = "Failed to start new game";
                    _logger.LogError("Failed to start new game");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting new game");
                GameMessage = $"Error: {ex.Message}";
            }
        }

        private async Task SendGuessAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentGuess))
                return;

            try
            {
                _logger.LogDebug("Sending guess: {Guess}", CurrentGuess);

                var result = await _modernClient.SendGuessAsync(CurrentGuess, _cancellationTokenSource.Token);
                
                if (!result)
                {
                    GameMessage = "Failed to send guess";
                    _logger.LogError("Failed to send guess");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending guess");
                GameMessage = $"Error: {ex.Message}";
            }
        }

        private void UpdateGuessButtonState()
        {
            EnableGuessButton = _isConnected && 
                               _gameInProgress && 
                               !string.IsNullOrWhiteSpace(_currentGuess) &&
                               _currentGuess.Length == 4 &&
                               _currentGuess.All(char.IsDigit);
        }

        private void OnConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            try
            {
                // Update UI on the UI thread
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsConnected = e.IsConnected;
                    ConnectionStatus = e.IsConnected ? "Connected" : $"Disconnected: {e.Reason}";
                    
                    if (!e.IsConnected)
                    {
                        GameInProgress = false;
                        GameMessage = "Connection lost. Please reconnect.";
                    }
                    else
                    {
                        GameMessage = "Connected! Click 'New Game' to start playing.";
                    }
                });
                
                _logger.LogInformation("Connection status changed: {IsConnected} - {Reason}", 
                    e.IsConnected, e.Reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling connection status change");
            }
        }

        private void OnGameResponseReceived(object sender, GameResponseEventArgs e)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    switch (e.ActionType)
                    {
                        case CowBull.Common.Models.GameActionType.Response:
                            HandleGameResponse(e);
                            break;
                            
                        case CowBull.Common.Models.GameActionType.NewGame:
                            GameMessage = e.Context ?? "New game started!";
                            break;
                            
                        case CowBull.Common.Models.GameActionType.GameOver:
                            HandleGameOver(e);
                            break;
                            
                        default:
                            _logger.LogDebug("Received game response: {ActionType}", e.ActionType);
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling game response");
            }
        }

        private void HandleGameResponse(GameResponseEventArgs e)
        {
            var jugada = new NumeroJugado
            {
                Numero = e.Number,
                Toros = e.Bulls,
                Vacas = e.Cows,
                CertezaJugada = $"{e.Bulls} Bulls, {e.Cows} Cows"
            };

            ListShow.Add(jugada);
            ServerResponse = $"Last guess: {e.Number} - {e.Bulls} Bulls, {e.Cows} Cows";
            GameMessage = e.Context ?? "Make your next guess";
            
            // Check if game is won
            if (e.Bulls == 4) // Assuming 4-digit number
            {
                GameInProgress = false;
                GameMessage = "Congratulations! You guessed the number!";
            }
            else
            {
                AttemptsRemaining--;
                if (AttemptsRemaining <= 0)
                {
                    GameInProgress = false;
                    GameMessage = "Game over! No more attempts remaining.";
                }
            }
            
            CurrentGuess = string.Empty;
        }

        private void HandleGameOver(GameResponseEventArgs e)
        {
            GameInProgress = false;
            GameMessage = e.Context ?? "Game over!";
            
            if (!string.IsNullOrEmpty(e.Number))
            {
                GameMessage += $" The number was: {e.Number}";
            }
        }

        private void OnSystemMessageReceived(object sender, SystemMessageEventArgs e)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    _logger.LogDebug("Received system message: {ActionType} - {Content}", 
                        e.ActionType, e.Content);
                    
                    // Handle system messages as needed
                    if (!string.IsNullOrEmpty(e.Content))
                    {
                        ServerResponse = e.Content;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling system message");
            }
        }

        private void OnErrorMessageReceived(object sender, ErrorMessageEventArgs e)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    GameMessage = $"Error: {e.ErrorDescription}";
                    ServerResponse = $"Error {e.ErrorCode}: {e.ErrorDescription}";
                    
                    _logger.LogError("Received error message: {ErrorCode} - {ErrorDescription}", 
                        e.ErrorCode, e.ErrorDescription);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling error message");
            }
        }

        // Helper method to create loggers (in a real app, use DI)
        private ILogger<T> CreateLogger<T>()
        {
            return new SimpleLogger<T>();
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                try
                {
                    _cancellationTokenSource?.Cancel();
                    _modernClient?.Dispose();
                    _cancellationTokenSource?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during disposal");
                }
                
                GC.SuppressFinalize(this);
            }
        }

        #endregion
    }

    /// <summary>
    /// Simple logger implementation for demonstration
    /// In production, use proper logging framework
    /// </summary>
    internal class SimpleLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                var message = formatter(state, exception);
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"[{timestamp}] [{logLevel}] [{typeof(T).Name}] {message}");
                
                if (exception != null)
                {
                    Console.WriteLine($"Exception: {exception}");
                }
            }
        }
    }

    /// <summary>
    /// Async RelayCommand for the client
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting = false;

        public AsyncRelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_isExecuting)
                return false;
                
            return _canExecute?.Invoke() ?? true;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                _isExecuting = true;
                RaiseCanExecuteChanged();
                await _executeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Async command execution error: {ex.Message}");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}