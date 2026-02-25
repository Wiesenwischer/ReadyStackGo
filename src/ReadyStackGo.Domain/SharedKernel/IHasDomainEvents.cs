namespace ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Non-generic interface for entities that raise domain events.
/// Used by the infrastructure layer (DbContext) to discover entities with pending events.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
