using Microsoft.EntityFrameworkCore;
using Planova.Identity.Domain;
using Planova.Identity.Infrastructure;
using Planova.Infrastructure.Events;
using Planova.Infrastructure.Persistence;
using Planova.SharedKernel.Events;
using Planova.SharedKernel.Identity;
using Planova.SharedKernel.Time;

namespace Planova.IntegrationTests;

public sealed class PersistenceFoundationTests
{
    private static readonly Guid OrganisationA = Guid.Parse("019a0000-0000-7000-8000-000000000001");
    private static readonly Guid OrganisationB = Guid.Parse("019a0000-0000-7000-8000-000000000002");

    [Fact]
    public async Task Identity_queries_are_filtered_to_current_tenant()
    {
        var tenant = new TestTenant(OrganisationA);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new IdentityDbContext(options, tenant);
        db.Organisations.AddRange(
            new Organisation(OrganisationA, "A", "A", "UTC", "USD"),
            new Organisation(OrganisationB, "B", "B", "UTC", "USD"));
        db.Users.AddRange(
            new User(Guid.CreateVersion7(), OrganisationA, "a", "a@example.invalid", "A"),
            new User(Guid.CreateVersion7(), OrganisationB, "b", "b@example.invalid", "B"));
        await db.SaveChangesAsync();

        var users = await db.Users.ToArrayAsync();

        Assert.Single(users);
        Assert.Equal(OrganisationA, users[0].OrganisationId);
    }

    [Fact]
    public async Task Audit_is_append_only_and_outbox_is_persisted_with_it()
    {
        var tenant = new TestTenant(OrganisationA);
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new PlatformDbContext(options, tenant);
        var now = DateTimeOffset.UtcNow;
        var audit = new AuditEvent(Guid.CreateVersion7(), OrganisationA, "user", "subject", Guid.CreateVersion7(),
            "foundation.view", "foundation", null, "succeeded", null, now, "correlation", null, "test", "{}");
        db.AuditEvents.Add(audit);
        db.OutboxMessages.Add(new OutboxMessage(new DomainEventEnvelope(Guid.CreateVersion7(), OrganisationA,
            "FoundationViewed", 1, "foundation", Guid.CreateVersion7(), now, "correlation", null, "{}")));
        await db.SaveChangesAsync();

        Assert.Single(await db.AuditEvents.ToArrayAsync());
        Assert.Single(await db.OutboxMessages.ToArrayAsync());

        db.Entry(audit).State = EntityState.Modified;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Inbox_processor_executes_duplicate_message_once()
    {
        var tenant = new TestTenant(OrganisationA);
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new PlatformDbContext(options, tenant);
        var processor = new IdempotentEventProcessor(db, new TestClock());
        var messageId = Guid.CreateVersion7();
        var calls = 0;

        var first = await processor.ProcessOnceAsync("test-consumer", messageId, _ => { calls++; return Task.CompletedTask; });
        var duplicate = await processor.ProcessOnceAsync("test-consumer", messageId, _ => { calls++; return Task.CompletedTask; });

        Assert.True(first);
        Assert.False(duplicate);
        Assert.Equal(1, calls);
    }

    private sealed class TestTenant(Guid organisationId) : ITenantContext
    {
        public Guid? OrganisationId => organisationId;
        public bool HasOrganisation => true;
        public Guid RequireOrganisationId() => organisationId;
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
