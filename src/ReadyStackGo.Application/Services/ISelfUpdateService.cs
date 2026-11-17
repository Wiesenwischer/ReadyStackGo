namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for orchestrating self-update of the RSGO container.
/// Pulls the target image and uses a helper container to perform the container swap.
/// </summary>
public interface ISelfUpdateService
{
    /// <summary>
    /// Triggers a self-update to the specified version.
    /// The update runs in the background — call <see cref="GetProgress"/> to monitor.
    /// Returns immediately after basic validation.
    /// </summary>
    SelfUpdateResult TriggerUpdate(string targetVersion);

    /// <summary>
    /// Gets the current progress of a running (or last completed) self-update.
    /// </summary>
    UpdateProgress GetProgress();
}

/// <summary>
/// Result of a self-update trigger operation.
/// </summary>
public record SelfUpdateResult(bool Success, string Message);

/// <summary>
/// Progress of a self-update operation.
/// </summary>
/// <param name="Phase">idle, pulling, creating, starting, handed_off, error</param>
/// <param name="Message">Human-readable status message</param>
/// <param name="ProgressPercent">0-100 during pulling phase, null otherwise</param>
/// <param name="Details">Technical failure reason (e.g. the Docker daemon's own error text), only set in the error phase</param>
public record UpdateProgress(string Phase, string? Message, int? ProgressPercent, string? Details = null)
{
    public static UpdateProgress Idle => new("idle", null, null);
}
