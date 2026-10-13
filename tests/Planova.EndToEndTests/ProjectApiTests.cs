using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planova.Identity.Infrastructure;
using Planova.Infrastructure.Persistence;

namespace Planova.EndToEndTests;

public sealed class ProjectApiTests(PlanovaWebApplicationFactory factory) : IClassFixture<PlanovaWebApplicationFactory>
{
    [Fact]
    public async Task Authorised_create_is_idempotent_and_writes_evidence()
    {
        using var client = Client("admin-a"); var key = Guid.NewGuid().ToString(); var request = ValidRequest("PRJ-IDEMP");
        var first = await PostAsync(client, request, key); var retry = await PostAsync(client, request, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var firstJson = await first.Content.ReadFromJsonAsync<JsonElement>(); var retryJson = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(firstJson.GetProperty("id").GetGuid(), retryJson.GetProperty("id").GetGuid()); Assert.Equal("draft", firstJson.GetProperty("lifecycleStatus").GetString());
        using var scope = factory.Services.CreateScope(); var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Contains(await platform.AuditEvents.IgnoreQueryFilters().ToArrayAsync(), x => x.Action == "project.created");
        Assert.Contains(await platform.OutboxMessages.IgnoreQueryFilters().ToArrayAsync(), x => x.EventType == "ProjectCreated");
    }

    [Fact]
    public async Task Duplicate_normalised_code_and_changed_idempotent_payload_are_conflicts()
    {
        using var client = Client("admin-a");
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, ValidRequest(" PRJ-DUP "), Guid.NewGuid().ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, ValidRequest("prj-dup"), Guid.NewGuid().ToString())).StatusCode);
        var key = Guid.NewGuid().ToString(); Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, ValidRequest("PRJ-KEY"), key)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, ValidRequest("PRJ-OTHER"), key)).StatusCode);
    }

    [Fact]
    public async Task Invalid_project_and_unpermitted_user_are_rejected()
    {
        using var admin = Client("admin-a"); using var reader = Client("reader-a");
        var invalid = ValidRequest("PRJ-BAD") with { TargetEndDate = new DateOnly(2025, 1, 1), ProgressPercentage = 101 };
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(admin, invalid, Guid.NewGuid().ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(reader, ValidRequest("PRJ-DENIED"), Guid.NewGuid().ToString())).StatusCode);
    }

    [Fact]
    public async Task Project_management_journey_preserves_history_and_evidence()
    {
        using var client=Client("admin-a"); var created=await PostAsync(client,ValidRequest("PRJ-JOURNEY"),Guid.NewGuid().ToString());
        var project=await created.Content.ReadFromJsonAsync<JsonElement>(); var id=project.GetProperty("id").GetGuid();
        var proposed=await client.PostAsJsonAsync($"/api/v1/projects/{id}/transitions",new { targetStatus="proposed", reason="Ready for review", expectedVersion=0 });
        Assert.Equal(HttpStatusCode.OK,proposed.StatusCode);
        var active=await client.PostAsJsonAsync($"/api/v1/projects/{id}/transitions",new { targetStatus="active", reason="Authorised direct activation", expectedVersion=0 });
        Assert.Equal(HttpStatusCode.OK,active.StatusCode);
        var member=await client.PostAsJsonAsync($"/api/v1/projects/{id}/members",new { userId=DevelopmentIdentitySeeder.AdminA, role="contributor", startDate=new DateOnly(2026,1,1), endDate=(DateOnly?)null });
        Assert.Equal(HttpStatusCode.OK,member.StatusCode);
        var health=await client.PostAsJsonAsync($"/api/v1/projects/{id}/health",new { overall="amber", schedule="amber", cost="unknown", scope="green", resource="green", summary="Manual report", rationale="Schedule risk", effectiveDate=new DateOnly(2026,6,1), correctsUpdateId=(Guid?)null });
        Assert.Equal(HttpStatusCode.OK,health.StatusCode);
        var baseline=await client.PostAsJsonAsync($"/api/v1/projects/{id}/baselines",new { name="Approved plan",scopeSummary="Initial scope",planningBudgetAmount=1000m,currencyCode="AED" });
        var baselineJson=await baseline.Content.ReadFromJsonAsync<JsonElement>(); var baselineId=baselineJson.GetProperty("id").GetGuid();
        var established=await client.PostAsJsonAsync($"/api/v1/projects/{id}/baselines/{baselineId}/establish",new { reason="Governance decision" });
        Assert.Equal(HttpStatusCode.OK,established.StatusCode); Assert.Equal("established",(await established.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Single(await (await client.GetAsync($"/api/v1/projects/{id}/members")).Content.ReadFromJsonAsync<JsonElement[]>() ?? []);
        using var scope=factory.Services.CreateScope(); var platform=scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Contains(await platform.OutboxMessages.IgnoreQueryFilters().ToArrayAsync(),x=>x.EventType=="ProjectBaselineEstablished");
    }

    [Fact]
    public async Task Invalid_transition_and_stale_update_are_conflicts_or_rejected()
    {
        using var client=Client("admin-a"); var created=await PostAsync(client,ValidRequest("PRJ-RULES"),Guid.NewGuid().ToString()); var json=await created.Content.ReadFromJsonAsync<JsonElement>(); var id=json.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/v1/projects/{id}/transitions",new { targetStatus="completed",actualEndDate=new DateOnly(2026,2,1),reason="Invalid jump",expectedVersion=0 })).StatusCode);
        var update=ValidUpdate() with { ExpectedVersion=99 };
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/v1/projects/{id}",update)).StatusCode);
    }

    private static ProjectRequest ValidRequest(string code) => new(code, "Project " + code, null, DevelopmentIdentitySeeder.DepartmentA,
        DevelopmentIdentitySeeder.AdminA, DevelopmentIdentitySeeder.AdminA, "delivery", "Hybrid", "high",
        new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 0, "AED");
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, ProjectRequest request, string key)
    { using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects") { Content = JsonContent.Create(request) }; message.Headers.Add("Idempotency-Key", key); return await client.SendAsync(message); }
    private HttpClient Client(string alias) { var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("X-Planova-Dev-User", alias); return client; }
    private sealed record ProjectRequest(string Code, string Name, string? Description, Guid OwningOrganisationUnitId, Guid SponsorUserId,
        Guid ManagerUserId, string ProjectType, string DeliveryMethod, string Priority, DateOnly StartDate, DateOnly TargetEndDate,
        decimal ProgressPercentage, string CurrencyCode);
    private static UpdateRequest ValidUpdate()=>new("Updated",null,DevelopmentIdentitySeeder.DepartmentA,DevelopmentIdentitySeeder.AdminA,DevelopmentIdentitySeeder.AdminA,"delivery","Hybrid","high",new DateOnly(2026,1,1),new DateOnly(2026,12,31),10,"AED","Plan adjusted",0);
    private sealed record UpdateRequest(string Name,string? Description,Guid OwningOrganisationUnitId,Guid SponsorUserId,Guid ManagerUserId,string ProjectType,string DeliveryMethod,string Priority,DateOnly StartDate,DateOnly TargetEndDate,decimal ProgressPercentage,string CurrencyCode,string Reason,uint ExpectedVersion);
}
