using Microsoft.EntityFrameworkCore;
using Planova.Projects.Domain;
using Planova.SharedKernel.Identity;

namespace Planova.Projects.Infrastructure;

public sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectStatusHistory> StatusHistory => Set<ProjectStatusHistory>();
    public DbSet<ProjectMember> Members => Set<ProjectMember>();
    public DbSet<ProjectHealthUpdate> HealthUpdates => Set<ProjectHealthUpdate>();
    public DbSet<ProjectBaseline> Baselines => Set<ProjectBaseline>();
    public DbSet<ProjectSetting> Settings => Set<ProjectSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("projects");
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id"); entity.Property(x => x.OrganisationId).HasColumnName("organisation_id");
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(50); entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000); entity.Property(x => x.OwningOrganisationUnitId).HasColumnName("owning_organisation_unit_id");
            entity.Property(x => x.SponsorUserId).HasColumnName("sponsor_user_id"); entity.Property(x => x.ManagerUserId).HasColumnName("manager_user_id");
            entity.Property(x => x.ProjectType).HasColumnName("project_type").HasMaxLength(50); entity.Property(x => x.DeliveryMethod).HasColumnName("delivery_method").HasMaxLength(20);
            entity.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(30); entity.Property(x => x.LifecycleStatus).HasColumnName("lifecycle_status").HasMaxLength(20);
            entity.Property(x => x.OverallHealth).HasColumnName("overall_health").HasMaxLength(20); entity.Property(x => x.StartDate).HasColumnName("start_date");
            entity.Property(x => x.TargetEndDate).HasColumnName("target_end_date"); entity.Property(x => x.ActualEndDate).HasColumnName("actual_end_date");
            entity.Property(x => x.ProgressPercentage).HasColumnName("progress_percentage").HasPrecision(5, 2); entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength();
            entity.Property(x => x.CreateIdempotencyKey).HasColumnName("create_idempotency_key").HasMaxLength(200); entity.Property(x => x.CreateRequestFingerprint).HasColumnName("create_request_fingerprint").HasMaxLength(64);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at"); entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at"); entity.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            entity.Property(x => x.RowVersion).HasColumnName("xmin").IsRowVersion();
            entity.HasIndex(x => new { x.OrganisationId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.OrganisationId, x.CreatedBy, x.CreateIdempotencyKey }).IsUnique();
            entity.HasIndex(x => new { x.OrganisationId, x.OwningOrganisationUnitId, x.Name, x.Id });
            entity.HasQueryFilter(x => tenant.OrganisationId != null && x.OrganisationId == tenant.OrganisationId);
        });
        modelBuilder.Entity<ProjectStatusHistory>(e => { e.ToTable("project_status_history"); e.HasKey(x=>x.Id); Snake(e); e.Property(x=>x.FromStatus).HasMaxLength(20); e.Property(x=>x.ToStatus).HasMaxLength(20); e.Property(x=>x.Reason).HasMaxLength(1000); e.HasIndex(x=>new{x.OrganisationId,x.ProjectId,x.ChangedAt}); e.HasQueryFilter(x=>tenant.OrganisationId!=null&&x.OrganisationId==tenant.OrganisationId); });
        modelBuilder.Entity<ProjectMember>(e => { e.ToTable("project_members"); e.HasKey(x=>x.Id); Snake(e); e.Property(x=>x.Role).HasMaxLength(50); e.Property(x=>x.Status).HasMaxLength(20); e.HasIndex(x=>new{x.OrganisationId,x.ProjectId,x.UserId,x.Status}); e.HasQueryFilter(x=>tenant.OrganisationId!=null&&x.OrganisationId==tenant.OrganisationId); });
        modelBuilder.Entity<ProjectHealthUpdate>(e => { e.ToTable("project_health_updates"); e.HasKey(x=>x.Id); Snake(e); e.Property(x=>x.Overall).HasMaxLength(10); e.Property(x=>x.Schedule).HasMaxLength(10); e.Property(x=>x.Cost).HasMaxLength(10); e.Property(x=>x.Scope).HasMaxLength(10); e.Property(x=>x.Resource).HasMaxLength(10); e.Property(x=>x.Summary).HasMaxLength(1000); e.Property(x=>x.Rationale).HasMaxLength(2000); e.HasIndex(x=>new{x.OrganisationId,x.ProjectId,x.EffectiveDate,x.SubmittedAt}); e.HasQueryFilter(x=>tenant.OrganisationId!=null&&x.OrganisationId==tenant.OrganisationId); });
        modelBuilder.Entity<ProjectBaseline>(e => { e.ToTable("project_baselines"); e.HasKey(x=>x.Id); Snake(e); e.Property(x=>x.Name).HasMaxLength(200); e.Property(x=>x.ScopeSummary).HasMaxLength(4000); e.Property(x=>x.CurrencyCode).HasMaxLength(3); e.Property(x=>x.SnapshotHash).HasMaxLength(64); e.Property(x=>x.Status).HasMaxLength(20); e.HasIndex(x=>new{x.OrganisationId,x.ProjectId,x.Version}).IsUnique(); e.HasQueryFilter(x=>tenant.OrganisationId!=null&&x.OrganisationId==tenant.OrganisationId); });
        modelBuilder.Entity<ProjectSetting>(e => { e.ToTable("project_settings"); e.HasKey(x=>x.ProjectId); Snake(e); e.Property(x=>x.ProgressMethod).HasMaxLength(20); e.HasQueryFilter(x=>tenant.OrganisationId!=null&&x.OrganisationId==tenant.OrganisationId); });
    }

    private static void Snake<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e) where T:class
    {
        foreach (var p in e.Metadata.GetProperties()) p.SetColumnName(ToSnake(p.Name));
    }
    private static string ToSnake(string value) => string.Concat(value.Select((c,i)=>char.IsUpper(c)&&i>0?"_"+char.ToLowerInvariant(c):char.ToLowerInvariant(c).ToString()));
}
