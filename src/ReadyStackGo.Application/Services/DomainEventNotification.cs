namespace ReadyStackGo.Application.Services;

using MediatR;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Wraps an <see cref="IDomainEvent"/> as a MediatR <see cref="INotification"/>
/// so domain events can be dispatched through the MediatR pipeline.
/// </summary>
public class DomainEventNotification<T> : INotification where T : IDomainEvent
{
    public T DomainEvent { get; }

    public DomainEventNotification(T domainEvent)
    {
        DomainEvent = domainEvent;
    }
}
