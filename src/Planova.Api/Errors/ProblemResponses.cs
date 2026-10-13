using Microsoft.AspNetCore.Mvc;
using Planova.SharedKernel.Diagnostics;

namespace Planova.Api.Errors;

public static class ProblemResponses
{
    public static IResult Forbidden(HttpContext context, string code = "access_denied") => Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Access denied",
        detail: "You do not have permission to perform this operation.",
        extensions: Extensions(context, code));

    public static IResult Validation(HttpContext context, IReadOnlyDictionary<string, string[]> errors) =>
        Results.ValidationProblem(
            errors,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation failed",
            extensions: Extensions(context, "validation_failed"));

    private static Dictionary<string, object?> Extensions(HttpContext context, string code) => new()
    {
        ["code"] = code,
        ["correlationId"] = context.Items[CorrelationHeaders.CorrelationId]?.ToString()
    };
}
