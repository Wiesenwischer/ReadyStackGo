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
    private readonly IUpdateNotificationService _notificationService;
    private readonly DockerClient _client;
    private bool _disposed;

    private volatile UpdateProgress _progress = UpdateProgress.Idle;
    private volatile bool _updateInProgress;

    public SelfUpdateService(
        IConfiguration configuration,
        ILogger<SelfUpdateService> logger,
        IUpdateNotificationService notificationService)
    {
        _configuration = configuration;
        _logger = logger;
        _notificationService = notificationService;

        var socketUri = OperatingSystem.IsWindows()
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");

        _client = new DockerClientConfiguration(socketUri).CreateClient();
    }

    public UpdateProgress GetProgress() => _progress;

    public SelfUpdateResult TriggerUpdate(string targetVersion)
    {
        if (_updateInProgress)
        {
            return new SelfUpdateResult(false, "An update is already in progress.");
        }

        _updateInProgress = true;
        SetProgress(new UpdateProgress("pulling", $"Downloading v{targetVersion}...", 0));

        // Fire-and-forget — progress is pushed via SignalR
        _ = Task.Run(() => ExecuteUpdateAsync(targetVersion));

        return new SelfUpdateResult(true, $"Update to v{targetVersion} started.");
    }

    private void SetProgress(UpdateProgress progress)
    {
        _progress = progress;
        // Fire-and-forget push to SignalR clients
        _ = _notificationService.NotifyProgressAsync(progress);
    }

    private async Task ExecuteUpdateAsync(string targetVersion)
    {
        _logger.LogInformation("Self-update triggered to version {TargetVersion}", targetVersion);

        try
        {
            // 1. Identify own container
            var containerId = GetOwnContainerId();
            var inspection = await _client.Containers.InspectContainerAsync(containerId);
            var containerName = inspection.Name.TrimStart('/');

            _logger.LogInformation("Own container: {ContainerId} ({ContainerName})", containerId, containerName);

            // 2. Pull the new image (with progress tracking)
            var newImageTag = $"{DockerHubImage}:{targetVersion}";
            _logger.LogInformation("Pulling image {Image}", newImageTag);

            var pullTracker = new PullProgressTracker();
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

                    pullTracker.Update(msg);
                    var percent = pullTracker.GetPercent();
                    SetProgress(new UpdateProgress("pulling", $"Downloading v{targetVersion}...", percent));
                }));

            _logger.LogInformation("Successfully pulled {Image}", newImageTag);

            // 3. Pre-create the replacement container (not started)
            SetProgress(new UpdateProgress("creating", "Preparing new container...", null));

            var updateContainerName = containerName + UpdateContainerSuffix;

            // Clean up any leftover update container from a previous failed attempt
            await RemoveContainerIfExistsAsync(updateContainerName);

            var createParams = BuildCreateParamsFromInspection(inspection, newImageTag, updateContainerName);
            var createResponse = await _client.Containers.CreateContainerAsync(createParams);

            _logger.LogInformation("Pre-created update container {ContainerId} ({Name})",
                createResponse.ID, updateContainerName);

            // Connect to additional networks (beyond the primary one set in NetworkMode)
            await ConnectAdditionalNetworks(inspection, createResponse.ID);

            // 4. Ensure helper image is available
            await EnsureHelperImageAsync();

            // 5. Create and start the helper container
            SetProgress(new UpdateProgress("starting", "Starting update process...", null));

            var helperContainerName = $"rsgo-updater-{DateTime.UtcNow:yyyyMMddHHmmss}";

            await RemoveContainerIfExistsAsync(helperContainerName);

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

            var helperResponse = await _client.Containers.CreateContainerAsync(helperParams);
            await _client.Containers.StartContainerAsync(helperResponse.ID, new ContainerStartParameters());

            _logger.LogInformation("Started helper container {ContainerId} ({Name}) — self-update will proceed asynchronously",
                helperResponse.ID, helperContainerName);

            SetProgress(new UpdateProgress("handed_off", "Restarting with new version...", null));

            // Monitor the helper container — if it exits with error, we're still alive
            // and should reset state + notify the user
            _ = Task.Run(() => MonitorHelperContainerAsync(helperResponse.ID, helperContainerName));
        }
        catch (DockerApiException ex)
        {
            _logger.LogError(ex, "Docker API error during self-update to {TargetVersion}", targetVersion);
            SetProgress(new UpdateProgress("error", $"Docker error: {ex.Message}", null));
            _updateInProgress = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during self-update to {TargetVersion}", targetVersion);
            SetProgress(new UpdateProgress("error", $"Update failed: {ex.Message}", null));
            _updateInProgress = false;
        }
    }

    /// <summary>
    /// Monitors the helper container after handoff. If the helper exits with an error
    /// and we're still alive (meaning the swap failed), reset state and notify the user.
    /// </summary>
    private async Task MonitorHelperContainerAsync(string helperId, string helperName)
    {
        try
        {
            // Wait for the helper container to finish (up to 120s)
            var waitResponse = await _client.Containers.WaitContainerAsync(helperId);

            // If we reach here, we're still alive — the helper failed to swap us out
            if (waitResponse.StatusCode != 0)
            {
                _logger.LogError("Helper container {Name} exited with code {ExitCode}",
                    helperName, waitResponse.StatusCode);
                SetProgress(new UpdateProgress("error",
                    $"Update helper failed (exit code {waitResponse.StatusCode}). The previous version is still running.",
                    null));
            }
            else
            {
                // Helper exited 0 but we're still alive — unexpected
                _logger.LogWarning("Helper container {Name} exited successfully but we're still running", helperName);
                SetProgress(new UpdateProgress("error",
                    "Update process completed but the server was not restarted. Please restart manually.",
                    null));
            }

            _updateInProgress = false;
        }
        catch (DockerContainerNotFoundException)
        {
            // Container was already removed (AutoRemove) — check after a delay if we're still alive
            await Task.Delay(TimeSpan.FromSeconds(10));

            // If we're still here after 10s, the update likely failed
            _logger.LogWarning("Helper container {Name} was removed (AutoRemove) but we're still running", helperName);
            SetProgress(new UpdateProgress("error",
                "Update process failed. The previous version is still running.",
                null));
            _updateInProgress = false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error monitoring helper container {Name}", helperName);
            // After a timeout, assume failure if we're still alive
            await Task.Delay(TimeSpan.FromSeconds(30));
            if (_updateInProgress)
            {
                SetProgress(new UpdateProgress("error",
                    "Update process timed out. The previous version is still running.",
                    null));
                _updateInProgress = false;
            }
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
        ContainerInspectResponse inspection, string newContainerId)
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
                });

                _logger.LogDebug("Connected update container to network {Network}", networkName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect update container to network {Network}", networkName);
            }
        }
    }

    private async Task EnsureHelperImageAsync()
    {
        var fullImage = $"{HelperImage}:{HelperImageTag}";

        try
        {
            await _client.Images.InspectImageAsync(fullImage);
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
                }));
        }
    }

    private async Task RemoveContainerIfExistsAsync(string containerName)
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
                });

            var container = containers.FirstOrDefault(c =>
                c.Names.Any(n => n.TrimStart('/') == containerName));

            if (container != null)
            {
                _logger.LogWarning("Removing leftover container {ContainerName} from previous attempt", containerName);
                await _client.Containers.RemoveContainerAsync(container.ID,
                    new ContainerRemoveParameters { Force = true });
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

    /// <summary>
    /// Tracks Docker image pull progress across multiple layers to produce an overall percentage.
    /// </summary>
    internal class PullProgressTracker
    {
        private readonly Dictionary<string, (long Current, long Total)> _layers = new();

        public void Update(JSONMessage msg)
        {
            if (string.IsNullOrEmpty(msg.ID))
                return;

            if (msg.Progress is { Current: > 0, Total: > 0 })
            {
                _layers[msg.ID] = (msg.Progress.Current, msg.Progress.Total);
            }
            else if (msg.Status is "Pull complete" or "Already exists")
            {
                // Mark layer as done — use its known total or a default
                if (_layers.TryGetValue(msg.ID, out var existing))
                    _layers[msg.ID] = (existing.Total, existing.Total);
            }
        }

        public int GetPercent()
        {
            if (_layers.Count == 0)
                return 0;

            long totalBytes = 0;
            long downloadedBytes = 0;

            foreach (var (current, total) in _layers.Values)
            {
                totalBytes += total;
                downloadedBytes += current;
            }

            if (totalBytes == 0)
                return 0;

            return (int)Math.Min(100, downloadedBytes * 100 / totalBytes);
        }
    }
}
