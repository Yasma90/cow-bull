using CowBull.Application.Games;
using CowBull.Domain.Games;
using CowBull.Infrastructure.Networking;

namespace CowBull.Architecture.Tests;

public sealed class DependencyRuleTests
{
    [Fact]
    public void Domain_has_no_dependencies_on_outer_cowbull_layers()
    {
        string[] references = CowBullReferences(typeof(GameSession).Assembly);

        Assert.Empty(references);
    }

    [Fact]
    public void Application_depends_only_on_domain()
    {
        string[] references = CowBullReferences(typeof(GameService).Assembly);

        Assert.Equal(["CowBull.Domain"], references);
    }

    [Fact]
    public void Infrastructure_depends_only_on_application_and_domain()
    {
        string[] references = CowBullReferences(typeof(AsyncTcpServer).Assembly);

        Assert.Equal(["CowBull.Application", "CowBull.Domain"], references);
    }

    private static string[] CowBullReferences(System.Reflection.Assembly assembly) =>
        assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name?.StartsWith("CowBull.", StringComparison.Ordinal) == true)
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();
}
