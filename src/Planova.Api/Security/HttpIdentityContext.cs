using System.Security.Claims;
using Planova.SharedKernel.Identity;

namespace Planova.Api.Security;

public sealed class HttpIdentityContext(IHttpContextAccessor httpContextAccessor) : ICurrentUser, ITenantContext
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid? UserId => ReadGuid(PlanovaClaimTypes.UserId);
    public string? ExternalSubject => Principal?.FindFirstValue("sub") ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? DisplayName => Principal?.FindFirstValue("name") ?? Principal?.Identity?.Name;
    public Guid? OrganisationId => ReadGuid(PlanovaClaimTypes.OrganisationId);
    public bool HasOrganisation => OrganisationId.HasValue;

    public Guid RequireOrganisationId() => OrganisationId
        ?? throw new InvalidOperationException("The authenticated principal has no PLANOVA organisation mapping.");

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(Principal?.FindFirstValue(claimType), out var value) ? value : null;
}
