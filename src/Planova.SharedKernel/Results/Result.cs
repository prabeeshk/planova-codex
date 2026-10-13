namespace Planova.SharedKernel.Results;

public sealed record PlanovaError(string Code, string Message, IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public sealed class Result<T>
{
    public Result(T? value, PlanovaError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public PlanovaError? Error { get; }
    public bool IsSuccess => Error is null;
}
