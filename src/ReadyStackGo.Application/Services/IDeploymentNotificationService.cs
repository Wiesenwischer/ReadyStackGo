namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for sending real-time deployment progress notifications to connected clients.
/// Used during stack deployments to provide progress updates via SignalR.
/// </summary>
public interface IDeploymentNotificationService
{
    /// <summary>
    /// Notify clients about deployment progress.
    /// </summary>
    /// <param name="sessionId">Unique deployment session ID for targeting specific clients.</param>
    /// <param name="phase">Current deployment phase (e.g., "Validating", "Deploying", "Complete").</param>
    /// <param name="message">Human-readable status message.</param>
    /// <param name="percentComplete">Progress percentage (0-100).</param>
    /// <param name="currentService">Name of the service currently being deployed.</param>
    /// <param name="totalServices">Total number of regular services in the deployment.</param>
    /// <param name="completedServices">Number of regular services successfully deployed.</param>
    /// <param name="totalInitContainers">Total number of init containers.</param>
    /// <param name="completedInitContainers">Number of init containers completed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyProgressAsync(
        string sessionId,
        string phase,
        string message,
        int percentComplete,
        string? currentService,
        int totalServices,
        int completedServices,
        int totalInitContainers,
        int completedInitContainers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify clients that deployment completed successfully.
    /// </summary>
    /// <param name="sessionId">Unique deployment session ID.</param>
    /// <param name="message">Completion message.</param>
    /// <param name="serviceCount">Number of services deployed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyCompletedAsync(
        string sessionId,
        string message,
        int serviceCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify clients that deployment failed.
    /// </summary>
    /// <param name="sessionId">Unique deployment session ID.</param>
    /// <param name="errorMessage">Error description.</param>
    /// <param name="failedService">Name of the service that failed (if applicable).</param>
    /// <param name="totalServices">Total number of services.</param>
    /// <param name="completedServices">Number of services that were deployed before failure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyErrorAsync(
        string sessionId,
        string errorMessage,
        string? failedService,
        int totalServices,
        int completedServices,
        CancellationToken cancellationToken = default);
}
