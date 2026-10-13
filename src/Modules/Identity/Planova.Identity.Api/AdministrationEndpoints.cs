using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Planova.Identity.Application;
using Planova.Identity.Contracts;
using Planova.SharedKernel.Diagnostics;
using Planova.SharedKernel.Results;

namespace Planova.Identity.Api;

public static class AdministrationEndpoints
{
    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/administration").RequireAuthorization().WithTags("Identity administration");

        group.MapGet("/organisation", async (IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.GetOrganisationAsync(ct))).WithSummary("Gets server-resolved organisation settings. Requires settings.manage.");
        group.MapPut("/organisation", async (UpdateOrganisationRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.UpdateOrganisationAsync(request, ct))).WithSummary("Updates organisation settings with optimistic concurrency. Requires settings.manage.");
        group.MapGet("/organisation-units", async (IIdentityAdministrationService service, CancellationToken ct) => ToResult(await service.ListOrganisationUnitsAsync(ct)))
            .WithSummary("Lists tenant-filtered organisation units. Requires settings.manage.");
        group.MapPost("/organisation-units", async (CreateOrganisationUnitRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToCreated(await service.CreateOrganisationUnitAsync(request, ct), "/api/v1/administration/organisation-units"))
            .WithSummary("Creates an organisation unit within the caller's scope. Requires settings.manage.");
        group.MapPost("/organisation-units/{id:guid}/move", async (Guid id, MoveOrganisationUnitRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.MoveOrganisationUnitAsync(id, request, ct))).WithSummary("Moves an organisation unit and rejects hierarchy cycles.");

        group.MapGet("/users", async (string? search, int? take, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.ListUsersAsync(search, take ?? 50, ct))).WithSummary("Lists users with tenant-safe search and count. Requires user.manage.");
        group.MapGet("/users/{id:guid}", async (Guid id, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.GetUserAsync(id, ct))).WithSummary("Gets a tenant-filtered user. Requires user.manage.");
        group.MapPost("/users", async (ProvisionUserRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToCreated(await service.ProvisionUserAsync(request, ct), "/api/v1/administration/users"))
            .WithSummary("Provisions a user linked by validated issuer and subject. Requires user.manage.");
        foreach (var command in new[] { "suspend", "deactivate", "reactivate" })
        {
            var lifecycleCommand = command;
            group.MapPost($"/users/{{id:guid}}/{lifecycleCommand}", async (Guid id, LifecycleRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
                ToResult(await service.ChangeUserStatusAsync(id, lifecycleCommand, request, ct)))
                .WithSummary($"Explicitly {lifecycleCommand}s a user. Requires user.manage.");
        }
        group.MapPost("/users/{id:guid}/identity-link", async (Guid id, ChangeIdentityLinkRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.ChangeIdentityLinkAsync(id, request, ct))).WithSummary("Changes an enterprise identity link and audits the change.");

        group.MapGet("/groups", async (string? search, int? take, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.ListGroupsAsync(search, take ?? 50, ct))).WithSummary("Lists tenant-filtered groups. Requires user.manage.");
        group.MapPost("/groups", async (CreateGroupRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToCreated(await service.CreateGroupAsync(request, ct), "/api/v1/administration/groups"))
            .WithSummary("Creates a group. Requires user.manage.");
        group.MapPost("/groups/{id:guid}/members", async (Guid id, AddGroupMemberRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.AddGroupMemberAsync(id, request, ct))).WithSummary("Adds a dated membership after grant-authority validation.");
        group.MapPost("/groups/{id:guid}/members/{userId:guid}/revoke", async (Guid id, Guid userId, RevokeRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.RevokeGroupMemberAsync(id, userId, request, ct))).WithSummary("Revokes a group membership explicitly.");

        group.MapGet("/permissions", async (IIdentityAdministrationService service, CancellationToken ct) => ToResult(await service.ListPermissionsAsync(ct)))
            .WithSummary("Lists the protected stable permission catalogue and permitted scopes. Requires role.manage.");
        group.MapGet("/roles", async (IIdentityAdministrationService service, CancellationToken ct) => ToResult(await service.ListRolesAsync(ct)))
            .WithSummary("Lists configurable roles and grants. Requires role.manage.");
        group.MapPost("/roles", async (CreateRoleRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToCreated(await service.CreateRoleAsync(request, ct), "/api/v1/administration/roles"))
            .WithSummary("Creates a configurable role. Requires role.manage.");
        group.MapPut("/roles/{id:guid}", async (Guid id, UpdateRoleRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.UpdateRoleAsync(id, request, ct))).WithSummary("Updates a role using optimistic concurrency.");
        group.MapPost("/roles/{id:guid}/grants", async (Guid id, AddRoleGrantRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.AddRoleGrantAsync(id, request, ct))).WithSummary("Adds a validated permission grant without exceeding grant authority.");
        group.MapPost("/roles/{id:guid}/grants/{grantId:guid}/revoke", async (Guid id, Guid grantId, RevokeRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.RemoveRoleGrantAsync(id, grantId, request, ct))).WithSummary("Removes a role grant explicitly.");

        group.MapPost("/role-assignments", async (HttpContext context, CreateRoleAssignmentRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToCreated(await service.CreateAssignmentAsync(request, context.Request.Headers["Idempotency-Key"].ToString(), ct), "/api/v1/administration/role-assignments"))
            .WithSummary("Creates an idempotent, scoped user or group role assignment.");
        group.MapPost("/role-assignments/{id:guid}/revoke", async (Guid id, RevokeRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.RevokeAssignmentAsync(id, request, ct))).WithSummary("Revokes a role assignment explicitly.");
        group.MapGet("/users/{id:guid}/effective-access", async (Guid id, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.GetEffectiveAccessAsync(id, ct))).WithSummary("Explains effective access sources. Other-user inspection requires role.manage.");

        group.MapGet("/delegations", async (IIdentityAdministrationService service, CancellationToken ct) => ToResult(await service.ListDelegationsAsync(ct)))
            .WithSummary("Lists tenant-filtered delegations. Requires role.manage.");
        group.MapPost("/delegations", async (CreateDelegationRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToCreated(await service.CreateDelegationAsync(request, ct), "/api/v1/administration/delegations"))
            .WithSummary("Creates a time-bound, non-chainable delegation bounded by current authority.");
        group.MapPost("/delegations/{id:guid}/revoke", async (Guid id, RevokeRequest request, IIdentityAdministrationService service, CancellationToken ct) =>
            ToResult(await service.RevokeDelegationAsync(id, request, ct))).WithSummary("Revokes a delegation explicitly.");

        return endpoints;
    }

    private static IResult ToCreated<T>(Result<T> result, string collectionPath) => result.IsSuccess
        ? Results.Created($"{collectionPath}/{ReadId(result.Value)}", result.Value)
        : ToProblem(result.Error!);

    private static IResult ToResult<T>(Result<T> result) => result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error!);

    private static IResult ToProblem(PlanovaError error)
    {
        var status = error.Code switch
        {
            "not_found" => StatusCodes.Status404NotFound,
            "concurrency_conflict" or "organisation_unit_code_exists" or "identity_link_exists" or
                "duplicate_active_membership" => StatusCodes.Status409Conflict,
            "access_denied" or "permission_not_granted" or "explicit_deny" or "grant_authority_exceeded" or
                "delegation_authority_exceeded" or "cross_tenant_access_denied" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Request could not be completed",
            detail: error.Message, extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    private static Guid ReadId<T>(T? value)
    {
        var property = value?.GetType().GetProperty("Id");
        return property?.GetValue(value) is Guid id ? id : Guid.Empty;
    }
}
