using Planova.SharedKernel.Domain;
using Planova.SharedKernel.Security;

namespace Planova.Identity.Domain;

public abstract class TenantAggregate : AggregateRoot
{
    public Guid OrganisationId { get; protected init; }
    public DateTimeOffset CreatedAt { get; protected set; }
    public Guid CreatedBy { get; protected init; }
    public DateTimeOffset UpdatedAt { get; protected set; }
    public Guid UpdatedBy { get; protected set; }

    protected void Touch(Guid actorId, DateTimeOffset now)
    {
        UpdatedBy = actorId;
        UpdatedAt = now;
    }
}

public sealed class OrganisationUnit : TenantAggregate
{
    private OrganisationUnit() { }

    public OrganisationUnit(Guid id, Guid organisationId, Guid? parentId, string type, string code, string name,
        string path, Guid actorId, DateTimeOffset now)
    {
        Id = id;
        OrganisationId = organisationId;
        ParentId = parentId;
        Type = Text(type, 50);
        Code = Text(code, 50).ToUpperInvariant();
        Name = Text(name, 200);
        Path = Text(path, 2000);
        CreatedBy = UpdatedBy = actorId;
        CreatedAt = UpdatedAt = now;
    }

    public Guid? ParentId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Path { get; private set; } = string.Empty;
    public string Status { get; private set; } = "active";

    public void Move(Guid? parentId, string path, Guid actorId, DateTimeOffset now)
    {
        if (parentId == Id) throw new DomainRuleException("organisation_hierarchy_cycle", "An organisation unit cannot contain itself.");
        ParentId = parentId;
        Path = Text(path, 2000);
        Touch(actorId, now);
    }

    private static string Text(string value, int maximum)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is 0 || text.Length > maximum) throw new DomainRuleException("invalid_organisation_unit", "An organisation unit value is invalid.");
        return text;
    }
}

public sealed class UserOrganisationUnitMembership
{
    private UserOrganisationUnitMembership() { }
    public UserOrganisationUnitMembership(Guid id, Guid organisationId, Guid userId, Guid organisationUnitId,
        DateOnly effectiveFrom, DateOnly? effectiveTo, bool isPrimary, Guid actorId, DateTimeOffset now)
    {
        if (effectiveTo < effectiveFrom) throw new DomainRuleException("invalid_effective_dates", "The end date must be after the start date.");
        Id = id; OrganisationId = organisationId; UserId = userId; OrganisationUnitId = organisationUnitId;
        EffectiveFrom = effectiveFrom; EffectiveTo = effectiveTo; IsPrimary = isPrimary;
        CreatedBy = actorId; CreatedAt = now;
    }
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrganisationUnitId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
}

public sealed class Group : TenantAggregate
{
    private Group() { }
    public Group(Guid id, Guid organisationId, string name, string type, string? externalKey, Guid actorId, DateTimeOffset now)
    {
        Id = id; OrganisationId = organisationId; Name = name.Trim(); Type = type.Trim(); ExternalKey = externalKey?.Trim();
        if (Name.Length is 0 or > 200 || Type.Length is 0 or > 30) throw new DomainRuleException("invalid_group", "Group values are invalid.");
        CreatedBy = UpdatedBy = actorId; CreatedAt = UpdatedAt = now;
    }
    public string? ExternalKey { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Type { get; private set; } = "security";
    public string Status { get; private set; } = "active";
}

public sealed class GroupMember
{
    private GroupMember() { }
    public GroupMember(Guid id, Guid organisationId, Guid groupId, Guid userId, DateTimeOffset validFrom,
        DateTimeOffset? validTo, string source, Guid actorId, DateTimeOffset now)
    {
        ValidateDates(validFrom, validTo);
        Id = id; OrganisationId = organisationId; GroupId = groupId; UserId = userId; ValidFrom = validFrom;
        ValidTo = validTo; Source = source; CreatedBy = actorId; CreatedAt = now;
    }
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public string Source { get; private set; } = "manual";
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && ValidFrom <= now && (ValidTo is null || ValidTo > now);
    public void Revoke(Guid actorId, DateTimeOffset now) { RevokedAt = now; RevokedBy = actorId; }
    private static void ValidateDates(DateTimeOffset start, DateTimeOffset? end)
    { if (end <= start) throw new DomainRuleException("invalid_effective_dates", "The end time must be after the start time."); }
}

public sealed class PermissionDefinition
{
    private PermissionDefinition() { }
    public PermissionDefinition(Guid id, string code, string description, string riskLevel, bool implemented)
    {
        var permission = PermissionCode.Create(code);
        Id = id; Code = permission.Value; Resource = code[..code.IndexOf('.')]; Action = code[(code.IndexOf('.') + 1)..];
        Description = description; RiskLevel = riskLevel; IsImplemented = implemented;
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Resource { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string RiskLevel { get; private set; } = "standard";
    public bool IsImplemented { get; private set; }
    public bool IsSystem { get; private set; } = true;
}

public sealed class PermissionScopeType
{
    private PermissionScopeType() { }
    public PermissionScopeType(Guid permissionId, PermissionScope scope) { PermissionId = permissionId; Scope = scope; }
    public Guid PermissionId { get; private set; }
    public PermissionScope Scope { get; private set; }
}

public sealed class Role : TenantAggregate
{
    private Role() { }
    public Role(Guid id, Guid organisationId, string code, string name, string? description, bool isSystem, Guid actorId, DateTimeOffset now)
    {
        Id = id; OrganisationId = organisationId; Code = code.Trim().ToUpperInvariant(); Name = name.Trim(); Description = description?.Trim(); IsSystem = isSystem;
        if (Code.Length is 0 or > 50 || Name.Length is 0 or > 200) throw new DomainRuleException("invalid_role", "Role values are invalid.");
        CreatedBy = UpdatedBy = actorId; CreatedAt = UpdatedAt = now;
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public string Status { get; private set; } = "active";
    public void Update(string name, string? description, Guid actorId, DateTimeOffset now)
    { Name = name.Trim(); Description = description?.Trim(); Touch(actorId, now); }
    public void Disable(Guid actorId, DateTimeOffset now) { Status = "disabled"; Touch(actorId, now); }
}

public sealed class RoleGrant
{
    private RoleGrant() { }
    public RoleGrant(Guid id, Guid organisationId, Guid roleId, Guid permissionId, GrantEffect effect,
        PermissionScope scope, Guid? scopeReferenceId, string? conditionsJson, Guid actorId, DateTimeOffset now)
    {
        if (scope == PermissionScope.Organisation && scopeReferenceId is not null) throw new DomainRuleException("invalid_scope_reference", "Organisation scope cannot have a reference.");
        if (scope != PermissionScope.Organisation && scopeReferenceId is null && scope is not PermissionScope.Self and not PermissionScope.Assigned) throw new DomainRuleException("scope_reference_required", "This scope requires a reference.");
        if (!string.IsNullOrWhiteSpace(conditionsJson)) throw new DomainRuleException("unsupported_grant_condition", "Grant conditions are not supported in this release.");
        Id = id; OrganisationId = organisationId; RoleId = roleId; PermissionId = permissionId; Effect = effect; Scope = scope;
        ScopeReferenceId = scopeReferenceId; ConditionsJson = conditionsJson; CreatedBy = actorId; CreatedAt = now;
    }
    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public GrantEffect Effect { get; private set; }
    public PermissionScope Scope { get; private set; }
    public Guid? ScopeReferenceId { get; private set; }
    public string? ConditionsJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
}

public sealed class PrincipalRoleAssignment : TenantAggregate
{
    private PrincipalRoleAssignment() { }
    public PrincipalRoleAssignment(Guid id, Guid organisationId, string principalType, Guid principalId, Guid roleId,
        PermissionScope scope, Guid? scopeReferenceId, DateTimeOffset validFrom, DateTimeOffset? validTo,
        Guid actorId, string reason, string idempotencyKey, DateTimeOffset now)
    {
        if (principalType is not ("user" or "group")) throw new DomainRuleException("invalid_principal_type", "Principal type must be user or group.");
        if (validTo <= validFrom) throw new DomainRuleException("invalid_effective_dates", "The end time must be after the start time.");
        if (scope == PermissionScope.Organisation && scopeReferenceId is not null) throw new DomainRuleException("invalid_scope_reference", "Organisation scope cannot have a reference.");
        Id = id; OrganisationId = organisationId; PrincipalType = principalType; PrincipalId = principalId; RoleId = roleId;
        Scope = scope; ScopeReferenceId = scopeReferenceId; ValidFrom = validFrom; ValidTo = validTo; Reason = reason.Trim();
        IdempotencyKey = idempotencyKey; AssignedBy = actorId; CreatedBy = UpdatedBy = actorId; CreatedAt = UpdatedAt = now;
    }
    public string PrincipalType { get; private set; } = string.Empty;
    public Guid PrincipalId { get; private set; }
    public Guid RoleId { get; private set; }
    public PermissionScope Scope { get; private set; }
    public Guid? ScopeReferenceId { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public Guid AssignedBy { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && ValidFrom <= now && (ValidTo is null || ValidTo > now);
    public void Revoke(Guid actorId, DateTimeOffset now) { RevokedAt = now; RevokedBy = actorId; Touch(actorId, now); }
}

public sealed class Delegation : TenantAggregate
{
    private Delegation() { }
    public Delegation(Guid id, Guid organisationId, Guid delegatorUserId, Guid delegateUserId,
        IReadOnlyCollection<string> permissionCodes, PermissionScope scope, Guid? scopeReferenceId,
        DateTimeOffset startsAt, DateTimeOffset endsAt, string reason, Guid actorId, DateTimeOffset now)
    {
        if (delegatorUserId == delegateUserId) throw new DomainRuleException("self_delegation", "A user cannot delegate to themselves.");
        if (endsAt <= startsAt) throw new DomainRuleException("invalid_effective_dates", "Delegation end must be after its start.");
        if (permissionCodes.Count == 0) throw new DomainRuleException("permission_filter_required", "At least one permission is required.");
        Id = id; OrganisationId = organisationId; DelegatorUserId = delegatorUserId; DelegateUserId = delegateUserId;
        PermissionFilter = string.Join(',', permissionCodes.Select(x => PermissionCode.Create(x).Value).Distinct(StringComparer.Ordinal).Order());
        Scope = scope; ScopeReferenceId = scopeReferenceId; StartsAt = startsAt; EndsAt = endsAt; Reason = reason.Trim();
        CreatedBy = UpdatedBy = actorId; CreatedAt = UpdatedAt = now;
    }
    public Guid DelegatorUserId { get; private set; }
    public Guid DelegateUserId { get; private set; }
    public string PermissionFilter { get; private set; } = string.Empty;
    public PermissionScope Scope { get; private set; }
    public Guid? ScopeReferenceId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string Status { get; private set; } = "active";
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public bool IsActiveAt(DateTimeOffset now) => Status == "active" && RevokedAt is null && StartsAt <= now && EndsAt > now;
    public IReadOnlyCollection<string> PermissionCodes => PermissionFilter.Split(',', StringSplitOptions.RemoveEmptyEntries);
    public void Revoke(Guid actorId, DateTimeOffset now) { Status = "revoked"; RevokedAt = now; RevokedBy = actorId; Touch(actorId, now); }
}
