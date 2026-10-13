using Microsoft.EntityFrameworkCore;
using Planova.Infrastructure.Persistence;
using Planova.SharedKernel.Time;

namespace Planova.Infrastructure.Events;

public sealed class IdempotentEventProcessor(PlatformDbContext dbContext, IClock clock)
{
    public async Task<bool> ProcessOnceAsync(
        string consumer,
        Guid messageId,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.InboxMessages.AnyAsync(
                x => x.Consumer == consumer && x.MessageId == messageId,
                cancellationToken))
        {
            return false;
        }

        var inbox = new InboxMessage(consumer, messageId, clock.UtcNow);
        dbContext.InboxMessages.Add(inbox);
        await handler(cancellationToken);
        inbox.MarkProcessed(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
