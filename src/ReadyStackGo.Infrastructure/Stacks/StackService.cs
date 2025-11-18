using Docker.DotNet;
using Docker.DotNet.Models;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Application.Stacks.DTOs;
using ReadyStackGo.Domain.Stacks;

namespace ReadyStackGo.Infrastructure.Stacks;

public class StackService : IStackService
{
    private readonly DockerClient _dockerClient;
    private readonly Dictionary<string, Stack> _stacks = new();

    // Hardcoded stack definition for v0.2
    private static readonly Stack DemoStack = new()
    {
        Id = "demo-stack",
        Name = "Demo Stack",
        Description = "A simple demo stack with Nginx and Redis",
        Status = StackStatus.NotDeployed,
        Services = new List<ContainerService>
        {
            new()
            {
                Name = "nginx",
                Image = "nginx:alpine",
                Ports = new List<string> { "8080:80" },
                Environment = new Dictionary<string, string>()
            },
            new()
            {
                Name = "redis",
                Image = "redis:alpine",
                Ports = new List<string> { "6379:6379" },
                Environment = new Dictionary<string, string>()
            }
        }
    };

    public StackService()
    {
        // Connect to Docker using named pipe on Windows or Unix socket on Linux
        var dockerUri = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        _dockerClient = new DockerClientConfiguration(new Uri(dockerUri))
            .CreateClient();

        // Initialize with the demo stack
        _stacks[DemoStack.Id] = DemoStack;
    }

    public async Task<StackDto?> DeployStackAsync(string stackId, CancellationToken cancellationToken = default)
    {
        if (!_stacks.TryGetValue(stackId, out var stack))
            return null;

        stack.Status = StackStatus.Deploying;

        try
        {
            foreach (var service in stack.Services)
            {
                var containerName = $"{stack.Id}-{service.Name}";

                // Check if container with this name already exists and remove it
                await RemoveExistingContainerByNameAsync(containerName, cancellationToken);

                // Create container
                var createResponse = await _dockerClient.Containers.CreateContainerAsync(
                    new CreateContainerParameters
                    {
                        Image = service.Image,
                        Name = $"{stack.Id}-{service.Name}",
                        Env = service.Environment?.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList(),
                        HostConfig = new HostConfig
                        {
                            PortBindings = ParsePortBindings(service.Ports),
                            AutoRemove = false
                        },
                        Labels = new Dictionary<string, string>
                        {
                            ["readystackgo.stack.id"] = stack.Id,
                            ["readystackgo.stack.name"] = stack.Name,
                            ["readystackgo.service.name"] = service.Name
                        }
                    },
                    cancellationToken);

                service.ContainerId = createResponse.ID;

                // Pull image if needed
                await PullImageIfNotExistsAsync(service.Image, cancellationToken);

                // Start container
                await _dockerClient.Containers.StartContainerAsync(
                    createResponse.ID,
                    new ContainerStartParameters(),
                    cancellationToken);

                service.ContainerStatus = "running";
            }

            stack.Status = StackStatus.Running;
            stack.DeployedAt = DateTime.UtcNow;
            stack.UpdatedAt = DateTime.UtcNow;

            return MapToStackDto(stack);
        }
        catch (Exception)
        {
            stack.Status = StackStatus.Failed;
            throw;
        }
    }

    public Task<IEnumerable<StackDto>> ListStacksAsync(CancellationToken cancellationToken = default)
    {
        var stackDtos = _stacks.Values.Select(MapToStackDto);
        return Task.FromResult(stackDtos);
    }

    public Task<StackDto?> GetStackAsync(string stackId, CancellationToken cancellationToken = default)
    {
        if (!_stacks.TryGetValue(stackId, out var stack))
            return Task.FromResult<StackDto?>(null);

        return Task.FromResult<StackDto?>(MapToStackDto(stack));
    }

    public async Task RemoveStackAsync(string stackId, CancellationToken cancellationToken = default)
    {
        if (!_stacks.TryGetValue(stackId, out var stack))
            return;

        foreach (var service in stack.Services)
        {
            if (service.ContainerId is not null)
            {
                try
                {
                    // Stop container
                    await _dockerClient.Containers.StopContainerAsync(
                        service.ContainerId,
                        new ContainerStopParameters { WaitBeforeKillSeconds = 10 },
                        cancellationToken);

                    // Remove container
                    await _dockerClient.Containers.RemoveContainerAsync(
                        service.ContainerId,
                        new ContainerRemoveParameters { Force = true },
                        cancellationToken);
                }
                catch
                {
                    // Continue removing other containers even if one fails
                }

                service.ContainerId = null;
                service.ContainerStatus = null;
            }
        }

        stack.Status = StackStatus.NotDeployed;
        stack.DeployedAt = null;
        stack.UpdatedAt = DateTime.UtcNow;
    }

    private async Task RemoveExistingContainerByNameAsync(string containerName, CancellationToken cancellationToken)
    {
        try
        {
            // List all containers (including stopped ones) with this name
            var containers = await _dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["name"] = new Dictionary<string, bool> { [containerName] = true }
                    }
                },
                cancellationToken);

            foreach (var container in containers)
            {
                // Stop container if running
                if (container.State == "running")
                {
                    await _dockerClient.Containers.StopContainerAsync(
                        container.ID,
                        new ContainerStopParameters { WaitBeforeKillSeconds = 10 },
                        cancellationToken);
                }

                // Remove container
                await _dockerClient.Containers.RemoveContainerAsync(
                    container.ID,
                    new ContainerRemoveParameters { Force = true },
                    cancellationToken);
            }
        }
        catch
        {
            // Ignore errors - container might not exist
        }
    }

    private async Task PullImageIfNotExistsAsync(string image, CancellationToken cancellationToken)
    {
        try
        {
            await _dockerClient.Images.InspectImageAsync(image, cancellationToken);
        }
        catch (DockerImageNotFoundException)
        {
            // Image doesn't exist, pull it
            await _dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = image },
                null,
                new Progress<JSONMessage>(),
                cancellationToken);
        }
    }

    private static Dictionary<string, IList<PortBinding>>? ParsePortBindings(List<string>? ports)
    {
        if (ports is null || ports.Count == 0)
            return null;

        var bindings = new Dictionary<string, IList<PortBinding>>();

        foreach (var port in ports)
        {
            var parts = port.Split(':');
            if (parts.Length != 2)
                continue;

            var hostPort = parts[0];
            var containerPort = parts[1];

            bindings[$"{containerPort}/tcp"] = new List<PortBinding>
            {
                new() { HostPort = hostPort }
            };
        }

        return bindings;
    }

    private static StackDto MapToStackDto(Stack stack)
    {
        return new StackDto
        {
            Id = stack.Id,
            Name = stack.Name,
            Description = stack.Description,
            Status = stack.Status.ToString(),
            DeployedAt = stack.DeployedAt,
            UpdatedAt = stack.UpdatedAt,
            Services = stack.Services.Select(s => new StackServiceDto
            {
                Name = s.Name,
                Image = s.Image,
                Ports = s.Ports,
                Environment = s.Environment,
                Volumes = s.Volumes,
                ContainerId = s.ContainerId,
                ContainerStatus = s.ContainerStatus
            }).ToList()
        };
    }
}
