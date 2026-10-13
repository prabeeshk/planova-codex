using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Planova.Api.Security;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddPlanovaAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var development = configuration.GetSection(DevelopmentAuthOptions.SectionName).Get<DevelopmentAuthOptions>()
            ?? new DevelopmentAuthOptions();
        services.Configure<DevelopmentAuthOptions>(configuration.GetSection(DevelopmentAuthOptions.SectionName));

        if (development.Enabled)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Development authentication can run only in the Development environment.");
            }

            services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
                    DevelopmentAuthenticationHandler.SchemeName,
                    _ => { });
            return services;
        }

        var authority = configuration["Authentication:Authority"];
        var clientId = configuration["Authentication:ClientId"];
        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Authentication:Authority and Authentication:ClientId are required when development authentication is disabled.");
        }

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = false;
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = authority;
                options.ClientId = clientId;
                options.ClientSecret = configuration["Authentication:ClientSecret"];
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
            });
        return services;
    }
}
