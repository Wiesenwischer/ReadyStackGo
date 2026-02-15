namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for checking application version and updates.
/// </summary>
public interface IVersionCheckService
{
    /// <summary>
    /// Gets the current server version.
    /// </summary>
    string GetCurrentVersion();

    /// <summary>
    /// Gets the latest available version from GitHub releases.
    /// Returns null if check fails or is disabled.
    /// </summary>
    /// <param name="forceCheck">If true, bypasses the 24h cache and fetches fresh data from GitHub.</param>
    Task<LatestVersionInfo?> GetLatestVersionAsync(bool forceCheck = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about the latest available version.
/// </summary>
public record LatestVersionInfo(
    string Version,
    string ReleaseUrl,
    DateTime? PublishedAt,
    DateTime CheckedAt);
