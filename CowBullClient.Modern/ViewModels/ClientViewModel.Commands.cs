using System.IO;
using System.Net.Sockets;

using CowBull.Infrastructure.Protocol;

namespace CowBullClient.Modern.ViewModels;

public sealed partial class ClientViewModel
{
    private async Task ConnectAsync()
    {
        IsBusy = true;
        ConnectionStatus = "Connecting...";
        try
        {
            await _client.ConnectAsync();
            IsConnected = _client.IsConnected;
            ConnectionStatus = IsConnected ? "Connected" : "Disconnected";
            StatusMessage = IsConnected
                ? "Connected. Start a new game when ready."
                : "The server did not accept the connection.";
        }
        catch (Exception exception) when (
            exception is IOException or SocketException or TimeoutException or
            OperationCanceledException or InvalidOperationException)
        {
            IsConnected = false;
            ConnectionStatus = "Connection failed";
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            await _client.DisconnectAsync();
        }
        catch (Exception exception) when (
            exception is IOException or SocketException or OperationCanceledException or
            InvalidOperationException)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            ResetDisconnectedState("Disconnected.");
            IsBusy = false;
        }
    }

    private async Task StartNewGameAsync()
    {
        IsBusy = true;
        StatusMessage = "Requesting a new game...";
        var request = new NewGameRequest(
            Guid.NewGuid(),
            DefaultNumberLength,
            DefaultMaximumAttempts);
        BeginRequest(request.MessageId);
        try
        {
            await _client.SendAsync(request);
        }
        catch (Exception exception) when (
            exception is IOException or SocketException or TimeoutException or
            OperationCanceledException or InvalidOperationException)
        {
            CompleteRequest();
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SubmitGuessAsync()
    {
        if (_sessionId is not Guid sessionId)
        {
            return;
        }

        IsBusy = true;
        string guess = CurrentGuess;
        var request = new GuessRequest(Guid.NewGuid(), sessionId, guess);
        BeginRequest(request.MessageId);
        try
        {
            await _client.SendAsync(request);
            CurrentGuess = string.Empty;
            StatusMessage = "Waiting for the server score...";
        }
        catch (Exception exception) when (
            exception is IOException or SocketException or TimeoutException or
            OperationCanceledException or InvalidOperationException)
        {
            CompleteRequest();
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SurrenderAsync()
    {
        if (_sessionId is not Guid sessionId)
        {
            return;
        }

        IsBusy = true;
        var request = new SurrenderRequest(Guid.NewGuid(), sessionId);
        BeginRequest(request.MessageId);
        try
        {
            await _client.SendAsync(request);
            StatusMessage = "Ending the game...";
        }
        catch (Exception exception) when (
            exception is IOException or SocketException or TimeoutException or
            OperationCanceledException or InvalidOperationException)
        {
            CompleteRequest();
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSubmitGuess() =>
        !IsBusy &&
        IsConnected &&
        IsGameActive &&
        _isSessionSynchronized &&
        _pendingRequestId is null &&
        _sessionId is not null &&
        CurrentGuess.Length == NumberLength &&
        CurrentGuess.All(static character => character is >= '0' and <= '9') &&
        CurrentGuess.Distinct().Count() == CurrentGuess.Length;
}
