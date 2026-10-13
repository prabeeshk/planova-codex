using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Planova.Api.Security;
using Planova.Identity.Application;
using Planova.SharedKernel.Identity;
using Planova.SharedKernel.Security;
using Planova.SharedKernel.Time;

namespace Planova.UnitTests;

public sealed class PermissionAndScopeTests
{
    private static readonly Guid OrganisationA = Guid.Parse("019a0000-0000-7000-8000-000000000001");
    private static readonly Guid OrganisationB = Guid.Parse("019a0000-0000-7000-8000-000000000002");
    private static readonly Guid UserA = Guid.Parse("019a0000-0000-7000-8000-000000000101");
    private static readonly PermissionCode Permission = PermissionCode.Create("foundation.view");

    [Fact]
    public void Permission_code_requires_stable_resource_action_form()
    {
        Assert.Equal("project.create", PermissionCode.Create("project.create").Value);
        Assert.Throws<ArgumentException>(() => PermissionCode.Create("Project Manager"));
        Assert.Throws<ArgumentException>(() => PermissionCode.Create("project"));
    }

    [Fact]
    public async Task Organisation_grant_allows_same_tenant()
    {
        var authorizer = CreateAuthorizer([
            new PermissionGrant(Permission, PermissionScope.Organisation, null, GrantEffect.Allow)
        ]);

        var decision = await authorizer.AuthorizeAsync(Permission, new AuthorizationResource(OrganisationA));

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task Explicit_deny_wins_over_allow()
    {
        var authorizer = CreateAuthorizer([
            new PermissionGrant(Permission, PermissionScope.Organisation, null, GrantEffect.Allow),
            new PermissionGrant(Permission, PermissionScope.Organisation, null, GrantEffect.Deny)
        ]);

        var decision = await authorizer.AuthorizeAsync(Permission, new AuthorizationResource(OrganisationA));

        Assert.False(decision.IsAllowed);
        Assert.Equal("explicit_deny", decision.ReasonCode);
    }

    [Fact]
    public async Task Cross_tenant_resource_is_denied_before_grants_are_used()
    {
        var authorizer = CreateAuthorizer([
            new PermissionGrant(Permission, PermissionScope.Organisation, null, GrantEffect.Allow)
        ]);

        var decision = await authorizer.AuthorizeAsync(Permission, new AuthorizationResource(OrganisationB));

        Assert.False(decision.IsAllowed);
        Assert.Equal("cross_tenant_access_denied", decision.ReasonCode);
    }

    [Fact]
    public void Development_authentication_is_rejected_in_production()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DevelopmentAuth:Enabled"] = "true" })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPlanovaAuthentication(configuration, new TestEnvironment("Production")));
    }

    private static CapabilityAuthorizer CreateAuthorizer(IReadOnlyCollection<PermissionGrant> grants) =>
        new(new TestIdentity(UserA, OrganisationA), new TestIdentity(UserA, OrganisationA), new TestGrantStore(grants), new TestScopeResolver(), new FixedClock());

    private sealed class TestIdentity(Guid userId, Guid organisationId) : ICurrentUser, ITenantContext
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string ExternalSubject => "test-user";
        public string DisplayName => "Test User";
        public Guid? OrganisationId => organisationId;
        public bool HasOrganisation => true;
        public Guid RequireOrganisationId() => organisationId;
    }

    private sealed class TestGrantStore(IReadOnlyCollection<PermissionGrant> grants) : IPermissionGrantStore
    {
        public Task<IReadOnlyCollection<PermissionGrant>> GetGrantsAsync(Guid organisationId, Guid userId, PermissionCode permission, CancellationToken cancellationToken) => Task.FromResult(grants);
        public Task<IReadOnlyCollection<string>> GetEffectivePermissionCodesAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<string>>(grants.Select(x => x.Permission.Value).ToArray());
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestScopeResolver : IAuthorizationScopeResolver
    {
        public Task<bool> IsDepartmentWithinAsync(Guid? authorityDepartmentId, Guid? resourceDepartmentId,
            CancellationToken cancellationToken) => Task.FromResult(authorityDepartmentId == resourceDepartmentId);
    }

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
