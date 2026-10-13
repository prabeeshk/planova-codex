using Planova.SharedKernel.Domain;

namespace Planova.Identity.Domain;

public sealed class User : AggregateRoot
{
    private User() { }

    public User(Guid id, Guid organisationId, string externalSubject, string email, string displayName)
        : this(id, organisationId, "development", externalSubject, email, displayName, null, "UTC", "en", null)
    {
    }

    public User(Guid id, Guid organisationId, string externalIssuer, string externalSubject, string email,
        string displayName, string? jobTitle, string timeZone, string locale, Guid? actorId)
    {
        Id = id;
        OrganisationId = organisationId;
        ExternalIssuer = Required(externalIssuer, 500);
        ExternalSubject = Required(externalSubject, 255);
        Email = Required(email, 320).ToLowerInvariant();
        DisplayName = Required(displayName, 200);
        JobTitle = Optional(jobTitle, 200);
        TimeZone = Required(timeZone, 100);
        Locale = Required(locale, 20);
        CreatedBy = actorId;
        UpdatedBy = actorId;
    }

    public Guid OrganisationId { get; private set; }
    public string ExternalIssuer { get; private set; } = string.Empty;
    public string ExternalSubject { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? JobTitle { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public string Locale { get; private set; } = "en";
    public string Status { get; private set; } = "active";
    public bool IsEmergencyAdministrator { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public void Suspend(Guid actorId, DateTimeOffset now) => ChangeStatus("suspended", actorId, now);
    public void Deactivate(Guid actorId, DateTimeOffset now) => ChangeStatus("deactivated", actorId, now);
    public void Reactivate(Guid actorId, DateTimeOffset now) => ChangeStatus("active", actorId, now);

    public void ChangeIdentityLink(string issuer, string subject, Guid actorId, DateTimeOffset now)
    {
        ExternalIssuer = Required(issuer, 500);
        ExternalSubject = Required(subject, 255);
        UpdatedBy = actorId;
        UpdatedAt = now;
    }

    public void DesignateEmergencyAdministrator(Guid actorId, DateTimeOffset now)
    {
        IsEmergencyAdministrator = true;
        UpdatedBy = actorId;
        UpdatedAt = now;
    }

    public void RemoveEmergencyAdministrator(Guid actorId, DateTimeOffset now)
    {
        IsEmergencyAdministrator = false;
        UpdatedBy = actorId;
        UpdatedAt = now;
    }

    private void ChangeStatus(string status, Guid actorId, DateTimeOffset now)
    {
        Status = status;
        UpdatedBy = actorId;
        UpdatedAt = now;
    }

    private static string Required(string value, int maximumLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 || trimmed.Length > maximumLength)
        {
            throw new DomainRuleException("invalid_user", "A required user value is invalid.");
        }
        return trimmed;
    }

    private static string? Optional(string? value, int maximumLength)
    {
        var trimmed = value?.Trim();
        if (trimmed?.Length > maximumLength)
        {
            throw new DomainRuleException("invalid_user", "A user value is too long.");
        }
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
