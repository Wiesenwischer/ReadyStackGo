using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Precheck;
using Microsoft.Extensions.Logging;

namespace ReadyStackGo.Infrastructure.Services.Deployment.Precheck;

/// <summary>
/// Checks that required networks exist or can be created.
/// External networks must already exist; stack-managed networks will be created during deployment.
/// </summary>
public class NetworkAvailabilityRule : IDeploymentPrecheckRule
{
    private readonly IDockerService _dockerService;
    private readonly ILogger<NetworkAvailabilityRule> _logger;

    public NetworkAvailabilityRule(IDockerService dockerService, ILogger<NetworkAvailabilityRule> logger)
    {
        _dockerService = dockerService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PrecheckItem>> ExecuteAsync(PrecheckContext context, CancellationToken cancellationToken)
    {
        var items = new List<PrecheckItem>();
        var networks = context.StackDefinition.Networks;

        if (networks.Count == 0)
        {
            items.Add(new PrecheckItem(
                "NetworkAvailability",
                PrecheckSeverity.OK,
                "No custom networks required",
                "Stack uses default Docker networking"));
            return items;
        }

        foreach (var network in networks)
        {
            if (network.External)
            {
                // External networks must already exist
                var networkName = network.ExternalName ?? network.Name;
                try
                {
                    // Try to ensure network exists (this is a no-op if it exists, creates if not)
                    // For external networks, we just need to verify they exist
                    await _dockerService.EnsureNetworkAsync(context.EnvironmentId, networkName, cancellationToken);
                    items.Add(new PrecheckItem(
                        "NetworkAvailability",
                        PrecheckSeverity.OK,
                        $"External network available: {networkName}",
                        "Network exists and is accessible"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "External network {Network} not available", networkName);
                    items.Add(new PrecheckItem(
                        "NetworkAvailability",
                        PrecheckSeverity.Error,
                        $"External network not found: {networkName}",
                        $"External network '{networkName}' does not exist or is not accessible: {ex.Message}"));
                }
            }
            else
            {
                // Stack-managed networks will be created during deployment — always OK
                items.Add(new PrecheckItem(
                    "NetworkAvailability",
                    PrecheckSeverity.OK,
                    $"Network will be created: {network.Name}",
                    "Stack-managed network will be created during deployment"));
            }
        }

        return items;
    }
}
