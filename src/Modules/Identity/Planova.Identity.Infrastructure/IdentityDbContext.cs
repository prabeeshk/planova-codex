using Microsoft.EntityFrameworkCore;
using Planova.Identity.Domain;
using Planova.SharedKernel.Identity;

namespace Planova.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermissionGrant> UserPermissionGrants => Set<UserPermissionGrant>();
    public DbSet<OrganisationUnit> OrganisationUnits => Set<OrganisationUnit>();
    public DbSet<UserOrganisationUnitMembership> UserOrganisationUnitMemberships => Set<UserOrganisationUnitMembership>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<PermissionDefinition> Permissions => Set<PermissionDefinition>();
    public DbSet<PermissionScopeType> PermissionScopeTypes => Set<PermissionScopeType>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleGrant> RoleGrants => Set<RoleGrant>();
    public DbSet<PrincipalRoleAssignment> PrincipalRoleAssignments => Set<PrincipalRoleAssignment>();
    public DbSet<Delegation> Delegations => Set<Delegation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.Entity<Organisation>(entity =>
        {
            entity.ToTable("organisations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(50);
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            entity.Property(x => x.DefaultTimeZone).HasColumnName("default_timezone").HasMaxLength(100);
            entity.Property(x => x.DefaultCurrency).HasColumnName("default_currency").HasMaxLength(3).IsFixedLength();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            entity.Property(x => x.RowVersion).HasColumnName("xmin").IsRowVersion();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasQueryFilter(x => tenantContext.OrganisationId != null && x.Id == tenantContext.OrganisationId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OrganisationId).HasColumnName("organisation_id");
            entity.Property(x => x.ExternalIssuer).HasColumnName("external_issuer").HasMaxLength(500);
            entity.Property(x => x.ExternalSubject).HasColumnName("external_subject").HasMaxLength(255);
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
            entity.Property(x => x.JobTitle).HasColumnName("job_title").HasMaxLength(200);
            entity.Property(x => x.TimeZone).HasColumnName("timezone").HasMaxLength(100);
            entity.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(20);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            entity.Property(x => x.IsEmergencyAdministrator).HasColumnName("is_emergency_administrator");
            entity.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            entity.Property(x => x.RowVersion).HasColumnName("xmin").IsRowVersion();
            entity.HasIndex(x => new { x.OrganisationId, x.ExternalIssuer, x.ExternalSubject }).IsUnique();
            entity.HasIndex(x => new { x.OrganisationId, x.Email }).IsUnique();
            entity.HasQueryFilter(x => tenantContext.OrganisationId != null && x.OrganisationId == tenantContext.OrganisationId);
        });

        modelBuilder.Entity<UserPermissionGrant>(entity =>
        {
            entity.ToTable("user_permission_grants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OrganisationId).HasColumnName("organisation_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.PermissionCode).HasColumnName("permission_code").HasMaxLength(100);
            entity.Property(x => x.Scope).HasColumnName("scope_type").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.ScopeReferenceId).HasColumnName("scope_ref_id");
            entity.Property(x => x.Effect).HasColumnName("effect").HasConversion<string>().HasMaxLength(10);
            entity.Property(x => x.ValidFrom).HasColumnName("valid_from");
            entity.Property(x => x.ValidTo).HasColumnName("valid_to");
            entity.HasIndex(x => new { x.OrganisationId, x.UserId, x.PermissionCode });
            entity.HasQueryFilter(x => tenantContext.OrganisationId != null && x.OrganisationId == tenantContext.OrganisationId);
        });

        ConfigureTenantAggregate(modelBuilder.Entity<OrganisationUnit>(), "organisation_units", tenantContext);
        modelBuilder.Entity<OrganisationUnit>(entity =>
        {
            entity.Property(x => x.ParentId).HasColumnName("parent_id");
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(50);
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(50);
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(x => x.Path).HasColumnName("path").HasMaxLength(2000);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            entity.HasIndex(x => new { x.OrganisationId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.OrganisationId, x.Path });
            entity.HasOne<OrganisationUnit>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserOrganisationUnitMembership>(entity =>
        {
            entity.ToTable("user_organisation_unit_memberships"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.OrganisationId).HasColumnName("organisation_id");
            entity.Property(x => x.UserId).HasColumnName("user_id"); entity.Property(x => x.OrganisationUnitId).HasColumnName("organisation_unit_id");
            entity.Property(x => x.EffectiveFrom).HasColumnName("effective_from"); entity.Property(x => x.EffectiveTo).HasColumnName("effective_to");
            entity.Property(x => x.IsPrimary).HasColumnName("is_primary"); entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.HasIndex(x => new { x.OrganisationId, x.UserId, x.OrganisationUnitId, x.EffectiveFrom }).IsUnique();
            entity.HasQueryFilter(x => tenantContext.OrganisationId != null && x.OrganisationId == tenantContext.OrganisationId);
        });

        ConfigureTenantAggregate(modelBuilder.Entity<Group>(), "groups", tenantContext);
        modelBuilder.Entity<Group>(entity =>
        {
            entity.Property(x => x.ExternalKey).HasColumnName("external_key").HasMaxLength(255);
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(30);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            entity.HasIndex(x => new { x.OrganisationId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.OrganisationId, x.ExternalKey }).IsUnique();
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.ToTable("group_members"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.OrganisationId).HasColumnName("organisation_id");
            entity.Property(x => x.GroupId).HasColumnName("group_id"); entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ValidFrom).HasColumnName("valid_from"); entity.Property(x => x.ValidTo).HasColumnName("valid_to");
            entity.Property(x => x.Source).HasColumnName("source").HasMaxLength(30); entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by"); entity.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            entity.Property(x => x.RevokedBy).HasColumnName("revoked_by");
            entity.HasIndex(x => new { x.OrganisationId, x.GroupId, x.UserId, x.ValidFrom }).IsUnique();
            entity.HasQueryFilter(x => tenantContext.OrganisationId != null && x.OrganisationId == tenantContext.OrganisationId);
        });

        modelBuilder.Entity<PermissionDefinition>(entity =>
        {
            entity.ToTable("permissions"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(100);
            entity.Property(x => x.Resource).HasColumnName("resource").HasMaxLength(50); entity.Property(x => x.Action).HasColumnName("action").HasMaxLength(50);
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(500); entity.Property(x => x.RiskLevel).HasColumnName("risk_level").HasMaxLength(20);
            entity.Property(x => x.IsImplemented).HasColumnName("is_implemented"); entity.Property(x => x.IsSystem).HasColumnName("is_system");
            entity.HasIndex(x => x.Code).IsUnique();
        });
        modelBuilder.Entity<PermissionScopeType>(entity =>
        {
            entity.ToTable("permission_scope_types"); entity.HasKey(x => new { x.PermissionId, x.Scope });
            entity.Property(x => x.PermissionId).HasColumnName("permission_id");
            entity.Property(x => x.Scope).HasColumnName("scope_type").HasConversion<string>().HasMaxLength(30);
        });

        ConfigureTenantAggregate(modelBuilder.Entity<Role>(), "roles", tenantContext);
        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(50); entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000); entity.Property(x => x.IsSystem).HasColumnName("is_system");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20); entity.HasIndex(x => new { x.OrganisationId, x.Code }).IsUnique();
        });
        modelBuilder.Entity<RoleGrant>(entity =>
        {
            entity.ToTable("role_grants"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.OrganisationId).HasColumnName("organisation_id");
            entity.Property(x => x.RoleId).HasColumnName("role_id"); entity.Property(x => x.PermissionId).HasColumnName("permission_id");
            entity.Property(x => x.Effect).HasColumnName("effect").HasConversion<string>().HasMaxLength(10);
            entity.Property(x => x.Scope).HasColumnName("scope_type").HasConversion<string>().HasMaxLength(30); entity.Property(x => x.ScopeReferenceId).HasColumnName("scope_ref_id");
            entity.Property(x => x.ConditionsJson).HasColumnName("conditions_json").HasColumnType("jsonb"); entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.CreatedBy).HasColumnName("created_by"); entity.HasIndex(x => new { x.OrganisationId, x.RoleId, x.PermissionId, x.Effect, x.Scope, x.ScopeReferenceId }).IsUnique();
            entity.HasQueryFilter(x => tenantContext.OrganisationId != null && x.OrganisationId == tenantContext.OrganisationId);
        });

        ConfigureTenantAggregate(modelBuilder.Entity<PrincipalRoleAssignment>(), "principal_role_assignments", tenantContext);
        modelBuilder.Entity<PrincipalRoleAssignment>(entity =>
        {
            entity.Property(x => x.PrincipalType).HasColumnName("principal_type").HasMaxLength(20); entity.Property(x => x.PrincipalId).HasColumnName("principal_id");
            entity.Property(x => x.RoleId).HasColumnName("role_id"); entity.Property(x => x.Scope).HasColumnName("scope_type").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.ScopeReferenceId).HasColumnName("scope_ref_id"); entity.Property(x => x.ValidFrom).HasColumnName("valid_from");
            entity.Property(x => x.ValidTo).HasColumnName("valid_to"); entity.Property(x => x.AssignedBy).HasColumnName("assigned_by");
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500); entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200);
            entity.Property(x => x.RevokedAt).HasColumnName("revoked_at"); entity.Property(x => x.RevokedBy).HasColumnName("revoked_by");
            entity.HasIndex(x => new { x.OrganisationId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => new { x.OrganisationId, x.PrincipalType, x.PrincipalId, x.ValidFrom });
        });

        ConfigureTenantAggregate(modelBuilder.Entity<Delegation>(), "delegations", tenantContext);
        modelBuilder.Entity<Delegation>(entity =>
        {
            entity.Property(x => x.DelegatorUserId).HasColumnName("delegator_user_id"); entity.Property(x => x.DelegateUserId).HasColumnName("delegate_user_id");
            entity.Property(x => x.PermissionFilter).HasColumnName("permission_filter").HasMaxLength(4000);
            entity.Property(x => x.Scope).HasColumnName("scope_type").HasConversion<string>().HasMaxLength(30); entity.Property(x => x.ScopeReferenceId).HasColumnName("scope_ref_id");
            entity.Property(x => x.StartsAt).HasColumnName("starts_at"); entity.Property(x => x.EndsAt).HasColumnName("ends_at");
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500); entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);
            entity.Property(x => x.RevokedAt).HasColumnName("revoked_at"); entity.Property(x => x.RevokedBy).HasColumnName("revoked_by");
            entity.HasIndex(x => new { x.OrganisationId, x.DelegateUserId, x.StartsAt, x.EndsAt });
        });
    }

    private static void ConfigureTenantAggregate<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity,
        string table,
        ITenantContext tenantContext) where TEntity : TenantAggregate
    {
        entity.ToTable(table); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.OrganisationId).HasColumnName("organisation_id"); entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.CreatedBy).HasColumnName("created_by"); entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Property(x => x.UpdatedBy).HasColumnName("updated_by"); entity.Property(x => x.RowVersion).HasColumnName("xmin").IsRowVersion();
        entity.HasQueryFilter(x => tenantContext.OrganisationId != null && x.OrganisationId == tenantContext.OrganisationId);
    }
}
