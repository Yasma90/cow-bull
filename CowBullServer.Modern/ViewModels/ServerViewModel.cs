using System.Collections.ObjectModel;
using System.IO;
using CowBullServer.Modern.Presentation;
using CowBullServer.Modern.Services;

namespace CowBullServer.Modern.ViewModels;

public sealed class ServerViewModel : ObservableObject, IAsyncDisposable
{
    private const int MaximumActivityEntries = 200;
    private readonly IGameServerHost _server;
    private readonly IUiDispatcher _dispatcher;
    private readonly object _activitySync = new();
    private readonly Queue<ServerActivityEventArgs> _pendingActivity = new();
    private bool _isRunning;
    private bool _isBusy;
    private bool _activityDispatchScheduled;
    private int _connectedClients;
    private string _serverStatus = "Stopped";
    private string _endpoint;
    private int _disposeStarted;

    public ServerViewModel(IGameServerHost server, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _server = server;
        _dispatcher = dispatcher;
        _endpoint = server.Endpoint;
        _server.ActivityOccurred += OnActivityOccurred;

        StartCommand = new AsyncRelayCommand(
            StartAsync,
            () => !IsBusy && !IsRunning,
            HandleUnexpectedError);
        StopCommand = new AsyncRelayCommand(
            StopAsync,
            () => !IsBusy && IsRunning,
            HandleUnexpectedError);
    }

    public ObservableCollection<string> Activity { get; } = [];

    public AsyncRelayCommand StartCommand { get; }

    public AsyncRelayCommand StopCommand { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    public int ConnectedClients
    {
        get => _connectedClients;
        private set => SetProperty(ref _connectedClients, value);
    }

    public string ServerStatus
    {
        get => _serverStatus;
        private set => SetProperty(ref _serverStatus, value);
    }

    public string Endpoint
    {
        get => _endpoint;
        private set => SetProperty(ref _endpoint, value);
    }

    private async Task StartAsync()
    {
        IsBusy = true;
        ServerStatus = "Starting...";
        try
        {
            await _server.StartAsync();
            IsRunning = _server.IsRunning;
            Endpoint = _server.Endpoint;
            ServerStatus = IsRunning ? "Listening" : "Stopped";
        }
        catch (Exception exception) when (
            exception is IOException or System.Net.Sockets.SocketException or
            OperationCanceledException or InvalidOperationException)
        {
            IsRunning = false;
            ServerStatus = $"Start failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StopAsync()
    {
        IsBusy = true;
        ServerStatus = "Stopping...";
        try
        {
            await _server.StopAsync();
        }
        catch (Exception exception) when (
            exception is IOException or System.Net.Sockets.SocketException or
            OperationCanceledException or InvalidOperationException)
        {
            AddActivity($"Stop failed: {exception.Message}");
        }
        finally
        {
            IsRunning = false;
            ConnectedClients = 0;
            ServerStatus = "Stopped";
            IsBusy = false;
        }
    }

    private void OnActivityOccurred(object? sender, ServerActivityEventArgs eventArgs)
    {
        bool shouldSchedule;
        lock (_activitySync)
        {
            if (_pendingActivity.Count == MaximumActivityEntries)
            {
                _pendingActivity.Dequeue();
            }

            _pendingActivity.Enqueue(eventArgs);
            shouldSchedule = !_activityDispatchScheduled;
            _activityDispatchScheduled = true;
        }

        if (shouldSchedule)
        {
            _dispatcher.Post(DrainActivity);
        }
    }

    private void DrainActivity()
    {
        while (true)
        {
            ServerActivityEventArgs eventArgs;
            lock (_activitySync)
            {
                if (_pendingActivity.Count == 0)
                {
                    _activityDispatchScheduled = false;
                    return;
                }

                eventArgs = _pendingActivity.Dequeue();
            }

            ConnectedClients = eventArgs.ConnectedClientCount;
            AddActivity($"[{DateTimeOffset.Now:HH:mm:ss}] {eventArgs.Message}");
        }
    }

    private void AddActivity(string message)
    {
        while (Activity.Count >= MaximumActivityEntries)
        {
            Activity.RemoveAt(0);
        }

        Activity.Add(message);
    }

    private void RefreshCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private void HandleUnexpectedError(Exception exception)
    {
        IsBusy = false;
        ServerStatus = $"Unexpected error: {exception.Message}";
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        _server.ActivityOccurred -= OnActivityOccurred;
        await _server.DisposeAsync();
    }
}
