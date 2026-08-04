using CowBull.Domain.Games;
using CowBull.Infrastructure.Persistence;

namespace CowBull.Infrastructure.Tests.Persistence;

public sealed class InMemoryGameRepositoryTests
{
    private static readonly GameConfiguration Configuration =
        new(4, 10, false, TimeSpan.FromMinutes(10));

    [Fact]
    public void Add_and_get_preserve_the_aggregate()
    {
        var repository = new InMemoryGameRepository();
        var game = CreateGame(Guid.NewGuid());

        repository.Add(game);

        Assert.Same(game, repository.GetById(game.GameId));
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public void Add_rejects_duplicate_identifiers()
    {
        var repository = new InMemoryGameRepository();
        var gameId = Guid.NewGuid();
        repository.Add(CreateGame(gameId));

        Assert.Throws<InvalidOperationException>(() => repository.Add(CreateGame(gameId)));
    }

    [Fact]
    public void Update_rejects_an_unregistered_aggregate()
    {
        var repository = new InMemoryGameRepository();

        Assert.Throws<KeyNotFoundException>(() => repository.Update(CreateGame(Guid.NewGuid())));
    }

    [Fact]
    public void Remove_releases_registered_aggregate_and_is_idempotent()
    {
        var repository = new InMemoryGameRepository();
        var game = CreateGame(Guid.NewGuid());
        repository.Add(game);

        Assert.True(repository.Remove(game.GameId));
        Assert.False(repository.Remove(game.GameId));
        Assert.Null(repository.GetById(game.GameId));
        Assert.Equal(0, repository.Count);
    }

    private static GameSession CreateGame(Guid gameId) =>
        new(gameId, Configuration, "0123", DateTimeOffset.UtcNow);
}
