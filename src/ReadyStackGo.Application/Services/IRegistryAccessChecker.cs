namespace ReadyStackGo.Application.Services;

/// <summary>
/// Result of checking whether a container registry allows anonymous (public) access.
/// </summary>
public enum RegistryAccessLevel
{
    /// <summary>Anonymous pull confirmed — no credentials needed.</summary>
    Public,

    /// <summary>Authentication is definitely required to pull images.</summary>
    AuthRequired,

    /// <summary>Could not determine (network error, timeout, unsupported registry).</summary>
    Unknown
}

/// <summary>
/// Checks whether a container registry allows anonymous (public) image pulls
/// using the Docker Registry v2 API token flow.
/// </summary>
public interface IRegistryAccessChecker
{
    /// <summary>
    /// Checks if the given registry allows anonymous access for a specific image.
    /// Uses the v2 token auth flow: tries /v2/, then requests an anonymous token.
    /// </summary>
    /// <param name="host">Registry host (e.g., "ghcr.io", "docker.io")</param>
    /// <param name="namespacePath">Image namespace (e.g., "library", "wiesenwischer")</param>
    /// <param name="repository">Image repository name (e.g., "nginx", "ams-api")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Access level: Public, AuthRequired, or Unknown</returns>
    Task<RegistryAccessLevel> CheckAccessAsync(
        string host,
        string namespacePath,
        string repository,
        CancellationToken ct = default);
}
