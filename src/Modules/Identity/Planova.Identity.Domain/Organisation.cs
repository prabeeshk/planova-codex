using Planova.SharedKernel.Domain;

namespace Planova.Identity.Domain;

public sealed class Organisation : AggregateRoot
{
    private Organisation() { }

    public Organisation(Guid id, string code, string name, string defaultTimeZone, string defaultCurrency, Guid? actorId = null)
    {
        Id = id;
        Code = Required(code, 50);
        Name = Required(name, 200);
        DefaultTimeZone = Required(defaultTimeZone, 100);
        DefaultCurrency = Currency(defaultCurrency);
        CreatedBy = actorId;
        UpdatedBy = actorId;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Status { get; private set; } = "active";
    public string DefaultTimeZone { get; private set; } = "UTC";
    public string DefaultCurrency { get; private set; } = "USD";
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public void UpdateSettings(string name, string defaultTimeZone, string defaultCurrency, Guid actorId, DateTimeOffset now)
    {
        Name = Required(name, 200);
        DefaultTimeZone = Required(defaultTimeZone, 100);
        DefaultCurrency = Currency(defaultCurrency);
        UpdatedBy = actorId;
        UpdatedAt = now;
    }

    private static string Required(string value, int maximumLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 || trimmed.Length > maximumLength)
        {
            throw new DomainRuleException("invalid_organisation_settings", "A required organisation setting is invalid.");
        }
        return trimmed;
    }

    private static string Currency(string value)
    {
        var currency = Required(value, 3).ToUpperInvariant();
        if (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
        {
            throw new DomainRuleException("invalid_currency", "Currency must be a three-letter ISO code.");
        }
        return currency;
    }
}
