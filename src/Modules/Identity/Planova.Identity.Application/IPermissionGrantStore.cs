using Planova.SharedKernel.Security;

namespace Planova.Identity.Application;

public interface IPermissionGrantStore
{
    Task<IReadOnlyCollection<PermissionGrant>> GetGrantsAsync(
        Guid organisationId,
        Guid userId,
        PermissionCode permission,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetEffectivePermissionCodesAsync(
        Guid organisationId,
        Guid userId,
        CancellationToken cancellationToken);
}

public interface IAuthorizationScopeResolver
{
    Task<bool> IsDepartmentWithinAsync(Guid? authorityDepartmentId, Guid? resourceDepartmentId,
        CancellationToken cancellationToken);
}
