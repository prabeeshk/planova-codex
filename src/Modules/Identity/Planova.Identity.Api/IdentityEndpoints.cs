using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Planova.Identity.Application;
using Planova.Identity.Contracts;
using Planova.SharedKernel.Diagnostics;
using Planova.SharedKernel.Identity;
using Planova.SharedKernel.Operations;
using Planova.SharedKernel.Security;

namespace Planova.Identity.Api;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAdministrationEndpoints();
        endpoints.MapGet("/api/v1/foundation/context", GetFoundationContextAsync)
            .RequireAuthorization()
            .WithName("GetFoundationContextV1")
            .WithTags("Foundation")
            .WithSummary("Returns the authenticated Phase 0 reference context.")
            .Produces<FoundationContextResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        endpoints.MapGet("/api/v1/foundation/organisations/{organisationId:guid}/context", GetOrganisationContextAsync)
            .RequireAuthorization()
            .WithName("GetOrganisationFoundationContextV1")
            .WithTags("Foundation")
            .WithSummary("Tenant-bound reference endpoint used to prove cross-tenant rejection.")
            .Produces<FoundationContextResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);
        endpoints.MapPost("/api/v1/foundation/validation-probe", ValidateProbe)
            .RequireAuthorization()
            .WithName("ValidateFoundationProbeV1")
            .WithTags("Foundation")
            .WithSummary("Technical validation reference; it stores no data.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
        return endpoints;
    }

    private static Task<IResult> GetOrganisationContextAsync(
        Guid organisationId,
        HttpContext httpContext,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        ICorrelationContext correlation,
        ICapabilityAuthorizer authorizer,
        IPermissionGrantStore grantStore,
        IFoundationReferenceRecorder recorder,
        IDependencyHealthReader healthReader,
        CancellationToken cancellationToken) =>
        GetFoundationContextCoreAsync(organisationId, httpContext, currentUser, tenantContext, correlation,
            authorizer, grantStore, recorder, healthReader, cancellationToken);

    private static async Task<IResult> GetFoundationContextAsync(
        HttpContext httpContext,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        ICorrelationContext correlation,
        ICapabilityAuthorizer authorizer,
        IPermissionGrantStore grantStore,
        IFoundationReferenceRecorder recorder,
        IDependencyHealthReader healthReader,
        CancellationToken cancellationToken) =>
        await GetFoundationContextCoreAsync(tenantContext.RequireOrganisationId(), httpContext, currentUser,
            tenantContext, correlation, authorizer, grantStore, recorder, healthReader, cancellationToken);

    private static async Task<IResult> GetFoundationContextCoreAsync(
        Guid organisationId,
        HttpContext httpContext,
        ICurrentUser currentUser,
        ITenantContext tenantContext,
        ICorrelationContext correlation,
        ICapabilityAuthorizer authorizer,
        IPermissionGrantStore grantStore,
        IFoundationReferenceRecorder recorder,
        IDependencyHealthReader healthReader,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            PermissionCodes.FoundationView,
            new AuthorizationResource(organisationId),
            cancellationToken);

        if (!decision.IsAllowed)
        {
            await recorder.RecordAsync("denied", decision.ReasonCode, cancellationToken);
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Access denied",
                detail: "You do not have permission to perform this operation.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "access_denied",
                    ["correlationId"] = correlation.CorrelationId
                });
        }

        var userId = currentUser.UserId!.Value;
        var permissions = await grantStore.GetEffectivePermissionCodesAsync(organisationId, userId, cancellationToken);
        var health = await healthReader.GetStatusAsync(cancellationToken);
        await recorder.RecordAsync("succeeded", null, cancellationToken);

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        return Results.Ok(new FoundationContextResponse(
            userId,
            currentUser.DisplayName ?? "Unknown user",
            organisationId,
            permissions,
            correlation.CorrelationId,
            version,
            health));
    }

    private static IResult ValidateProbe(HttpContext context, ValidationProbeRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Value))
        {
            errors["value"] = ["Value is required."];
        }
        else if (request.Value.Length > 50)
        {
            errors["value"] = ["Value must be 50 characters or fewer."];
        }

        return errors.Count == 0
            ? Results.NoContent()
            : Results.ValidationProblem(
                errors,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "validation_failed",
                    ["correlationId"] = context.Items[CorrelationHeaders.CorrelationId]?.ToString()
                });
    }

    private sealed record ValidationProbeRequest(string? Value);
}
