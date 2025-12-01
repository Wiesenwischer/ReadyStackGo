namespace ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Marker interface for domain events.
/// Based on Vaughn Vernon's IDDD implementation.
/// </summary>
public interface IDomainEvent
{
    int EventVersion { get; }
    DateTime OccurredOn { get; }
}
