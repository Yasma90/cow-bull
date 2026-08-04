using System.Net.Sockets;
using CowBull.Infrastructure.Protocol;
using CowBullClient.Modern.Presentation;
using CowBullClient.Modern.Services;
using CowBullClient.Modern.ViewModels;

namespace CowBull.Presentation.Tests.Client;

public sealed class ClientViewModelTests
{
    private static readonly Guid SessionId =
        Guid.Parse("7114bf9c-2c65-413d-b145-a07f8e9fc640");

    [Fact]
    public void New_game_remains_pending_until_correlated_response_and_cannot_overlap()
    {
        using var context = CreateConnectedContext();

        context.ViewModel.NewGameCommand.Execute(null);
        var request = Assert.IsType<NewGameRequest>(Assert.Single(context.Client.Sent));

        Assert.False(context.ViewModel.NewGameCommand.CanExecute(null));
        context.Client.RaiseMessage(
            new NewGameResponse(Guid.NewGuid(), SessionId, 4, 10));
        Assert.False(context.ViewModel.IsGameActive);
        Assert.False(context.ViewModel.NewGameCommand.CanExecute(null));

        context.Client.RaiseMessage(
            new NewGameResponse(request.MessageId, SessionId, 4, 10));

        Assert.True(context.ViewModel.IsGameActive);
        Assert.Equal(10, context.ViewModel.AttemptsRemaining);
        Assert.False(context.ViewModel.NewGameCommand.CanExecute(null));
    }

    [Fact]
    public void Out_of_order_guess_response_requires_surrender_or_disconnect()
    {
        using var context = CreateActiveGameContext();
        context.ViewModel.CurrentGuess = "1023";

        context.ViewModel.SubmitGuessCommand.Execute(null);
        var firstRequest = Assert.IsType<GuessRequest>(context.Client.Sent[^1]);
        Assert.False(context.ViewModel.SubmitGuessCommand.CanExecute(null));
        Assert.False(context.ViewModel.SurrenderCommand.CanExecute(null));

        context.Client.RaiseMessage(
            new GuessResponse(
                firstRequest.MessageId,
                SessionId,
                "1023",
                bulls: 2,
                cows: 2,
                attemptNumber: 2,
                isComplete: false,
                isWon: false));
        Assert.Empty(context.ViewModel.Attempts);
        Assert.True(context.ViewModel.SurrenderCommand.CanExecute(null));

        context.ViewModel.CurrentGuess = "4567";

        Assert.False(context.ViewModel.SubmitGuessCommand.CanExecute(null));
        Assert.Contains(
            "out of order",
            context.ViewModel.StatusMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Replayed_guess_response_does_not_duplicate_an_attempt()
    {
        using var context = CreateActiveGameContext();
        context.ViewModel.CurrentGuess = "1023";
        context.ViewModel.SubmitGuessCommand.Execute(null);
        var request = Assert.IsType<GuessRequest>(context.Client.Sent[^1]);
        var validResponse = new GuessResponse(
            request.MessageId,
            SessionId,
            "1023",
            bulls: 2,
            cows: 2,
            attemptNumber: 1,
            isComplete: false,
            isWon: false);
        context.Client.RaiseMessage(validResponse);
        context.Client.RaiseMessage(validResponse);

        ClientAttemptViewModel attempt = Assert.Single(context.ViewModel.Attempts);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(9, context.ViewModel.AttemptsRemaining);
    }

    [Fact]
    public void Timed_out_terminal_response_ends_game_and_reveals_secret()
    {
        using var context = CreateActiveGameContext();
        context.ViewModel.CurrentGuess = "1023";
        context.ViewModel.SubmitGuessCommand.Execute(null);
        var request = Assert.IsType<GuessRequest>(context.Client.Sent[^1]);

        context.Client.RaiseMessage(
            new GameEndedResponse(
                request.MessageId,
                SessionId,
                GameEndReason.TimedOut,
                "0123",
                attemptsUsed: 0));

        Assert.False(context.ViewModel.IsGameActive);
        Assert.Contains("timed out", context.ViewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0123", context.ViewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Disconnect_clears_stale_game_input_and_attempts()
    {
        using var context = CreateActiveGameContext();
        context.ViewModel.CurrentGuess = "1023";
        context.ViewModel.SubmitGuessCommand.Execute(null);
        var request = Assert.IsType<GuessRequest>(context.Client.Sent[^1]);
        context.Client.RaiseMessage(
            new GuessResponse(
                request.MessageId,
                SessionId,
                "1023",
                bulls: 2,
                cows: 2,
                attemptNumber: 1,
                isComplete: false,
                isWon: false));
        context.ViewModel.CurrentGuess = "4567";

        context.Client.RaiseStatus(isConnected: false, "Connection closed.");

        Assert.False(context.ViewModel.IsConnected);
        Assert.False(context.ViewModel.IsGameActive);
        Assert.Empty(context.ViewModel.Attempts);
        Assert.Empty(context.ViewModel.CurrentGuess);
        Assert.Equal(10, context.ViewModel.AttemptsRemaining);
    }

    [Fact]
    public void Socket_failure_is_presented_and_releases_pending_request()
    {
        using var context = CreateConnectedContext();
        context.Client.SendFailure = new SocketException((int)SocketError.ConnectionReset);

        context.ViewModel.NewGameCommand.Execute(null);

        Assert.Contains(
            "connection",
            context.ViewModel.StatusMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(context.ViewModel.NewGameCommand.CanExecute(null));
    }

    [Fact]
    public void Protocol_fault_releases_pending_game_command()
    {
        using var context = CreateActiveGameContext();
        context.ViewModel.CurrentGuess = "1023";
        context.ViewModel.SubmitGuessCommand.Execute(null);
        Assert.False(context.ViewModel.SurrenderCommand.CanExecute(null));

        context.Client.RaiseFault(new System.Text.Json.JsonException("Malformed response."));

        Assert.True(context.ViewModel.SurrenderCommand.CanExecute(null));
        context.ViewModel.CurrentGuess = "4567";
        Assert.False(context.ViewModel.SubmitGuessCommand.CanExecute(null));
        Assert.Contains("Protocol failure.", context.ViewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Response_timeout_releases_correlated_pending_request()
    {
        var timeout = new ManualResponseTimeout();
        using var context = CreateConnectedContext(timeout);
        context.ViewModel.NewGameCommand.Execute(null);
        Assert.False(context.ViewModel.NewGameCommand.CanExecute(null));

        timeout.Trigger();

        Assert.True(
            SpinWait.SpinUntil(
                () => context.ViewModel.NewGameCommand.CanExecute(null),
                TimeSpan.FromSeconds(1)));
        Assert.Contains(
            "timed out",
            context.ViewModel.StatusMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Active_game_response_timeout_prevents_more_guesses_until_cleanup()
    {
        var timeout = new ManualResponseTimeout();
        using var context = CreateActiveGameContext(timeout);
        context.ViewModel.CurrentGuess = "1023";
        context.ViewModel.SubmitGuessCommand.Execute(null);

        timeout.Trigger();

        Assert.True(
            SpinWait.SpinUntil(
                () => context.ViewModel.SurrenderCommand.CanExecute(null),
                TimeSpan.FromSeconds(1)));
        context.ViewModel.CurrentGuess = "4567";
        Assert.False(context.ViewModel.SubmitGuessCommand.CanExecute(null));
        Assert.Contains(
            "Surrender or disconnect",
            context.ViewModel.StatusMessage,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unexpected_command_failure_is_routed_to_safe_view_model_state()
    {
        using var context = CreateConnectedContext();
        context.Client.SendFailure = new NotSupportedException("Unexpected adapter failure.");

        await context.ViewModel.NewGameCommand.ExecuteAsync();

        Assert.Contains("Unexpected error", context.ViewModel.StatusMessage);
        Assert.True(context.ViewModel.NewGameCommand.CanExecute(null));
    }

    private static TestContext CreateActiveGameContext(
        IResponseTimeout? responseTimeout = null)
    {
        TestContext context = CreateConnectedContext(responseTimeout);
        context.ViewModel.NewGameCommand.Execute(null);
        var request = Assert.IsType<NewGameRequest>(Assert.Single(context.Client.Sent));
        context.Client.RaiseMessage(
            new NewGameResponse(request.MessageId, SessionId, 4, 10));
        return context;
    }

    private static TestContext CreateConnectedContext(
        IResponseTimeout? responseTimeout = null)
    {
        var client = new FakeGameClient();
        var viewModel = new ClientViewModel(
            client,
            new ImmediateDispatcher(),
            responseTimeout);
        viewModel.ConnectCommand.Execute(null);
        Assert.True(viewModel.IsConnected);
        return new TestContext(viewModel, client);
    }

    private sealed class TestContext(
        ClientViewModel viewModel,
        FakeGameClient client) : IDisposable
    {
        public ClientViewModel ViewModel { get; } = viewModel;
        public FakeGameClient Client { get; } = client;

        public void Dispose() =>
            ViewModel.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();
    }

    private sealed class ManualResponseTimeout : IResponseTimeout
    {
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitAsync(CancellationToken cancellationToken) =>
            _completion.Task.WaitAsync(cancellationToken);

        public void Trigger() => _completion.TrySetResult();
    }

    private sealed class FakeGameClient : IGameClient
    {
        public event EventHandler<GameClientMessageEventArgs>? MessageReceived;
        public event EventHandler<GameClientStatusEventArgs>? StatusChanged;
        public event EventHandler<GameClientFaultEventArgs>? Faulted;

        public List<ProtocolMessage> Sent { get; } = [];

        public Exception? SendFailure { get; set; }

        public bool IsConnected { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public ValueTask SendAsync(
            ProtocolMessage message,
            CancellationToken cancellationToken = default)
        {
            if (SendFailure is not null)
            {
                return new ValueTask(Task.FromException(SendFailure));
            }

            Sent.Add(message);
            return ValueTask.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        public void RaiseMessage(ProtocolMessage message) =>
            MessageReceived?.Invoke(this, new GameClientMessageEventArgs(message));

        public void RaiseStatus(bool isConnected, string reason)
        {
            IsConnected = isConnected;
            StatusChanged?.Invoke(this, new GameClientStatusEventArgs(isConnected, reason));
        }

        public void RaiseFault(Exception exception) =>
            Faulted?.Invoke(
                this,
                new GameClientFaultEventArgs("Protocol failure.", exception));
    }
}
