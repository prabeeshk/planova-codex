using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planova.Identity.Contracts;
using Planova.Projects.Application;
using Planova.Projects.Contracts;
using Planova.Projects.Domain;
using Planova.SharedKernel.Identity;
using Planova.SharedKernel.Results;
using Planova.SharedKernel.Security;
using Planova.SharedKernel.Time;

namespace Planova.Projects.Infrastructure;

public sealed class ProjectService(ProjectsDbContext db, ICurrentUser currentUser, ITenantContext tenant,
    ICapabilityAuthorizer authorizer, IProjectIdentityReader identity, IProjectEvidenceWriter evidence, IClock clock) : IProjectService
{
    private Guid OrganisationId => tenant.RequireOrganisationId();
    private Guid ActorId => currentUser.UserId ?? throw new InvalidOperationException("An authenticated user is required.");

    public async Task<Result<ProjectResponse>> CreateAsync(CreateProjectRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200) return Fail<ProjectResponse>("idempotency_key_required", "A valid Idempotency-Key header is required.");
        var fingerprint = Fingerprint(request);
        var existing = await db.Projects.SingleOrDefaultAsync(x => x.CreatedBy == ActorId && x.CreateIdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) return existing.CreateRequestFingerprint == fingerprint ? Ok(ToResponse(existing)) : Fail<ProjectResponse>("idempotency_key_reused", "The idempotency key was already used with a different request.");
        var decision = await authorizer.AuthorizeAsync(PermissionCodes.From("project.create"),
            new AuthorizationResource(OrganisationId, PermissionScope.Department, request.OwningOrganisationUnitId), cancellationToken);
        if (!decision.IsAllowed) return Fail<ProjectResponse>(decision.ReasonCode);
        if (!await identity.IsOrganisationUnitAsync(OrganisationId, request.OwningOrganisationUnitId, cancellationToken)) return Fail<ProjectResponse>("invalid_organisation_unit", "The owning organisation unit is unavailable.");
        if (!await identity.IsActiveUserAsync(OrganisationId, request.ManagerUserId, cancellationToken)) return Fail<ProjectResponse>("invalid_manager", "The manager must be an active user in this organisation.");
        if (!await identity.IsActiveUserAsync(OrganisationId, request.SponsorUserId, cancellationToken)) return Fail<ProjectResponse>("invalid_sponsor", "The sponsor must be an active user in this organisation.");
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
#pragma warning disable CA1862
        if (await db.Projects.AnyAsync(x => x.Code == normalizedCode, cancellationToken)) return Fail<ProjectResponse>("project_code_exists", "A project with this code already exists.");
#pragma warning restore CA1862
        try
        {
            var project = new Project(Guid.CreateVersion7(), OrganisationId, request.Code, request.Name, request.Description,
                request.OwningOrganisationUnitId, request.SponsorUserId, request.ManagerUserId, request.ProjectType,
                request.DeliveryMethod, request.Priority, request.StartDate, request.TargetEndDate, request.ProgressPercentage,
                request.CurrencyCode, idempotencyKey, fingerprint, ActorId, clock.UtcNow);
            db.Projects.Add(project);
            db.Settings.Add(new ProjectSetting(project.Id, OrganisationId));
            await CommitAsync(new("project.created", project.Id, "ProjectCreated", JsonSerializer.Serialize(new { request.Code, request.Name, request.OwningOrganisationUnitId }),
                JsonSerializer.Serialize(new { projectId = project.Id, project.Code, project.Name, project.OwningOrganisationUnitId, status = project.LifecycleStatus })), cancellationToken);
            return Ok(ToResponse(project));
        }
        catch (ProjectRuleException exception) { return Fail<ProjectResponse>(exception.Code, exception.Message); }
    }

    public async Task<Result<ProjectListResponse>> ListAsync(string? search, int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 100);
        var values = await db.Projects.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).ToArrayAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(search)) values = values.Where(x => x.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) || x.Code.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        var allowed = new List<Project>();
        foreach (var value in values)
            if ((await AuthorizeViewAsync(value, cancellationToken)).IsAllowed) allowed.Add(value);
        return Ok(new ProjectListResponse(allowed.Take(take).Select(ToResponse).ToArray(), allowed.Count));
    }

    public async Task<Result<ProjectResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var value = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (value is null) return Fail<ProjectResponse>("not_found", "The project was not found.");
        var decision = await AuthorizeViewAsync(value, cancellationToken);
        return decision.IsAllowed ? Ok(ToResponse(value)) : Fail<ProjectResponse>("not_found", "The project was not found.");
    }

    public async Task<Result<ProjectResponse>> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await EditableAsync(id, "project.edit", cancellationToken); if (project is null) return Fail<ProjectResponse>("not_found", "The project was not found.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return Fail<ProjectResponse>("reason_required", "A reason is required for planning changes.");
        if (!VersionMatches(project, request.ExpectedVersion)) return Fail<ProjectResponse>("concurrency_conflict", "The project changed. Reload and retry.");
        if (project.OwningOrganisationUnitId != request.OwningOrganisationUnitId)
        {
            var oldAuth = await DepartmentEditAsync(project.OwningOrganisationUnitId, cancellationToken); var newAuth = await DepartmentEditAsync(request.OwningOrganisationUnitId, cancellationToken);
            if (!oldAuth.IsAllowed || !newAuth.IsAllowed) return Fail<ProjectResponse>("permission_not_granted");
        }
        if (!await identity.IsOrganisationUnitAsync(OrganisationId, request.OwningOrganisationUnitId, cancellationToken)) return Fail<ProjectResponse>("invalid_organisation_unit");
        if (!await identity.IsActiveUserAsync(OrganisationId, request.ManagerUserId, cancellationToken)) return Fail<ProjectResponse>("invalid_manager");
        if (!await identity.IsActiveUserAsync(OrganisationId, request.SponsorUserId, cancellationToken)) return Fail<ProjectResponse>("invalid_sponsor");
        try { project.Edit(request.Name, request.Description, request.OwningOrganisationUnitId, request.SponsorUserId, request.ManagerUserId, request.ProjectType, request.DeliveryMethod, request.Priority, request.StartDate, request.TargetEndDate, request.ProgressPercentage, request.CurrencyCode, ActorId, clock.UtcNow);
            await CommitAsync(new("project.updated", id, "ProjectUpdated", JsonSerializer.Serialize(new { request.Reason }), JsonSerializer.Serialize(new { projectId=id, project.Name, project.OwningOrganisationUnitId })), cancellationToken); return Ok(ToResponse(project)); }
        catch(ProjectRuleException ex){return Fail<ProjectResponse>(ex.Code,ex.Message);} catch(DbUpdateConcurrencyException){return Fail<ProjectResponse>("concurrency_conflict");}
    }

    public async Task<Result<ProjectResponse>> TransitionAsync(Guid id, TransitionProjectRequest request, CancellationToken cancellationToken)
    {
        var permission = request.TargetStatus.Equals("archived", StringComparison.OrdinalIgnoreCase) ? "project.archive" : "project.edit";
        var project = await EditableAsync(id, permission, cancellationToken); if(project is null)return Fail<ProjectResponse>("not_found","The project was not found.");
        if(string.IsNullOrWhiteSpace(request.Reason))return Fail<ProjectResponse>("reason_required","A lifecycle reason is required.");
        if(!VersionMatches(project,request.ExpectedVersion))return Fail<ProjectResponse>("concurrency_conflict");
        var from=project.LifecycleStatus; try { project.TransitionTo(request.TargetStatus,request.ActualEndDate,ActorId,clock.UtcNow); db.StatusHistory.Add(new(Guid.CreateVersion7(),OrganisationId,id,from,project.LifecycleStatus,request.Reason,ActorId,clock.UtcNow));
            await CommitAsync(new(project.LifecycleStatus=="archived"?"project.archived":"project.lifecycle_changed",id,"ProjectLifecycleChanged",JsonSerializer.Serialize(new{from,to=project.LifecycleStatus,request.Reason}),JsonSerializer.Serialize(new{projectId=id,from,to=project.LifecycleStatus})),cancellationToken); return Ok(ToResponse(project)); }
        catch(ProjectRuleException ex){return Fail<ProjectResponse>(ex.Code,ex.Message);} catch(DbUpdateConcurrencyException){return Fail<ProjectResponse>("concurrency_conflict");}
    }

    public async Task<Result<IReadOnlyCollection<ProjectMemberResponse>>> ListMembersAsync(Guid id,CancellationToken ct)
    { if(await VisibleAsync(id,ct) is null)return Fail<IReadOnlyCollection<ProjectMemberResponse>>("not_found"); var rows=await db.Members.AsNoTracking().Where(x=>x.ProjectId==id).OrderBy(x=>x.StartDate).ThenBy(x=>x.Id).ToArrayAsync(ct); return Ok<IReadOnlyCollection<ProjectMemberResponse>>(rows.Select(Member).ToArray()); }
    public async Task<Result<ProjectMemberResponse>> AddMemberAsync(Guid id,AddProjectMemberRequest request,CancellationToken ct)
    { var project=await EditableAsync(id,"project.manage_members",ct); if(project is null)return Fail<ProjectMemberResponse>("not_found"); if(project.LifecycleStatus=="archived")return Fail<ProjectMemberResponse>("project_archived"); if(!await identity.IsActiveUserAsync(OrganisationId,request.UserId,ct))return Fail<ProjectMemberResponse>("invalid_member");
      try { var row=new ProjectMember(Guid.CreateVersion7(),OrganisationId,id,request.UserId,request.Role,request.StartDate,request.EndDate,ActorId,clock.UtcNow); db.Members.Add(row); await CommitAsync(new("project.membership_created",id,"ProjectMembershipChanged",JsonSerializer.Serialize(new{request.UserId,request.Role}),JsonSerializer.Serialize(new{projectId=id,request.UserId,request.Role,status="active"})),ct); return Ok(Member(row)); } catch(ProjectRuleException ex){return Fail<ProjectMemberResponse>(ex.Code,ex.Message);} }
    public async Task<Result<bool>> RevokeMemberAsync(Guid id,Guid memberId,CancellationToken ct)
    { var project=await EditableAsync(id,"project.manage_members",ct); if(project is null)return Fail<bool>("not_found"); var row=await db.Members.SingleOrDefaultAsync(x=>x.ProjectId==id&&x.Id==memberId,ct); if(row is null)return Fail<bool>("not_found"); row.Revoke(ActorId,clock.UtcNow); await CommitAsync(new("project.membership_revoked",id,"ProjectMembershipChanged",JsonSerializer.Serialize(new{memberId}),JsonSerializer.Serialize(new{projectId=id,memberId,status="revoked"})),ct); return Ok(true); }

    public async Task<Result<IReadOnlyCollection<ProjectHealthResponse>>> ListHealthAsync(Guid id,CancellationToken ct)
    { if(await VisibleAsync(id,ct) is null)return Fail<IReadOnlyCollection<ProjectHealthResponse>>("not_found"); var rows=await db.HealthUpdates.AsNoTracking().Where(x=>x.ProjectId==id).OrderByDescending(x=>x.EffectiveDate).ThenByDescending(x=>x.SubmittedAt).ToArrayAsync(ct); return Ok<IReadOnlyCollection<ProjectHealthResponse>>(rows.Select(Health).ToArray()); }
    public async Task<Result<ProjectHealthResponse>> ReportHealthAsync(Guid id,ReportProjectHealthRequest request,CancellationToken ct)
    { var project=await EditableAsync(id,"project.edit",ct); if(project is null)return Fail<ProjectHealthResponse>("not_found"); if(project.LifecycleStatus=="archived")return Fail<ProjectHealthResponse>("project_archived"); if(request.CorrectsUpdateId is Guid corrected&&!await db.HealthUpdates.AnyAsync(x=>x.ProjectId==id&&x.Id==corrected,ct))return Fail<ProjectHealthResponse>("invalid_correction");
      try { var row=new ProjectHealthUpdate(Guid.CreateVersion7(),OrganisationId,id,request.Overall,request.Schedule,request.Cost,request.Scope,request.Resource,request.Summary,request.Rationale,request.EffectiveDate,request.CorrectsUpdateId,ActorId,clock.UtcNow); db.HealthUpdates.Add(row); if(request.EffectiveDate<=DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)){var latest=await db.HealthUpdates.Where(x=>x.ProjectId==id&&x.EffectiveDate<=DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)).OrderByDescending(x=>x.EffectiveDate).ThenByDescending(x=>x.SubmittedAt).FirstOrDefaultAsync(ct); if(latest is null||request.EffectiveDate>latest.EffectiveDate||request.EffectiveDate==latest.EffectiveDate)project.SetOverallHealth(row.Overall,ActorId,clock.UtcNow);} await CommitAsync(new("project.health_reported",id,"ProjectHealthReported",JsonSerializer.Serialize(new{request.Overall,request.EffectiveDate}),JsonSerializer.Serialize(new{projectId=id,row.Id,row.Overall,row.EffectiveDate})),ct); return Ok(Health(row)); } catch(ProjectRuleException ex){return Fail<ProjectHealthResponse>(ex.Code,ex.Message);} }

    public async Task<Result<IReadOnlyCollection<ProjectBaselineResponse>>> ListBaselinesAsync(Guid id,CancellationToken ct)
    { if(await VisibleAsync(id,ct) is null)return Fail<IReadOnlyCollection<ProjectBaselineResponse>>("not_found"); var rows=await db.Baselines.AsNoTracking().Where(x=>x.ProjectId==id).OrderByDescending(x=>x.Version).ToArrayAsync(ct); return Ok<IReadOnlyCollection<ProjectBaselineResponse>>(rows.Select(Baseline).ToArray()); }
    public async Task<Result<ProjectBaselineResponse>> CreateBaselineAsync(Guid id,CreateProjectBaselineRequest request,CancellationToken ct)
    { var project=await EditableAsync(id,"project.baseline",ct); if(project is null)return Fail<ProjectBaselineResponse>("not_found"); if(project.LifecycleStatus=="archived")return Fail<ProjectBaselineResponse>("project_archived"); var version=(await db.Baselines.Where(x=>x.ProjectId==id).MaxAsync(x=>(int?)x.Version,ct)??0)+1; var payload=JsonSerializer.Serialize(new{id,version,request.Name,request.ScopeSummary,project.StartDate,project.TargetEndDate,request.PlanningBudgetAmount,request.CurrencyCode}); var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))); var row=new ProjectBaseline(Guid.CreateVersion7(),OrganisationId,id,version,request.Name,request.ScopeSummary,project.StartDate,project.TargetEndDate,request.PlanningBudgetAmount,request.CurrencyCode,hash,ActorId,clock.UtcNow); db.Baselines.Add(row); await CommitAsync(new("project.baseline_created",id,"ProjectUpdated",JsonSerializer.Serialize(new{version}),JsonSerializer.Serialize(new{projectId=id,row.Id,version,status="draft"})),ct); return Ok(Baseline(row)); }
    public async Task<Result<ProjectBaselineResponse>> EstablishBaselineAsync(Guid id,Guid baselineId,EstablishBaselineRequest request,CancellationToken ct)
    { var project=await EditableAsync(id,"project.baseline",ct); if(project is null)return Fail<ProjectBaselineResponse>("not_found"); if(string.IsNullOrWhiteSpace(request.Reason))return Fail<ProjectBaselineResponse>("reason_required"); var row=await db.Baselines.SingleOrDefaultAsync(x=>x.ProjectId==id&&x.Id==baselineId,ct); if(row is null)return Fail<ProjectBaselineResponse>("not_found"); try { foreach(var old in await db.Baselines.Where(x=>x.ProjectId==id&&x.Status=="established").ToArrayAsync(ct))old.Supersede(); row.Establish(ActorId,clock.UtcNow); await CommitAsync(new("project.baseline_established",id,"ProjectBaselineEstablished",JsonSerializer.Serialize(new{row.Version,request.Reason}),JsonSerializer.Serialize(new{projectId=id,row.Id,row.Version,row.SnapshotHash})),ct); return Ok(Baseline(row)); }catch(ProjectRuleException ex){return Fail<ProjectBaselineResponse>(ex.Code,ex.Message);} }
    public async Task<Result<ProjectSettingsResponse>> GetSettingsAsync(Guid id,CancellationToken ct)
    { if(await VisibleAsync(id,ct) is null)return Fail<ProjectSettingsResponse>("not_found"); var value=await db.Settings.AsNoTracking().SingleOrDefaultAsync(x=>x.ProjectId==id,ct); return Ok(new ProjectSettingsResponse(value?.ProgressMethod??"manual")); }

    private async Task<Project?> VisibleAsync(Guid id,CancellationToken ct){var p=await db.Projects.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct); return p is not null&&(await AuthorizeViewAsync(p,ct)).IsAllowed?p:null;}
    private async Task<Project?> EditableAsync(Guid id,string permission,CancellationToken ct){var p=await db.Projects.SingleOrDefaultAsync(x=>x.Id==id,ct); if(p is null)return null; var d=await authorizer.AuthorizeAsync(PermissionCodes.From(permission),new AuthorizationResource(OrganisationId,PermissionScope.Project,p.Id,AssignedUserId:p.ManagerUserId,DepartmentId:p.OwningOrganisationUnitId,ProjectId:p.Id),ct); return d.IsAllowed?p:null;}
    private Task<AuthorizationDecision> DepartmentEditAsync(Guid unit,CancellationToken ct)=>authorizer.AuthorizeAsync(PermissionCodes.From("project.edit"),new AuthorizationResource(OrganisationId,PermissionScope.Department,unit),ct);
    private bool VersionMatches(Project p,uint expected){if(p.RowVersion!=expected)return false;if(db.Database.ProviderName=="Npgsql.EntityFrameworkCore.PostgreSQL")db.Entry(p).Property(x=>x.RowVersion).OriginalValue=expected;return true;}
    private static ProjectMemberResponse Member(ProjectMember x)=>new(x.Id,x.UserId,x.Role,x.StartDate,x.EndDate,x.Status);
    private static ProjectHealthResponse Health(ProjectHealthUpdate x)=>new(x.Id,x.Overall,x.Schedule,x.Cost,x.Scope,x.Resource,x.Summary,x.Rationale,x.EffectiveDate,x.CorrectsUpdateId,x.SubmittedBy,x.SubmittedAt);
    private static ProjectBaselineResponse Baseline(ProjectBaseline x)=>new(x.Id,x.Version,x.Name,x.ScopeSummary,x.StartDate,x.EndDate,x.PlanningBudgetAmount,x.CurrencyCode,x.Status,x.SnapshotHash,x.CreatedBy,x.CreatedAt,x.EstablishedBy,x.EstablishedAt);

    private Task<AuthorizationDecision> AuthorizeViewAsync(Project project, CancellationToken cancellationToken) =>
        authorizer.AuthorizeAsync(PermissionCodes.From("project.view"), new AuthorizationResource(OrganisationId,
            PermissionScope.Project, project.Id, AssignedUserId: project.ManagerUserId,
            DepartmentId: project.OwningOrganisationUnitId, ProjectId: project.Id), cancellationToken);

    private async Task CommitAsync(ProjectOperationEvidence operation, CancellationToken cancellationToken)
    {
        var shared = string.Equals(db.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal);
        await using var transaction = shared ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken) : null;
        if (transaction is not null) await evidence.EnlistAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken); evidence.Add(operation); await evidence.SaveAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private static string Fingerprint(CreateProjectRequest request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
    private static ProjectResponse ToResponse(Project x) => new(x.Id, x.Code, x.Name, x.Description, x.OwningOrganisationUnitId, x.SponsorUserId, x.ManagerUserId, x.ProjectType, x.DeliveryMethod, x.Priority, x.LifecycleStatus, x.OverallHealth, x.StartDate, x.TargetEndDate, x.ActualEndDate, x.ProgressPercentage, x.CurrencyCode, x.RowVersion);
    private static Result<T> Ok<T>(T value) => new(value, null);
    private static Result<T> Fail<T>(string code, string? message = null) => new(default, new(code, message ?? "You do not have permission to perform this operation."));
}
