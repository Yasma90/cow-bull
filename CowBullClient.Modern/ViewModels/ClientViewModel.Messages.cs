using CowBull.Infrastructure.Protocol;
using CowBullClient.Modern.Services;

namespace CowBullClient.Modern.ViewModels;

public sealed partial class ClientViewModel
{
    private void OnMessageReceived(object? sender, GameClientMessageEventArgs eventArgs) =>
        _dispatcher.Post(() => ApplyMessage(eventArgs.Message));

    private void OnStatusChanged(object? sender, GameClientStatusEventArgs eventArgs) =>
        _dispatcher.Post(
            () =>
            {
                IsConnected = eventArgs.IsConnected;
                ConnectionStatus = eventArgs.Reason;
                if (!eventArgs.IsConnected)
                {
                    ResetDisconnectedState(eventArgs.Reason);
                }
            });

    private void OnFaulted(object? sender, GameClientFaultEventArgs eventArgs) =>
        _dispatcher.Post(
            () =>
            {
                CompleteRequest();
                if (IsGameActive)
                {
                    MarkSessionUnsynchronized(
                        $"{eventArgs.Description} Surrender or disconnect before continuing.");
                }
                else
                {
                    StatusMessage = eventArgs.Description;
                }
            });

    private void ApplyMessage(ProtocolMessage message)
    {
        if (!IsExpectedResponse(message.MessageId))
        {
            StatusMessage = "Ignored a stale or unsolicited server response.";
            return;
        }

        switch (message)
        {
            case NewGameResponse response:
                ApplyNewGame(response);
                break;
            case GuessResponse response:
                ApplyGuess(response);
                break;
            case GameEndedResponse response:
                ApplyGameEnded(response);
                break;
            case ErrorResponse response:
                StatusMessage = $"{response.Code}: {response.Description}";
                CompleteRequest();
                break;
            default:
                StatusMessage = "The server sent an unexpected response.";
                CompleteRequest();
                break;
        }
    }

    private void ApplyNewGame(NewGameResponse response)
    {
        _sessionId = response.SessionId;
        CompleteRequest();
        NumberLength = response.NumberLength;
        _maximumAttempts = response.MaximumAttempts;
        AttemptsRemaining = response.MaximumAttempts;
        Attempts.Clear();
        _lastAttemptNumber = 0;
        _isSessionSynchronized = true;
        CurrentGuess = string.Empty;
        IsGameActive = true;
        StatusMessage = $"Game started. Guess the {NumberLength}-digit number.";
    }

    private void ApplyGuess(GuessResponse response)
    {
        if (_sessionId != response.SessionId)
        {
            StatusMessage = "Ignored a response for another game.";
            CompleteRequest();
            return;
        }

        if (response.AttemptNumber != _lastAttemptNumber + 1)
        {
            CompleteRequest();
            MarkSessionUnsynchronized(
                "The server response was out of order. Surrender or disconnect before continuing.");
            return;
        }

        _lastAttemptNumber = response.AttemptNumber;
        Attempts.Add(
            new ClientAttemptViewModel(
                response.AttemptNumber,
                response.Guess,
                response.Bulls,
                response.Cows));
        AttemptsRemaining = Math.Max(0, _maximumAttempts - response.AttemptNumber);
        IsGameActive = !response.IsComplete;
        _isSessionSynchronized = !response.IsComplete;
        if (!response.IsComplete)
        {
            CompleteRequest();
        }

        StatusMessage = response.IsWon
            ? "Correct. Waiting for the final game result..."
            : response.IsComplete
                ? "No attempts remain. Waiting for the final game result..."
                : $"{response.Bulls} bulls, {response.Cows} cows.";
    }

    private void ApplyGameEnded(GameEndedResponse response)
    {
        if (_sessionId != response.SessionId)
        {
            StatusMessage = "Ignored a final response for another game.";
            CompleteRequest();
            return;
        }

        CompleteRequest();
        IsGameActive = false;
        _isSessionSynchronized = false;
        _sessionId = null;
        StatusMessage = response.Reason switch
        {
            GameEndReason.Won =>
                $"You won in {response.AttemptsUsed} attempts. Secret: {response.RevealedSecret}.",
            GameEndReason.AttemptsExhausted =>
                $"Game over. Secret: {response.RevealedSecret}.",
            GameEndReason.Surrendered =>
                $"Game surrendered. Secret: {response.RevealedSecret}.",
            GameEndReason.TimedOut =>
                $"Game timed out. Secret: {response.RevealedSecret}.",
            _ => "The game ended."
        };
    }

    private void ResetDisconnectedState(string message)
    {
        IsConnected = false;
        IsGameActive = false;
        _sessionId = null;
        CancelResponseTimeout();
        _pendingRequestId = null;
        _lastAttemptNumber = 0;
        _isSessionSynchronized = false;
        NumberLength = DefaultNumberLength;
        _maximumAttempts = DefaultMaximumAttempts;
        AttemptsRemaining = DefaultMaximumAttempts;
        Attempts.Clear();
        CurrentGuess = string.Empty;
        StatusMessage = message;
        RefreshCommands();
    }
}
