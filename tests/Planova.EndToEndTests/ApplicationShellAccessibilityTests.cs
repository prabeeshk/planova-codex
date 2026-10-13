using System.Net;

namespace Planova.EndToEndTests;

public sealed class ApplicationShellAccessibilityTests(PlanovaWebApplicationFactory factory) : IClassFixture<PlanovaWebApplicationFactory>
{
    [Fact]
    public async Task Shell_contains_core_accessibility_landmarks()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("lang=\"en\"", html, StringComparison.Ordinal);
        Assert.Contains("Skip to main content", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Primary navigation\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"main-content\"", html, StringComparison.Ordinal);
        Assert.Contains("<h1>", html, StringComparison.Ordinal);
    }
}
