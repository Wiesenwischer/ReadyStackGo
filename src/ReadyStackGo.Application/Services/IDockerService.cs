using ReadyStackGo.Application.UseCases.Containers;

namespace ReadyStackGo.Application.Services;

public interface IDockerService
{
    /// <summary>
    /// Lists all containers for the specified environment.
    /// </summary>
    Task<IEnumerable<ContainerDto>> ListContainersAsync(string environmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all containers with detailed info (including RestartCount) for the specified environment.
    /// This is slower than ListContainersAsync as it requires inspecting each container.
    /// </summary>
    Task<IEnumerable<ContainerDto>> ListContainersWithDetailsAsync(string environmentId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Creates and starts a container in the specified environment.
    /// </summary>
    Task<string> CreateAndStartContainerAsync(
        string environmentId,
        CreateContainerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a container (stops if running) in the specified environment.
    /// </summary>
    Task RemoveContainerAsync(string environmentId, string containerId, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a Docker network exists in the specified environment.
    /// </summary>
    Task EnsureNetworkAsync(string environmentId, string networkName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls an image in the specified environment.
    /// </summary>
    Task PullImageAsync(string environmentId, string image, string tag = "latest", CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a container by name in the specified environment.
    /// </summary>
    Task<ContainerDto?> GetContainerByNameAsync(string environmentId, string containerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an image exists locally in the specified environment.
    /// </summary>
    Task<bool> ImageExistsAsync(string environmentId, string image, string tag = "latest", CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the restart count for a specific container.
    /// This requires an additional API call (inspect) so use sparingly.
    /// </summary>
    Task<int> GetContainerRestartCountAsync(string environmentId, string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all containers belonging to a stack (identified by rsgo.stack label).
    /// Containers with rsgo.maintenance=ignore label will be excluded.
    /// </summary>
    /// <param name="environmentId">Environment ID</param>
    /// <param name="stackName">Stack name (rsgo.stack label value)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of container IDs that were stopped</returns>
    Task<IReadOnlyList<string>> StopStackContainersAsync(
        string environmentId,
        string stackName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts all containers belonging to a stack (identified by rsgo.stack label).
    /// Containers with rsgo.maintenance=ignore label will be excluded.
    /// </summary>
    /// <param name="environmentId">Environment ID</param>
    /// <param name="stackName">Stack name (rsgo.stack label value)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of container IDs that were started</returns>
    Task<IReadOnlyList<string>> StartStackContainersAsync(
        string environmentId,
        string stackName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of testing a Docker connection.
/// </summary>
public record TestConnectionResult(bool Success, string Message, string? DockerVersion = null);

/// <summary>
/// Request to create a container.
/// </summary>
public class CreateContainerRequest
{
    /// <summary>
    /// Container name (must be unique).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Image name with tag (e.g., "nginx:alpine").
    /// </summary>
    public required string Image { get; set; }

    /// <summary>
    /// Environment variables.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Port bindings in format "hostPort:containerPort" or "containerPort".
    /// </summary>
    public List<string> Ports { get; set; } = new();

    /// <summary>
    /// Volume bindings in format "hostPath:containerPath" or "volumeName:containerPath".
    /// </summary>
    public Dictionary<string, string> Volumes { get; set; } = new();

    /// <summary>
    /// Networks to attach to.
    /// </summary>
    public List<string> Networks { get; set; } = new();

    /// <summary>
    /// Network aliases (DNS names) for this container.
    /// These allow other containers to reach this one by these names.
    /// </summary>
    public List<string> NetworkAliases { get; set; } = new();

    /// <summary>
    /// Labels to apply to the container.
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Restart policy (e.g., "no", "always", "unless-stopped", "on-failure").
    /// </summary>
    public string RestartPolicy { get; set; } = "unless-stopped";
}
