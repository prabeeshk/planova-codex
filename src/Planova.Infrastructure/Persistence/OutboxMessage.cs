using Planova.SharedKernel.Events;

namespace Planova.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(DomainEventEnvelope envelope)
    {
        Id = envelope.MessageId;
        OrganisationId = envelope.OrganisationId;
        EventType = envelope.EventType;
        EventVersion = envelope.EventVersion;
        AggregateType = envelope.AggregateType;
        AggregateId = envelope.AggregateId;
        OccurredAt = envelope.OccurredAt;
        CorrelationId = envelope.CorrelationId;
        CausationId = envelope.CausationId;
        PayloadJson = envelope.Payload;
        Status = "pending";
        NextAttemptAt = envelope.OccurredAt;
    }

    public Guid Id { get; private set; }
    public Guid OrganisationId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public int EventVersion { get; private set; }
    public string AggregateType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? CausationId { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public string Status { get; private set; } = "pending";
    public DateTimeOffset? PublishedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }

    public void MarkPublished(DateTimeOffset instant)
    {
        Status = "published";
        PublishedAt = instant;
        LastError = null;
    }

    public void MarkFailed(DateTimeOffset instant, string safeError, int deadLetterAfter)
    {
        AttemptCount++;
        Status = AttemptCount >= deadLetterAfter ? "dead_letter" : "pending";
        NextAttemptAt = instant.AddSeconds(Math.Min(Math.Pow(2, AttemptCount), 300));
        LastError = safeError.Length <= 500 ? safeError : safeError[..500];
    }
}
