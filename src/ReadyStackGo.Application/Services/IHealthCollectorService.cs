using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service responsible for collecting health data from deployed stacks.
/// Queries Docker container status and creates HealthSnapshots.
/// </summary>
public interface IHealthCollectorService
{
    /// <summary>
    /// Collect health data for all deployments in an environment.
    /// </summary>
    Task CollectEnvironmentHealthAsync(EnvironmentId environmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Collect health data for a specific deployment.
    /// </summary>
    Task CollectDeploymentHealthAsync(DeploymentId deploymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Collect health data for all deployments across all environments.
    /// Used by the background service for periodic collection.
    /// </summary>
    Task CollectAllHealthAsync(CancellationToken cancellationToken = default);
}
