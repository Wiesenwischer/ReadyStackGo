using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Integrations.Prtg.V3;

/// <summary>
/// MediatR notification handlers for Variant 3 (PrtgConnection-based auto-register).
/// React to the ProductDeployment lifecycle and tell the linked PRTG instance
/// to create / delete a corresponding device.
///
/// All handlers are best-effort: a PRTG failure must not break the RSGO
/// deployment flow. The actual PRTG calls live in <see cref="IPrtgDeviceSyncService"/>
/// so the LinkPrtgConnectionHandler can reuse them for the "register
/// immediately on Save link" flow.
/// </summary>
public sealed class PrtgRegisterDeviceOnCompletedHandler
    : INotificationHandler<DomainEventNotification<ProductDeploymentCompleted>>
{
    private readonly IPrtgDeviceSyncService _sync;
    private readonly IProductDeploymentRepository _deployments;

    public PrtgRegisterDeviceOnCompletedHandler(
        IPrtgDeviceSyncService sync,
        IProductDeploymentRepository deployments)
    {
        _sync = sync;
        _deployments = deployments;
    }

    public async Task Handle(
        DomainEventNotification<ProductDeploymentCompleted> notification, CancellationToken ct)
    {
        var evt = notification.DomainEvent;
        var deployment = _deployments.Get(evt.ProductDeploymentId);
        if (deployment is null || !deployment.HasPrtgTarget)
            return;
        await _sync.RegisterAsync(deployment, ct);
    }
}

public sealed class PrtgDeregisterOnRemovedHandler
    : INotificationHandler<DomainEventNotification<ProductDeploymentRemoved>>
{
    private readonly IPrtgDeviceSyncService _sync;

    public PrtgDeregisterOnRemovedHandler(IPrtgDeviceSyncService sync) => _sync = sync;

    public Task Handle(DomainEventNotification<ProductDeploymentRemoved> notification, CancellationToken ct)
        => _sync.DeregisterAsync(notification.DomainEvent.ProductDeploymentId, ct);
}

/// <summary>
/// Deregister on ProductDeploymentSuperseded — same as Removed; the successor
/// (the new ProductDeployment after an upgrade) gets its own PRTG device on
/// its own Completed event.
/// </summary>
public sealed class PrtgDeregisterOnSupersededHandler
    : INotificationHandler<DomainEventNotification<ProductDeploymentSuperseded>>
{
    private readonly IPrtgDeviceSyncService _sync;

    public PrtgDeregisterOnSupersededHandler(IPrtgDeviceSyncService sync) => _sync = sync;

    public Task Handle(DomainEventNotification<ProductDeploymentSuperseded> notification, CancellationToken ct)
        => _sync.DeregisterAsync(notification.DomainEvent.ProductDeploymentId, ct);
}
