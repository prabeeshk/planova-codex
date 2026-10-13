using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Planova.Infrastructure.Events;
using Planova.Infrastructure.Persistence;
using Planova.Identity.Application;
using Planova.Projects.Application;
using Planova.SharedKernel.Operations;
using Planova.SharedKernel.Time;

namespace Planova.Infrastructure;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddPlanovaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureForTests = null,
        bool addDispatcher = true)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddDbContext<PlatformDbContext>(options =>
        {
            if (configureForTests is not null)
            {
                configureForTests(options);
                return;
            }

            var connectionString = configuration.GetConnectionString("Planova")
                ?? throw new InvalidOperationException("ConnectionStrings:Planova is required.");
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "platform"));
        });
        services.AddScoped<IOutboxWriter, EfOutboxWriter>();
        services.AddScoped<IFoundationReferenceRecorder, FoundationReferenceRecorder>();
        services.AddScoped<IIdentityEvidenceWriter, IdentityEvidenceWriter>();
        services.AddScoped<IProjectEvidenceWriter, ProjectEvidenceWriter>();
        services.AddScoped<IDependencyHealthReader, DatabaseDependencyHealthReader>();
        services.AddScoped<IdempotentEventProcessor>();
        services.AddScoped<IOutboxSink, LoggingOutboxSink>();
        if (addDispatcher)
        {
            services.AddHostedService<OutboxDispatcher>();
        }

        return services;
    }
}
