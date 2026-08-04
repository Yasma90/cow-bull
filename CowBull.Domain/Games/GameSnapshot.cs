using System.Collections.ObjectModel;

namespace CowBull.Domain.Games;

/// <summary>
/// An immutable public representation of a game. The secret is deliberately
/// omitted until the game reaches a terminal state.
/// </summary>
public sealed record GameSnapshot
{
    private readonly ReadOnlyCollection<GameAttempt> _attempts;

    public GameSnapshot(
        Guid gameId,
        GameConfiguration configuration,
        GameStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt,
        IEnumerable<GameAttempt> attempts,
        string? secretNumber)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("A game identifier cannot be empty.", nameof(gameId));
        }

        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(attempts);

        var attemptCopy = attempts.ToArray();
        if (attemptCopy.Any(static attempt => attempt is null))
        {
            throw new ArgumentException("Attempts cannot contain null values.", nameof(attempts));
        }

        if (status == GameStatus.Active)
        {
            if (endedAt is not null)
            {
                throw new ArgumentException("An active game cannot have an end time.", nameof(endedAt));
            }

            if (secretNumber is not null)
            {
                throw new ArgumentException("An active game cannot expose its secret.", nameof(secretNumber));
            }
        }
        else
        {
            if (endedAt is null)
            {
                throw new ArgumentException("A terminal game must have an end time.", nameof(endedAt));
            }

            ArgumentException.ThrowIfNullOrEmpty(secretNumber);
        }

        GameId = gameId;
        Configuration = configuration;
        Status = status;
        StartedAt = startedAt;
        EndedAt = endedAt;
        _attempts = Array.AsReadOnly(attemptCopy);
        SecretNumber = secretNumber;
    }

    public Guid GameId { get; }

    public GameConfiguration Configuration { get; }

    public GameStatus Status { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? EndedAt { get; }

    public IReadOnlyList<GameAttempt> Attempts => _attempts;

    public int RemainingAttempts => Math.Max(0, Configuration.MaxAttempts - _attempts.Count);

    public string? SecretNumber { get; }

    public bool IsTerminal => Status != GameStatus.Active;
}
