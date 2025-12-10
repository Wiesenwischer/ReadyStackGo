namespace ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Base class for domain events.
/// Based on Vaughn Vernon's IDDD implementation.
/// </summary>
public abstract class DomainEvent : IDomainEvent
{
    protected DomainEvent()
    {
        EventVersion = 1;
        OccurredOn = SystemClock.UtcNow;
    }

    public int EventVersion { get; protected set; }
    public DateTime OccurredOn { get; protected set; }
}
