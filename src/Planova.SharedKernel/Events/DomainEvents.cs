namespace Planova.SharedKernel.Events;

public interface IDomainEvent
{
    string EventType { get; }
    int EventVersion { get; }
}

public sealed record DomainEventEnvelope(
    Guid MessageId,
    Guid OrganisationId,
    string EventType,
    int EventVersion,
    string AggregateType,
    Guid AggregateId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? CausationId,
    string Payload);

public interface IEventConsumer<in TEvent> where TEvent : IDomainEvent
{
    string ConsumerName { get; }
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
