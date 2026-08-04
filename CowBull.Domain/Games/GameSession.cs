namespace CowBull.Domain.Games;

/// <summary>
/// The game aggregate. All state transitions are serialized so a guess either
/// completes in full or does not consume an attempt.
/// </summary>
public sealed class GameSession
{
    private readonly object _gate = new();
    private readonly List<GameAttempt> _attempts = [];
    private readonly string _secretNumber;
    private GameStatus _status = GameStatus.Active;
    private DateTimeOffset? _endedAt;

    public GameSession(
        Guid gameId,
        GameConfiguration configuration,
        string secretNumber,
        DateTimeOffset startedAt)
    {
        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("A game identifier cannot be empty.", nameof(gameId));
        }

        ArgumentNullException.ThrowIfNull(configuration);
        ValidateNumber(secretNumber, configuration, nameof(secretNumber));

        GameId = gameId;
        Configuration = configuration;
        StartedAt = startedAt;
        _secretNumber = secretNumber;
    }

    public Guid GameId { get; }

    public GameConfiguration Configuration { get; }

    public DateTimeOffset StartedAt { get; }

    public GameStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public GuessResult SubmitGuess(string guess, DateTimeOffset submittedAt)
    {
        lock (_gate)
        {
            EnsureTimestampIsValid(submittedAt, nameof(submittedAt));
            EnsureActive(submittedAt);

            // Validate before mutating the attempt collection. Invalid input is
            // therefore guaranteed not to consume an attempt.
            ValidateNumber(guess, Configuration, nameof(guess));
            var score = Score(_secretNumber, guess);
            var attempt = new GameAttempt(_attempts.Count + 1, guess, score, submittedAt);

            _attempts.Add(attempt);

            if (score.ExactMatches == Configuration.NumberLength)
            {
                Complete(GameStatus.Won, submittedAt);
            }
            else if (_attempts.Count == Configuration.MaxAttempts)
            {
                Complete(GameStatus.Lost, submittedAt);
            }

            return new GuessResult(attempt, CreateSnapshot());
        }
    }

    public GameSnapshot GetSnapshot(DateTimeOffset observedAt)
    {
        lock (_gate)
        {
            EnsureTimestampIsValid(observedAt, nameof(observedAt));
            ApplyTimeout(observedAt);
            return CreateSnapshot();
        }
    }

    public GameSnapshot Abandon(DateTimeOffset abandonedAt)
    {
        lock (_gate)
        {
            EnsureTimestampIsValid(abandonedAt, nameof(abandonedAt));
            ApplyTimeout(abandonedAt);

            if (_status == GameStatus.Active)
            {
                Complete(GameStatus.Abandoned, abandonedAt);
            }

            return CreateSnapshot();
        }
    }

    /// <summary>
    /// Calculates a score without observing or changing game state.
    /// Duplicate digits are counted as a multiset after exact matches have been
    /// removed, so each secret digit can match at most one guessed digit.
    /// </summary>
    public static GuessScore Score(string secretNumber, string guess)
    {
        ValidateScoringInput(secretNumber, nameof(secretNumber));
        ValidateScoringInput(guess, nameof(guess));

        if (secretNumber.Length != guess.Length)
        {
            throw new ArgumentException("The secret and guess must have the same length.", nameof(guess));
        }

        var exactMatches = 0;
        Span<int> remainingSecretDigits = stackalloc int[10];
        Span<int> remainingGuessDigits = stackalloc int[10];

        for (var index = 0; index < secretNumber.Length; index++)
        {
            if (secretNumber[index] == guess[index])
            {
                exactMatches++;
                continue;
            }

            remainingSecretDigits[secretNumber[index] - '0']++;
            remainingGuessDigits[guess[index] - '0']++;
        }

        var misplacedMatches = 0;
        for (var digit = 0; digit < remainingSecretDigits.Length; digit++)
        {
            misplacedMatches += Math.Min(remainingSecretDigits[digit], remainingGuessDigits[digit]);
        }

        return new GuessScore(exactMatches, misplacedMatches);
    }

    private static void ValidateNumber(
        string number,
        GameConfiguration configuration,
        string parameterName)
    {
        ValidateScoringInput(number, parameterName);

        if (number.Length != configuration.NumberLength)
        {
            throw new ArgumentException(
                $"The number must contain exactly {configuration.NumberLength} digits.",
                parameterName);
        }

        if (!configuration.AllowDuplicateDigits && ContainsDuplicateDigits(number))
        {
            throw new ArgumentException("Duplicate digits are not allowed.", parameterName);
        }
    }

    private static void ValidateScoringInput(string number, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(number, parameterName);

        if (number.Length == 0)
        {
            throw new ArgumentException("A number cannot be empty.", parameterName);
        }

        for (var index = 0; index < number.Length; index++)
        {
            if (number[index] is < '0' or > '9')
            {
                throw new ArgumentException("A number may contain ASCII digits only.", parameterName);
            }
        }
    }

    private static bool ContainsDuplicateDigits(string number)
    {
        Span<bool> seen = stackalloc bool[10];
        foreach (var character in number)
        {
            var digit = character - '0';
            if (seen[digit])
            {
                return true;
            }

            seen[digit] = true;
        }

        return false;
    }

    private void EnsureActive(DateTimeOffset observedAt)
    {
        ApplyTimeout(observedAt);

        if (_status != GameStatus.Active)
        {
            throw new InvalidOperationException($"A {_status} game cannot accept guesses.");
        }
    }

    private void ApplyTimeout(DateTimeOffset observedAt)
    {
        if (_status == GameStatus.Active && observedAt - StartedAt >= Configuration.Timeout)
        {
            Complete(GameStatus.TimedOut, observedAt);
        }
    }

    private void EnsureTimestampIsValid(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp < StartedAt)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                timestamp,
                "A game operation cannot occur before the game started.");
        }
    }

    private void Complete(GameStatus status, DateTimeOffset endedAt)
    {
        _status = status;
        _endedAt = endedAt;
    }

    private GameSnapshot CreateSnapshot() =>
        new(
            GameId,
            Configuration,
            _status,
            StartedAt,
            _endedAt,
            _attempts,
            _status == GameStatus.Active ? null : _secretNumber);
}
