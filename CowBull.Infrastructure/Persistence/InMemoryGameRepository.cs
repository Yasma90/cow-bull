using System.Collections.Concurrent;
using CowBull.Application.Ports;
using CowBull.Domain.Games;

namespace CowBull.Infrastructure.Persistence;

/// <summary>
/// Process-local repository suitable for the desktop server and demos.
/// The domain aggregate serializes its own state transitions; this adapter
/// provides atomic aggregate registration and lookup.
/// </summary>
public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<Guid, GameSession> _games = new();

    public int Count => _games.Count;

    public GameSession? GetById(Guid gameId) =>
        _games.GetValueOrDefault(gameId);

    public void Add(GameSession game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (!_games.TryAdd(game.GameId, game))
        {
            throw new InvalidOperationException($"Game '{game.GameId}' already exists.");
        }
    }

    public void Update(GameSession game)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (!_games.TryGetValue(game.GameId, out GameSession? current))
        {
            throw new KeyNotFoundException($"Game '{game.GameId}' does not exist.");
        }

        if (!_games.TryUpdate(game.GameId, game, current))
        {
            throw new InvalidOperationException($"Game '{game.GameId}' was changed concurrently.");
        }
    }

    public bool Remove(Guid gameId) =>
        _games.TryRemove(gameId, out _);
}
