using Planova.SharedKernel.Identity;
using Planova.SharedKernel.Security;
using Planova.Web.Navigation;

namespace Planova.Api.Security;

public sealed class ServerNavigationCapabilityProvider(
    ITenantContext tenantContext,
    ICapabilityAuthorizer authorizer) : INavigationCapabilityProvider
{
    public async Task<bool> IsVisibleAsync(NavigationItem item, CancellationToken cancellationToken = default)
    {
        if (item.RequiredPermission is null)
        {
            return true;
        }

        if (!tenantContext.HasOrganisation)
        {
            return false;
        }

        var decision = await authorizer.AuthorizeAsync(
            item.RequiredPermission,
            new AuthorizationResource(tenantContext.RequireOrganisationId()),
            cancellationToken);
        return decision.IsAllowed;
    }
}
