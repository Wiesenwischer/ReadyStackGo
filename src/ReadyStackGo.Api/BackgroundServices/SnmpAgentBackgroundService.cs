using ReadyStackGo.Application.Snmp;
using ReadyStackGo.Infrastructure.Snmp;

namespace ReadyStackGo.Api.BackgroundServices;

/// <summary>
/// Hosts the <see cref="SnmpAgent"/> alongside the application lifetime. The
/// agent is started when the host starts and stopped during shutdown.
///
/// The agent itself decides whether to actually bind a socket based on
/// <see cref="SnmpAgentOptions.Enabled"/>; this service unconditionally calls
/// Start/Stop so flipping the option (Feature 5) only requires a host restart.
/// </summary>
public sealed class SnmpAgentBackgroundService : BackgroundService
{
    private readonly SnmpAgent _agent;
    private readonly ILogger<SnmpAgentBackgroundService> _logger;

    public SnmpAgentBackgroundService(
        SnmpAgent agent,
        ILogger<SnmpAgentBackgroundService> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _agent.StartAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SNMP agent");
            return;
        }

        // Hold the service alive until shutdown; the agent does the work on its
        // own background task once started.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _agent.StopAsync(cancellationToken).ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
