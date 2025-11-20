using ReadyStackGo.Application.Containers.DTOs;

namespace ReadyStackGo.Application.Containers;

public interface IDockerService
{
    /// <summary>
    /// Lists all containers for the specified environment.
    /// </summary>
    Task<IEnumerable<ContainerDto>> ListContainersAsync(string environmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a container in the specified environment.
    /// </summary>
    Task StartContainerAsync(string environmentId, string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a container in the specified environment.
    /// </summary>
    Task StopContainerAsync(string environmentId, string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the connection to a Docker host.
    /// </summary>
    Task<TestConnectionResult> TestConnectionAsync(string dockerHost, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of testing a Docker connection.
/// </summary>
public record TestConnectionResult(bool Success, string Message, string? DockerVersion = null);
