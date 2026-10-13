namespace Planova.SharedKernel.Identity;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    string? ExternalSubject { get; }
    string? DisplayName { get; }
}

public interface ICurrentOrganisation
{
    Guid? OrganisationId { get; }
    bool HasOrganisation { get; }
}

public interface ITenantContext : ICurrentOrganisation
{
    Guid RequireOrganisationId();
}
