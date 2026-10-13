using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Planova.Identity.Infrastructure;

namespace Planova.Api.Security;

public sealed class EnterpriseIdentityClaimsTransformation(
    IdentityDbContext dbContext,
    IConfiguration configuration) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(claim => claim.Type == PlanovaClaimTypes.UserId)) return principal;
        var subject = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var issuer = principal.FindFirstValue("iss") ?? principal.FindFirst("sub")?.Issuer;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer)) return principal;

        Guid? configuredOrganisation = Guid.TryParse(configuration["Authentication:OrganisationId"], out var value) ? value : null;
        var candidates = await dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.ExternalIssuer == issuer && x.ExternalSubject == subject && x.Status == "active"
                && (configuredOrganisation == null || x.OrganisationId == configuredOrganisation))
            .Select(x => new { x.Id, x.OrganisationId, x.DisplayName })
            .Take(2)
            .ToArrayAsync();
        if (candidates.Length != 1) return principal;

        var identity = principal.Identity as ClaimsIdentity;
        if (identity is null) return principal;
        identity.AddClaim(new Claim(PlanovaClaimTypes.UserId, candidates[0].Id.ToString()));
        identity.AddClaim(new Claim(PlanovaClaimTypes.OrganisationId, candidates[0].OrganisationId.ToString()));
        if (!principal.HasClaim(claim => claim.Type == "name")) identity.AddClaim(new Claim("name", candidates[0].DisplayName));
        return principal;
    }
}
