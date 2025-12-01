namespace ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Base class for aggregate roots with a typed identity.
/// An aggregate root is the entry point to an aggregate and ensures
/// transactional consistency within the aggregate boundary.
/// Based on Vaughn Vernon's IDDD implementation.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    /// <summary>
    /// Version for optimistic concurrency control.
    /// </summary>
    public int Version { get; protected set; }

    protected void IncrementVersion()
    {
        Version++;
    }
}

/// <summary>
/// Aggregate root with a Guid-based identity.
/// </summary>
public abstract class AggregateRoot : AggregateRoot<Guid>
{
    protected AggregateRoot()
    {
        Id = Guid.NewGuid();
    }

    protected AggregateRoot(Guid id)
    {
        Id = id;
    }
}
