using CowBull.Application.Games;
using CowBull.Application.Ports;
using CowBull.Domain.Games;
using Xunit;

namespace CowBull.Application.Tests.Games;

public sealed class GameServiceTests
{
    private static readonly Guid GameId = Guid.Parse("5fd211b6-c459-44fc-864c-e9b43aa91aaa");
    private static readonly DateTimeOffset StartedAt = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StartGame_uses_generators_clock_and_repository()
    {
        var context = CreateContext();

        var snapshot = context.Service.StartGame(Configuration());

        Assert.Equal(GameId, snapshot.GameId);
        Assert.Equal(StartedAt, snapshot.StartedAt);
        Assert.Null(snapshot.SecretNumber);
        Assert.Equal("0123", context.SecretGenerator.LastGeneratedSecret);
        Assert.NotNull(context.Repository.GetById(GameId));
        Assert.Equal(1, context.Repository.AddCount);
    }

    [Fact]
    public void SubmitGuess_updates_persisted_game_and_returns_result()
    {
        var context = CreateContext();
        context.Service.StartGame(Configuration());
        context.Clock.SetUtcNow(StartedAt.AddSeconds(1));

        var result = context.Service.SubmitGuess(GameId, "0123");

        Assert.Equal(GameStatus.Won, result.Game.Status);
        Assert.Equal(4, result.Attempt.Score.Bulls);
        Assert.Equal(1, context.Repository.UpdateCount);
    }

    [Fact]
    public void GetGame_applies_timeout_using_injected_clock()
    {
        var context = CreateContext();
        context.Service.StartGame(Configuration(timeout: TimeSpan.FromSeconds(10)));
        context.Clock.SetUtcNow(StartedAt.AddSeconds(10));

        var snapshot = context.Service.GetGame(GameId);

        Assert.Equal(GameStatus.TimedOut, snapshot.Status);
        Assert.Equal("0123", snapshot.SecretNumber);
        Assert.Equal(1, context.Repository.UpdateCount);
    }

    [Fact]
    public void EndGame_abandons_active_game()
    {
        var context = CreateContext();
        context.Service.StartGame(Configuration());
        context.Clock.SetUtcNow(StartedAt.AddSeconds(2));

        var snapshot = context.Service.EndGame(GameId);

        Assert.Equal(GameStatus.Abandoned, snapshot.Status);
        Assert.Equal("0123", snapshot.SecretNumber);
        Assert.Equal(1, context.Repository.UpdateCount);
    }

    [Fact]
    public void Operations_for_unknown_game_throw_typed_exception()
    {
        var context = CreateContext();

        var exception = Assert.Throws<GameNotFoundException>(
            () => context.Service.GetGame(GameId));

        Assert.Equal(GameId, exception.GameId);
    }

    [Fact]
    public void Invalid_guess_does_not_update_repository_or_consume_attempt()
    {
        var context = CreateContext();
        context.Service.StartGame(Configuration());

        Assert.Throws<ArgumentException>(() => context.Service.SubmitGuess(GameId, "11a3"));

        Assert.Equal(0, context.Repository.UpdateCount);
        Assert.Empty(context.Service.GetGame(GameId).Attempts);
    }

    private static TestContext CreateContext()
    {
        var repository = new InMemoryGameRepository();
        var secretGenerator = new StubSecretNumberGenerator("0123");
        var clock = new ManualTimeProvider(StartedAt);
        var service = new GameService(
            repository,
            secretGenerator,
            new StubGameIdGenerator(GameId),
            clock);

        return new TestContext(service, repository, secretGenerator, clock);
    }

    private static GameConfiguration Configuration(TimeSpan? timeout = null) =>
        new(4, 5, false, timeout ?? TimeSpan.FromMinutes(1));

    private sealed record TestContext(
        GameService Service,
        InMemoryGameRepository Repository,
        StubSecretNumberGenerator SecretGenerator,
        ManualTimeProvider Clock);

    private sealed class InMemoryGameRepository : IGameRepository
    {
        private readonly Dictionary<Guid, GameSession> _games = [];

        public int AddCount { get; private set; }

        public int UpdateCount { get; private set; }

        public GameSession? GetById(Guid gameId) => _games.GetValueOrDefault(gameId);

        public void Add(GameSession game)
        {
            _games.Add(game.GameId, game);
            AddCount++;
        }

        public void Update(GameSession game)
        {
            _games[game.GameId] = game;
            UpdateCount++;
        }
    }

    private sealed class StubSecretNumberGenerator(string secret) : ISecretNumberGenerator
    {
        public string? LastGeneratedSecret { get; private set; }

        public string Generate(GameConfiguration configuration)
        {
            LastGeneratedSecret = secret;
            return secret;
        }
    }

    private sealed class StubGameIdGenerator(Guid gameId) : IGameIdGenerator
    {
        public Guid Create() => gameId;
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void SetUtcNow(DateTimeOffset value) => _utcNow = value;
    }
}
