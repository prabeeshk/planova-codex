using Planova.SharedKernel.Security;

namespace Planova.Identity.Domain;

public sealed class UserPermissionGrant
{
    private UserPermissionGrant() { }

    public UserPermissionGrant(
        Guid id,
        Guid organisationId,
        Guid userId,
        string permissionCode,
        PermissionScope scope,
        Guid? scopeReferenceId,
        GrantEffect effect)
    {
        Id = id;
        OrganisationId = organisationId;
        UserId = userId;
        PermissionCode = permissionCode;
        Scope = scope;
        ScopeReferenceId = scopeReferenceId;
        Effect = effect;
    }

    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public Guid UserId { get; private set; }
    public string PermissionCode { get; private set; } = string.Empty;
    public PermissionScope Scope { get; private set; }
    public Guid? ScopeReferenceId { get; private set; }
    public GrantEffect Effect { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
}
