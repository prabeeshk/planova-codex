using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Planova.SharedKernel.Diagnostics;

namespace Planova.Api.Security;

public sealed class ProblemDetailsAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _fallback = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged || authorizeResult.Forbidden)
        {
            var status = authorizeResult.Challenged ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(context.Response.Body, new
            {
                type = "about:blank",
                title = authorizeResult.Challenged ? "Authentication required" : "Access denied",
                status,
                detail = authorizeResult.Challenged
                    ? "Authentication is required to access this resource."
                    : "You do not have permission to access this resource.",
                code = authorizeResult.Challenged ? "authentication_required" : "access_denied",
                correlationId = context.Items[CorrelationHeaders.CorrelationId]?.ToString()
            });
            return;
        }

        await _fallback.HandleAsync(next, context, policy, authorizeResult);
    }
}
