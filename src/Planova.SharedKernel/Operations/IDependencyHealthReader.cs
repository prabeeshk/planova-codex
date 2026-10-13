namespace Planova.SharedKernel.Operations;

public interface IDependencyHealthReader
{
    Task<string> GetStatusAsync(CancellationToken cancellationToken = default);
}
