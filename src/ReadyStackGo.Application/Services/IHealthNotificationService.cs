using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for sending real-time health notifications to connected clients.
/// </summary>
public interface IHealthNotificationService
{
    /// <summary>
    /// Notify clients subscribed to a specific deployment about a health update.
    /// </summary>
    Task NotifyDeploymentHealthChangedAsync(
        DeploymentId deploymentId,
        StackHealthSummaryDto healthSummary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify clients subscribed to an environment about health changes.
    /// </summary>
    Task NotifyEnvironmentHealthChangedAsync(
        EnvironmentId environmentId,
        EnvironmentHealthSummaryDto summary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify all clients subscribed to global health updates.
    /// </summary>
    Task NotifyGlobalHealthChangedAsync(
        StackHealthSummaryDto healthSummary,
        CancellationToken cancellationToken = default);
}
