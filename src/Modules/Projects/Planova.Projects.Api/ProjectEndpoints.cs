using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Planova.Projects.Application;
using Planova.Projects.Contracts;
using Planova.SharedKernel.Results;

namespace Planova.Projects.Api;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects").RequireAuthorization().WithTags("Projects");
        group.MapPost("/", async (HttpContext context, CreateProjectRequest request, IProjectService service, CancellationToken ct) =>
            ToCreated(await service.CreateAsync(request, context.Request.Headers["Idempotency-Key"].ToString(), ct)))
            .WithSummary("Creates a Draft project with project.create authority over the owning department.");
        group.MapGet("/", async (string? search, int? take, IProjectService service, CancellationToken ct) =>
            ToResult(await service.ListAsync(search, take ?? 50, ct))).WithSummary("Lists only projects visible through project.view scope.");
        group.MapGet("/{id:guid}", async (Guid id, IProjectService service, CancellationToken ct) =>
            ToResult(await service.GetAsync(id, ct))).WithSummary("Gets a safely scoped project.");
        group.MapPut("/{id:guid}", async (Guid id, UpdateProjectRequest request, IProjectService service, CancellationToken ct) => ToResult(await service.UpdateAsync(id, request, ct)))
            .WithSummary("Edits planning fields using project.edit, a reason, and an expected concurrency version.");
        group.MapPost("/{id:guid}/transitions", async (Guid id, TransitionProjectRequest request, IProjectService service, CancellationToken ct) => ToResult(await service.TransitionAsync(id, request, ct)))
            .WithSummary("Performs an explicit lifecycle transition; archival requires project.archive.");
        group.MapGet("/{id:guid}/members", async (Guid id,IProjectService service,CancellationToken ct)=>ToResult(await service.ListMembersAsync(id,ct)));
        group.MapPost("/{id:guid}/members", async (Guid id,AddProjectMemberRequest request,IProjectService service,CancellationToken ct)=>ToResult(await service.AddMemberAsync(id,request,ct)));
        group.MapDelete("/{id:guid}/members/{memberId:guid}", async (Guid id,Guid memberId,IProjectService service,CancellationToken ct)=>ToResult(await service.RevokeMemberAsync(id,memberId,ct)));
        group.MapGet("/{id:guid}/health", async (Guid id,IProjectService service,CancellationToken ct)=>ToResult(await service.ListHealthAsync(id,ct)));
        group.MapPost("/{id:guid}/health", async (Guid id,ReportProjectHealthRequest request,IProjectService service,CancellationToken ct)=>ToResult(await service.ReportHealthAsync(id,request,ct)));
        group.MapGet("/{id:guid}/baselines", async (Guid id,IProjectService service,CancellationToken ct)=>ToResult(await service.ListBaselinesAsync(id,ct)));
        group.MapPost("/{id:guid}/baselines", async (Guid id,CreateProjectBaselineRequest request,IProjectService service,CancellationToken ct)=>ToResult(await service.CreateBaselineAsync(id,request,ct)));
        group.MapPost("/{id:guid}/baselines/{baselineId:guid}/establish", async (Guid id,Guid baselineId,EstablishBaselineRequest request,IProjectService service,CancellationToken ct)=>ToResult(await service.EstablishBaselineAsync(id,baselineId,request,ct)));
        group.MapGet("/{id:guid}/settings", async (Guid id,IProjectService service,CancellationToken ct)=>ToResult(await service.GetSettingsAsync(id,ct)));
        return endpoints;
    }

    private static IResult ToCreated<T>(Result<T> result) => result.IsSuccess
        ? Results.Created($"/api/v1/projects/{result.Value?.GetType().GetProperty("Id")?.GetValue(result.Value)}", result.Value)
        : ToProblem(result.Error!);
    private static IResult ToResult<T>(Result<T> result) => result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error!);
    private static IResult ToProblem(PlanovaError error)
    {
        var status = error.Code switch
        {
            "not_found" => 404,
            "project_code_exists" or "idempotency_key_reused" or "concurrency_conflict" or "baseline_immutable" => 409,
            "permission_not_granted" or "explicit_deny" or "cross_tenant_access_denied" => 403,
            _ => 400
        };
        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Request could not be completed",
            detail: error.Message, extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
