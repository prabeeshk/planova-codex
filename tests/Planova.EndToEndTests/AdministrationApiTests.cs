using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Planova.EndToEndTests;

public sealed class AdministrationApiTests(PlanovaWebApplicationFactory factory) : IClassFixture<PlanovaWebApplicationFactory>
{
    [Fact]
    public async Task User_without_administration_permission_is_forbidden()
    {
        using var client = CreateClient("reader-a");

        var response = await client.GetAsync("/api/v1/administration/organisation");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("permission_not_granted", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Organisation_unit_move_rejects_a_hierarchy_cycle()
    {
        using var client = CreateClient("admin-a");
        var parent = await CreateUnitAsync(client, null, "CYCLE-PARENT", "Cycle parent");
        var child = await CreateUnitAsync(client, parent.GetProperty("id").GetGuid(), "CYCLE-CHILD", "Cycle child");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/administration/organisation-units/{parent.GetProperty("id").GetGuid()}/move",
            new
            {
                parentId = child.GetProperty("id").GetGuid(),
                rowVersion = parent.GetProperty("rowVersion").GetUInt32()
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("organisation_hierarchy_cycle", problem.GetProperty("code").GetString());
    }

    private async Task<JsonElement> CreateUnitAsync(HttpClient client, Guid? parentId, string code, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/administration/organisation-units", new
        {
            parentId,
            type = "department",
            code,
            name
        });
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but received {response.StatusCode}. {factory.LastException}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private HttpClient CreateClient(string alias)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Planova-Dev-User", alias);
        return client;
    }
}
