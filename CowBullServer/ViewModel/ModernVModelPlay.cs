using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using CowBullServer.Model;
using CowBullServer.Services;
using CowBull.Common.Configuration;
using CowBull.Common.Services;

namespace CowBullServer.ViewModel
{
    /// <summary>
    /// Modern ViewModel using the new server architecture
    /// </summary>
    public class ModernVModelPlay : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        private readonly ModernCowBullServer _modernServer;
        private readonly IGameService _gameService;
        private readonly ILogger<ModernVModelPlay> _logger;
        private TimeSound _timeSound = new TimeSound();
        private Control _vistaActual;
        private ObservableCollection<NumeroJugado> _listShow;
        private bool _enableButton = false;
        private bool _isServerRunning = false;
        private string _serverStatus = "Stopped";
        private int _connectedClients = 0;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _disposed = false;

        #endregion

        #region Constructor

        public ModernVModelPlay()
        {
            // Initialize services
            _logger = CreateLogger<ModernVModelPlay>();
            _gameService = new GameService(CreateLogger<GameService>());
            
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

            _modernServer = new ModernCowBullServer(networkConfig, _gameService, CreateLogger<ModernCowBullServer>());
            _cancellationTokenSource = new CancellationTokenSource();

            // Initialize collections
            ListShow = new ObservableCollection<NumeroJugado>();

            // Setup event handlers
            SetupEventHandlers();

            _logger.LogInformation("ModernVModelPlay initialized");
        }

        #endregion

        #region Commands

        private RelayCommand _startServerCommand;
        public ICommand StartServerCommand
        {
            get
            {
                return _startServerCommand ??= new RelayCommand(
                    async _ => await StartServerAsync(),
                    _ => !_isServerRunning
                );
            }
        }

        private RelayCommand _stopServerCommand;
        public ICommand StopServerCommand
        {
            get
            {
                return _stopServerCommand ??= new RelayCommand(
                    async _ => await StopServerAsync(),
                    _ => _isServerRunning
                );
            }
        }

        private RelayCommand _sendMessageCommand;
        public ICommand SendMessageCommand
        {
            get
            {
                return _sendMessageCommand ??= new RelayCommand(
                    async parameter =>
                    {
                        if (parameter is string message && !string.IsNullOrWhiteSpace(message))
                        {
                            await BroadcastMessageAsync(message);
                        }
                    },
                    _ => _isServerRunning && _connectedClients > 0
                );
            }
        }

        private RelayCommand _clearListCommand;
        public ICommand ClearListCommand
        {
            get
            {
                return _clearListCommand ??= new RelayCommand(
                    _ => ListShow.Clear(),
                    _ => ListShow.Count > 0
                );
            }
        }

        #endregion

        #region Properties

        public Control VistaActual
        {
            get => _vistaActual;
            set
            {
                _vistaActual = value;
                RaisePropertyChanged(nameof(VistaActual));
            }
        }

        public bool IsEnableTime
        {
            get
            {
                if (ListShow.Count == 0)
                    return false;
                RaisePropertyChanged(nameof(IsEnableTime));
                return true;
            }
        }

        public TimeSound TimeSound
        {
            get => _timeSound;
            set
            {
                _timeSound = value;
                RaisePropertyChanged(nameof(TimeSound));
            }
        }

        public ObservableCollection<NumeroJugado> ListShow
        {
            get => _listShow;
            set
            {
                _listShow = value;
                RaisePropertyChanged(nameof(ListShow));
                RaisePropertyChanged(nameof(IsEnableTime));
            }
        }

        public bool EnableButton
        {
            get => _enableButton;
            set
            {
                _enableButton = value;
                RaisePropertyChanged(nameof(EnableButton));
            }
        }

        public bool IsServerRunning
        {
            get => _isServerRunning;
            private set
            {
                _isServerRunning = value;
                RaisePropertyChanged(nameof(IsServerRunning));
                // Notify command can execute changed
                ((RelayCommand)StartServerCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopServerCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SendMessageCommand).RaiseCanExecuteChanged();
            }
        }

        public string ServerStatus
        {
            get => _serverStatus;
            private set
            {
                _serverStatus = value;
                RaisePropertyChanged(nameof(ServerStatus));
            }
        }

        public int ConnectedClients
        {
            get => _connectedClients;
            private set
            {
                _connectedClients = value;
                RaisePropertyChanged(nameof(ConnectedClients));
                ((RelayCommand)SendMessageCommand).RaiseCanExecuteChanged();
            }
        }

        #endregion

        #region Private Methods

        private void SetupEventHandlers()
        {
            _modernServer.StatusChanged += OnServerStatusChanged;
            
            // Additional event handlers can be added here for game events, client connections, etc.
        }

        private async Task StartServerAsync()
        {
            try
            {
                _logger.LogInformation("Starting server...");
                ServerStatus = "Starting...";

                var result = await _modernServer.StartAsync(_cancellationTokenSource.Token);
                
                if (result)
                {
                    IsServerRunning = true;
                    ServerStatus = "Running";
                    _logger.LogInformation("Server started successfully");
                }
                else
                {
                    ServerStatus = "Failed to start";
                    _logger.LogError("Failed to start server");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting server");
                ServerStatus = $"Error: {ex.Message}";
                IsServerRunning = false;
            }
        }

        private async Task StopServerAsync()
        {
            try
            {
                _logger.LogInformation("Stopping server...");
                ServerStatus = "Stopping...";

                await _modernServer.StopAsync(_cancellationTokenSource.Token);
                
                IsServerRunning = false;
                ServerStatus = "Stopped";
                ConnectedClients = 0;
                _logger.LogInformation("Server stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping server");
                ServerStatus = $"Error: {ex.Message}";
            }
        }

        private async Task BroadcastMessageAsync(string message)
        {
            try
            {
                _logger.LogDebug("Broadcasting message: {Message}", message);
                await _modernServer.BroadcastMessageAsync(message, _cancellationTokenSource.Token);
                
                // Add to display list
                var jugada = new NumeroJugado
                {
                    Numero = message,
                    Toros = 0,
                    Vacas = 0,
                    CertezaJugada = "Broadcast"
                };
                
                ListShow.Add(jugada);
                EnableButton = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting message");
            }
        }

        private void OnServerStatusChanged(object sender, ServerStatusChangedEventArgs e)
        {
            try
            {
                // Update UI on the UI thread
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsServerRunning = e.IsRunning;
                    ServerStatus = e.Message;
                    
                    if (!e.IsRunning)
                    {
                        ConnectedClients = 0;
                    }
                });
                
                _logger.LogInformation("Server status changed: {IsRunning} - {Message}", e.IsRunning, e.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling server status change");
            }
        }

        // Helper method to create loggers (in a real app, use DI)
        private ILogger<T> CreateLogger<T>()
        {
            // Simple console logger for demonstration
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
                    _modernServer?.Dispose();
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
}