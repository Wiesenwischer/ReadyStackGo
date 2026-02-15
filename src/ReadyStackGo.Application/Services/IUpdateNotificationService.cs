namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for sending real-time self-update progress notifications to connected clients.
/// Used during self-update to push download progress and phase changes via SignalR.
/// </summary>
public interface IUpdateNotificationService
{
    /// <summary>
    /// Notify all connected clients about update progress.
    /// </summary>
    Task NotifyProgressAsync(UpdateProgress progress, CancellationToken cancellationToken = default);
}
