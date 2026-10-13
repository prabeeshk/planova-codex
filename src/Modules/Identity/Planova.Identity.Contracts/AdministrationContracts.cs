using Planova.SharedKernel.Security;

namespace Planova.Identity.Contracts;

public sealed record OrganisationResponse(Guid Id, string Code, string Name, string Status, string DefaultTimeZone,
    string DefaultCurrency, uint RowVersion);
public sealed record UpdateOrganisationRequest(string Name, string DefaultTimeZone, string DefaultCurrency, uint RowVersion);
public sealed record OrganisationUnitResponse(Guid Id, Guid? ParentId, string Type, string Code, string Name, string Path,
    string Status, uint RowVersion);
public sealed record CreateOrganisationUnitRequest(Guid? ParentId, string Type, string Code, string Name);
public sealed record MoveOrganisationUnitRequest(Guid? ParentId, uint RowVersion);

public sealed record UserResponse(Guid Id, string ExternalIssuer, string ExternalSubject, string Email, string DisplayName,
    string? JobTitle, string TimeZone, string Locale, string Status, bool IsEmergencyAdministrator, uint RowVersion);
public sealed record ProvisionUserRequest(string ExternalIssuer, string ExternalSubject, string Email, string DisplayName,
    string? JobTitle, string TimeZone, string Locale, Guid? OrganisationUnitId, DateOnly? EffectiveFrom);
public sealed record ChangeIdentityLinkRequest(string ExternalIssuer, string ExternalSubject, string Reason, uint RowVersion);
public sealed record LifecycleRequest(string Reason, uint RowVersion);
public sealed record CreateGroupRequest(string Name, string Type, string? ExternalKey);
public sealed record GroupResponse(Guid Id, string Name, string Type, string? ExternalKey, string Status, uint RowVersion);
public sealed record AddGroupMemberRequest(Guid UserId, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo, string Source);

public sealed record PermissionResponse(Guid Id, string Code, string Description, string RiskLevel, bool IsImplemented,
    IReadOnlyCollection<PermissionScope> PermittedScopes);
public sealed record RoleResponse(Guid Id, string Code, string Name, string? Description, bool IsSystem, string Status,
    uint RowVersion, IReadOnlyCollection<RoleGrantResponse> Grants);
public sealed record RoleGrantResponse(Guid Id, string PermissionCode, GrantEffect Effect, PermissionScope Scope,
    Guid? ScopeReferenceId);
public sealed record CreateRoleRequest(string Code, string Name, string? Description);
public sealed record UpdateRoleRequest(string Name, string? Description, uint RowVersion);
public sealed record AddRoleGrantRequest(string PermissionCode, GrantEffect Effect, PermissionScope Scope,
    Guid? ScopeReferenceId, string? ConditionsJson);

public sealed record CreateRoleAssignmentRequest(string PrincipalType, Guid PrincipalId, Guid RoleId,
    PermissionScope Scope, Guid? ScopeReferenceId, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo, string Reason);
public sealed record RoleAssignmentResponse(Guid Id, string PrincipalType, Guid PrincipalId, Guid RoleId,
    PermissionScope Scope, Guid? ScopeReferenceId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo,
    DateTimeOffset? RevokedAt, uint RowVersion);
public sealed record RevokeRequest(string Reason, uint RowVersion);

public sealed record EffectiveAccessResponse(Guid UserId, IReadOnlyCollection<EffectiveAccessGrantResponse> Grants);
public sealed record EffectiveAccessGrantResponse(string PermissionCode, GrantEffect Effect, PermissionScope Scope,
    Guid? ScopeReferenceId, string SourceType, Guid SourceId, string SourceName, DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo, bool IsDelegated);

public sealed record CreateDelegationRequest(Guid DelegateUserId, IReadOnlyCollection<string> PermissionCodes,
    PermissionScope Scope, Guid? ScopeReferenceId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason);
public sealed record DelegationResponse(Guid Id, Guid DelegatorUserId, Guid DelegateUserId,
    IReadOnlyCollection<string> PermissionCodes, PermissionScope Scope, Guid? ScopeReferenceId,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason, string Status, uint RowVersion);

public sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Count);
