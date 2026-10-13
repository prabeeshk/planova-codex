namespace Planova.Infrastructure.Persistence;

public sealed class AuditEvent
{
    private AuditEvent() { }

    public AuditEvent(
        Guid id,
        Guid organisationId,
        string actorType,
        string actorId,
        Guid? effectiveUserId,
        string action,
        string entityType,
        Guid? entityId,
        string outcome,
        string? reason,
        DateTimeOffset occurredAt,
        string correlationId,
        string? causationId,
        string source,
        string changesJson,
        Guid? projectId = null)
    {
        Id = id;
        OrganisationId = organisationId;
        ActorType = actorType;
        ActorId = actorId;
        EffectiveUserId = effectiveUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        ProjectId = projectId;
        Outcome = outcome;
        Reason = reason;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        CausationId = causationId;
        Source = source;
        ChangesJson = changesJson;
    }

    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public string ActorId { get; private set; } = string.Empty;
    public Guid? EffectiveUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? CausationId { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string ChangesJson { get; private set; } = "{}";
}
