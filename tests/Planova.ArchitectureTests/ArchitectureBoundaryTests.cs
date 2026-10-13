using System.Xml.Linq;

namespace Planova.ArchitectureTests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void Domain_projects_do_not_reference_infrastructure_or_api()
    {
        var root = FindRepositoryRoot();
        var domainProjects = Directory.GetFiles(Path.Combine(root, "src", "Modules"), "*Domain.csproj", SearchOption.AllDirectories);

        Assert.NotEmpty(domainProjects);
        foreach (var project in domainProjects)
        {
            var references = XDocument.Load(project)
                .Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .ToArray();
            Assert.DoesNotContain(references, reference => reference.Contains("Infrastructure", StringComparison.Ordinal));
            Assert.DoesNotContain(references, reference => reference.Contains(".Api", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Source_contains_no_role_name_authorisation_checks()
    {
        var root = FindRepositoryRoot();
        var sources = Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories);

        foreach (var source in sources)
        {
            var text = File.ReadAllText(source);
            Assert.DoesNotContain("IsInRole(", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Authorize(Roles", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Module_projects_cannot_reference_another_modules_infrastructure()
    {
        var root = FindRepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src", "Modules"), "*.csproj", SearchOption.AllDirectories);

        foreach (var project in projects)
        {
            var ownModule = new DirectoryInfo(project).Parent?.Parent?.Name;
            var references = XDocument.Load(project).Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty);
            Assert.DoesNotContain(references, reference =>
                reference.Contains("Infrastructure", StringComparison.Ordinal) &&
                ownModule is not null && !reference.Contains(ownModule, StringComparison.Ordinal));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Planova.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
