namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for orchestrating self-update of the RSGO container.
/// Pulls the target image and uses a helper container to perform the container swap.
/// </summary>
public interface ISelfUpdateService
{
    /// <summary>
    /// Triggers a self-update to the specified version.
    /// Pulls the new image, pre-creates the replacement container,
    /// and starts a helper container that performs the swap asynchronously.
    /// Returns once the helper is started — the actual restart happens after this method returns.
    /// </summary>
    Task<SelfUpdateResult> TriggerUpdateAsync(string targetVersion, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a self-update trigger operation.
/// </summary>
public record SelfUpdateResult(bool Success, string Message);
