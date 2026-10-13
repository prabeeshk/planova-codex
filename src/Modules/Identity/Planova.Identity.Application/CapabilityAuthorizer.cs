using Planova.SharedKernel.Identity;
using Planova.SharedKernel.Security;
using Planova.SharedKernel.Time;

namespace Planova.Identity.Application;

public sealed class CapabilityAuthorizer(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IPermissionGrantStore grantStore,
    IAuthorizationScopeResolver scopeResolver,
    IClock clock) : ICapabilityAuthorizer
{
    public async Task<AuthorizationDecision> AuthorizeAsync(
        PermissionCode permission,
        AuthorizationResource resource,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId)
        {
            return AuthorizationDecision.Deny("authentication_required");
        }

        if (!tenantContext.HasOrganisation || tenantContext.RequireOrganisationId() != resource.OrganisationId)
        {
            return AuthorizationDecision.Deny("cross_tenant_access_denied");
        }

        var grants = await grantStore.GetGrantsAsync(resource.OrganisationId, userId, permission, cancellationToken);
        var applicable = new List<PermissionGrant>();
        foreach (var grant in grants.Where(grant => grant.IsValidAt(clock.UtcNow)))
        {
            if (await MatchesAsync(grant, resource, userId, cancellationToken)) applicable.Add(grant);
        }

        if (applicable.Any(grant => grant.Effect == GrantEffect.Deny))
        {
            return AuthorizationDecision.Deny("explicit_deny");
        }

        return applicable.Any(grant => grant.Effect == GrantEffect.Allow)
            ? AuthorizationDecision.Allow()
            : AuthorizationDecision.Deny("permission_not_granted");
    }

    private async Task<bool> MatchesAsync(PermissionGrant grant, AuthorizationResource resource, Guid userId,
        CancellationToken cancellationToken) => grant.Scope switch
        {
            PermissionScope.Organisation => true,
            PermissionScope.Self => resource.OwnerUserId == userId,
            PermissionScope.Assigned => resource.AssignedUserId == userId,
            PermissionScope.Department when resource.RequiredScope == PermissionScope.Department =>
                await scopeResolver.IsDepartmentWithinAsync(grant.ScopeReferenceId, resource.ScopeReferenceId, cancellationToken),
            PermissionScope.Team or PermissionScope.Project or PermissionScope.Portfolio => false,
            _ => grant.Scope == resource.RequiredScope && grant.ScopeReferenceId == resource.ScopeReferenceId
        };
}
