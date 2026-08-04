using CowBull.Domain.Games;
using CowBull.Infrastructure.Games;

namespace CowBull.Infrastructure.Tests.Games;

public sealed class CryptographicSecretNumberGeneratorTests
{
    private readonly CryptographicSecretNumberGenerator _generator = new();

    [Fact]
    public void Generate_preserves_requested_length_and_ascii_digits()
    {
        var configuration = new GameConfiguration(32, 10, true, TimeSpan.FromMinutes(1));

        var secret = _generator.Generate(configuration);

        Assert.Equal(32, secret.Length);
        Assert.All(secret, character => Assert.InRange(character, '0', '9'));
    }

    [Fact]
    public void Generate_respects_unique_digit_policy()
    {
        var configuration = new GameConfiguration(10, 10, false, TimeSpan.FromMinutes(1));

        var secret = _generator.Generate(configuration);

        Assert.Equal(10, secret.Distinct().Count());
    }
}
