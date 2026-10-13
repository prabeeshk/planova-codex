namespace Planova.Identity.Application;

public interface IFoundationReferenceRecorder
{
    Task RecordAsync(string outcome, string? reason, CancellationToken cancellationToken = default);
}
