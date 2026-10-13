using System.Text.RegularExpressions;

namespace Planova.SharedKernel.Security;

public sealed partial record PermissionCode
{
    private PermissionCode(string value) => Value = value;

    public string Value { get; }

    public static PermissionCode Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!PermissionPattern().IsMatch(value))
        {
            throw new ArgumentException("A permission code must use lower-case resource.action form.", nameof(value));
        }

        return new PermissionCode(value);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z][a-z0-9_]*\\.[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PermissionPattern();
}

public enum PermissionScope
{
    Self,
    Assigned,
    Team,
    Project,
    Department,
    Portfolio,
    Organisation
}

public enum GrantEffect
{
    Allow,
    Deny
}

public sealed record PermissionGrant(
    PermissionCode Permission,
    PermissionScope Scope,
    Guid? ScopeReferenceId,
    GrantEffect Effect,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidTo = null)
{
    public bool IsValidAt(DateTimeOffset instant) =>
        (ValidFrom is null || ValidFrom <= instant) &&
        (ValidTo is null || ValidTo > instant);
}
