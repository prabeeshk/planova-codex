using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Planova.Projects.Application;

namespace Planova.Projects.Infrastructure;

public static class ProjectsInfrastructureExtensions
{
    public static IServiceCollection AddProjectsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProjectsDbContext>(options => options.UseNpgsql(configuration.GetConnectionString("Planova")
            ?? throw new InvalidOperationException("ConnectionStrings:Planova is required."), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "projects")));
        services.AddScoped<IProjectService, ProjectService>();
        return services;
    }
}
