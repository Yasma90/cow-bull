using CowBull.Application.Abstractions;
using CowBull.Application.Ports;
using CowBull.Domain.Games;

namespace CowBull.Application.Games;

public sealed class GameService : IGameService
{
    private readonly IGameRepository _repository;
    private readonly ISecretNumberGenerator _secretNumberGenerator;
    private readonly IGameIdGenerator _gameIdGenerator;
    private readonly TimeProvider _timeProvider;

    public GameService(
        IGameRepository repository,
        ISecretNumberGenerator secretNumberGenerator,
        IGameIdGenerator gameIdGenerator,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(secretNumberGenerator);
        ArgumentNullException.ThrowIfNull(gameIdGenerator);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _secretNumberGenerator = secretNumberGenerator;
        _gameIdGenerator = gameIdGenerator;
        _timeProvider = timeProvider;
    }

    public GameSnapshot StartGame(GameConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var gameId = _gameIdGenerator.Create();
        var secretNumber = _secretNumberGenerator.Generate(configuration);
        var now = _timeProvider.GetUtcNow();
        var game = new GameSession(gameId, configuration, secretNumber, now);

        _repository.Add(game);
        return game.GetSnapshot(now);
    }

    public GuessResult SubmitGuess(Guid gameId, string guess)
    {
        var game = GetRequiredGame(gameId);
        var result = game.SubmitGuess(guess, _timeProvider.GetUtcNow());
        _repository.Update(game);
        return result;
    }

    public GameSnapshot GetGame(Guid gameId)
    {
        var game = GetRequiredGame(gameId);
        var snapshot = game.GetSnapshot(_timeProvider.GetUtcNow());

        // Reading may apply the timeout transition, so it is persisted as an
        // update just like command-driven transitions.
        _repository.Update(game);
        return snapshot;
    }

    public GameSnapshot EndGame(Guid gameId)
    {
        var game = GetRequiredGame(gameId);
        var snapshot = game.Abandon(_timeProvider.GetUtcNow());
        _repository.Update(game);
        return snapshot;
    }

    private GameSession GetRequiredGame(Guid gameId)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("A game identifier cannot be empty.", nameof(gameId));
        }

        return _repository.GetById(gameId) ?? throw new GameNotFoundException(gameId);
    }
}
