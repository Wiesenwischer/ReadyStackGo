using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Snmp;

/// <summary>
/// Sends an SNMP trap whenever a product deployment fails.
/// </summary>
public sealed class ProductDeploymentFailedTrapHandler
    : INotificationHandler<DomainEventNotification<ProductDeploymentFailed>>
{
    private readonly ISnmpTrapEmitter _emitter;
    private readonly ILogger<ProductDeploymentFailedTrapHandler> _logger;

    public ProductDeploymentFailedTrapHandler(
        ISnmpTrapEmitter emitter,
        ILogger<ProductDeploymentFailedTrapHandler> logger)
    {
        _emitter = emitter;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductDeploymentFailed> notification, CancellationToken cancellationToken)
    {
        var evt = notification.DomainEvent;
        try
        {
            await _emitter.EmitAsync(new SnmpTrap(
                TrapOid: SnmpTrapOids.TrapProductDeploymentFailed,
                Variables: new[]
                {
                    new SnmpTrapVariable(SnmpTrapOids.VarProductName, SnmpTrapValueType.OctetString, evt.ProductName),
                    new SnmpTrapVariable(SnmpTrapOids.VarMessage, SnmpTrapValueType.OctetString, evt.ErrorMessage ?? string.Empty),
                }), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to emit ProductDeploymentFailed trap");
        }
    }
}

public sealed class ProductDeploymentAutoFinalizedTrapHandler
    : INotificationHandler<DomainEventNotification<ProductDeploymentAutoFinalized>>
{
    private readonly ISnmpTrapEmitter _emitter;
    private readonly ILogger<ProductDeploymentAutoFinalizedTrapHandler> _logger;

    public ProductDeploymentAutoFinalizedTrapHandler(
        ISnmpTrapEmitter emitter,
        ILogger<ProductDeploymentAutoFinalizedTrapHandler> logger)
    {
        _emitter = emitter;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductDeploymentAutoFinalized> notification, CancellationToken cancellationToken)
    {
        var evt = notification.DomainEvent;
        try
        {
            await _emitter.EmitAsync(new SnmpTrap(
                TrapOid: SnmpTrapOids.TrapProductDeploymentAutoFinalized,
                Variables: new[]
                {
                    new SnmpTrapVariable(SnmpTrapOids.VarProductName, SnmpTrapValueType.OctetString, evt.ProductName),
                    new SnmpTrapVariable(SnmpTrapOids.VarStatus, SnmpTrapValueType.Integer32, ((int)evt.NewStatus).ToString()),
                    new SnmpTrapVariable(SnmpTrapOids.VarStatusText, SnmpTrapValueType.OctetString, evt.NewStatus.ToString()),
                    new SnmpTrapVariable(SnmpTrapOids.VarMessage, SnmpTrapValueType.OctetString, evt.Reason ?? string.Empty),
                }), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to emit ProductDeploymentAutoFinalized trap");
        }
    }
}

public sealed class ProductMaintenanceModeChangedTrapHandler
    : INotificationHandler<DomainEventNotification<ProductMaintenanceModeChanged>>
{
    private readonly ISnmpTrapEmitter _emitter;
    private readonly ILogger<ProductMaintenanceModeChangedTrapHandler> _logger;

    public ProductMaintenanceModeChangedTrapHandler(
        ISnmpTrapEmitter emitter,
        ILogger<ProductMaintenanceModeChangedTrapHandler> logger)
    {
        _emitter = emitter;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ProductMaintenanceModeChanged> notification, CancellationToken cancellationToken)
    {
        var evt = notification.DomainEvent;
        try
        {
            await _emitter.EmitAsync(new SnmpTrap(
                TrapOid: SnmpTrapOids.TrapProductMaintenanceModeChanged,
                Variables: new[]
                {
                    new SnmpTrapVariable(SnmpTrapOids.VarOperationMode, SnmpTrapValueType.Integer32, ((int)evt.NewMode).ToString()),
                    new SnmpTrapVariable(SnmpTrapOids.VarMessage, SnmpTrapValueType.OctetString, evt.Trigger?.Reason ?? string.Empty),
                }), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to emit ProductMaintenanceModeChanged trap");
        }
    }
}
