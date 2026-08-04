using System.Collections.Concurrent;
using CowBull.Application.Abstractions;
using CowBull.Application.Games;
using CowBull.Domain.Games;
using CowBull.Infrastructure.Protocol;

namespace CowBullServer.Modern.Services;

/// <summary>
/// Maps typed client requests to application use cases and enforces that a
/// client may operate only its own server-created session.
/// </summary>
public sealed class GameRequestHandler
{
    // Keep a grace interval below the transport's five-minute idle timeout so
    // a request at the domain deadline can receive its typed terminal response.
    private static readonly TimeSpan GameTimeout = TimeSpan.FromMinutes(4);
    private readonly IGameService _gameService;
    private readonly ConcurrentDictionary<Guid, Guid> _sessionsByClient = new();

    public GameRequestHandler(IGameService gameService)
    {
        ArgumentNullException.ThrowIfNull(gameService);
        _gameService = gameService;
    }

    public IReadOnlyList<ProtocolMessage> Handle(Guid clientId, ProtocolMessage request)
    {
        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("A client identifier cannot be empty.", nameof(clientId));
        }

        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return request switch
            {
                NewGameRequest newGame => HandleNewGame(clientId, newGame),
                GuessRequest guess => HandleGuess(clientId, guess),
                SurrenderRequest surrender => HandleSurrender(clientId, surrender),
                _ => Error(
                    request.MessageId,
                    null,
                    "unexpectedMessage",
                    "Clients may send request messages only.")
            };
        }
        catch (GameInactiveException exception)
        {
            _sessionsByClient.TryRemove(clientId, out _);
            return [CreateEndedResponse(request.MessageId, exception.Game)];
        }
        catch (GameNotFoundException exception)
        {
            _sessionsByClient.TryRemove(clientId, out _);
            return Error(request.MessageId, exception.GameId, "gameNotFound", exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Error(request.MessageId, SessionId(request), "invalidRequest", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Error(
                request.MessageId,
                SessionId(request),
                "gameNotActive",
                exception.Message);
        }
    }

    public void Disconnect(Guid clientId)
    {
        if (!_sessionsByClient.TryRemove(clientId, out Guid sessionId))
        {
            return;
        }

        try
        {
            _gameService.EndGame(sessionId);
        }
        catch (Exception exception) when (
            exception is GameNotFoundException or InvalidOperationException)
        {
            // The aggregate is already terminal or was removed independently.
        }
    }

    private IReadOnlyList<ProtocolMessage> HandleNewGame(
        Guid clientId,
        NewGameRequest request)
    {
        Disconnect(clientId);

        var configuration = new GameConfiguration(
            request.NumberLength,
            request.MaximumAttempts,
            allowDuplicateDigits: false,
            GameTimeout);
        GameSnapshot game = _gameService.StartGame(configuration);
        _sessionsByClient[clientId] = game.GameId;

        return
        [
            new NewGameResponse(
                request.MessageId,
                game.GameId,
                game.Configuration.NumberLength,
                game.Configuration.MaxAttempts)
        ];
    }

    private IReadOnlyList<ProtocolMessage> HandleGuess(
        Guid clientId,
        GuessRequest request)
    {
        if (!OwnsSession(clientId, request.SessionId))
        {
            return Error(
                request.MessageId,
                request.SessionId,
                "sessionNotOwned",
                "The requested game does not belong to this client.");
        }

        GuessResult result = _gameService.SubmitGuess(request.SessionId, request.Guess);
        var guessResponse = new GuessResponse(
            request.MessageId,
            result.Game.GameId,
            result.Attempt.Guess,
            result.Attempt.Score.Bulls,
            result.Attempt.Score.Cows,
            result.Attempt.AttemptNumber,
            result.Game.IsTerminal,
            result.Game.Status == GameStatus.Won);

        if (!result.Game.IsTerminal)
        {
            return [guessResponse];
        }

        _sessionsByClient.TryRemove(clientId, out _);
        return
        [
            guessResponse,
            CreateEndedResponse(request.MessageId, result.Game)
        ];
    }

    private IReadOnlyList<ProtocolMessage> HandleSurrender(
        Guid clientId,
        SurrenderRequest request)
    {
        if (!OwnsSession(clientId, request.SessionId))
        {
            return Error(
                request.MessageId,
                request.SessionId,
                "sessionNotOwned",
                "The requested game does not belong to this client.");
        }

        GameSnapshot game = _gameService.EndGame(request.SessionId);
        _sessionsByClient.TryRemove(clientId, out _);
        return [CreateEndedResponse(request.MessageId, game)];
    }

    private bool OwnsSession(Guid clientId, Guid sessionId) =>
        _sessionsByClient.TryGetValue(clientId, out Guid ownedSessionId) &&
        ownedSessionId == sessionId;

    private static GameEndedResponse CreateEndedResponse(
        Guid messageId,
        GameSnapshot game) =>
        new(
            messageId,
            game.GameId,
            game.Status switch
            {
                GameStatus.Won => GameEndReason.Won,
                GameStatus.Lost => GameEndReason.AttemptsExhausted,
                GameStatus.Abandoned => GameEndReason.Surrendered,
                GameStatus.TimedOut => GameEndReason.TimedOut,
                _ => throw new InvalidOperationException("An active game cannot produce a final response.")
            },
            game.SecretNumber ??
                throw new InvalidOperationException("A terminal game must expose its secret."),
            game.Attempts.Count);

    private static Guid? SessionId(ProtocolMessage request) =>
        request switch
        {
            GuessRequest guess => guess.SessionId,
            SurrenderRequest surrender => surrender.SessionId,
            _ => null
        };

    private static IReadOnlyList<ProtocolMessage> Error(
        Guid messageId,
        Guid? sessionId,
        string code,
        string description) =>
        [new ErrorResponse(messageId, sessionId, code, Truncate(description, 512))];

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
