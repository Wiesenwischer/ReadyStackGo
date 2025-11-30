using System.Collections.Concurrent;
using System.Text.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Docker;

public class DockerService : IDockerService, IDisposable
{
    private readonly IConfigStore _configStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DockerService> _logger;
    private readonly ConcurrentDictionary<string, DockerClient> _clientCache = new();
    private bool _disposed;

    public DockerService(IConfigStore configStore, IConfiguration configuration, ILogger<DockerService> logger)
    {
        _configStore = configStore;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IEnumerable<ContainerDto>> ListContainersAsync(string environmentId, CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync(environmentId);

        var containers = await client.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true // Show all containers, not just running ones
            },
            cancellationToken);

        return containers.Select(MapToContainerDto);
    }

    public async Task StartContainerAsync(string environmentId, string containerId, CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync(environmentId);

        await client.Containers.StartContainerAsync(
            containerId,
            new ContainerStartParameters(),
            cancellationToken);

        _logger.LogInformation("Started container {ContainerId} in environment {EnvironmentId}", containerId, environmentId);
    }

    public async Task StopContainerAsync(string environmentId, string containerId, CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync(environmentId);

        await client.Containers.StopContainerAsync(
            containerId,
            new ContainerStopParameters
            {
                WaitBeforeKillSeconds = 10 // Give container 10 seconds to gracefully shut down
            },
            cancellationToken);

        _logger.LogInformation("Stopped container {ContainerId} in environment {EnvironmentId}", containerId, environmentId);
    }

    public async Task<TestConnectionResult> TestConnectionAsync(string dockerHost, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Testing connection to Docker host: {DockerHost}", dockerHost);

            var uri = ParseDockerUri(dockerHost);
            using var client = new DockerClientConfiguration(uri).CreateClient();

            // Try to get Docker system info to verify connection
            var systemInfo = await client.System.GetSystemInfoAsync(cancellationToken);

            var message = $"Connected to Docker {systemInfo.ServerVersion} on {systemInfo.OperatingSystem}";
            _logger.LogInformation("Connection test successful: {Message}", message);

            return new TestConnectionResult(true, message, systemInfo.ServerVersion);
        }
        catch (Exception ex)
        {
            var message = $"Connection failed: {ex.Message}";
            _logger.LogWarning(ex, "Connection test failed for {DockerHost}", dockerHost);
            return new TestConnectionResult(false, message);
        }
    }

    public async Task<string> CreateAndStartContainerAsync(
        string environmentId,
        CreateContainerRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync(environmentId);

        _logger.LogInformation("Creating container {Name} from image {Image} in environment {EnvironmentId}",
            request.Name, request.Image, environmentId);

        // Parse port bindings
        var portBindings = new Dictionary<string, IList<PortBinding>>();
        var exposedPorts = new Dictionary<string, EmptyStruct>();

        foreach (var port in request.Ports)
        {
            var parts = port.Split(':');
            string containerPort, hostPort;

            if (parts.Length == 2)
            {
                hostPort = parts[0];
                containerPort = parts[1];
            }
            else
            {
                containerPort = parts[0];
                hostPort = parts[0];
            }

            // Ensure port has protocol
            if (!containerPort.Contains('/'))
            {
                containerPort += "/tcp";
            }

            exposedPorts[containerPort] = new EmptyStruct();
            portBindings[containerPort] = new List<PortBinding>
            {
                new PortBinding { HostPort = hostPort }
            };
        }

        // Parse volume bindings
        var binds = new List<string>();
        foreach (var (hostPath, containerPath) in request.Volumes)
        {
            binds.Add($"{hostPath}:{containerPath}");
        }

        // Convert environment variables to list format
        var envVars = request.EnvironmentVariables
            .Select(kv => $"{kv.Key}={kv.Value}")
            .ToList();

        // Parse restart policy
        var restartPolicy = new RestartPolicy
        {
            Name = request.RestartPolicy switch
            {
                "always" => RestartPolicyKind.Always,
                "unless-stopped" => RestartPolicyKind.UnlessStopped,
                "on-failure" => RestartPolicyKind.OnFailure,
                _ => RestartPolicyKind.No
            }
        };

        // Create container with network aliases for service name resolution
        var primaryNetwork = request.Networks.FirstOrDefault() ?? "bridge";
        var networkingConfig = new NetworkingConfig
        {
            EndpointsConfig = new Dictionary<string, EndpointSettings>
            {
                [primaryNetwork] = new EndpointSettings
                {
                    Aliases = request.NetworkAliases
                }
            }
        };

        var createParams = new CreateContainerParameters
        {
            Name = request.Name,
            Image = request.Image,
            Env = envVars,
            ExposedPorts = exposedPorts,
            Labels = request.Labels,
            HostConfig = new HostConfig
            {
                PortBindings = portBindings,
                Binds = binds,
                RestartPolicy = restartPolicy,
                NetworkMode = primaryNetwork
            },
            NetworkingConfig = networkingConfig
        };

        var response = await client.Containers.CreateContainerAsync(createParams, cancellationToken);

        // Connect to additional networks with aliases
        foreach (var network in request.Networks.Skip(1))
        {
            try
            {
                await client.Networks.ConnectNetworkAsync(network, new NetworkConnectParameters
                {
                    Container = response.ID,
                    EndpointConfig = new EndpointSettings
                    {
                        Aliases = request.NetworkAliases
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect container {ContainerId} to network {Network}",
                    response.ID, network);
            }
        }

        // Start container
        await client.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), cancellationToken);

        _logger.LogInformation("Created and started container {ContainerId} ({Name}) in environment {EnvironmentId}",
            response.ID, request.Name, environmentId);

        return response.ID;
    }

    public async Task RemoveContainerAsync(string environmentId, string containerId, bool force = false, CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync(environmentId);

        _logger.LogInformation("Removing container {ContainerId} in environment {EnvironmentId}", containerId, environmentId);

        await client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters
        {
            Force = force,
            RemoveVolumes = false
        }, cancellationToken);

        _logger.LogInformation("Removed container {ContainerId} in environment {EnvironmentId}", containerId, environmentId);
    }

    public async Task EnsureNetworkAsync(string environmentId, string networkName, CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync(environmentId);

        try
        {
            // Check if network exists
            var networks = await client.Networks.ListNetworksAsync(new NetworksListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["name"] = new Dictionary<string, bool> { [networkName] = true }
                }
            }, cancellationToken);

            if (networks.Any(n => n.Name == networkName))
            {
                _logger.LogDebug("Network {NetworkName} already exists in environment {EnvironmentId}",
                    networkName, environmentId);
                return;
            }

            // Create network
            await client.Networks.CreateNetworkAsync(new NetworksCreateParameters
            {
                Name = networkName,
                Driver = "bridge",
                CheckDuplicate = true
            }, cancellationToken);

            _logger.LogInformation("Created network {NetworkName} in environment {EnvironmentId}",
                networkName, environmentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure network {NetworkName} in environment {EnvironmentId}",
                networkName, environmentId);
            throw;
        }
    }

    public async Task PullImageAsync(string environmentId, string image, string tag = "latest", CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync(environmentId);

        var fullImage = $"{image}:{tag}";
        _logger.LogInformation("Pulling image {Image} in environment {EnvironmentId}", fullImage, environmentId);

        // Try to get auth config for the registry
        var authConfig = GetAuthConfigForImage(image);
        if (authConfig != null)
        {
            _logger.LogDebug("Using registry credentials for {Image}", image);
        }

        await client.Images.CreateImageAsync(
            new ImagesCreateParameters
            {
                FromImage = image,
                Tag = tag
            },
            authConfig,
            new Progress<JSONMessage>(msg =>
            {
                if (!string.IsNullOrEmpty(msg.Status))
                {
                    _logger.LogDebug("Pull progress: {Status}", msg.Status);
                }
            }),
            cancellationToken);

        _logger.LogInformation("Pulled image {Image} in environment {EnvironmentId}", fullImage, environmentId);
    }

    /// <summary>
    /// Get authentication config for a Docker image from ~/.docker/config.json
    /// </summary>
    private AuthConfig? GetAuthConfigForImage(string image)
    {
        try
        {
            // Extract registry from image name
            var registry = GetRegistryFromImage(image);
            _logger.LogInformation("Looking for credentials for image {Image}, registry: {Registry}", image, registry);

            // Read Docker config file
            var configPath = GetDockerConfigPath();
            _logger.LogInformation("Docker config path: {Path}", configPath);

            if (!File.Exists(configPath))
            {
                _logger.LogWarning("Docker config file not found at {Path}", configPath);
                return null;
            }

            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<DockerConfigFile>(configJson);

            // Log available registries
            if (config?.Auths != null)
            {
                _logger.LogInformation("Available registries in config: {Registries}", string.Join(", ", config.Auths.Keys));
            }
            else
            {
                _logger.LogWarning("No auths section found in Docker config");
                return null;
            }

            DockerAuthEntry? auth = null;
            string? foundRegistry = null;

            // Try exact match first
            if (config.Auths.TryGetValue(registry, out auth))
            {
                foundRegistry = registry;
            }
            // Try with https:// prefix
            else if (config.Auths.TryGetValue($"https://{registry}", out auth))
            {
                foundRegistry = $"https://{registry}";
            }
            // For Docker Hub, also try without /v1/
            else if (registry == "https://index.docker.io/v1/" && config.Auths.TryGetValue("https://index.docker.io/v1", out auth))
            {
                foundRegistry = "https://index.docker.io/v1";
            }

            if (auth == null)
            {
                _logger.LogWarning("No credentials found for registry {Registry}", registry);
                return null;
            }

            _logger.LogInformation("Found credentials for registry {Registry}", foundRegistry);

            // Docker stores credentials as base64(username:password)
            if (!string.IsNullOrEmpty(auth.Auth))
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(auth.Auth));
                var parts = decoded.Split(':', 2);
                if (parts.Length == 2)
                {
                    _logger.LogInformation("Using credentials for user {Username}", parts[0]);
                    return new AuthConfig
                    {
                        Username = parts[0],
                        Password = parts[1],
                        ServerAddress = registry
                    };
                }
            }

            // Fallback to username/password if stored directly
            if (!string.IsNullOrEmpty(auth.Username))
            {
                _logger.LogInformation("Using direct credentials for user {Username}", auth.Username);
                return new AuthConfig
                {
                    Username = auth.Username,
                    Password = auth.Password,
                    ServerAddress = registry
                };
            }

            _logger.LogWarning("Auth entry found but no credentials could be extracted");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Docker credentials for image {Image}", image);
            return null;
        }
    }

    /// <summary>
    /// Extract the registry hostname from a Docker image reference.
    /// Examples:
    /// - nginx -> docker.io
    /// - myregistry.com/myimage -> myregistry.com
    /// - myregistry.com:5000/myimage -> myregistry.com:5000
    /// </summary>
    private static string GetRegistryFromImage(string image)
    {
        // If no slash, it's a Docker Hub image
        if (!image.Contains('/'))
        {
            return "https://index.docker.io/v1/";
        }

        var firstPart = image.Split('/')[0];

        // Check if the first part looks like a registry (contains . or :)
        if (firstPart.Contains('.') || firstPart.Contains(':'))
        {
            return firstPart;
        }

        // Otherwise it's a Docker Hub user/image
        return "https://index.docker.io/v1/";
    }

    /// <summary>
    /// Get the path to the Docker config file.
    /// Checks configuration in this order:
    /// 1. Docker:ConfigPath from IConfiguration (appsettings.json or environment variable DOCKER__CONFIGPATH)
    /// 2. DOCKER_CONFIG environment variable (standard Docker convention)
    /// 3. /root/.docker/config.json on Linux (container environment)
    /// 4. ~/.docker/config.json (user profile)
    /// </summary>
    private string GetDockerConfigPath()
    {
        // 1. Check IConfiguration (supports appsettings.json and env vars like DOCKER__CONFIGPATH)
        var configuredPath = _configuration["Docker:ConfigPath"];
        if (!string.IsNullOrEmpty(configuredPath))
        {
            _logger.LogDebug("Using Docker config path from configuration: {Path}", configuredPath);
            return configuredPath;
        }

        // 2. Check DOCKER_CONFIG env var (standard Docker convention)
        var dockerConfigDir = _configuration["DOCKER_CONFIG"];
        if (!string.IsNullOrEmpty(dockerConfigDir))
        {
            var path = Path.Combine(dockerConfigDir, "config.json");
            _logger.LogDebug("Using Docker config from DOCKER_CONFIG: {Path}", path);
            return path;
        }

        // 3. On Linux, prefer /root/.docker (container environment)
        if (!OperatingSystem.IsWindows())
        {
            var linuxPath = "/root/.docker/config.json";
            if (File.Exists(linuxPath))
            {
                _logger.LogDebug("Using Docker config from Linux root: {Path}", linuxPath);
                return linuxPath;
            }
        }

        // 4. Fallback to user profile (~/.docker/config.json)
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fallbackPath = Path.Combine(home, ".docker", "config.json");
        _logger.LogDebug("Using Docker config from user profile: {Path}", fallbackPath);
        return fallbackPath;
    }

    /// <summary>
    /// Docker config.json structure
    /// </summary>
    private class DockerConfigFile
    {
        [System.Text.Json.Serialization.JsonPropertyName("auths")]
        public Dictionary<string, DockerAuthEntry>? Auths { get; set; }
    }

    private class DockerAuthEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("auth")]
        public string? Auth { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("username")]
        public string? Username { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("password")]
        public string? Password { get; set; }
    }

    public async Task<ContainerDto?> GetContainerByNameAsync(string environmentId, string containerName, CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync(environmentId);

        var containers = await client.Containers.ListContainersAsync(
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

        return container != null ? MapToContainerDto(container) : null;
    }

    public async Task<bool> ImageExistsAsync(string environmentId, string image, string tag = "latest", CancellationToken cancellationToken = default)
    {
        var client = await GetDockerClientAsync(environmentId);
        var fullImage = $"{image}:{tag}";

        try
        {
            var images = await client.Images.ListImagesAsync(
                new ImagesListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["reference"] = new Dictionary<string, bool> { [fullImage] = true }
                    }
                },
                cancellationToken);

            return images.Any();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking if image {Image} exists", fullImage);
            return false;
        }
    }

    private async Task<DockerClient> GetDockerClientAsync(string environmentId)
    {
        // Check cache first
        if (_clientCache.TryGetValue(environmentId, out var cachedClient))
        {
            return cachedClient;
        }

        // Load environment configuration
        var systemConfig = await _configStore.GetSystemConfigAsync();

        if (systemConfig.Organization == null)
        {
            throw new InvalidOperationException("Organization not configured. Complete the setup wizard first.");
        }

        var environment = systemConfig.Organization.GetEnvironment(environmentId);

        if (environment == null)
        {
            throw new InvalidOperationException($"Environment '{environmentId}' not found.");
        }

        var connectionString = environment.GetConnectionString();
        var uri = ParseDockerUri(connectionString);

        _logger.LogDebug("Creating Docker client for environment {EnvironmentId} with URI {Uri}", environmentId, uri);

        var client = new DockerClientConfiguration(uri).CreateClient();

        // Cache the client
        _clientCache[environmentId] = client;

        return client;
    }

    private static Uri ParseDockerUri(string connectionString)
    {
        // Handle different Docker URI formats
        if (connectionString.StartsWith("unix://"))
        {
            return new Uri(connectionString);
        }

        if (connectionString.StartsWith("npipe://"))
        {
            return new Uri(connectionString);
        }

        if (connectionString.StartsWith("tcp://"))
        {
            return new Uri(connectionString);
        }

        // If it's a plain path, assume it's a Unix socket
        if (connectionString.StartsWith("/"))
        {
            return new Uri($"unix://{connectionString}");
        }

        // If it's a Windows named pipe path
        if (connectionString.Contains("pipe"))
        {
            return new Uri($"npipe://{connectionString}");
        }

        // Default: treat as TCP
        return new Uri($"tcp://{connectionString}");
    }

    private static ContainerDto MapToContainerDto(ContainerListResponse container)
    {
        // Extract health status from Status string (e.g., "Up 2 hours (healthy)")
        var healthStatus = ExtractHealthStatus(container.Status);

        return new ContainerDto
        {
            Id = container.ID,
            Name = container.Names.FirstOrDefault()?.TrimStart('/') ?? "Unknown",
            Image = container.Image,
            State = container.State,
            Status = container.Status,
            Created = container.Created,
            Ports = container.Ports?.Select(p => new PortDto
            {
                PrivatePort = (int)p.PrivatePort,
                PublicPort = (int)p.PublicPort,
                Type = p.Type
            }).ToList() ?? [],
            Labels = container.Labels != null
                ? new Dictionary<string, string>(container.Labels)
                : new Dictionary<string, string>(),
            HealthStatus = healthStatus
        };
    }

    private static string ExtractHealthStatus(string status)
    {
        if (string.IsNullOrEmpty(status))
            return "none";

        // Docker status format: "Up 2 hours (healthy)" or "Up 2 hours (unhealthy)"
        if (status.Contains("(healthy)"))
            return "healthy";
        if (status.Contains("(unhealthy)"))
            return "unhealthy";
        if (status.Contains("(health: starting)"))
            return "starting";

        return "none";
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var client in _clientCache.Values)
        {
            client.Dispose();
        }
        _clientCache.Clear();

        _disposed = true;
    }
}
