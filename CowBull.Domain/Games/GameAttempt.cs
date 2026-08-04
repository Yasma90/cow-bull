namespace CowBull.Domain.Games;

public sealed record GameAttempt
{
    public GameAttempt(
        int attemptNumber,
        string guess,
        GuessScore score,
        DateTimeOffset submittedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptNumber);

        ArgumentException.ThrowIfNullOrEmpty(guess);
        ArgumentNullException.ThrowIfNull(score);

        AttemptNumber = attemptNumber;
        Guess = guess;
        Score = score;
        SubmittedAt = submittedAt;
    }

    public int AttemptNumber { get; }

    public string Guess { get; }

    public GuessScore Score { get; }

    public DateTimeOffset SubmittedAt { get; }
}
