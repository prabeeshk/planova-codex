using Planova.Projects.Domain;

namespace Planova.UnitTests;

public sealed class ProjectDomainTests
{
    [Fact]
    public void Project_starts_as_draft_and_normalises_code_and_delivery_method()
    {
        var project = Create(" p-001 ", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 25, "Agile");
        Assert.Equal("P-001", project.Code); Assert.Equal("agile", project.DeliveryMethod); Assert.Equal("draft", project.LifecycleStatus);
    }

    [Fact]
    public void Project_rejects_invalid_dates_progress_and_delivery_method()
    {
        Assert.Equal("invalid_project_dates", Assert.Throws<ProjectRuleException>(() => Create("P-1", new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), 0, "Agile")).Code);
        Assert.Equal("invalid_project_progress", Assert.Throws<ProjectRuleException>(() => Create("P-2", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), 101, "Agile")).Code);
        Assert.Equal("invalid_delivery_method", Assert.Throws<ProjectRuleException>(() => Create("P-3", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), 0, "Scrum")).Code);
    }

    private static Project Create(string code, DateOnly start, DateOnly end, decimal progress, string delivery) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), code, "Project", null, Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), "delivery", delivery, "high", start, end, progress, "AED", "key", new string('A', 64),
            Guid.CreateVersion7(), DateTimeOffset.UtcNow);
}
