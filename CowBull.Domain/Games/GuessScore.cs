namespace CowBull.Domain.Games;

/// <summary>
/// A score where exact matches are traditionally called bulls and misplaced
/// matches are traditionally called cows.
/// </summary>
public sealed record GuessScore
{
    public GuessScore(int exactMatches, int misplacedMatches)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exactMatches);
        ArgumentOutOfRangeException.ThrowIfNegative(misplacedMatches);

        ExactMatches = exactMatches;
        MisplacedMatches = misplacedMatches;
    }

    public int ExactMatches { get; }

    public int MisplacedMatches { get; }

    public int Bulls => ExactMatches;

    public int Cows => MisplacedMatches;
}
