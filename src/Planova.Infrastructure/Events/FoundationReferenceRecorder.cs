using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Planova.Identity.Application;
using Planova.Infrastructure.Persistence;
using Planova.SharedKernel.Diagnostics;
using Planova.SharedKernel.Events;
using Planova.SharedKernel.Identity;
using Planova.SharedKernel.Operations;
using Planova.SharedKernel.Time;

namespace Planova.Infrastructure.Events;

public sealed class FoundationReferenceRecorder(
    PlatformDbContext dbContext,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    ICorrelationContext correlation,
    IClock clock) : IFoundationReferenceRecorder
{
    public async Task RecordAsync(string outcome, string? reason, CancellationToken cancellationToken = default)
    {
        var organisationId = tenantContext.RequireOrganisationId();
        var userId = currentUser.UserId ?? throw new InvalidOperationException("An authenticated user is required.");
        var occurredAt = clock.UtcNow;

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.CreateVersion7(), organisationId, "user", currentUser.ExternalSubject ?? userId.ToString(), userId,
            "foundation.context.view", "foundation_context", null, outcome, reason, occurredAt,
            correlation.CorrelationId, correlation.CausationId, "api", "{}"));

        var eventName = outcome == "succeeded" ? "FoundationContextViewed" : "FoundationContextAccessDenied";
        dbContext.OutboxMessages.Add(new OutboxMessage(new DomainEventEnvelope(
            Guid.CreateVersion7(), organisationId, eventName, 1, "foundation_context", userId, occurredAt,
            correlation.CorrelationId, correlation.CausationId,
            JsonSerializer.Serialize(new { userId, outcome }))));

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }
}

public sealed class DatabaseDependencyHealthReader(PlatformDbContext dbContext) : IDependencyHealthReader
{
    public async Task<string> GetStatusAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Database.CanConnectAsync(cancellationToken) ? "healthy" : "unhealthy";
}
