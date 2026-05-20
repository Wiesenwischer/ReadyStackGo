using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Snmp;

namespace ReadyStackGo.Application.Snmp;

/// <summary>
/// Forwards the <see cref="SnmpSettingsChanged"/> domain event to the live
/// agent so the listener rebinds with the new settings without a container
/// restart.
/// </summary>
public sealed class SnmpSettingsChangedHandler : INotificationHandler<DomainEventNotification<SnmpSettingsChanged>>
{
    private readonly ISnmpAgentReloader _reloader;
    private readonly ILogger<SnmpSettingsChangedHandler> _logger;

    public SnmpSettingsChangedHandler(
        ISnmpAgentReloader reloader,
        ILogger<SnmpSettingsChangedHandler> logger)
    {
        _reloader = reloader;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<SnmpSettingsChanged> notification, CancellationToken cancellationToken)
    {
        try
        {
            await _reloader.ReloadAsync().ConfigureAwait(false);
            _logger.LogInformation("SNMP agent reloaded after settings change");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload SNMP agent after settings change");
        }
    }
}

/// <summary>
/// Hides the Infrastructure-layer SnmpAgent from the Application-layer
/// notification handler. The Api wires this against the real SnmpAgent
/// singleton.
/// </summary>
public interface ISnmpAgentReloader
{
    Task ReloadAsync();
}
