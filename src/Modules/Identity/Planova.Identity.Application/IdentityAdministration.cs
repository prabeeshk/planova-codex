using System.Data.Common;
using Planova.Identity.Contracts;
using Planova.SharedKernel.Results;
using Planova.SharedKernel.Security;

namespace Planova.Identity.Application;

public interface IIdentityAdministrationService
{
    Task<Result<OrganisationResponse>> GetOrganisationAsync(CancellationToken cancellationToken);
    Task<Result<OrganisationResponse>> UpdateOrganisationAsync(UpdateOrganisationRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<OrganisationUnitResponse>>> ListOrganisationUnitsAsync(CancellationToken cancellationToken);
    Task<Result<OrganisationUnitResponse>> CreateOrganisationUnitAsync(CreateOrganisationUnitRequest request, CancellationToken cancellationToken);
    Task<Result<OrganisationUnitResponse>> MoveOrganisationUnitAsync(Guid id, MoveOrganisationUnitRequest request, CancellationToken cancellationToken);

    Task<Result<PagedResponse<UserResponse>>> ListUsersAsync(string? search, int take, CancellationToken cancellationToken);
    Task<Result<UserResponse>> GetUserAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<UserResponse>> ProvisionUserAsync(ProvisionUserRequest request, CancellationToken cancellationToken);
    Task<Result<UserResponse>> ChangeUserStatusAsync(Guid id, string command, LifecycleRequest request, CancellationToken cancellationToken);
    Task<Result<UserResponse>> ChangeIdentityLinkAsync(Guid id, ChangeIdentityLinkRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResponse<GroupResponse>>> ListGroupsAsync(string? search, int take, CancellationToken cancellationToken);
    Task<Result<GroupResponse>> CreateGroupAsync(CreateGroupRequest request, CancellationToken cancellationToken);
    Task<Result<bool>> AddGroupMemberAsync(Guid groupId, AddGroupMemberRequest request, CancellationToken cancellationToken);
    Task<Result<bool>> RevokeGroupMemberAsync(Guid groupId, Guid userId, RevokeRequest request, CancellationToken cancellationToken);

    Task<Result<IReadOnlyCollection<PermissionResponse>>> ListPermissionsAsync(CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<RoleResponse>>> ListRolesAsync(CancellationToken cancellationToken);
    Task<Result<RoleResponse>> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken);
    Task<Result<RoleResponse>> UpdateRoleAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken);
    Task<Result<RoleGrantResponse>> AddRoleGrantAsync(Guid roleId, AddRoleGrantRequest request, CancellationToken cancellationToken);
    Task<Result<bool>> RemoveRoleGrantAsync(Guid roleId, Guid grantId, RevokeRequest request, CancellationToken cancellationToken);

    Task<Result<RoleAssignmentResponse>> CreateAssignmentAsync(CreateRoleAssignmentRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<Result<bool>> RevokeAssignmentAsync(Guid id, RevokeRequest request, CancellationToken cancellationToken);
    Task<Result<EffectiveAccessResponse>> GetEffectiveAccessAsync(Guid userId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<DelegationResponse>>> ListDelegationsAsync(CancellationToken cancellationToken);
    Task<Result<DelegationResponse>> CreateDelegationAsync(CreateDelegationRequest request, CancellationToken cancellationToken);
    Task<Result<bool>> RevokeDelegationAsync(Guid id, RevokeRequest request, CancellationToken cancellationToken);
}

public sealed record IdentityOperationEvidence(
    string Action,
    string EntityType,
    Guid EntityId,
    string EventType,
    int EventVersion,
    string RedactedChangesJson,
    string EventPayloadJson,
    string? Reason = null);

public interface IIdentityEvidenceWriter
{
    Task EnlistAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
    void Add(IdentityOperationEvidence evidence);
    Task SaveAsync(CancellationToken cancellationToken);
}

public interface IAuthorizationCache
{
    void InvalidateOrganisation(Guid organisationId);
}

public static class ScopeIntersection
{
    public static (bool HasValue, PermissionScope Scope, Guid? ReferenceId) Intersect(
        PermissionScope grantScope, Guid? grantReference,
        PermissionScope assignmentScope, Guid? assignmentReference,
        Func<Guid, Guid, bool> isDescendantOrSelf)
    {
        if (grantScope == PermissionScope.Organisation) return (true, assignmentScope, assignmentReference);
        if (assignmentScope == PermissionScope.Organisation) return (true, grantScope, grantReference);
        if (grantScope == assignmentScope && grantScope == PermissionScope.Department && grantReference.HasValue && assignmentReference.HasValue)
        {
            if (isDescendantOrSelf(grantReference.Value, assignmentReference.Value)) return (true, PermissionScope.Department, grantReference);
            if (isDescendantOrSelf(assignmentReference.Value, grantReference.Value)) return (true, PermissionScope.Department, assignmentReference);
            return default;
        }
        return grantScope == assignmentScope && grantReference == assignmentReference
            ? (true, grantScope, grantReference)
            : default;
    }
}
