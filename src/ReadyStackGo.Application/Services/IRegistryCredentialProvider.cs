namespace ReadyStackGo.Application.Services;

/// <summary>
/// Provides registry credentials for Docker image pulls.
/// Used by DockerService to get authentication for private registries.
/// </summary>
public interface IRegistryCredentialProvider
{
    /// <summary>
    /// Gets credentials for a Docker image based on configured registries and their image patterns.
    /// </summary>
    /// <param name="imageReference">Full image reference (e.g., "ghcr.io/myorg/myimage:v1", "nginx:latest")</param>
    /// <returns>Credentials if a matching registry is found, null otherwise</returns>
    RegistryCredentials? GetCredentialsForImage(string imageReference);
}

/// <summary>
/// Docker registry credentials.
/// </summary>
public record RegistryCredentials(
    string ServerAddress,
    string Username,
    string Password);
