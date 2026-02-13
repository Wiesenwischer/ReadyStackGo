using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Docker;

/// <summary>
/// Orchestrates RSGO self-update by pulling a new image and using a helper container
/// to swap the running container with a new one.
/// </summary>
public class SelfUpdateService : ISelfUpdateService, IDisposable
{
    private const string DockerHubImage = "wiesenwischer/readystackgo";
    private const string HelperImage = "wiesenwischer/rsgo-updater";
    private const string HelperImageTag = "latest";
    private const string UpdateContainerSuffix = "-update";

    private readonly IConfiguration _configuration;
    private readonly ILogger<SelfUpdateService> _logger;
    private readonly DockerClient _client;
    private bool _disposed;

    public SelfUpdateService(
        IConfiguration configuration,
        ILogger<SelfUpdateService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var socketUri = OperatingSystem.IsWindows()
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");

        _client = new DockerClientConfiguration(socketUri).CreateClient();
    }

    public async Task<SelfUpdateResult> TriggerUpdateAsync(string targetVersion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Self-update triggered to version {TargetVersion}", targetVersion);

        try
        {
            // 1. Identify own container
            var containerId = GetOwnContainerId();
            var inspection = await _client.Containers.InspectContainerAsync(containerId, cancellationToken);
            var containerName = inspection.Name.TrimStart('/');

            _logger.LogInformation("Own container: {ContainerId} ({ContainerName})", containerId, containerName);

            // 2. Pull the new image
            var newImageTag = $"{DockerHubImage}:{targetVersion}";
            _logger.LogInformation("Pulling image {Image}", newImageTag);

            var authConfig = GetAuthConfig();
            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = DockerHubImage,
                    Tag = targetVersion
                },
                authConfig,
                new Progress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrEmpty(msg.Status))
                        _logger.LogDebug("Pull progress: {Status}", msg.Status);
                }),
                cancellationToken);

            _logger.LogInformation("Successfully pulled {Image}", newImageTag);

            // 3. Pre-create the replacement container (not started)
            var updateContainerName = containerName + UpdateContainerSuffix;

            // Clean up any leftover update container from a previous failed attempt
            await RemoveContainerIfExistsAsync(updateContainerName, cancellationToken);

            var createParams = BuildCreateParamsFromInspection(inspection, newImageTag, updateContainerName);
            var createResponse = await _client.Containers.CreateContainerAsync(createParams, cancellationToken);

            _logger.LogInformation("Pre-created update container {ContainerId} ({Name})",
                createResponse.ID, updateContainerName);

            // Connect to additional networks (beyond the primary one set in NetworkMode)
            await ConnectAdditionalNetworks(inspection, createResponse.ID, cancellationToken);

            // 4. Ensure helper image is available
            await EnsureHelperImageAsync(cancellationToken);

            // 5. Create and start the helper container
            var helperContainerName = $"rsgo-updater-{DateTime.UtcNow:yyyyMMddHHmmss}";

            await RemoveContainerIfExistsAsync(helperContainerName, cancellationToken);

            var helperParams = new CreateContainerParameters
            {
                Name = helperContainerName,
                Image = $"{HelperImage}:{HelperImageTag}",
                Env = new List<string>
                {
                    $"OLD_CONTAINER={containerName}",
                    $"NEW_CONTAINER={updateContainerName}"
                },
                HostConfig = new HostConfig
                {
                    NetworkMode = "host",
                    Binds = new List<string> { "/var/run/docker.sock:/var/run/docker.sock" },
                    AutoRemove = true
                }
            };

            var helperResponse = await _client.Containers.CreateContainerAsync(helperParams, cancellationToken);
            await _client.Containers.StartContainerAsync(helperResponse.ID, new ContainerStartParameters(), cancellationToken);

            _logger.LogInformation("Started helper container {ContainerId} ({Name}) — self-update will proceed asynchronously",
                helperResponse.ID, helperContainerName);

            return new SelfUpdateResult(true, $"Update to v{targetVersion} initiated. RSGO will restart momentarily.");
        }
        catch (DockerApiException ex)
        {
            _logger.LogError(ex, "Docker API error during self-update to {TargetVersion}", targetVersion);
            return new SelfUpdateResult(false, $"Docker error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during self-update to {TargetVersion}", targetVersion);
            return new SelfUpdateResult(false, $"Update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the container ID of the currently running RSGO instance.
    /// In Docker, the hostname is set to the short container ID by default.
    /// </summary>
    internal static string GetOwnContainerId()
    {
        return Environment.MachineName;
    }

    /// <summary>
    /// Builds CreateContainerParameters from the inspection of the current container,
    /// using the new image but preserving all other configuration.
    /// </summary>
    internal static CreateContainerParameters BuildCreateParamsFromInspection(
        ContainerInspectResponse inspection, string newImage, string newName)
    {
        var hostConfig = inspection.HostConfig;

        // Get the primary network from NetworkMode
        var primaryNetwork = hostConfig.NetworkMode ?? "bridge";

        // Build networking config for the primary network
        NetworkingConfig? networkingConfig = null;
        if (inspection.NetworkSettings?.Networks != null &&
            inspection.NetworkSettings.Networks.TryGetValue(primaryNetwork, out var primaryEndpoint))
        {
            networkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [primaryNetwork] = new EndpointSettings
                    {
                        Aliases = primaryEndpoint.Aliases
                    }
                }
            };
        }

        return new CreateContainerParameters
        {
            Name = newName,
            Image = newImage,
            Env = inspection.Config.Env,
            ExposedPorts = inspection.Config.ExposedPorts,
            Labels = inspection.Config.Labels,
            HostConfig = new HostConfig
            {
                Binds = hostConfig.Binds,
                PortBindings = hostConfig.PortBindings,
                RestartPolicy = hostConfig.RestartPolicy,
                NetworkMode = primaryNetwork
            },
            NetworkingConfig = networkingConfig
        };
    }

    /// <summary>
    /// Connects the new container to any additional networks beyond the primary one.
    /// </summary>
    private async Task ConnectAdditionalNetworks(
        ContainerInspectResponse inspection, string newContainerId, CancellationToken cancellationToken)
    {
        if (inspection.NetworkSettings?.Networks == null)
            return;

        var primaryNetwork = inspection.HostConfig.NetworkMode ?? "bridge";

        foreach (var (networkName, endpoint) in inspection.NetworkSettings.Networks)
        {
            if (networkName == primaryNetwork)
                continue;

            try
            {
                await _client.Networks.ConnectNetworkAsync(networkName, new NetworkConnectParameters
                {
                    Container = newContainerId,
                    EndpointConfig = new EndpointSettings
                    {
                        Aliases = endpoint.Aliases
                    }
                }, cancellationToken);

                _logger.LogDebug("Connected update container to network {Network}", networkName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect update container to network {Network}", networkName);
            }
        }
    }

    private async Task EnsureHelperImageAsync(CancellationToken cancellationToken)
    {
        var fullImage = $"{HelperImage}:{HelperImageTag}";

        try
        {
            await _client.Images.InspectImageAsync(fullImage, cancellationToken);
            _logger.LogDebug("Helper image {Image} already available", fullImage);
        }
        catch (DockerImageNotFoundException)
        {
            _logger.LogInformation("Pulling helper image {Image}", fullImage);

            var authConfig = GetAuthConfig();
            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = HelperImage,
                    Tag = HelperImageTag
                },
                authConfig,
                new Progress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrEmpty(msg.Status))
                        _logger.LogDebug("Helper pull: {Status}", msg.Status);
                }),
                cancellationToken);
        }
    }

    private async Task RemoveContainerIfExistsAsync(string containerName, CancellationToken cancellationToken)
    {
        try
        {
            var containers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["name"] = new Dictionary<string, bool> { [containerName] = true }
                    }
                },
                cancellationToken);

            var container = containers.FirstOrDefault(c =>
                c.Names.Any(n => n.TrimStart('/') == containerName));

            if (container != null)
            {
                _logger.LogWarning("Removing leftover container {ContainerName} from previous attempt", containerName);
                await _client.Containers.RemoveContainerAsync(container.ID,
                    new ContainerRemoveParameters { Force = true }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking for leftover container {ContainerName}", containerName);
        }
    }

    /// <summary>
    /// Gets Docker Hub auth config from configuration or docker config file.
    /// Returns null if no credentials are configured (public image pull).
    /// </summary>
    private AuthConfig? GetAuthConfig()
    {
        try
        {
            // 1. Try environment variables / configuration
            var username = _configuration["Docker:Username"];
            var password = _configuration["Docker:Password"];

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                return new AuthConfig
                {
                    Username = username,
                    Password = password,
                    ServerAddress = "https://index.docker.io/v1/"
                };
            }

            // 2. Try Docker config file
            var configPath = GetDockerConfigPath();
            if (!File.Exists(configPath))
                return null;

            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<DockerConfigFile>(configJson);

            if (config?.Auths == null)
                return null;

            // Try Docker Hub entries
            DockerAuthEntry? auth = null;
            if (config.Auths.TryGetValue("https://index.docker.io/v1/", out auth) ||
                config.Auths.TryGetValue("https://index.docker.io/v1", out auth))
            {
                if (!string.IsNullOrEmpty(auth.Auth))
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth.Auth));
                    var parts = decoded.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        return new AuthConfig
                        {
                            Username = parts[0],
                            Password = parts[1],
                            ServerAddress = "https://index.docker.io/v1/"
                        };
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get Docker auth config, proceeding without auth");
            return null;
        }
    }

    private string GetDockerConfigPath()
    {
        var configuredPath = _configuration["Docker:ConfigPath"];
        if (!string.IsNullOrEmpty(configuredPath))
            return configuredPath;

        var dockerConfigDir = _configuration["DOCKER_CONFIG"];
        if (!string.IsNullOrEmpty(dockerConfigDir))
            return Path.Combine(dockerConfigDir, "config.json");

        if (!OperatingSystem.IsWindows())
        {
            var linuxPath = "/root/.docker/config.json";
            if (File.Exists(linuxPath))
                return linuxPath;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".docker", "config.json");
    }

    private class DockerConfigFile
    {
        [JsonPropertyName("auths")]
        public Dictionary<string, DockerAuthEntry>? Auths { get; set; }
    }

    private class DockerAuthEntry
    {
        [JsonPropertyName("auth")]
        public string? Auth { get; set; }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client.Dispose();
            _disposed = true;
        }
    }
}
