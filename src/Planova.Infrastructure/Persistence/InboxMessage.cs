namespace Planova.Infrastructure.Persistence;

public sealed class InboxMessage
{
    private InboxMessage() { }

    public InboxMessage(string consumer, Guid messageId, DateTimeOffset receivedAt)
    {
        Consumer = consumer;
        MessageId = messageId;
        ReceivedAt = receivedAt;
        Outcome = "processing";
    }

    public string Consumer { get; private set; } = string.Empty;
    public Guid MessageId { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string Outcome { get; private set; } = string.Empty;

    public void MarkProcessed(DateTimeOffset instant)
    {
        ProcessedAt = instant;
        Outcome = "processed";
    }
}
