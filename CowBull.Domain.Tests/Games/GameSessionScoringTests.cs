using CowBull.Domain.Games;
using Xunit;

namespace CowBull.Domain.Tests.Games;

public sealed class GameSessionScoringTests
{
    [Theory]
    [InlineData("1234", "1234", 4, 0)]
    [InlineData("1234", "4321", 0, 4)]
    [InlineData("1234", "1567", 1, 0)]
    [InlineData("0012", "0100", 1, 2)]
    [InlineData("1122", "2211", 0, 4)]
    [InlineData("1122", "1111", 2, 0)]
    public void Score_counts_exact_and_multiset_misplaced_matches(
        string secret,
        string guess,
        int expectedExact,
        int expectedMisplaced)
    {
        var score = GameSession.Score(secret, guess);

        Assert.Equal(expectedExact, score.ExactMatches);
        Assert.Equal(expectedMisplaced, score.MisplacedMatches);
    }

    [Fact]
    public void Score_rejects_unicode_digits()
    {
        Assert.Throws<ArgumentException>(() => GameSession.Score("1234", "١٢٣٤"));
    }
}
