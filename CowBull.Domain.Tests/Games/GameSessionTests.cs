using CowBull.Domain.Games;
using Xunit;

namespace CowBull.Domain.Tests.Games;

public sealed class GameSessionTests
{
    private static readonly Guid GameId = Guid.Parse("3b37328d-a9d7-427f-9589-91d44028d82d");
    private static readonly DateTimeOffset StartedAt = new(2026, 8, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Snapshot_hides_secret_while_game_is_active()
    {
        var game = CreateGame("0123");

        var snapshot = game.GetSnapshot(StartedAt);

        Assert.Equal(GameStatus.Active, snapshot.Status);
        Assert.Null(snapshot.SecretNumber);
    }

    [Fact]
    public void Leading_zero_is_preserved_and_can_win_the_game()
    {
        var game = CreateGame("0123");

        var result = game.SubmitGuess("0123", StartedAt.AddSeconds(1));

        Assert.Equal(GameStatus.Won, result.Game.Status);
        Assert.Equal("0123", result.Attempt.Guess);
        Assert.Equal("0123", result.Game.SecretNumber);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12a4")]
    [InlineData("١٢٣٤")]
    [InlineData("1123")]
    public void Invalid_guess_does_not_consume_an_attempt(string guess)
    {
        var game = CreateGame("0123");

        Assert.Throws<ArgumentException>(() => game.SubmitGuess(guess, StartedAt.AddSeconds(1)));

        var snapshot = game.GetSnapshot(StartedAt.AddSeconds(1));
        Assert.Empty(snapshot.Attempts);
        Assert.Equal(snapshot.Configuration.MaxAttempts, snapshot.RemainingAttempts);
    }

    [Fact]
    public void Last_unsuccessful_attempt_loses_and_reveals_secret()
    {
        var game = CreateGame("0123", maxAttempts: 2);

        game.SubmitGuess("4567", StartedAt.AddSeconds(1));
        var result = game.SubmitGuess("4567", StartedAt.AddSeconds(2));

        Assert.Equal(GameStatus.Lost, result.Game.Status);
        Assert.Equal("0123", result.Game.SecretNumber);
        Assert.Equal(2, result.Game.Attempts.Count);
        Assert.Equal(0, result.Game.RemainingAttempts);
    }

    [Fact]
    public void Observing_at_timeout_marks_game_timed_out_without_an_attempt()
    {
        var game = CreateGame("0123", timeout: TimeSpan.FromSeconds(30));

        var snapshot = game.GetSnapshot(StartedAt.AddSeconds(30));

        Assert.Equal(GameStatus.TimedOut, snapshot.Status);
        Assert.Equal("0123", snapshot.SecretNumber);
        Assert.Empty(snapshot.Attempts);
    }

    [Fact]
    public void Guess_after_timeout_is_rejected_and_game_remains_timed_out()
    {
        var game = CreateGame("0123", timeout: TimeSpan.FromSeconds(30));

        Assert.Throws<InvalidOperationException>(
            () => game.SubmitGuess("0123", StartedAt.AddSeconds(31)));

        Assert.Equal(GameStatus.TimedOut, game.Status);
        Assert.Empty(game.GetSnapshot(StartedAt.AddSeconds(31)).Attempts);
    }

    [Fact]
    public void Abandon_is_idempotent_and_reveals_secret()
    {
        var game = CreateGame("0123");

        var first = game.Abandon(StartedAt.AddSeconds(1));
        var second = game.Abandon(StartedAt.AddSeconds(2));

        Assert.Equal(GameStatus.Abandoned, first.Status);
        Assert.Equal(first, second);
        Assert.Equal("0123", second.SecretNumber);
    }

    [Fact]
    public void Snapshot_attempt_collection_cannot_be_changed_through_a_later_guess()
    {
        var game = CreateGame("0123");
        var before = game.GetSnapshot(StartedAt);

        game.SubmitGuess("4567", StartedAt.AddSeconds(1));

        Assert.Empty(before.Attempts);
    }

    [Fact]
    public void Secret_with_duplicates_is_rejected_when_duplicates_are_disabled()
    {
        var configuration = Configuration(allowDuplicateDigits: false);

        Assert.Throws<ArgumentException>(
            () => new GameSession(GameId, configuration, "0012", StartedAt));
    }

    private static GameSession CreateGame(
        string secret,
        int maxAttempts = 5,
        TimeSpan? timeout = null,
        bool allowDuplicateDigits = false) =>
        new(
            GameId,
            Configuration(maxAttempts, timeout, allowDuplicateDigits),
            secret,
            StartedAt);

    private static GameConfiguration Configuration(
        int maxAttempts = 5,
        TimeSpan? timeout = null,
        bool allowDuplicateDigits = false) =>
        new(4, maxAttempts, allowDuplicateDigits, timeout ?? TimeSpan.FromMinutes(1));
}
