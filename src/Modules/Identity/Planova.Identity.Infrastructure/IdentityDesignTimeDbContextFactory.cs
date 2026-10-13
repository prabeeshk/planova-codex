using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Planova.SharedKernel.Identity;

namespace Planova.Identity.Infrastructure;

public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Planova")
            ?? "Host=localhost;Port=5432;Database=planova;Username=planova";
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity"))
            .Options;
        return new IdentityDbContext(options, DesignTimeTenantContext.Instance);
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public static DesignTimeTenantContext Instance { get; } = new();
        public Guid? OrganisationId => null;
        public bool HasOrganisation => false;
        public Guid RequireOrganisationId() => throw new InvalidOperationException("No tenant exists at design time.");
    }
}
