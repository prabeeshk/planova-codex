namespace Planova.Projects.Contracts;

public sealed record CreateProjectRequest(string Code, string Name, string? Description, Guid OwningOrganisationUnitId,
    Guid SponsorUserId, Guid ManagerUserId, string ProjectType, string DeliveryMethod, string Priority,
    DateOnly StartDate, DateOnly TargetEndDate, decimal ProgressPercentage, string CurrencyCode);

public sealed record ProjectResponse(Guid Id, string Code, string Name, string? Description, Guid OwningOrganisationUnitId,
    Guid SponsorUserId, Guid ManagerUserId, string ProjectType, string DeliveryMethod, string Priority,
    string LifecycleStatus, string OverallHealth, DateOnly StartDate, DateOnly TargetEndDate, DateOnly? ActualEndDate,
    decimal ProgressPercentage, string CurrencyCode, uint RowVersion);

public sealed record ProjectListResponse(IReadOnlyCollection<ProjectResponse> Items, int Count);

public sealed record UpdateProjectRequest(string Name, string? Description, Guid OwningOrganisationUnitId, Guid SponsorUserId,
    Guid ManagerUserId, string ProjectType, string DeliveryMethod, string Priority, DateOnly StartDate,
    DateOnly TargetEndDate, decimal ProgressPercentage, string CurrencyCode, string Reason, uint ExpectedVersion);
public sealed record TransitionProjectRequest(string TargetStatus, DateOnly? ActualEndDate, string Reason, uint ExpectedVersion);
public sealed record AddProjectMemberRequest(Guid UserId, string Role, DateOnly StartDate, DateOnly? EndDate);
public sealed record ProjectMemberResponse(Guid Id, Guid UserId, string Role, DateOnly StartDate, DateOnly? EndDate, string Status);
public sealed record ReportProjectHealthRequest(string Overall, string Schedule, string Cost, string Scope, string Resource,
    string Summary, string Rationale, DateOnly EffectiveDate, Guid? CorrectsUpdateId);
public sealed record ProjectHealthResponse(Guid Id, string Overall, string Schedule, string Cost, string Scope, string Resource,
    string Summary, string Rationale, DateOnly EffectiveDate, Guid? CorrectsUpdateId, Guid SubmittedBy, DateTimeOffset SubmittedAt);
public sealed record CreateProjectBaselineRequest(string Name, string ScopeSummary, decimal? PlanningBudgetAmount, string CurrencyCode);
public sealed record EstablishBaselineRequest(string Reason);
public sealed record ProjectBaselineResponse(Guid Id, int Version, string Name, string ScopeSummary, DateOnly StartDate, DateOnly EndDate,
    decimal? PlanningBudgetAmount, string CurrencyCode, string Status, string SnapshotHash, Guid CreatedBy, DateTimeOffset CreatedAt,
    Guid? EstablishedBy, DateTimeOffset? EstablishedAt);
public sealed record ProjectSettingsResponse(string ProgressMethod);
