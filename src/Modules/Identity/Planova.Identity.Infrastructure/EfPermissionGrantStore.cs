using Microsoft.EntityFrameworkCore;
using Planova.Identity.Application;
using Planova.SharedKernel.Security;
using Planova.SharedKernel.Time;

namespace Planova.Identity.Infrastructure;

public sealed class EfPermissionGrantStore(IdentityDbContext dbContext, IClock clock) : IPermissionGrantStore
{
    public async Task<IReadOnlyCollection<PermissionGrant>> GetGrantsAsync(
        Guid organisationId, Guid userId, PermissionCode permission, CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(x => x.Id == userId && x.OrganisationId == organisationId && x.Status == "active", cancellationToken))
            return [];

        var permissionDefinition = await dbContext.Permissions
            .FirstOrDefaultAsync(x => x.Code == permission.Value && x.IsImplemented, cancellationToken);
        var grants = new List<PermissionGrant>();
        if (permissionDefinition is not null)
            grants.AddRange(await GetRoleGrantsAsync(organisationId, userId, permissionDefinition.Id, permission, cancellationToken));

        grants.AddRange(await dbContext.UserPermissionGrants
            .Where(x => x.OrganisationId == organisationId && x.UserId == userId && x.PermissionCode == permission.Value)
            .Select(x => new PermissionGrant(PermissionCode.Create(x.PermissionCode), x.Scope, x.ScopeReferenceId,
                x.Effect, x.ValidFrom, x.ValidTo)).ToArrayAsync(cancellationToken));

        if (permissionDefinition is not null)
        {
            var now = clock.UtcNow;
            var delegations = await dbContext.Delegations
                .Where(x => x.OrganisationId == organisationId && x.DelegateUserId == userId && x.Status == "active"
                    && x.RevokedAt == null && x.StartsAt <= now && x.EndsAt > now)
                .ToArrayAsync(cancellationToken);
            foreach (var delegation in delegations.Where(x => x.PermissionCodes.Contains(permission.Value, StringComparer.Ordinal)))
            {
                var source = await GetRoleGrantsAsync(organisationId, delegation.DelegatorUserId,
                    permissionDefinition.Id, permission, cancellationToken);
                foreach (var sourceGrant in source.Where(x => x.Effect == GrantEffect.Allow && x.IsValidAt(now)))
                {
                    var intersection = await IntersectAsync(sourceGrant.Scope, sourceGrant.ScopeReferenceId,
                        delegation.Scope, delegation.ScopeReferenceId, cancellationToken);
                    if (intersection.HasValue)
                        grants.Add(new PermissionGrant(permission, intersection.Scope, intersection.ReferenceId,
                            GrantEffect.Allow, delegation.StartsAt, delegation.EndsAt));
                }
            }
        }
        return grants;
    }

    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionCodesAsync(
        Guid organisationId, Guid userId, CancellationToken cancellationToken)
    {
        var codes = await dbContext.Permissions.Where(x => x.IsImplemented).Select(x => x.Code).ToArrayAsync(cancellationToken);
        var legacy = await dbContext.UserPermissionGrants.Where(x => x.OrganisationId == organisationId && x.UserId == userId)
            .Select(x => x.PermissionCode).Distinct().ToArrayAsync(cancellationToken);
        var effective = new List<string>();
        foreach (var code in codes.Concat(legacy).Distinct(StringComparer.Ordinal))
        {
            var grants = await GetGrantsAsync(organisationId, userId, PermissionCode.Create(code), cancellationToken);
            var valid = grants.Where(x => x.IsValidAt(clock.UtcNow)).ToArray();
            if (valid.Any(x => x.Effect == GrantEffect.Allow) && !valid.Any(x => x.Effect == GrantEffect.Deny)) effective.Add(code);
        }
        return effective.Order(StringComparer.Ordinal).ToArray();
    }

    private async Task<IReadOnlyCollection<PermissionGrant>> GetRoleGrantsAsync(Guid organisationId, Guid userId,
        Guid permissionId, PermissionCode permission, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var groupIds = await dbContext.GroupMembers
            .Where(x => x.OrganisationId == organisationId && x.UserId == userId && x.RevokedAt == null
                && x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now))
            .Select(x => x.GroupId).ToArrayAsync(cancellationToken);
        var assignments = await dbContext.PrincipalRoleAssignments
            .Where(x => x.OrganisationId == organisationId && x.RevokedAt == null && x.ValidFrom <= now
                && (x.ValidTo == null || x.ValidTo > now)
                && ((x.PrincipalType == "user" && x.PrincipalId == userId)
                    || (x.PrincipalType == "group" && groupIds.Contains(x.PrincipalId))))
            .ToArrayAsync(cancellationToken);
        if (assignments.Length == 0) return [];
        var roleIds = assignments.Select(x => x.RoleId).Distinct().ToArray();
        var activeRoleIds = await dbContext.Roles.Where(x => roleIds.Contains(x.Id) && x.Status == "active")
            .Select(x => x.Id).ToArrayAsync(cancellationToken);
        var roleGrants = await dbContext.RoleGrants.Where(x => activeRoleIds.Contains(x.RoleId) && x.PermissionId == permissionId)
            .ToArrayAsync(cancellationToken);
        var result = new List<PermissionGrant>();
        foreach (var assignment in assignments.Where(x => activeRoleIds.Contains(x.RoleId)))
        foreach (var grant in roleGrants.Where(x => x.RoleId == assignment.RoleId))
        {
            var intersection = await IntersectAsync(grant.Scope, grant.ScopeReferenceId, assignment.Scope,
                assignment.ScopeReferenceId, cancellationToken);
            if (intersection.HasValue)
                result.Add(new PermissionGrant(permission, intersection.Scope, intersection.ReferenceId, grant.Effect,
                    assignment.ValidFrom, assignment.ValidTo));
        }
        return result;
    }

    private async Task<(bool HasValue, PermissionScope Scope, Guid? ReferenceId)> IntersectAsync(
        PermissionScope leftScope, Guid? leftRef, PermissionScope rightScope, Guid? rightRef, CancellationToken cancellationToken)
    {
        var departments = await dbContext.OrganisationUnits.Select(x => new { x.Id, x.Path }).ToArrayAsync(cancellationToken);
        var paths = departments.ToDictionary(x => x.Id, x => x.Path);
        return ScopeIntersection.Intersect(leftScope, leftRef, rightScope, rightRef,
            (child, ancestor) => paths.TryGetValue(child, out var childPath) && paths.TryGetValue(ancestor, out var ancestorPath)
                && childPath.StartsWith(ancestorPath, StringComparison.Ordinal));
    }
}

public sealed class EfAuthorizationScopeResolver(IdentityDbContext dbContext) : IAuthorizationScopeResolver
{
    public async Task<bool> IsDepartmentWithinAsync(Guid? authorityDepartmentId, Guid? resourceDepartmentId,
        CancellationToken cancellationToken)
    {
        if (!authorityDepartmentId.HasValue || !resourceDepartmentId.HasValue) return false;
        var authorityPath = await dbContext.OrganisationUnits.Where(x => x.Id == authorityDepartmentId)
            .Select(x => x.Path).SingleOrDefaultAsync(cancellationToken);
        var resourcePath = await dbContext.OrganisationUnits.Where(x => x.Id == resourceDepartmentId)
            .Select(x => x.Path).SingleOrDefaultAsync(cancellationToken);
        return authorityPath is not null && resourcePath is not null
            && resourcePath.StartsWith(authorityPath, StringComparison.Ordinal);
    }
}
