namespace Planova.Api.Security;

public sealed class DevelopmentAuthOptions
{
    public const string SectionName = "DevelopmentAuth";
    public bool Enabled { get; init; }
    public Dictionary<string, DevelopmentUser> Users { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DevelopmentUser
{
    public required Guid UserId { get; init; }
    public required Guid OrganisationId { get; init; }
    public required string Subject { get; init; }
    public required string DisplayName { get; init; }
}
