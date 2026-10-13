using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Planova.Infrastructure.Persistence;
using Planova.Projects.Application;
using Planova.SharedKernel.Diagnostics;
using Planova.SharedKernel.Events;
using Planova.SharedKernel.Identity;
using Planova.SharedKernel.Time;

namespace Planova.Infrastructure.Events;

public sealed class ProjectEvidenceWriter(PlatformDbContext db, ICurrentUser user, ITenantContext tenant,
    ICorrelationContext correlation, IClock clock) : IProjectEvidenceWriter
{
    public async Task EnlistAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    { db.Database.SetDbConnection(connection, false); await db.Database.UseTransactionAsync(transaction, cancellationToken); }
    public void Add(ProjectOperationEvidence evidence)
    {
        var organisationId = tenant.RequireOrganisationId(); var actor = user.UserId ?? throw new InvalidOperationException("Authenticated user required."); var now = clock.UtcNow;
        db.AuditEvents.Add(new AuditEvent(Guid.CreateVersion7(), organisationId, "user", user.ExternalSubject ?? actor.ToString(), actor,
            evidence.Action, "project", evidence.ProjectId, "succeeded", null, now, correlation.CorrelationId, correlation.CausationId, "api", evidence.RedactedChangesJson));
        db.OutboxMessages.Add(new OutboxMessage(new DomainEventEnvelope(Guid.CreateVersion7(), organisationId, evidence.EventType, 1,
            "project", evidence.ProjectId, now, correlation.CorrelationId, correlation.CausationId, evidence.EventPayloadJson)));
    }
    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
