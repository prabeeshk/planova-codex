using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planova.Infrastructure.Persistence;
using Planova.SharedKernel.Events;
using Planova.SharedKernel.Time;

namespace Planova.Infrastructure.Events;

public interface IOutboxWriter
{
    void Add(DomainEventEnvelope envelope);
}

public sealed class EfOutboxWriter(PlatformDbContext dbContext) : IOutboxWriter
{
    public void Add(DomainEventEnvelope envelope) => dbContext.OutboxMessages.Add(new OutboxMessage(envelope));
}

public interface IOutboxSink
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}

public sealed class LoggingOutboxSink(ILogger<LoggingOutboxSink> logger) : IOutboxSink
{
    private static readonly Action<ILogger, Guid, string, int, Exception?> PublishedMessage =
        LoggerMessage.Define<Guid, string, int>(
            LogLevel.Information,
            new EventId(1001, nameof(PublishedMessage)),
            "Published outbox message {MessageId} {EventType}.v{EventVersion}");

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        PublishedMessage(logger, message.Id, message.EventType, message.EventVersion, null);
        return Task.CompletedTask;
    }
}

public sealed class OutboxDispatcher(IServiceScopeFactory scopeFactory, IClock clock, ILogger<OutboxDispatcher> logger)
    : BackgroundService
{
    private static readonly Action<ILogger, Exception?> DispatchCycleFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1002, nameof(DispatchCycleFailed)),
            "Outbox dispatch cycle failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                DispatchCycleFailed(logger, exception);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sink = scope.ServiceProvider.GetRequiredService<IOutboxSink>();
        var messages = await dbContext.OutboxMessages
            .IgnoreQueryFilters()
            .Where(x => x.Status == "pending" && x.NextAttemptAt <= clock.UtcNow)
            .OrderBy(x => x.OccurredAt)
            .Take(50)
            .ToArrayAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await sink.PublishAsync(message, cancellationToken);
                message.MarkPublished(clock.UtcNow);
            }
            catch (Exception exception)
            {
                message.MarkFailed(clock.UtcNow, exception.GetType().Name, deadLetterAfter: 10);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record FoundationContextViewedV1(Guid UserId) : IDomainEvent
{
    public string EventType => "FoundationContextViewed";
    public int EventVersion => 1;
    public string ToPayload() => JsonSerializer.Serialize(this);
}
