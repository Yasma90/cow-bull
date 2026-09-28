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

    [Fact]
    public void Domain_has_no_ui_network_serialization_or_logging_dependencies()
    {
        string[] forbidden =
        [
            "System.Text.Json",
            "System.Net.Sockets",
            "System.Net.Http",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Logging.Abstractions",
            "PresentationFramework",
            "PresentationCore",
            "WindowsBase",
            "System.Windows.Forms",
        ];
        string[] references = typeof(GameSession).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.DoesNotContain(references, reference => forbidden.Contains(reference, StringComparer.Ordinal));
    }

    [Fact]
    public void Client_project_cannot_compile_against_domain_or_application()
    {
        string project = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "CowBullClient.Modern", "CowBullClient.Modern.csproj"));

        Assert.DoesNotContain("CowBull.Domain", project, StringComparison.Ordinal);
        Assert.DoesNotContain("CowBull.Application", project, StringComparison.Ordinal);
        Assert.Contains(
            "<DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>",
            project,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CowBull.Modern.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be found.");
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
