namespace Planova.SharedKernel.Domain;

public abstract class AggregateRoot
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
    public uint RowVersion { get; private set; }
}
