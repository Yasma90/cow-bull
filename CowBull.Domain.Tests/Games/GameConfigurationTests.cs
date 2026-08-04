using CowBull.Domain.Games;
using Xunit;

namespace CowBull.Domain.Tests.Games;

public sealed class GameConfigurationTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(4, 0)]
    [InlineData(4, -1)]
    public void Constructor_rejects_non_positive_lengths_and_attempts(int numberLength, int maxAttempts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameConfiguration(numberLength, maxAttempts, false, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Constructor_rejects_more_than_ten_unique_digits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameConfiguration(11, 5, false, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Constructor_allows_more_than_ten_digits_when_duplicates_are_enabled()
    {
        var configuration = new GameConfiguration(11, 5, true, TimeSpan.FromMinutes(1));

        Assert.Equal(11, configuration.NumberLength);
    }

    [Fact]
    public void Constructor_rejects_non_positive_timeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GameConfiguration(4, 5, false, TimeSpan.Zero));
    }
}
