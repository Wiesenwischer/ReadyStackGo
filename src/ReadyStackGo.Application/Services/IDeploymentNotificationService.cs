namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for sending real-time deployment progress notifications to connected clients.
/// </summary>
public interface IDeploymentNotificationService
{
    /// <summary>
    /// Notify clients about deployment progress.
    /// </summary>
    Task NotifyProgressAsync(
        string deploymentId,
        string phase,
        string message,
        int progressPercent,
        string? currentService = null,
        int totalServices = 0,
        int completedServices = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify clients that deployment has completed successfully.
    /// </summary>
    Task NotifyCompletedAsync(
        string deploymentId,
        string message,
        int totalServices,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify clients that deployment has failed.
    /// </summary>
    Task NotifyErrorAsync(
        string deploymentId,
        string errorMessage,
        string? currentService = null,
        int totalServices = 0,
        int completedServices = 0,
        CancellationToken cancellationToken = default);
}
