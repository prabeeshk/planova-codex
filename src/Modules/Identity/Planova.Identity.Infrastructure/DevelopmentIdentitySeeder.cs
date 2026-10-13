using Microsoft.EntityFrameworkCore;
using Planova.Identity.Contracts;
using Planova.Identity.Domain;
using Planova.SharedKernel.Security;

namespace Planova.Identity.Infrastructure;

public static class DevelopmentIdentitySeeder
{
    public static readonly Guid OrganisationA = Guid.Parse("019a0000-0000-7000-8000-000000000001");
    public static readonly Guid OrganisationB = Guid.Parse("019a0000-0000-7000-8000-000000000002");
    public static readonly Guid AdminA = Guid.Parse("019a0000-0000-7000-8000-000000000101");
    public static readonly Guid ReaderA = Guid.Parse("019a0000-0000-7000-8000-000000000102");
    public static readonly Guid UserB = Guid.Parse("019a0000-0000-7000-8000-000000000201");
    public static readonly Guid DepartmentA = Guid.Parse("019a0000-0000-7000-8000-000000001001");
    public static readonly Guid DepartmentB = Guid.Parse("019a0000-0000-7000-8000-000000002001");
    public static readonly Guid AdministratorRoleA = Guid.Parse("019a0000-0000-7000-8000-000000003001");

    public static async Task SeedAsync(IdentityDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (!await dbContext.Organisations.IgnoreQueryFilters().AnyAsync(x => x.Id == OrganisationA, cancellationToken))
            dbContext.Organisations.Add(new Organisation(OrganisationA, "DEMO-A", "Planova Demo A", "Asia/Dubai", "AED", AdminA));
        if (!await dbContext.Organisations.IgnoreQueryFilters().AnyAsync(x => x.Id == OrganisationB, cancellationToken))
            dbContext.Organisations.Add(new Organisation(OrganisationB, "DEMO-B", "Planova Demo B", "UTC", "USD", UserB));
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!await dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == AdminA, cancellationToken))
        {
            var admin = new User(AdminA, OrganisationA, "planova-development", "dev-admin-a", "admin-a@local.invalid",
                "Development Admin A", "Platform administrator", "Asia/Dubai", "en", AdminA);
            admin.DesignateEmergencyAdministrator(AdminA, now);
            dbContext.Users.Add(admin);
        }
        if (!await dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == ReaderA, cancellationToken))
            dbContext.Users.Add(new User(ReaderA, OrganisationA, "planova-development", "dev-reader-a", "reader-a@local.invalid",
                "Development Reader A", null, "Asia/Dubai", "en", AdminA));
        if (!await dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == UserB, cancellationToken))
        {
            var adminB = new User(UserB, OrganisationB, "planova-development", "dev-user-b", "user-b@local.invalid",
                "Development User B", null, "UTC", "en", UserB);
            adminB.DesignateEmergencyAdministrator(UserB, now);
            dbContext.Users.Add(adminB);
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!await dbContext.OrganisationUnits.IgnoreQueryFilters().AnyAsync(x => x.Id == DepartmentA, cancellationToken))
            dbContext.OrganisationUnits.Add(new OrganisationUnit(DepartmentA, OrganisationA, null, "department", "DEPT-A",
                "Demo Department A", $"/{DepartmentA:D}/", AdminA, now));
        if (!await dbContext.OrganisationUnits.IgnoreQueryFilters().AnyAsync(x => x.Id == DepartmentB, cancellationToken))
            dbContext.OrganisationUnits.Add(new OrganisationUnit(DepartmentB, OrganisationB, null, "department", "DEPT-B",
                "Demo Department B", $"/{DepartmentB:D}/", UserB, now));

        var existingCodes = await dbContext.Permissions.Select(x => x.Code).ToArrayAsync(cancellationToken);
        foreach (var code in PermissionCodes.All.Except(existingCodes, StringComparer.Ordinal))
        {
            var implemented = code is "foundation.view" or "user.manage" or "role.manage" or "settings.manage" or "audit.view";
            dbContext.Permissions.Add(new PermissionDefinition(Guid.CreateVersion7(), code, Description(code), Risk(code), implemented));
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var permissions = await dbContext.Permissions.ToArrayAsync(cancellationToken);
        var existingScopes = await dbContext.PermissionScopeTypes.ToArrayAsync(cancellationToken);
        foreach (var permission in permissions)
        foreach (var scope in PermittedScopes(permission.Code))
            if (!existingScopes.Any(x => x.PermissionId == permission.Id && x.Scope == scope))
                dbContext.PermissionScopeTypes.Add(new PermissionScopeType(permission.Id, scope));

        if (!await dbContext.Roles.IgnoreQueryFilters().AnyAsync(x => x.Id == AdministratorRoleA, cancellationToken))
            dbContext.Roles.Add(new Role(AdministratorRoleA, OrganisationA, "PLATFORM-ADMIN", "Platform Administrator",
                "Development bootstrap template. The display name is never used for authorisation.", true, AdminA, now));
        await dbContext.SaveChangesAsync(cancellationToken);

        var adminPermissions = permissions.Where(x => x.Code is "foundation.view" or "user.manage" or "role.manage" or "settings.manage" or "audit.view").ToArray();
        foreach (var permission in adminPermissions)
            if (!await dbContext.RoleGrants.IgnoreQueryFilters().AnyAsync(x => x.RoleId == AdministratorRoleA && x.PermissionId == permission.Id, cancellationToken))
                dbContext.RoleGrants.Add(new RoleGrant(Guid.CreateVersion7(), OrganisationA, AdministratorRoleA, permission.Id,
                    GrantEffect.Allow, PermissionScope.Organisation, null, null, AdminA, now));

        if (!await dbContext.PrincipalRoleAssignments.IgnoreQueryFilters().AnyAsync(x => x.PrincipalType == "user" && x.PrincipalId == AdminA && x.RoleId == AdministratorRoleA, cancellationToken))
            dbContext.PrincipalRoleAssignments.Add(new PrincipalRoleAssignment(Guid.CreateVersion7(), OrganisationA, "user",
                AdminA, AdministratorRoleA, PermissionScope.Organisation, null, now, null, AdminA,
                "Development bootstrap", "development-bootstrap-admin-a", now));

        if (!await dbContext.UserPermissionGrants.IgnoreQueryFilters().AnyAsync(x => x.UserId == UserB && x.PermissionCode == "foundation.view", cancellationToken))
            dbContext.UserPermissionGrants.Add(new UserPermissionGrant(Guid.CreateVersion7(), OrganisationB, UserB,
                PermissionCodes.FoundationView.Value, PermissionScope.Organisation, null, GrantEffect.Allow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PermissionScope[] PermittedScopes(string code) => code switch
    {
        "foundation.view" => [PermissionScope.Self, PermissionScope.Organisation],
        "user.manage" or "role.manage" or "settings.manage" or "audit.view" or "audit.export" =>
            [PermissionScope.Department, PermissionScope.Organisation],
        _ => Enum.GetValues<PermissionScope>()
    };

    private static string Description(string code) => $"Allows the {code.Replace('.', ' ')} capability. Reserved capabilities remain unavailable until their module is implemented.";
    private static string Risk(string code) => code is "role.manage" or "user.manage" or "settings.manage" or "audit.export" or "integration.manage" ? "high" : "standard";
}
