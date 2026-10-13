using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Planova.Api.Security;

public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<DevelopmentAuthOptions> developmentOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PlanovaDevelopment";
    public const string Header = "X-Planova-Dev-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Context.Request.Headers.TryGetValue(Header, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var alias = values.ToString();
        if (!developmentOptions.Value.Users.TryGetValue(alias, out var user))
        {
            return Task.FromResult(AuthenticateResult.Fail("Unknown development identity."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Subject),
            new Claim("sub", user.Subject),
            new Claim("iss", "planova-development"),
            new Claim("name", user.DisplayName),
            new Claim(PlanovaClaimTypes.UserId, user.UserId.ToString()),
            new Claim(PlanovaClaimTypes.OrganisationId, user.OrganisationId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "name", ClaimTypes.Role));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
