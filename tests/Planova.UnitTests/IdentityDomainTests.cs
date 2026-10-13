using Planova.Identity.Application;
using Planova.Identity.Domain;
using Planova.SharedKernel.Security;

namespace Planova.UnitTests;

public sealed class IdentityDomainTests
{
    [Fact]
    public void Organisation_unit_cannot_be_its_own_parent()
    {
        var id = Guid.CreateVersion7();
        var unit = new OrganisationUnit(id, Guid.CreateVersion7(), null, "department", "A", "A", $"/{id:D}/",
            Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        var exception = Assert.Throws<DomainRuleException>(() =>
            unit.Move(id, $"/{id:D}/{id:D}/", Guid.CreateVersion7(), DateTimeOffset.UtcNow));

        Assert.Equal("organisation_hierarchy_cycle", exception.Code);
    }

    [Fact]
    public void Role_grant_rejects_unsupported_conditions()
    {
        var exception = Assert.Throws<DomainRuleException>(() => new RoleGrant(Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), Guid.CreateVersion7(), GrantEffect.Allow, PermissionScope.Organisation, null,
            "{\"unknown\":true}", Guid.CreateVersion7(), DateTimeOffset.UtcNow));
        Assert.Equal("unsupported_grant_condition", exception.Code);
    }

    [Fact]
    public void Assignment_and_grant_scopes_constrain_one_another()
    {
        var parent = Guid.CreateVersion7();
        var child = Guid.CreateVersion7();
        var result = ScopeIntersection.Intersect(PermissionScope.Organisation, null, PermissionScope.Department, child,
            (_, _) => false);
        var hierarchy = ScopeIntersection.Intersect(PermissionScope.Department, parent, PermissionScope.Department, child,
            (candidate, ancestor) => candidate == child && ancestor == parent);

        Assert.True(result.HasValue);
        Assert.Equal(PermissionScope.Department, result.Scope);
        Assert.Equal(child, result.ReferenceId);
        Assert.True(hierarchy.HasValue);
        Assert.Equal(child, hierarchy.ReferenceId);
    }

    [Fact]
    public void Delegation_cannot_chain_to_self_or_have_an_invalid_period()
    {
        var user = Guid.CreateVersion7();
        Assert.Throws<DomainRuleException>(() => new Delegation(Guid.CreateVersion7(), Guid.CreateVersion7(), user, user,
            ["user.manage"], PermissionScope.Organisation, null, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1), "reason", user, DateTimeOffset.UtcNow));
    }
}
