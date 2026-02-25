namespace ReadyStackGo.Application.Services;

using MediatR;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Dispatches domain events through MediatR by wrapping each event
/// in a <see cref="DomainEventNotification{T}"/>.
/// </summary>
public class MediatRDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;

    public MediatRDomainEventDispatcher(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in events)
        {
            var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            var notification = Activator.CreateInstance(notificationType, domainEvent)
                ?? throw new InvalidOperationException(
                    $"Could not create DomainEventNotification for {domainEvent.GetType().Name}");

            await _mediator.Publish(notification, cancellationToken);
        }
    }
}
