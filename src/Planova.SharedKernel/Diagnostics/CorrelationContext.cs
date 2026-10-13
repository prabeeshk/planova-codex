namespace Planova.SharedKernel.Diagnostics;

public interface ICorrelationContext
{
    string CorrelationId { get; }
    string? CausationId { get; }
}

public static class CorrelationHeaders
{
    public const string CorrelationId = "X-Correlation-ID";
    public const string CausationId = "X-Causation-ID";
}
