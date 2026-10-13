using System.Diagnostics;
using Planova.SharedKernel.Diagnostics;

namespace Planova.Api.Diagnostics;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var requested = context.Request.Headers[CorrelationHeaders.CorrelationId].FirstOrDefault();
        var correlationId = IsSafeIdentifier(requested) ? requested! : Activity.Current?.TraceId.ToString() ?? Guid.CreateVersion7().ToString();
        var causation = context.Request.Headers[CorrelationHeaders.CausationId].FirstOrDefault();

        context.Items[CorrelationHeaders.CorrelationId] = correlationId;
        context.Items[CorrelationHeaders.CausationId] = IsSafeIdentifier(causation) ? causation : null;
        context.Response.Headers[CorrelationHeaders.CorrelationId] = correlationId;

        using (context.RequestServices.GetRequiredService<ILogger<CorrelationMiddleware>>().BeginScope(
                   new Dictionary<string, object?> { ["CorrelationId"] = correlationId, ["CausationId"] = causation }))
        {
            await next(context);
        }
    }

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
}

public sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public string CorrelationId => accessor.HttpContext?.Items[CorrelationHeaders.CorrelationId]?.ToString()
        ?? Activity.Current?.TraceId.ToString()
        ?? Guid.CreateVersion7().ToString();

    public string? CausationId => accessor.HttpContext?.Items[CorrelationHeaders.CausationId]?.ToString();
}
