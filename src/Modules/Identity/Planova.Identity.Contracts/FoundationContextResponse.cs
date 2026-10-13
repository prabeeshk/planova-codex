namespace Planova.Identity.Contracts;

public sealed record FoundationContextResponse(
    Guid UserId,
    string DisplayName,
    Guid OrganisationId,
    IReadOnlyCollection<string> EffectivePermissions,
    string CorrelationId,
    string ApplicationVersion,
    string DependencyHealth);
