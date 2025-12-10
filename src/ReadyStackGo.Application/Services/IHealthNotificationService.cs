using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for sending real-time health notifications to connected clients.
/// </summary>
public interface IHealthNotificationService
{
    /// <summary>
    /// Notify clients subscribed to a specific deployment about a health update (summary only).
    /// Used for environment-level broadcasts.
    /// </summary>
    Task NotifyDeploymentHealthChangedAsync(
        DeploymentId deploymentId,
        StackHealthSummaryDto healthSummary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify clients subscribed to a specific deployment about detailed health including services.
    /// Sent to deploy:{deploymentId} group for detail views.
    /// </summary>
    Task NotifyDeploymentDetailedHealthChangedAsync(
        DeploymentId deploymentId,
        StackHealthDto detailedHealth,
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

    /// <summary>
    /// Notify clients about a maintenance observer result.
    /// </summary>
    Task NotifyObserverResultAsync(
        DeploymentId deploymentId,
        string stackName,
        ObserverResultDto result,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for observer result notifications.
/// </summary>
public class ObserverResultDto
{
    /// <summary>
    /// Whether the check was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Whether maintenance mode is required.
    /// </summary>
    public bool IsMaintenanceRequired { get; set; }

    /// <summary>
    /// The observed value.
    /// </summary>
    public string? ObservedValue { get; set; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when the check was performed.
    /// </summary>
    public DateTimeOffset CheckedAt { get; set; }

    /// <summary>
    /// Observer type (sqlExtendedProperty, sqlQuery, http, file).
    /// </summary>
    public string? ObserverType { get; set; }

    public static ObserverResultDto FromDomain(ObserverResult result, string? observerType = null)
    {
        return new ObserverResultDto
        {
            IsSuccess = result.IsSuccess,
            IsMaintenanceRequired = result.IsMaintenanceRequired,
            ObservedValue = result.ObservedValue,
            ErrorMessage = result.ErrorMessage,
            CheckedAt = result.CheckedAt,
            ObserverType = observerType
        };
    }
}
