using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using CowBull.Common.Services;
using CowBull.Common.Infrastructure;
using CowBull.Common.Models;

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
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly ConcurrentDictionary<string, TcpClient> _connectedClients;
        private bool _isServerRunning = false;
        private string _serverStatus = "Stopped";
        private string _logMessages = string.Empty;

        public MainViewModel()
        {
            _logger = new ConsoleLogger<MainViewModel>();
            _gameService = new GameService(new ConsoleLogger<GameService>());
            _connectedClients = new ConcurrentDictionary<string, TcpClient>();
            
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
            get => _connectedClients.Count;
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
                
                // Crear y configurar TcpListener real
                _tcpListener = new TcpListener(IPAddress.Any, 4510);
                _tcpListener.Start();
                
                _cancellationTokenSource = new CancellationTokenSource();
                
                // Iniciar tarea para aceptar clientes
                _ = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
                
                ServerStatus = "Running on port 4510";
                AddLogMessage("Server started successfully on port 4510");
                AddLogMessage("Waiting for client connections...");
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
                
                // Cancelar tareas y cerrar conexiones
                _cancellationTokenSource?.Cancel();
                
                // Cerrar todas las conexiones de clientes
                foreach (var client in _connectedClients.Values)
                {
                    try
                    {
                        client.Close();
                    }
                    catch { /* Ignorar errores al cerrar */ }
                }
                _connectedClients.Clear();
                
                // Parar el listener
                _tcpListener?.Stop();
                _tcpListener = null;
                
                await Task.Delay(500); // Dar tiempo para limpiar
                
                IsServerRunning = false;
                ServerStatus = "Stopped";
                OnPropertyChanged(nameof(ConnectedClients));
                AddLogMessage("Server stopped");
                AddLogMessage("All clients disconnected");
            }
            catch (Exception ex)
            {
                ServerStatus = $"Error: {ex.Message}";
                AddLogMessage($"Error stopping server: {ex.Message}");
            }
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    var clientId = Guid.NewGuid().ToString();
                    var remoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                    
                    _connectedClients.TryAdd(clientId, tcpClient);
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(ConnectedClients));
                        AddLogMessage($"Client connected: {clientId} from {remoteEndPoint}");
                    });
                    
                    // Manejar cliente en tarea separada
                    _ = Task.Run(() => HandleClientAsync(clientId, tcpClient, cancellationToken));
                }
                catch (ObjectDisposedException)
                {
                    // TcpListener fue cerrado, salir del bucle
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddLogMessage($"Error accepting client: {ex.Message}");
                        });
                    }
                }
            }
        }
        
        private async Task HandleClientAsync(string clientId, TcpClient tcpClient, CancellationToken cancellationToken)
        {
            try
            {
                var stream = tcpClient.GetStream();
                var buffer = new byte[1024];
                
                while (!cancellationToken.IsCancellationRequested && tcpClient.Connected)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        // Cliente desconectado
                        break;
                    }
                    
                    // Aquí se procesarían los mensajes del cliente
                    // Por ahora solo mantenemos la conexión viva
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AddLogMessage($"Error handling client {clientId}: {ex.Message}");
                });
            }
            finally
            {
                // Remover cliente cuando se desconecta
                if (_connectedClients.TryRemove(clientId, out var removedClient))
                {
                    try
                    {
                        removedClient.Close();
                    }
                    catch { /* Ignorar errores al cerrar */ }
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(ConnectedClients));
                        AddLogMessage($"Client disconnected: {clientId}");
                    });
                }
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