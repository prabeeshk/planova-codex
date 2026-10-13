using System.Data.Common;
using Planova.Projects.Contracts;
using Planova.SharedKernel.Results;

namespace Planova.Projects.Application;

public interface IProjectService
{
    Task<Result<ProjectResponse>> CreateAsync(CreateProjectRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<Result<ProjectListResponse>> ListAsync(string? search, int take, CancellationToken cancellationToken);
    Task<Result<ProjectResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<ProjectResponse>> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken);
    Task<Result<ProjectResponse>> TransitionAsync(Guid id, TransitionProjectRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<ProjectMemberResponse>>> ListMembersAsync(Guid id, CancellationToken ct);
    Task<Result<ProjectMemberResponse>> AddMemberAsync(Guid id, AddProjectMemberRequest request, CancellationToken ct);
    Task<Result<bool>> RevokeMemberAsync(Guid id, Guid memberId, CancellationToken ct);
    Task<Result<IReadOnlyCollection<ProjectHealthResponse>>> ListHealthAsync(Guid id, CancellationToken ct);
    Task<Result<ProjectHealthResponse>> ReportHealthAsync(Guid id, ReportProjectHealthRequest request, CancellationToken ct);
    Task<Result<IReadOnlyCollection<ProjectBaselineResponse>>> ListBaselinesAsync(Guid id, CancellationToken ct);
    Task<Result<ProjectBaselineResponse>> CreateBaselineAsync(Guid id, CreateProjectBaselineRequest request, CancellationToken ct);
    Task<Result<ProjectBaselineResponse>> EstablishBaselineAsync(Guid id, Guid baselineId, EstablishBaselineRequest request, CancellationToken ct);
    Task<Result<ProjectSettingsResponse>> GetSettingsAsync(Guid id, CancellationToken ct);
}

public interface IProjectIdentityReader
{
    Task<bool> IsActiveUserAsync(Guid organisationId, Guid userId, CancellationToken cancellationToken);
    Task<bool> IsOrganisationUnitAsync(Guid organisationId, Guid unitId, CancellationToken cancellationToken);
}

public sealed record ProjectOperationEvidence(string Action, Guid ProjectId, string EventType,
    string RedactedChangesJson, string EventPayloadJson);

public interface IProjectEvidenceWriter
{
    Task EnlistAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
    void Add(ProjectOperationEvidence evidence);
    Task SaveAsync(CancellationToken cancellationToken);
}
