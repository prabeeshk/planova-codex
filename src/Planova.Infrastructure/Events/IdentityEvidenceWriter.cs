using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Planova.Identity.Application;
using Planova.Infrastructure.Persistence;
using Planova.SharedKernel.Diagnostics;
using Planova.SharedKernel.Events;
using Planova.SharedKernel.Identity;
using Planova.SharedKernel.Time;

namespace Planova.Infrastructure.Events;

public sealed class IdentityEvidenceWriter(
    PlatformDbContext dbContext,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ICorrelationContext correlation,
    IClock clock) : IIdentityEvidenceWriter
{
    public async Task EnlistAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational()) return;
        dbContext.Database.SetDbConnection(connection, contextOwnsConnection: false);
        await dbContext.Database.UseTransactionAsync(transaction, cancellationToken);
    }

    public void Add(IdentityOperationEvidence evidence)
    {
        var organisationId = tenantContext.RequireOrganisationId();
        var userId = currentUser.UserId ?? throw new InvalidOperationException("An authenticated user is required.");
        var now = clock.UtcNow;
        dbContext.AuditEvents.Add(new AuditEvent(Guid.CreateVersion7(), organisationId, "user",
            currentUser.ExternalSubject ?? userId.ToString(), userId, evidence.Action, evidence.EntityType,
            evidence.EntityId, "succeeded", evidence.Reason, now, correlation.CorrelationId,
            correlation.CausationId, "api", evidence.RedactedChangesJson));
        dbContext.OutboxMessages.Add(new OutboxMessage(new DomainEventEnvelope(Guid.CreateVersion7(), organisationId,
            evidence.EventType, evidence.EventVersion, evidence.EntityType, evidence.EntityId, now,
            correlation.CorrelationId, correlation.CausationId, evidence.EventPayloadJson)));
    }

    public Task SaveAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
