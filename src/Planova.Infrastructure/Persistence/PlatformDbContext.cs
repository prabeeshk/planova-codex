using Microsoft.EntityFrameworkCore;
using Planova.SharedKernel.Identity;

namespace Planova.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceAppendOnlyAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceAppendOnlyAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OrganisationId).HasColumnName("organisation_id");
            entity.Property(x => x.ActorType).HasColumnName("actor_type").HasMaxLength(30);
            entity.Property(x => x.ActorId).HasColumnName("actor_id").HasMaxLength(255);
            entity.Property(x => x.EffectiveUserId).HasColumnName("effective_user_id");
            entity.Property(x => x.Action).HasColumnName("action").HasMaxLength(100);
            entity.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100);
            entity.Property(x => x.EntityId).HasColumnName("entity_id");
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(30);
            entity.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
            entity.Property(x => x.CausationId).HasColumnName("causation_id").HasMaxLength(100);
            entity.Property(x => x.Source).HasColumnName("source").HasMaxLength(100);
            entity.Property(x => x.ChangesJson).HasColumnName("changes_json").HasColumnType("jsonb");
            entity.HasIndex(x => new { x.OrganisationId, x.OccurredAt });
            entity.HasQueryFilter(x => tenantContext.OrganisationId != null && x.OrganisationId == tenantContext.OrganisationId);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OrganisationId).HasColumnName("organisation_id");
            entity.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(200);
            entity.Property(x => x.EventVersion).HasColumnName("event_version");
            entity.Property(x => x.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100);
            entity.Property(x => x.AggregateId).HasColumnName("aggregate_id");
            entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            entity.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
            entity.Property(x => x.CausationId).HasColumnName("causation_id").HasMaxLength(100);
            entity.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(30);
            entity.Property(x => x.PublishedAt).HasColumnName("published_at");
            entity.Property(x => x.AttemptCount).HasColumnName("attempt_count");
            entity.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            entity.Property(x => x.LastError).HasColumnName("last_error").HasMaxLength(500);
            entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
            entity.HasIndex(x => new { x.OrganisationId, x.OccurredAt });
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(x => new { x.Consumer, x.MessageId });
            entity.Property(x => x.Consumer).HasColumnName("consumer").HasMaxLength(200);
            entity.Property(x => x.MessageId).HasColumnName("message_id");
            entity.Property(x => x.ReceivedAt).HasColumnName("received_at");
            entity.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            entity.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(30);
        });
    }

    private void EnforceAppendOnlyAudit()
    {
        if (ChangeTracker.Entries<AuditEvent>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Audit evidence is append-only.");
        }
    }
}
