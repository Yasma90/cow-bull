using CowBull.Infrastructure.Identity;

namespace CowBull.Infrastructure.Tests.Identity;

public sealed class GuidGameIdGeneratorTests
{
    [Fact]
    public void Create_returns_non_empty_unique_identifiers()
    {
        var generator = new GuidGameIdGenerator();

        Guid[] identifiers = Enumerable.Range(0, 100)
            .Select(_ => generator.Create())
            .ToArray();

        Assert.DoesNotContain(Guid.Empty, identifiers);
        Assert.Equal(identifiers.Length, identifiers.Distinct().Count());
    }
}
