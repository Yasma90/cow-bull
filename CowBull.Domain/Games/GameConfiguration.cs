namespace CowBull.Domain.Games;

/// <summary>
/// Defines the invariant rules for a game.
/// </summary>
public sealed record GameConfiguration
{
    public GameConfiguration(
        int numberLength,
        int maxAttempts,
        bool allowDuplicateDigits,
        TimeSpan timeout)
    {
        if (numberLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numberLength),
                numberLength,
                "Number length must be greater than zero.");
        }

        if (!allowDuplicateDigits && numberLength > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numberLength),
                numberLength,
                "A number without duplicate digits cannot contain more than ten digits.");
        }

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts),
                maxAttempts,
                "Maximum attempts must be greater than zero.");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "Timeout must be greater than zero.");
        }

        NumberLength = numberLength;
        MaxAttempts = maxAttempts;
        AllowDuplicateDigits = allowDuplicateDigits;
        Timeout = timeout;
    }

    public int NumberLength { get; }

    public int MaxAttempts { get; }

    public bool AllowDuplicateDigits { get; }

    public TimeSpan Timeout { get; }
}
