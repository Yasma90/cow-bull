namespace CowBull.Domain.Games;

public sealed record GuessResult
{
    public GuessResult(GameAttempt attempt, GameSnapshot game)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(game);

        Attempt = attempt;
        Game = game;
    }

    public GameAttempt Attempt { get; }

    public GameSnapshot Game { get; }
}
