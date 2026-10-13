using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Planova.Identity.Application;
using Planova.SharedKernel.Security;

namespace Planova.Identity.Infrastructure;

public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureForTests = null)
    {
        services.AddDbContext<IdentityDbContext>(options =>
        {
            if (configureForTests is not null)
            {
                configureForTests(options);
                return;
            }

            var connectionString = configuration.GetConnectionString("Planova")
                ?? throw new InvalidOperationException("ConnectionStrings:Planova is required.");
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity"));
        });
        services.AddScoped<IPermissionGrantStore, EfPermissionGrantStore>();
        services.AddScoped<IAuthorizationScopeResolver, EfAuthorizationScopeResolver>();
        services.AddScoped<ICapabilityAuthorizer, CapabilityAuthorizer>();
        services.AddScoped<IIdentityAdministrationService, IdentityAdministrationService>();
        services.AddSingleton<IAuthorizationCache, OrganisationAuthorizationCache>();
        return services;
    }
}
