using System.Collections.ObjectModel;
using CowBullClient.Modern.Presentation;
using CowBullClient.Modern.Services;

namespace CowBullClient.Modern.ViewModels;

public sealed partial class ClientViewModel : ObservableObject, IAsyncDisposable
{
    private const int DefaultNumberLength = 4;
    private const int DefaultMaximumAttempts = 10;

    private readonly IGameClient _client;
    private readonly IUiDispatcher _dispatcher;
    private readonly IResponseTimeout _responseTimeout;
    private bool _isConnected;
    private bool _isGameActive;
    private bool _isBusy;
    private string _connectionStatus = "Disconnected";
    private string _statusMessage = "Connect to a server to begin.";
    private string _currentGuess = string.Empty;
    private int _numberLength = DefaultNumberLength;
    private int _maximumAttempts = DefaultMaximumAttempts;
    private int _attemptsRemaining = DefaultMaximumAttempts;
    private int _lastAttemptNumber;
    private bool _isSessionSynchronized;
    private Guid? _sessionId;
    private Guid? _pendingRequestId;
    private CancellationTokenSource? _responseCancellation;
    private int _disposeStarted;

    public ClientViewModel(
        IGameClient client,
        IUiDispatcher dispatcher,
        IResponseTimeout? responseTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _client = client;
        _dispatcher = dispatcher;
        _responseTimeout = responseTimeout ??
            new ResponseTimeout(TimeSpan.FromSeconds(15));
        _client.MessageReceived += OnMessageReceived;
        _client.StatusChanged += OnStatusChanged;
        _client.Faulted += OnFaulted;

        ConnectCommand = new AsyncRelayCommand(
            ConnectAsync,
            () => !IsBusy && !IsConnected,
            HandleUnexpectedError);
        DisconnectCommand = new AsyncRelayCommand(
            DisconnectAsync,
            () => !IsBusy && IsConnected,
            HandleUnexpectedError);
        NewGameCommand = new AsyncRelayCommand(
            StartNewGameAsync,
            () => !IsBusy && IsConnected && !IsGameActive && _pendingRequestId is null,
            HandleUnexpectedError);
        SubmitGuessCommand = new AsyncRelayCommand(
            SubmitGuessAsync,
            CanSubmitGuess,
            HandleUnexpectedError);
        SurrenderCommand = new AsyncRelayCommand(
            SurrenderAsync,
            () => !IsBusy && IsGameActive && _pendingRequestId is null,
            HandleUnexpectedError);
    }

    public ObservableCollection<ClientAttemptViewModel> Attempts { get; } = [];

    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand DisconnectCommand { get; }
    public AsyncRelayCommand NewGameCommand { get; }
    public AsyncRelayCommand SubmitGuessCommand { get; }
    public AsyncRelayCommand SurrenderCommand { get; }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (SetProperty(ref _isConnected, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool IsGameActive
    {
        get => _isGameActive;
        private set
        {
            if (SetProperty(ref _isGameActive, value))
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

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string CurrentGuess
    {
        get => _currentGuess;
        set
        {
            if (SetProperty(ref _currentGuess, value ?? string.Empty))
            {
                SubmitGuessCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int NumberLength
    {
        get => _numberLength;
        private set => SetProperty(ref _numberLength, value);
    }

    public int AttemptsRemaining
    {
        get => _attemptsRemaining;
        private set => SetProperty(ref _attemptsRemaining, value);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        _client.MessageReceived -= OnMessageReceived;
        _client.StatusChanged -= OnStatusChanged;
        _client.Faulted -= OnFaulted;
        CancelResponseTimeout();
        await _client.DisposeAsync();
    }

    private void RefreshCommands()
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        NewGameCommand.NotifyCanExecuteChanged();
        SubmitGuessCommand.NotifyCanExecuteChanged();
        SurrenderCommand.NotifyCanExecuteChanged();
    }

    private void BeginRequest(Guid messageId)
    {
        CancelResponseTimeout();
        _pendingRequestId = messageId;
        _responseCancellation = new CancellationTokenSource();
        _ = ObserveResponseTimeoutAsync(messageId, _responseCancellation.Token);
        RefreshCommands();
    }

    private bool IsExpectedResponse(Guid messageId) =>
        _pendingRequestId == messageId;

    private void CompleteRequest()
    {
        _pendingRequestId = null;
        CancelResponseTimeout();
        RefreshCommands();
    }

    private async Task ObserveResponseTimeoutAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _responseTimeout.WaitAsync(cancellationToken).ConfigureAwait(false);
            _dispatcher.Post(
                () =>
                {
                    if (IsExpectedResponse(messageId))
                    {
                        IsBusy = false;
                        if (IsGameActive)
                        {
                            MarkSessionUnsynchronized(
                                "The server response timed out. Surrender or disconnect before continuing.");
                        }
                        else
                        {
                            StatusMessage = "The server response timed out. Try again.";
                        }

                        CompleteRequest();
                    }
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A matching response or lifecycle transition completed first.
        }
        catch (Exception exception)
        {
            _dispatcher.Post(() => HandleUnexpectedError(exception));
        }
    }

    private void CancelResponseTimeout()
    {
        CancellationTokenSource? cancellation = _responseCancellation;
        _responseCancellation = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void HandleUnexpectedError(Exception exception)
    {
        CompleteRequest();
        IsBusy = false;
        if (IsGameActive)
        {
            _isSessionSynchronized = false;
        }

        StatusMessage = $"Unexpected error: {exception.Message}";
        RefreshCommands();
    }

    private void MarkSessionUnsynchronized(string message)
    {
        _isSessionSynchronized = false;
        CurrentGuess = string.Empty;
        StatusMessage = message;
        RefreshCommands();
    }
}
