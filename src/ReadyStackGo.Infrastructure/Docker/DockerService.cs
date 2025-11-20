using System.Collections.Concurrent;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Application.Containers.DTOs;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Docker;

public class DockerService : IDockerService, IDisposable
{
    private readonly IConfigStore _configStore;
    private readonly ILogger<DockerService> _logger;
    private readonly ConcurrentDictionary<string, DockerClient> _clientCache = new();
    private bool _disposed;

    public DockerService(IConfigStore configStore, ILogger<DockerService> logger)
    {
        _configStore = configStore;
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
            }).ToList() ?? []
        };
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
