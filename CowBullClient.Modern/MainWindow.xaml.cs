using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using CowBull.Common.Services;
using CowBull.Common.Infrastructure;
using CowBull.Common.Models;

namespace CowBullClient.Modern
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }

    public class GameAttempt
    {
        public string Number { get; set; } = string.Empty;
        public int Bulls { get; set; }
        public int Cows { get; set; }
        public string Result => $"{Number} - {Bulls} Bulls, {Cows} Cows";
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly IGameService _gameService;
        private TcpClient? _tcpClient;
        private GameSession? _currentSession;
        private string _secretNumber = string.Empty;
        private bool _isConnected = false;
        private string _connectionStatus = "Disconnected";
        private string _currentGuess = string.Empty;
        private string _gameMessage = "Click Connect to start playing";
        private bool _gameInProgress = false;
        private int _attemptsRemaining = 10;

        public MainViewModel()
        {
            _logger = new ConsoleLogger<MainViewModel>();
            var gameLogger = new ConsoleLogger<GameService>();
            _gameService = new GameService(gameLogger);
            GameAttempts = new ObservableCollection<GameAttempt>();
            
            ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => !IsConnected);
            DisconnectCommand = new RelayCommand(async () => await DisconnectAsync(), () => IsConnected);
            NewGameCommand = new RelayCommand(async () => await NewGameAsync(), () => IsConnected);
            SendGuessCommand = new RelayCommand(async () => await SendGuessAsync(), () => CanSendGuess());
            SurrenderCommand = new RelayCommand(async () => await SurrenderAsync(), () => GameInProgress);
        }

        public ObservableCollection<GameAttempt> GameAttempts { get; }

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                _connectionStatus = value;
                OnPropertyChanged(nameof(ConnectionStatus));
            }
        }

        public string CurrentGuess
        {
            get => _currentGuess;
            set
            {
                _currentGuess = value;
                OnPropertyChanged(nameof(CurrentGuess));
            }
        }

        public string GameMessage
        {
            get => _gameMessage;
            set
            {
                _gameMessage = value;
                OnPropertyChanged(nameof(GameMessage));
            }
        }

        public bool GameInProgress
        {
            get => _gameInProgress;
            set
            {
                _gameInProgress = value;
                OnPropertyChanged(nameof(GameInProgress));
            }
        }

        public int AttemptsRemaining
        {
            get => _attemptsRemaining;
            set
            {
                _attemptsRemaining = value;
                OnPropertyChanged(nameof(AttemptsRemaining));
            }
        }

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand NewGameCommand { get; }
        public ICommand SendGuessCommand { get; }
        public ICommand SurrenderCommand { get; }

        private bool CanSendGuess()
        {
            return IsConnected && GameInProgress && 
                   !string.IsNullOrWhiteSpace(CurrentGuess) &&
                   CurrentGuess.Length == 4 &&
                   CurrentGuess.All(char.IsDigit);
        }

        private async Task ConnectAsync()
        {
            try
            {
                ConnectionStatus = "Connecting...";
                _logger.LogInformation("Connecting to server...");
                
                // Crear conexión TCP real
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync("127.0.0.1", 4510);
                
                if (_tcpClient.Connected)
                {
                    IsConnected = true;
                    ConnectionStatus = "Connected to 127.0.0.1:4510";
                    GameMessage = "Connected! Click 'New Game' to start playing.";
                    _logger.LogInformation("Connected to server");
                    
                    // Mantener conexión viva
                    _ = Task.Run(KeepConnectionAlive);
                }
                else
                {
                    throw new Exception("Failed to connect to server");
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Connection failed: {ex.Message}";
                _logger.LogError(ex, "Connection failed");
                _tcpClient?.Close();
                _tcpClient = null;
            }
        }

        private async Task DisconnectAsync()
        {
            try
            {
                ConnectionStatus = "Disconnecting...";
                _logger.LogInformation("Disconnecting from server...");
                
                // Cerrar conexión TCP real
                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                    _tcpClient = null;
                }
                
                await Task.Delay(500);
                
                IsConnected = false;
                ConnectionStatus = "Disconnected";
                GameInProgress = false;
                GameMessage = "Disconnected. Click Connect to reconnect.";
                _logger.LogInformation("Disconnected from server");
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Disconnect error: {ex.Message}";
                _logger.LogError(ex, "Disconnect failed");
            }
        }

        private async Task NewGameAsync()
        {
            try
            {
                GameMessage = "Starting new game...";
                _logger.LogInformation("Starting new game...");
                
                // Create a new game session using the GameService
                var config = new GameConfiguration
                {
                    NumberLength = 4,
                    AllowDuplicateDigits = false,
                    MaxAttempts = 10
                };

                _currentSession = await _gameService.CreateGameAsync(config);
                _secretNumber = _currentSession.SecretNumber;
                
                GameInProgress = true;
                AttemptsRemaining = 10;
                GameAttempts.Clear();
                CurrentGuess = string.Empty;
                GameMessage = "New game started! Guess the 4-digit number (no duplicates).";
                _logger.LogInformation("New game started with secret number: {SecretNumber}", _secretNumber);
            }
            catch (Exception ex)
            {
                GameMessage = $"Failed to start game: {ex.Message}";
                _logger.LogError(ex, "Failed to start new game");
            }
        }

        private async Task SendGuessAsync()
        {
            if (!CanSendGuess() || _currentSession == null) return;

            try
            {
                _logger.LogInformation("Processing guess: {Guess} against secret: {Secret}", CurrentGuess, _secretNumber);
                
                // Use the real game service to process the guess
                var result = await _gameService.ProcessGuessAsync(_currentSession.SessionId, CurrentGuess);
                
                if (!result.IsValid)
                {
                    GameMessage = result.Message;
                    return;
                }
                
                var attempt = new GameAttempt
                {
                    Number = CurrentGuess,
                    Bulls = result.Bulls,
                    Cows = result.Cows
                };
                
                GameAttempts.Add(attempt);
                AttemptsRemaining--;
                
                if (result.IsGameWon)
                {
                    GameInProgress = false;
                    GameMessage = $"🎉 Congratulations! You guessed the number {_secretNumber}!";
                }
                else if (AttemptsRemaining <= 0)
                {
                    GameInProgress = false;
                    GameMessage = $"😞 Game over! The secret number was {_secretNumber}.";
                }
                else
                {
                    GameMessage = $"Try again! {AttemptsRemaining} attempts remaining.";
                }
                
                CurrentGuess = string.Empty;
                _logger.LogInformation("Guess processed: {Bulls} bulls, {Cows} cows", result.Bulls, result.Cows);
            }
            catch (Exception ex)
            {
                GameMessage = $"Error processing guess: {ex.Message}";
                _logger.LogError(ex, "Error processing guess");
            }
        }

        private async Task SurrenderAsync()
        {
            if (!GameInProgress)
                return;
            GameInProgress = false;
            GameMessage = $"Te rendiste. El número secreto era {_secretNumber}.";
            _logger.LogInformation("El jugador se rindió. Número secreto: {SecretNumber}", _secretNumber);
        }

        private async Task KeepConnectionAlive()
        {
            try
            {
                while (_tcpClient != null && _tcpClient.Connected && IsConnected)
                {
                    // Mantener la conexión viva
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _logger.LogError(ex, "Connection lost");
                    IsConnected = false;
                    ConnectionStatus = "Connection lost";
                    GameInProgress = false;
                    _tcpClient?.Close();
                    _tcpClient = null;
                });
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting = false;

        public RelayCommand(Func<Task> executeAsync, Func<bool> canExecute = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (_isExecuting) return false;
            return _canExecute?.Invoke() ?? true;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            try
            {
                _isExecuting = true;
                CommandManager.InvalidateRequerySuggested();
                await _executeAsync();
            }
            finally
            {
                _isExecuting = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public class ConsoleLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                var message = formatter(state, exception);
                Console.WriteLine($"[{logLevel}] [{typeof(T).Name}] {message}");
                if (exception != null)
                {
                    Console.WriteLine($"Exception: {exception}");
                }
            }
        }
    }
}