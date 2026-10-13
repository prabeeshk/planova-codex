using Planova.SharedKernel.Domain;

namespace Planova.Projects.Domain;

public sealed class Project : AggregateRoot
{
    private Project() { }

    public Project(Guid id, Guid organisationId, string code, string name, string? description,
        Guid owningOrganisationUnitId, Guid sponsorUserId, Guid managerUserId, string projectType,
        string deliveryMethod, string priority, DateOnly startDate, DateOnly targetEndDate,
        decimal progressPercentage, string currencyCode, string idempotencyKey, string requestFingerprint,
        Guid actorId, DateTimeOffset now)
    {
        if (targetEndDate < startDate) throw new ProjectRuleException("invalid_project_dates", "Target end date cannot precede start date.");
        if (progressPercentage is < 0 or > 100) throw new ProjectRuleException("invalid_project_progress", "Progress must be between 0 and 100.");
        Id = id; OrganisationId = organisationId; Code = Required(code, 50).ToUpperInvariant();
        Name = Required(name, 200); Description = Optional(description, 4000); OwningOrganisationUnitId = owningOrganisationUnitId;
        SponsorUserId = sponsorUserId; ManagerUserId = managerUserId; ProjectType = Required(projectType, 50);
        DeliveryMethod = NormalizeDeliveryMethod(deliveryMethod); Priority = Required(priority, 30);
        StartDate = startDate; TargetEndDate = targetEndDate; ProgressPercentage = progressPercentage;
        CurrencyCode = Required(currencyCode, 3).ToUpperInvariant(); CreateIdempotencyKey = Required(idempotencyKey, 200);
        CreateRequestFingerprint = Required(requestFingerprint, 64); CreatedBy = UpdatedBy = actorId; CreatedAt = UpdatedAt = now;
    }

    public Guid OrganisationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid OwningOrganisationUnitId { get; private set; }
    public Guid SponsorUserId { get; private set; }
    public Guid ManagerUserId { get; private set; }
    public string ProjectType { get; private set; } = string.Empty;
    public string DeliveryMethod { get; private set; } = string.Empty;
    public string Priority { get; private set; } = string.Empty;
    public string LifecycleStatus { get; private set; } = "draft";
    public string OverallHealth { get; private set; } = "unknown";
    public DateOnly StartDate { get; private set; }
    public DateOnly TargetEndDate { get; private set; }
    public DateOnly? ActualEndDate { get; private set; }
    public decimal ProgressPercentage { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public string CreateIdempotencyKey { get; private set; } = string.Empty;
    public string CreateRequestFingerprint { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }

    public void Edit(string name, string? description, Guid owningUnitId, Guid sponsorId, Guid managerId,
        string projectType, string deliveryMethod, string priority, DateOnly startDate, DateOnly targetEndDate,
        decimal progress, string currencyCode, Guid actor, DateTimeOffset now)
    {
        EnsureEditable();
        if (targetEndDate < startDate) throw new ProjectRuleException("invalid_project_dates", "Target end date cannot precede start date.");
        if (progress is < 0 or > 100) throw new ProjectRuleException("invalid_project_progress", "Progress must be between 0 and 100.");
        Name = Required(name, 200); Description = Optional(description, 4000); OwningOrganisationUnitId = owningUnitId;
        SponsorUserId = sponsorId; ManagerUserId = managerId; ProjectType = Required(projectType, 50);
        DeliveryMethod = NormalizeDeliveryMethod(deliveryMethod); Priority = Required(priority, 30);
        StartDate = startDate; TargetEndDate = targetEndDate; ProgressPercentage = progress;
        CurrencyCode = Required(currencyCode, 3).ToUpperInvariant(); UpdatedBy = actor; UpdatedAt = now;
    }

    public void TransitionTo(string targetStatus, DateOnly? actualEndDate, Guid actor, DateTimeOffset now)
    {
        var target = targetStatus.Trim().ToLowerInvariant();
        var allowed = LifecycleStatus switch
        {
            "draft" => target is "proposed" or "cancelled",
            "proposed" => target is "draft" or "active" or "cancelled",
            "active" => target is "on_hold" or "completed" or "cancelled",
            "on_hold" => target is "active" or "cancelled",
            "completed" or "cancelled" => target is "archived",
            _ => false
        };
        if (!allowed) throw new ProjectRuleException("invalid_lifecycle_transition", $"A project cannot transition from {LifecycleStatus} to {target}.");
        if (target == "completed" && actualEndDate is null) throw new ProjectRuleException("actual_end_date_required", "Completion requires an actual end date.");
        LifecycleStatus = target; ActualEndDate = actualEndDate ?? ActualEndDate; UpdatedBy = actor; UpdatedAt = now;
    }

    public void SetOverallHealth(string health, Guid actor, DateTimeOffset now)
    { OverallHealth = ProjectValues.Health(health); UpdatedBy = actor; UpdatedAt = now; }

    private void EnsureEditable()
    { if (LifecycleStatus == "archived") throw new ProjectRuleException("project_archived", "Archived projects are read-only."); }

    private static string NormalizeDeliveryMethod(string value) => value.Trim().ToLowerInvariant() switch
    {
        "agile" => "agile", "waterfall" => "waterfall", "hybrid" => "hybrid", "operational" => "operational",
        _ => throw new ProjectRuleException("invalid_delivery_method", "Delivery method must be Agile, Waterfall, Hybrid, or Operational.")
    };
    private static string Required(string value, int maximum)
    { var result = value?.Trim() ?? string.Empty; if (result.Length is 0 || result.Length > maximum) throw new ProjectRuleException("invalid_project", "A required project value is invalid."); return result; }
    private static string? Optional(string? value, int maximum)
    { var result = value?.Trim(); if (result?.Length > maximum) throw new ProjectRuleException("invalid_project", "A project value is too long."); return string.IsNullOrEmpty(result) ? null : result; }
}

public static class ProjectValues
{
    public static string Health(string value) => value.Trim().ToLowerInvariant() switch
    { "unknown" => "unknown", "green" => "green", "amber" => "amber", "red" => "red", _ => throw new ProjectRuleException("invalid_health", "Health must be Unknown, Green, Amber, or Red.") };
    public static string Role(string value) => value.Trim().ToLowerInvariant() switch
    { "project_manager" => "project_manager", "contributor" => "contributor", "facilitator" => "facilitator", "sponsor" => "sponsor", "viewer" => "viewer", _ => throw new ProjectRuleException("invalid_project_role", "The project role is invalid.") };
}

public sealed class ProjectStatusHistory
{
    private ProjectStatusHistory() { }
    public ProjectStatusHistory(Guid id, Guid organisationId, Guid projectId, string from, string to, string reason, Guid actor, DateTimeOffset at)
    { Id = id; OrganisationId = organisationId; ProjectId = projectId; FromStatus = from; ToStatus = to; Reason = reason.Trim(); ChangedBy = actor; ChangedAt = at; }
    public Guid Id { get; private set; } public Guid OrganisationId { get; private set; } public Guid ProjectId { get; private set; }
    public string FromStatus { get; private set; } = ""; public string ToStatus { get; private set; } = ""; public string Reason { get; private set; } = "";
    public Guid ChangedBy { get; private set; } public DateTimeOffset ChangedAt { get; private set; }
}

public sealed class ProjectMember
{
    private ProjectMember() { }
    public ProjectMember(Guid id, Guid organisationId, Guid projectId, Guid userId, string role, DateOnly start, DateOnly? end, Guid actor, DateTimeOffset at)
    { if (end < start) throw new ProjectRuleException("invalid_membership_dates", "Membership end cannot precede its start."); Id=id; OrganisationId=organisationId; ProjectId=projectId; UserId=userId; Role=ProjectValues.Role(role); StartDate=start; EndDate=end; Status="active"; CreatedBy=UpdatedBy=actor; CreatedAt=UpdatedAt=at; }
    public Guid Id { get; private set; } public Guid OrganisationId { get; private set; } public Guid ProjectId { get; private set; } public Guid UserId { get; private set; }
    public string Role { get; private set; } = ""; public DateOnly StartDate { get; private set; } public DateOnly? EndDate { get; private set; } public string Status { get; private set; } = "";
    public Guid CreatedBy { get; private set; } public DateTimeOffset CreatedAt { get; private set; } public Guid UpdatedBy { get; private set; } public DateTimeOffset UpdatedAt { get; private set; }
    public void Revoke(Guid actor, DateTimeOffset at) { Status="revoked"; UpdatedBy=actor; UpdatedAt=at; }
}

public sealed class ProjectHealthUpdate
{
    private ProjectHealthUpdate() { }
    public ProjectHealthUpdate(Guid id, Guid organisationId, Guid projectId, string overall, string schedule, string cost, string scope, string resource,
        string summary, string rationale, DateOnly effectiveDate, Guid? correctsId, Guid actor, DateTimeOffset at)
    { Id=id; OrganisationId=organisationId; ProjectId=projectId; Overall=ProjectValues.Health(overall); Schedule=ProjectValues.Health(schedule); Cost=ProjectValues.Health(cost); Scope=ProjectValues.Health(scope); Resource=ProjectValues.Health(resource); Summary=summary.Trim(); Rationale=rationale.Trim(); EffectiveDate=effectiveDate; CorrectsUpdateId=correctsId; SubmittedBy=actor; SubmittedAt=at; }
    public Guid Id { get; private set; } public Guid OrganisationId { get; private set; } public Guid ProjectId { get; private set; }
    public string Overall { get; private set; }=""; public string Schedule { get; private set; }=""; public string Cost { get; private set; }=""; public string Scope { get; private set; }=""; public string Resource { get; private set; }="";
    public string Summary { get; private set; }=""; public string Rationale { get; private set; }=""; public DateOnly EffectiveDate { get; private set; } public Guid? CorrectsUpdateId { get; private set; } public Guid SubmittedBy { get; private set; } public DateTimeOffset SubmittedAt { get; private set; }
}

public sealed class ProjectBaseline
{
    private ProjectBaseline() { }
    public ProjectBaseline(Guid id, Guid organisationId, Guid projectId, int version, string name, string scopeSummary, DateOnly start, DateOnly end, decimal? budget, string currency, string snapshotHash, Guid actor, DateTimeOffset at)
    { Id=id; OrganisationId=organisationId; ProjectId=projectId; Version=version; Name=name.Trim(); ScopeSummary=scopeSummary.Trim(); StartDate=start; EndDate=end; PlanningBudgetAmount=budget; CurrencyCode=currency.Trim().ToUpperInvariant(); SnapshotHash=snapshotHash; Status="draft"; CreatedBy=actor; CreatedAt=at; }
    public Guid Id { get; private set; } public Guid OrganisationId { get; private set; } public Guid ProjectId { get; private set; } public int Version { get; private set; }
    public string Name { get; private set; }=""; public string ScopeSummary { get; private set; }=""; public DateOnly StartDate { get; private set; } public DateOnly EndDate { get; private set; } public decimal? PlanningBudgetAmount { get; private set; } public string CurrencyCode { get; private set; }=""; public string SnapshotHash { get; private set; }=""; public string Status { get; private set; }=""; public Guid CreatedBy { get; private set; } public DateTimeOffset CreatedAt { get; private set; } public Guid? EstablishedBy { get; private set; } public DateTimeOffset? EstablishedAt { get; private set; }
    public void Establish(Guid actor, DateTimeOffset at) { if(Status!="draft") throw new ProjectRuleException("baseline_immutable", "An established baseline is immutable."); Status="established"; EstablishedBy=actor; EstablishedAt=at; }
    public void Supersede() { if(Status=="established") Status="superseded"; }
}

public sealed class ProjectSetting
{
    private ProjectSetting() { }
    public ProjectSetting(Guid projectId, Guid organisationId) { ProjectId=projectId; OrganisationId=organisationId; ProgressMethod="manual"; }
    public Guid ProjectId { get; private set; } public Guid OrganisationId { get; private set; } public string ProgressMethod { get; private set; }="manual";
}

public sealed class ProjectRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
