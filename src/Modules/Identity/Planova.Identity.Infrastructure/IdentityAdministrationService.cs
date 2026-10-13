using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planova.Identity.Application;
using Planova.Identity.Contracts;
using Planova.Identity.Domain;
using Planova.SharedKernel.Identity;
using Planova.SharedKernel.Results;
using Planova.SharedKernel.Security;
using Planova.SharedKernel.Time;

namespace Planova.Identity.Infrastructure;

public sealed class IdentityAdministrationService(
    IdentityDbContext db,
    ICurrentUser currentUser,
    ITenantContext tenant,
    ICapabilityAuthorizer authorizer,
    IIdentityEvidenceWriter evidenceWriter,
    IAuthorizationCache authorizationCache,
    IClock clock) : IIdentityAdministrationService
{
    private static readonly string[] OrganisationSettingsFields = ["name", "defaultTimeZone", "defaultCurrency"];
    private static readonly string[] IdentityLinkFields = ["externalIssuer", "externalSubject"];

    private Guid OrganisationId => tenant.RequireOrganisationId();
    private Guid ActorId => currentUser.UserId ?? throw new InvalidOperationException("An authenticated user is required.");
    private DateTimeOffset Now => clock.UtcNow;

    public async Task<Result<OrganisationResponse>> GetOrganisationAsync(CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.SettingsManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<OrganisationResponse>(denied);
        var value = await db.Organisations.SingleOrDefaultAsync(x => x.Id == OrganisationId, cancellationToken);
        return value is null ? NotFound<OrganisationResponse>() : Success(ToResponse(value));
    }

    public async Task<Result<OrganisationResponse>> UpdateOrganisationAsync(UpdateOrganisationRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.SettingsManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<OrganisationResponse>(denied);
        var value = await db.Organisations.SingleOrDefaultAsync(x => x.Id == OrganisationId, cancellationToken);
        if (value is null) return NotFound<OrganisationResponse>();
        if (value.RowVersion != request.RowVersion) return Conflict<OrganisationResponse>();
        try { value.UpdateSettings(request.Name, request.DefaultTimeZone, request.DefaultCurrency, ActorId, Now); }
        catch (DomainRuleException exception) { return DomainFailure<OrganisationResponse>(exception); }
        await CommitAsync(new("organisation.settings.changed", "organisation", value.Id, "OrganisationSettingsChanged", 1,
            JsonSerializer.Serialize(new { fields = OrganisationSettingsFields }),
            JsonSerializer.Serialize(new { organisationId = value.Id })), cancellationToken);
        return Success(ToResponse(value));
    }

    public async Task<Result<IReadOnlyCollection<OrganisationUnitResponse>>> ListOrganisationUnitsAsync(CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.SettingsManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<IReadOnlyCollection<OrganisationUnitResponse>>(denied);
        return Success<IReadOnlyCollection<OrganisationUnitResponse>>(await db.OrganisationUnits.OrderBy(x => x.Path).ThenBy(x => x.Id).Select(x => ToResponse(x)).ToArrayAsync(cancellationToken));
    }

    public async Task<Result<OrganisationUnitResponse>> CreateOrganisationUnitAsync(CreateOrganisationUnitRequest request, CancellationToken cancellationToken)
    {
        OrganisationUnit? parent = null;
        if (request.ParentId.HasValue)
        {
            parent = await db.OrganisationUnits.SingleOrDefaultAsync(x => x.Id == request.ParentId, cancellationToken);
            if (parent is null) return NotFound<OrganisationUnitResponse>();
        }
        var resource = parent is null ? new AuthorizationResource(OrganisationId)
            : new AuthorizationResource(OrganisationId, PermissionScope.Department, parent.Id);
        var denied = await DeniedAsync(PermissionCodes.SettingsManage, resource, cancellationToken);
        if (denied is not null) return Failure<OrganisationUnitResponse>(denied);
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
#pragma warning disable CA1862 // The normalized persisted value requires translatable ordinal equality in the EF query.
        if (await db.OrganisationUnits.AnyAsync(x => x.Code == normalizedCode, cancellationToken))
#pragma warning restore CA1862
            return Failure<OrganisationUnitResponse>("organisation_unit_code_exists", "An organisation unit with this code already exists.");
        var id = Guid.CreateVersion7();
        try
        {
            var unit = new OrganisationUnit(id, OrganisationId, parent?.Id, request.Type, request.Code, request.Name,
                $"{parent?.Path ?? "/"}{id:D}/", ActorId, Now);
            db.OrganisationUnits.Add(unit);
            await CommitAsync(new("organisation_unit.created", "organisation_unit", id, "OrganisationUnitCreated", 1,
                JsonSerializer.Serialize(new { request.Code, request.Name, parentId = parent?.Id }),
                JsonSerializer.Serialize(new { organisationUnitId = id, parentId = parent?.Id })), cancellationToken);
            return Success(ToResponse(unit));
        }
        catch (DomainRuleException exception) { return DomainFailure<OrganisationUnitResponse>(exception); }
    }

    public async Task<Result<OrganisationUnitResponse>> MoveOrganisationUnitAsync(Guid id, MoveOrganisationUnitRequest request, CancellationToken cancellationToken)
    {
        var unit = await db.OrganisationUnits.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (unit is null) return NotFound<OrganisationUnitResponse>();
        if (unit.RowVersion != request.RowVersion) return Conflict<OrganisationUnitResponse>();
        OrganisationUnit? parent = null;
        if (request.ParentId.HasValue)
        {
            parent = await db.OrganisationUnits.SingleOrDefaultAsync(x => x.Id == request.ParentId, cancellationToken);
            if (parent is null) return NotFound<OrganisationUnitResponse>();
            if (parent.Path.StartsWith(unit.Path, StringComparison.Ordinal))
                return Failure<OrganisationUnitResponse>("organisation_hierarchy_cycle", "The move would create a hierarchy cycle.");
        }
        var resource = new AuthorizationResource(OrganisationId, PermissionScope.Department, unit.Id);
        var denied = await DeniedAsync(PermissionCodes.SettingsManage, resource, cancellationToken);
        if (denied is not null) return Failure<OrganisationUnitResponse>(denied);
        var oldPath = unit.Path;
        var newPath = $"{parent?.Path ?? "/"}{unit.Id:D}/";
        try
        {
            unit.Move(parent?.Id, newPath, ActorId, Now);
            var descendants = await db.OrganisationUnits.Where(x => x.Path.StartsWith(oldPath) && x.Id != id).ToArrayAsync(cancellationToken);
            foreach (var descendant in descendants)
                descendant.Move(descendant.ParentId, newPath + descendant.Path[oldPath.Length..], ActorId, Now);
        }
        catch (DomainRuleException exception) { return DomainFailure<OrganisationUnitResponse>(exception); }
        await CommitAsync(new("organisation_unit.moved", "organisation_unit", id, "OrganisationUnitMoved", 1,
            JsonSerializer.Serialize(new { fromParentId = unit.ParentId, toParentId = parent?.Id }),
            JsonSerializer.Serialize(new { organisationUnitId = id, parentId = parent?.Id })), cancellationToken);
        return Success(ToResponse(unit));
    }

    public async Task<Result<PagedResponse<UserResponse>>> ListUsersAsync(string? search, int take, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.UserManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<PagedResponse<UserResponse>>(denied);
        take = Math.Clamp(take, 1, 100);
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";
            query = query.Where(x => EF.Functions.ILike(x.DisplayName, pattern, "\\")
                || EF.Functions.ILike(x.Email, pattern, "\\"));
        }
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Take(take).Select(x => ToResponse(x)).ToArrayAsync(cancellationToken);
        return Success(new PagedResponse<UserResponse>(items, count));
    }

    public async Task<Result<UserResponse>> GetUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.UserManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<UserResponse>(denied);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return user is null ? NotFound<UserResponse>() : Success(ToResponse(user));
    }

    public async Task<Result<UserResponse>> ProvisionUserAsync(ProvisionUserRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.UserManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<UserResponse>(denied);
        if (await db.Users.AnyAsync(x => x.ExternalIssuer == request.ExternalIssuer && x.ExternalSubject == request.ExternalSubject, cancellationToken))
            return Failure<UserResponse>("identity_link_exists", "This enterprise identity is already linked.");
        OrganisationUnit? unit = null;
        if (request.OrganisationUnitId.HasValue)
        {
            unit = await db.OrganisationUnits.SingleOrDefaultAsync(x => x.Id == request.OrganisationUnitId, cancellationToken);
            if (unit is null) return NotFound<UserResponse>();
            denied = await DeniedAsync(PermissionCodes.UserManage,
                new AuthorizationResource(OrganisationId, PermissionScope.Department, unit.Id), cancellationToken);
            if (denied is not null) return Failure<UserResponse>(denied);
        }
        try
        {
            var user = new User(Guid.CreateVersion7(), OrganisationId, request.ExternalIssuer, request.ExternalSubject,
                request.Email, request.DisplayName, request.JobTitle, request.TimeZone, request.Locale, ActorId);
            db.Users.Add(user);
            if (unit is not null)
                db.UserOrganisationUnitMemberships.Add(new UserOrganisationUnitMembership(Guid.CreateVersion7(), OrganisationId,
                    user.Id, unit.Id, request.EffectiveFrom ?? DateOnly.FromDateTime(Now.UtcDateTime), null, true, ActorId, Now));
            await CommitAsync(new("user.provisioned", "user", user.Id, "UserProvisioned", 1,
                JsonSerializer.Serialize(new { request.DisplayName, request.Email, organisationUnitId = unit?.Id }),
                JsonSerializer.Serialize(new { userId = user.Id, organisationUnitId = unit?.Id })), cancellationToken);
            return Success(ToResponse(user));
        }
        catch (DomainRuleException exception) { return DomainFailure<UserResponse>(exception); }
    }

    public async Task<Result<UserResponse>> ChangeUserStatusAsync(Guid id, string command, LifecycleRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.UserManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<UserResponse>(denied);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return NotFound<UserResponse>();
        if (user.RowVersion != request.RowVersion) return Conflict<UserResponse>();
        if (command is "suspend" or "deactivate" && user.IsEmergencyAdministrator
            && await db.Users.CountAsync(x => x.IsEmergencyAdministrator && x.Status == "active", cancellationToken) <= 1)
            return Failure<UserResponse>("last_emergency_administrator", "The last active emergency administrator cannot be suspended or deactivated.");
        switch (command)
        {
            case "suspend": user.Suspend(ActorId, Now); break;
            case "deactivate": user.Deactivate(ActorId, Now); break;
            case "reactivate": user.Reactivate(ActorId, Now); break;
            default: return Failure<UserResponse>("invalid_lifecycle_command", "The lifecycle command is not supported.");
        }
        await CommitAsync(new($"user.{command}", "user", id, $"User{char.ToUpperInvariant(command[0])}{command[1..]}", 1,
            JsonSerializer.Serialize(new { status = user.Status }), JsonSerializer.Serialize(new { userId = id, status = user.Status }), request.Reason), cancellationToken);
        authorizationCache.InvalidateOrganisation(OrganisationId);
        return Success(ToResponse(user));
    }

    public async Task<Result<UserResponse>> ChangeIdentityLinkAsync(Guid id, ChangeIdentityLinkRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.UserManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<UserResponse>(denied);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return NotFound<UserResponse>();
        if (user.RowVersion != request.RowVersion) return Conflict<UserResponse>();
        if (await db.Users.AnyAsync(x => x.Id != id && x.ExternalIssuer == request.ExternalIssuer && x.ExternalSubject == request.ExternalSubject, cancellationToken))
            return Failure<UserResponse>("identity_link_exists", "This enterprise identity is already linked.");
        try { user.ChangeIdentityLink(request.ExternalIssuer, request.ExternalSubject, ActorId, Now); }
        catch (DomainRuleException exception) { return DomainFailure<UserResponse>(exception); }
        await CommitAsync(new("user.identity_link.changed", "user", id, "UserIdentityLinkChanged", 1,
            JsonSerializer.Serialize(new { fields = IdentityLinkFields }),
            JsonSerializer.Serialize(new { userId = id }), request.Reason), cancellationToken);
        authorizationCache.InvalidateOrganisation(OrganisationId);
        return Success(ToResponse(user));
    }

    public async Task<Result<PagedResponse<GroupResponse>>> ListGroupsAsync(string? search, int take, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.UserManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<PagedResponse<GroupResponse>>(denied);
        take = Math.Clamp(take, 1, 100);
        var query = db.Groups.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search.Trim())}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern, "\\"));
        }
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Take(take).Select(x => ToResponse(x)).ToArrayAsync(cancellationToken);
        return Success(new PagedResponse<GroupResponse>(items, count));
    }

    public async Task<Result<GroupResponse>> CreateGroupAsync(CreateGroupRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.UserManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<GroupResponse>(denied);
        try
        {
            var group = new Group(Guid.CreateVersion7(), OrganisationId, request.Name, request.Type, request.ExternalKey, ActorId, Now);
            db.Groups.Add(group);
            await CommitAsync(new("group.created", "group", group.Id, "GroupCreated", 1,
                JsonSerializer.Serialize(new { request.Name, request.Type }), JsonSerializer.Serialize(new { groupId = group.Id })), cancellationToken);
            return Success(ToResponse(group));
        }
        catch (DomainRuleException exception) { return DomainFailure<GroupResponse>(exception); }
    }

    public async Task<Result<bool>> AddGroupMemberAsync(Guid groupId, AddGroupMemberRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.UserManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<bool>(denied);
        var group = await db.Groups.SingleOrDefaultAsync(x => x.Id == groupId && x.Status == "active", cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (group is null || user is null) return NotFound<bool>();
        if (user.Status != "active") return Failure<bool>("inactive_principal", "Suspended or deactivated users cannot receive active membership.");
        if (!await CanManageGroupAuthorityAsync(groupId, cancellationToken)) return Failure<bool>("grant_authority_exceeded", "Group membership would grant authority beyond your own.");
        var start = request.ValidFrom ?? Now;
        if (await db.GroupMembers.AnyAsync(x => x.GroupId == groupId && x.UserId == request.UserId && x.RevokedAt == null
            && (x.ValidTo == null || x.ValidTo > start), cancellationToken))
            return Failure<bool>("duplicate_active_membership", "An active membership already exists.");
        try { db.GroupMembers.Add(new GroupMember(Guid.CreateVersion7(), OrganisationId, groupId, user.Id, start, request.ValidTo, request.Source, ActorId, Now)); }
        catch (DomainRuleException exception) { return DomainFailure<bool>(exception); }
        await CommitAsync(new("group.membership.added", "group", groupId, "GroupMembershipChanged", 1,
            JsonSerializer.Serialize(new { userId = user.Id, change = "added" }), JsonSerializer.Serialize(new { groupId, userId = user.Id, change = "added" })), cancellationToken);
        authorizationCache.InvalidateOrganisation(OrganisationId);
        return Success(true);
    }

    public async Task<Result<bool>> RevokeGroupMemberAsync(Guid groupId, Guid userId, RevokeRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.UserManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<bool>(denied);
        var member = await db.GroupMembers.Where(x => x.GroupId == groupId && x.UserId == userId && x.RevokedAt == null)
            .OrderByDescending(x => x.ValidFrom).FirstOrDefaultAsync(cancellationToken);
        if (member is null) return NotFound<bool>();
        member.Revoke(ActorId, Now);
        await CommitAsync(new("group.membership.revoked", "group", groupId, "GroupMembershipChanged", 1,
            JsonSerializer.Serialize(new { userId, change = "revoked" }), JsonSerializer.Serialize(new { groupId, userId, change = "revoked" }), request.Reason), cancellationToken);
        authorizationCache.InvalidateOrganisation(OrganisationId);
        return Success(true);
    }

    public async Task<Result<IReadOnlyCollection<PermissionResponse>>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<IReadOnlyCollection<PermissionResponse>>(denied);
        var permissions = await db.Permissions.AsNoTracking().OrderBy(x => x.Code).ToArrayAsync(cancellationToken);
        var scopes = await db.PermissionScopeTypes.AsNoTracking().ToArrayAsync(cancellationToken);
        return Success<IReadOnlyCollection<PermissionResponse>>(permissions.Select(x => new PermissionResponse(x.Id, x.Code, x.Description, x.RiskLevel, x.IsImplemented,
            scopes.Where(y => y.PermissionId == x.Id).Select(y => y.Scope).ToArray())).ToArray());
    }

    public async Task<Result<IReadOnlyCollection<RoleResponse>>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<IReadOnlyCollection<RoleResponse>>(denied);
        var roles = await db.Roles.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).ToArrayAsync(cancellationToken);
        var grants = await db.RoleGrants.AsNoTracking().ToArrayAsync(cancellationToken);
        var permissions = await db.Permissions.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
        return Success<IReadOnlyCollection<RoleResponse>>(roles.Select(role => ToResponse(role, grants.Where(x => x.RoleId == role.Id), permissions)).ToArray());
    }

    public async Task<Result<RoleResponse>> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<RoleResponse>(denied);
        try
        {
            var role = new Role(Guid.CreateVersion7(), OrganisationId, request.Code, request.Name, request.Description, false, ActorId, Now);
            db.Roles.Add(role);
            await CommitAsync(new("role.created", "role", role.Id, "RoleChanged", 1,
                JsonSerializer.Serialize(new { request.Code, request.Name }), JsonSerializer.Serialize(new { roleId = role.Id, change = "created" })), cancellationToken);
            return Success(ToResponse(role, [], new Dictionary<Guid, string>()));
        }
        catch (DomainRuleException exception) { return DomainFailure<RoleResponse>(exception); }
    }

    public async Task<Result<RoleResponse>> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<RoleResponse>(denied);
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (role is null) return NotFound<RoleResponse>();
        if (role.RowVersion != request.RowVersion) return Conflict<RoleResponse>();
        role.Update(request.Name, request.Description, ActorId, Now);
        await CommitAsync(new("role.changed", "role", role.Id, "RoleChanged", 1,
            JsonSerializer.Serialize(new { request.Name }), JsonSerializer.Serialize(new { roleId = role.Id, change = "updated" })), cancellationToken);
        authorizationCache.InvalidateOrganisation(OrganisationId);
        return Success(ToResponse(role, [], new Dictionary<Guid, string>()));
    }

    public async Task<Result<RoleGrantResponse>> AddRoleGrantAsync(Guid roleId, AddRoleGrantRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<RoleGrantResponse>(denied);
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == roleId, cancellationToken);
        var permission = await db.Permissions.SingleOrDefaultAsync(x => x.Code == request.PermissionCode, cancellationToken);
        if (role is null || permission is null) return NotFound<RoleGrantResponse>();
        if (!await db.PermissionScopeTypes.AnyAsync(x => x.PermissionId == permission.Id && x.Scope == request.Scope, cancellationToken))
            return Failure<RoleGrantResponse>("scope_not_permitted", "The permission does not support this scope.");
        if (!await CanGrantAsync(request.PermissionCode, request.Scope, request.ScopeReferenceId, cancellationToken))
            return Failure<RoleGrantResponse>("grant_authority_exceeded", "You cannot grant this permission or scope.");
        try
        {
            var grant = new RoleGrant(Guid.CreateVersion7(), OrganisationId, roleId, permission.Id, request.Effect,
                request.Scope, request.ScopeReferenceId, request.ConditionsJson, ActorId, Now);
            db.RoleGrants.Add(grant);
            await CommitAsync(new("role.permission_grant.changed", "role", role.Id, "RoleGrantChanged", 1,
                JsonSerializer.Serialize(new { request.PermissionCode, request.Effect, request.Scope, request.ScopeReferenceId, change = "added" }),
                JsonSerializer.Serialize(new { roleId, permissionCode = request.PermissionCode, change = "added" })), cancellationToken);
            authorizationCache.InvalidateOrganisation(OrganisationId);
            return Success(new RoleGrantResponse(grant.Id, permission.Code, grant.Effect, grant.Scope, grant.ScopeReferenceId));
        }
        catch (DomainRuleException exception) { return DomainFailure<RoleGrantResponse>(exception); }
    }

    public async Task<Result<bool>> RemoveRoleGrantAsync(Guid roleId, Guid grantId, RevokeRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<bool>(denied);
        var grant = await db.RoleGrants.SingleOrDefaultAsync(x => x.Id == grantId && x.RoleId == roleId, cancellationToken);
        if (grant is null) return NotFound<bool>();
        db.RoleGrants.Remove(grant);
        await CommitAsync(new("role.permission_grant.changed", "role", roleId, "RoleGrantChanged", 1,
            JsonSerializer.Serialize(new { grantId, change = "removed" }), JsonSerializer.Serialize(new { roleId, grantId, change = "removed" }), request.Reason), cancellationToken);
        authorizationCache.InvalidateOrganisation(OrganisationId);
        return Success(true);
    }

    public async Task<Result<RoleAssignmentResponse>> CreateAssignmentAsync(CreateRoleAssignmentRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<RoleAssignmentResponse>(denied);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            return Failure<RoleAssignmentResponse>("idempotency_key_required", "A valid Idempotency-Key header is required.");
        var existing = await db.PrincipalRoleAssignments.SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) return Success(ToResponse(existing));
        var principalExists = request.PrincipalType == "user"
            ? await db.Users.AnyAsync(x => x.Id == request.PrincipalId && x.Status == "active", cancellationToken)
            : request.PrincipalType == "group" && await db.Groups.AnyAsync(x => x.Id == request.PrincipalId && x.Status == "active", cancellationToken);
        if (!principalExists) return Failure<RoleAssignmentResponse>("invalid_principal", "The active principal was not found.");
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == request.RoleId && x.Status == "active", cancellationToken);
        if (role is null) return NotFound<RoleAssignmentResponse>();
        if (!await CanAssignRoleAsync(role.Id, request.Scope, request.ScopeReferenceId, cancellationToken))
            return Failure<RoleAssignmentResponse>("grant_authority_exceeded", "This role assignment exceeds your grant authority.");
        try
        {
            var assignment = new PrincipalRoleAssignment(Guid.CreateVersion7(), OrganisationId, request.PrincipalType,
                request.PrincipalId, request.RoleId, request.Scope, request.ScopeReferenceId, request.ValidFrom ?? Now,
                request.ValidTo, ActorId, request.Reason, idempotencyKey, Now);
            db.PrincipalRoleAssignments.Add(assignment);
            await CommitAsync(new("role.assignment.created", "principal_role_assignment", assignment.Id, "RoleAssignmentChanged", 1,
                JsonSerializer.Serialize(new { request.PrincipalType, request.PrincipalId, request.RoleId, request.Scope, request.ScopeReferenceId }),
                JsonSerializer.Serialize(new { assignmentId = assignment.Id, change = "created" }), request.Reason), cancellationToken);
            authorizationCache.InvalidateOrganisation(OrganisationId);
            return Success(ToResponse(assignment));
        }
        catch (DomainRuleException exception) { return DomainFailure<RoleAssignmentResponse>(exception); }
    }

    public async Task<Result<bool>> RevokeAssignmentAsync(Guid id, RevokeRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<bool>(denied);
        var assignment = await db.PrincipalRoleAssignments.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (assignment is null) return NotFound<bool>();
        if (assignment.RowVersion != request.RowVersion) return Conflict<bool>();
        if (assignment.PrincipalType == "user")
        {
            var target = await db.Users.SingleOrDefaultAsync(x => x.Id == assignment.PrincipalId, cancellationToken);
            if (target?.IsEmergencyAdministrator == true && target.Status == "active"
                && await db.Users.CountAsync(x => x.IsEmergencyAdministrator && x.Status == "active", cancellationToken) <= 1)
                return Failure<bool>("last_emergency_administrator", "The last emergency administrator's authority cannot be removed through normal administration.");
        }
        assignment.Revoke(ActorId, Now);
        await CommitAsync(new("role.assignment.revoked", "principal_role_assignment", id, "RoleAssignmentChanged", 1,
            JsonSerializer.Serialize(new { change = "revoked" }), JsonSerializer.Serialize(new { assignmentId = id, change = "revoked" }), request.Reason), cancellationToken);
        authorizationCache.InvalidateOrganisation(OrganisationId);
        return Success(true);
    }

    public async Task<Result<EffectiveAccessResponse>> GetEffectiveAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var resource = userId == ActorId
            ? new AuthorizationResource(OrganisationId, PermissionScope.Self, OwnerUserId: userId)
            : new AuthorizationResource(OrganisationId);
        var required = userId == ActorId ? PermissionCodes.FoundationView : PermissionCodes.RoleManage;
        var denied = await DeniedAsync(required, resource, cancellationToken);
        if (denied is not null) return Failure<EffectiveAccessResponse>(denied);
        if (!await db.Users.AnyAsync(x => x.Id == userId, cancellationToken)) return NotFound<EffectiveAccessResponse>();
        var grants = await ExplainAccessAsync(userId, cancellationToken);
        return Success(new EffectiveAccessResponse(userId, grants));
    }

    public async Task<Result<IReadOnlyCollection<DelegationResponse>>> ListDelegationsAsync(CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<IReadOnlyCollection<DelegationResponse>>(denied);
        return Success<IReadOnlyCollection<DelegationResponse>>(await db.Delegations.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => ToResponse(x)).ToArrayAsync(cancellationToken));
    }

    public async Task<Result<DelegationResponse>> CreateDelegationAsync(CreateDelegationRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<DelegationResponse>(denied);
        var delegateUser = await db.Users.SingleOrDefaultAsync(x => x.Id == request.DelegateUserId && x.Status == "active", cancellationToken);
        if (delegateUser is null) return NotFound<DelegationResponse>();
        foreach (var permission in request.PermissionCodes)
            if (!await CanGrantAsync(permission, request.Scope, request.ScopeReferenceId, cancellationToken))
                return Failure<DelegationResponse>("delegation_authority_exceeded", "Delegation cannot exceed the delegator's current authority.");
        try
        {
            var delegation = new Delegation(Guid.CreateVersion7(), OrganisationId, ActorId, request.DelegateUserId,
                request.PermissionCodes, request.Scope, request.ScopeReferenceId, request.StartsAt, request.EndsAt,
                request.Reason, ActorId, Now);
            db.Delegations.Add(delegation);
            await CommitAsync(new("delegation.created", "delegation", delegation.Id, "DelegationChanged", 1,
                JsonSerializer.Serialize(new { request.DelegateUserId, request.PermissionCodes, request.Scope, request.ScopeReferenceId }),
                JsonSerializer.Serialize(new { delegationId = delegation.Id, change = "created" }), request.Reason), cancellationToken);
            authorizationCache.InvalidateOrganisation(OrganisationId);
            return Success(ToResponse(delegation));
        }
        catch (DomainRuleException exception) { return DomainFailure<DelegationResponse>(exception); }
    }

    public async Task<Result<bool>> RevokeDelegationAsync(Guid id, RevokeRequest request, CancellationToken cancellationToken)
    {
        var denied = await DeniedAsync(PermissionCodes.RoleManage, new AuthorizationResource(OrganisationId), cancellationToken);
        if (denied is not null) return Failure<bool>(denied);
        var delegation = await db.Delegations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (delegation is null) return NotFound<bool>();
        if (delegation.RowVersion != request.RowVersion) return Conflict<bool>();
        delegation.Revoke(ActorId, Now);
        await CommitAsync(new("delegation.revoked", "delegation", id, "DelegationChanged", 1,
            JsonSerializer.Serialize(new { change = "revoked" }), JsonSerializer.Serialize(new { delegationId = id, change = "revoked" }), request.Reason), cancellationToken);
        authorizationCache.InvalidateOrganisation(OrganisationId);
        return Success(true);
    }

    private async Task CommitAsync(IdentityOperationEvidence evidence, CancellationToken cancellationToken)
    {
        var supportsSharedTransaction = string.Equals(
            db.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal);
        await using var transaction = supportsSharedTransaction
            ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        if (transaction is not null)
            await evidenceWriter.EnlistAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        evidenceWriter.Add(evidence);
        await evidenceWriter.SaveAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private async Task<string?> DeniedAsync(PermissionCode permission, AuthorizationResource resource, CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(permission, resource, cancellationToken);
        return decision.IsAllowed ? null : decision.ReasonCode;
    }

    private async Task<bool> CanGrantAsync(string permissionCode, PermissionScope scope, Guid? scopeReferenceId, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(x => x.Id == ActorId && x.Status == "active" && x.IsEmergencyAdministrator, cancellationToken)) return true;
        var resource = scope switch
        {
            PermissionScope.Self => new AuthorizationResource(OrganisationId, scope, OwnerUserId: ActorId),
            PermissionScope.Assigned => new AuthorizationResource(OrganisationId, scope, AssignedUserId: ActorId),
            _ => new AuthorizationResource(OrganisationId, scope, scopeReferenceId)
        };
        return (await authorizer.AuthorizeAsync(PermissionCode.Create(permissionCode), resource, cancellationToken)).IsAllowed;
    }

    private async Task<bool> CanAssignRoleAsync(Guid roleId, PermissionScope assignmentScope, Guid? assignmentRef, CancellationToken cancellationToken)
    {
        var grants = await db.RoleGrants.Where(x => x.RoleId == roleId && x.Effect == GrantEffect.Allow).ToArrayAsync(cancellationToken);
        var permissions = await db.Permissions.Where(x => grants.Select(g => g.PermissionId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
        foreach (var grant in grants)
        {
            var intersection = ScopeIntersection.Intersect(grant.Scope, grant.ScopeReferenceId, assignmentScope, assignmentRef, (_, _) => false);
            if (!intersection.HasValue || !await CanGrantAsync(permissions[grant.PermissionId], intersection.Scope, intersection.ReferenceId, cancellationToken)) return false;
        }
        return true;
    }

    private async Task<bool> CanManageGroupAuthorityAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var roleIds = await db.PrincipalRoleAssignments.Where(x => x.PrincipalType == "group" && x.PrincipalId == groupId && x.RevokedAt == null)
            .Select(x => x.RoleId).Distinct().ToArrayAsync(cancellationToken);
        foreach (var roleId in roleIds)
            if (!await CanAssignRoleAsync(roleId, PermissionScope.Organisation, null, cancellationToken)) return false;
        return true;
    }

    private async Task<IReadOnlyCollection<EffectiveAccessGrantResponse>> ExplainAccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = Now;
        var groupIds = await db.GroupMembers.Where(x => x.UserId == userId && x.RevokedAt == null && x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now))
            .Select(x => x.GroupId).ToArrayAsync(cancellationToken);
        var assignments = await db.PrincipalRoleAssignments.Where(x => x.RevokedAt == null && x.ValidFrom <= now && (x.ValidTo == null || x.ValidTo > now)
            && ((x.PrincipalType == "user" && x.PrincipalId == userId) || (x.PrincipalType == "group" && groupIds.Contains(x.PrincipalId))))
            .ToArrayAsync(cancellationToken);
        var roles = await db.Roles.Where(x => assignments.Select(a => a.RoleId).Contains(x.Id) && x.Status == "active")
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var grants = await db.RoleGrants.Where(x => roles.Keys.Contains(x.RoleId)).ToArrayAsync(cancellationToken);
        var permissions = await db.Permissions.Where(x => grants.Select(g => g.PermissionId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
        var groups = await db.Groups.Where(x => groupIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var result = new List<EffectiveAccessGrantResponse>();
        foreach (var assignment in assignments.Where(x => roles.ContainsKey(x.RoleId)))
        foreach (var grant in grants.Where(x => x.RoleId == assignment.RoleId))
        {
            var intersection = ScopeIntersection.Intersect(grant.Scope, grant.ScopeReferenceId, assignment.Scope, assignment.ScopeReferenceId, (_, _) => false);
            if (!intersection.HasValue) continue;
            var sourceType = assignment.PrincipalType == "group" ? "group" : "direct";
            var sourceName = assignment.PrincipalType == "group" && groups.TryGetValue(assignment.PrincipalId, out var groupName)
                ? $"{roles[assignment.RoleId].Name} via {groupName}" : roles[assignment.RoleId].Name;
            result.Add(new(permissions[grant.PermissionId], grant.Effect, intersection.Scope, intersection.ReferenceId,
                sourceType, assignment.Id, sourceName, assignment.ValidFrom, assignment.ValidTo, false));
        }
        var delegations = await db.Delegations.Where(x => x.DelegateUserId == userId && x.Status == "active" && x.RevokedAt == null
            && x.StartsAt <= now && x.EndsAt > now).ToArrayAsync(cancellationToken);
        foreach (var delegation in delegations)
        foreach (var permission in delegation.PermissionCodes)
        {
            if (!await CanUserGrantAsync(delegation.DelegatorUserId, permission, delegation.Scope, delegation.ScopeReferenceId, cancellationToken)) continue;
            result.Add(new(permission, GrantEffect.Allow, delegation.Scope, delegation.ScopeReferenceId, "delegation",
                delegation.Id, "Delegated authority", delegation.StartsAt, delegation.EndsAt, true));
        }
        return result.OrderBy(x => x.PermissionCode).ThenBy(x => x.SourceType).ToArray();
    }

    private async Task<bool> CanUserGrantAsync(Guid userId, string permission, PermissionScope scope, Guid? scopeRef, CancellationToken cancellationToken)
    {
        var definition = await db.Permissions.SingleOrDefaultAsync(x => x.Code == permission && x.IsImplemented, cancellationToken);
        if (definition is null) return false;
        var groupIds = await db.GroupMembers.Where(x => x.UserId == userId && x.RevokedAt == null && x.ValidFrom <= Now && (x.ValidTo == null || x.ValidTo > Now))
            .Select(x => x.GroupId).ToArrayAsync(cancellationToken);
        var assignments = await db.PrincipalRoleAssignments.Where(x => x.RevokedAt == null && x.ValidFrom <= Now && (x.ValidTo == null || x.ValidTo > Now)
            && ((x.PrincipalType == "user" && x.PrincipalId == userId) || (x.PrincipalType == "group" && groupIds.Contains(x.PrincipalId))))
            .ToArrayAsync(cancellationToken);
        var roleIds = assignments.Select(x => x.RoleId).ToArray();
        var grants = await db.RoleGrants.Where(x => roleIds.Contains(x.RoleId) && x.PermissionId == definition.Id && x.Effect == GrantEffect.Allow).ToArrayAsync(cancellationToken);
        return assignments.Any(assignment => grants.Where(x => x.RoleId == assignment.RoleId).Any(grant =>
            ScopeIntersection.Intersect(grant.Scope, grant.ScopeReferenceId, assignment.Scope, assignment.ScopeReferenceId, (_, _) => false) is { HasValue: true } effective
            && ScopeIntersection.Intersect(effective.Scope, effective.ReferenceId, scope, scopeRef, (_, _) => false).HasValue));
    }

    private static OrganisationResponse ToResponse(Organisation x) => new(x.Id, x.Code, x.Name, x.Status, x.DefaultTimeZone, x.DefaultCurrency, x.RowVersion);
    private static OrganisationUnitResponse ToResponse(OrganisationUnit x) => new(x.Id, x.ParentId, x.Type, x.Code, x.Name, x.Path, x.Status, x.RowVersion);
    private static UserResponse ToResponse(User x) => new(x.Id, x.ExternalIssuer, x.ExternalSubject, x.Email, x.DisplayName, x.JobTitle, x.TimeZone, x.Locale, x.Status, x.IsEmergencyAdministrator, x.RowVersion);
    private static GroupResponse ToResponse(Group x) => new(x.Id, x.Name, x.Type, x.ExternalKey, x.Status, x.RowVersion);
    private static RoleResponse ToResponse(Role role, IEnumerable<RoleGrant> grants, IReadOnlyDictionary<Guid, string> permissions) =>
        new(role.Id, role.Code, role.Name, role.Description, role.IsSystem, role.Status, role.RowVersion,
            grants.Select(x => new RoleGrantResponse(x.Id, permissions.GetValueOrDefault(x.PermissionId, "unknown"), x.Effect, x.Scope, x.ScopeReferenceId)).ToArray());
    private static RoleAssignmentResponse ToResponse(PrincipalRoleAssignment x) => new(x.Id, x.PrincipalType, x.PrincipalId, x.RoleId, x.Scope, x.ScopeReferenceId, x.ValidFrom, x.ValidTo, x.RevokedAt, x.RowVersion);
    private static DelegationResponse ToResponse(Delegation x) => new(x.Id, x.DelegatorUserId, x.DelegateUserId, x.PermissionCodes, x.Scope, x.ScopeReferenceId, x.StartsAt, x.EndsAt, x.Reason, x.Status, x.RowVersion);

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static Result<T> Success<T>(T value) => new(value, null);
    private static Result<T> Failure<T>(string code, string? message = null) => new(default, new(code, message ?? "You do not have permission to perform this operation."));
    private static Result<T> NotFound<T>() => Failure<T>("not_found", "The requested resource was not found.");
    private static Result<T> Conflict<T>() => Failure<T>("concurrency_conflict", "The resource changed. Reload it and try again.");
    private static Result<T> DomainFailure<T>(DomainRuleException exception) => Failure<T>(exception.Code, exception.Message);
}

public sealed class OrganisationAuthorizationCache : IAuthorizationCache
{
    private long _version;
    public long Version => Interlocked.Read(ref _version);
    public void InvalidateOrganisation(Guid organisationId) => Interlocked.Increment(ref _version);
}
