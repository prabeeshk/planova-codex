namespace Planova.SharedKernel.Security;

public sealed record AuthorizationResource(
    Guid OrganisationId,
    PermissionScope RequiredScope = PermissionScope.Organisation,
    Guid? ScopeReferenceId = null,
    Guid? OwnerUserId = null,
    Guid? AssignedUserId = null);

public sealed record AuthorizationDecision(bool IsAllowed, string ReasonCode)
{
    public static AuthorizationDecision Allow() => new(true, "allowed");
    public static AuthorizationDecision Deny(string reasonCode) => new(false, reasonCode);
}

public interface ICapabilityAuthorizer
{
    Task<AuthorizationDecision> AuthorizeAsync(
        PermissionCode permission,
        AuthorizationResource resource,
        CancellationToken cancellationToken = default);
}
