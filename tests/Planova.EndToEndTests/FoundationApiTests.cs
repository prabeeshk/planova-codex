using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planova.Identity.Infrastructure;
using Planova.Infrastructure.Persistence;

namespace Planova.EndToEndTests;

public sealed class FoundationApiTests(PlanovaWebApplicationFactory factory) : IClassFixture<PlanovaWebApplicationFactory>
{
    [Fact]
    public async Task Anonymous_request_returns_problem_details()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/foundation/context");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("authentication_required", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Permitted_user_receives_context_and_atomic_evidence()
    {
        using var client = CreateClient("admin-a");
        var response = await client.GetAsync("/api/v1/foundation/context");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(DevelopmentIdentitySeeder.OrganisationA, json.GetProperty("organisationId").GetGuid());
        Assert.Contains("foundation.view", json.GetProperty("effectivePermissions").EnumerateArray().Select(x => x.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("correlationId").GetString()));

        using var scope = factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Contains(await platform.AuditEvents.IgnoreQueryFilters().ToArrayAsync(), x => x.Outcome == "succeeded");
        Assert.Contains(await platform.OutboxMessages.ToArrayAsync(), x => x.EventType == "FoundationContextViewed");
    }

    [Fact]
    public async Task User_without_permission_is_forbidden_and_denial_is_audited()
    {
        using var client = CreateClient("reader-a");
        var response = await client.GetAsync("/api/v1/foundation/context");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("access_denied", json.GetProperty("code").GetString());

        using var scope = factory.Services.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Contains(await platform.AuditEvents.IgnoreQueryFilters().ToArrayAsync(), x => x.Outcome == "denied");
    }

    [Fact]
    public async Task Cross_tenant_route_is_denied()
    {
        using var client = CreateClient("admin-a");
        var response = await client.GetAsync($"/api/v1/foundation/organisations/{DevelopmentIdentitySeeder.OrganisationB}/context");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Validation_errors_use_safe_problem_details()
    {
        using var client = CreateClient("admin-a");
        var response = await client.PostAsJsonAsync("/api/v1/foundation/validation-probe", new { value = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_failed", json.GetProperty("code").GetString());
        Assert.True(json.GetProperty("errors").TryGetProperty("value", out _));
    }

    [Fact]
    public async Task Liveness_and_readiness_are_healthy()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task OpenApi_contains_versioned_reference_contract()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(document.GetProperty("paths").TryGetProperty("/api/v1/foundation/context", out _));
    }

    private HttpClient CreateClient(string alias)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Planova-Dev-User", alias);
        return client;
    }
}
