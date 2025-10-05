using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using CowBull.Common.Services;

namespace CowBullServer.Modern
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

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly IGameService _gameService;
        private bool _isServerRunning = false;
        private string _serverStatus = "Stopped";
        private int _connectedClients = 0;
        private string _logMessages = string.Empty;

        public MainViewModel()
        {
            _logger = new ConsoleLogger<MainViewModel>();
            _gameService = new GameService(new ConsoleLogger<GameService>());
            
            StartServerCommand = new RelayCommand(async () => await StartServerAsync(), () => !IsServerRunning);
            StopServerCommand = new RelayCommand(async () => await StopServerAsync(), () => IsServerRunning);
        }

        public bool IsServerRunning
        {
            get => _isServerRunning;
            set
            {
                _isServerRunning = value;
                OnPropertyChanged(nameof(IsServerRunning));
            }
        }

        public string ServerStatus
        {
            get => _serverStatus;
            set
            {
                _serverStatus = value;
                OnPropertyChanged(nameof(ServerStatus));
            }
        }

        public int ConnectedClients
        {
            get => _connectedClients;
            set
            {
                _connectedClients = value;
                OnPropertyChanged(nameof(ConnectedClients));
            }
        }

        public string LogMessages
        {
            get => _logMessages;
            set
            {
                _logMessages = value;
                OnPropertyChanged(nameof(LogMessages));
            }
        }

        public ICommand StartServerCommand { get; }
        public ICommand StopServerCommand { get; }

        private async Task StartServerAsync()
        {
            try
            {
                IsServerRunning = true;
                ServerStatus = "Starting...";
                AddLogMessage("Server starting...");
                
                // Simulate server start
                await Task.Delay(1000);
                
                ServerStatus = "Running on port 4510";
                AddLogMessage("Server started successfully on port 4510");
            }
            catch (Exception ex)
            {
                ServerStatus = $"Error: {ex.Message}";
                IsServerRunning = false;
                AddLogMessage($"Error starting server: {ex.Message}");
            }
        }

        private async Task StopServerAsync()
        {
            try
            {
                ServerStatus = "Stopping...";
                AddLogMessage("Server stopping...");
                
                // Simulate server stop
                await Task.Delay(500);
                
                IsServerRunning = false;
                ServerStatus = "Stopped";
                ConnectedClients = 0;
                AddLogMessage("Server stopped");
            }
            catch (Exception ex)
            {
                ServerStatus = $"Error: {ex.Message}";
                AddLogMessage($"Error stopping server: {ex.Message}");
            }
        }

        private void AddLogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogMessages += $"[{timestamp}] {message}\n";
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