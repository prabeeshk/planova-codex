using Planova.SharedKernel.Security;

namespace Planova.Web.Navigation;

public sealed record NavigationItem(string Label, string Route, PermissionCode? RequiredPermission, string IconLabel);

public interface INavigationCapabilityProvider
{
    Task<bool> IsVisibleAsync(NavigationItem item, CancellationToken cancellationToken = default);
}
