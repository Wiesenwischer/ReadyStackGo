using Docker.DotNet;
using Docker.DotNet.Models;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Application.Containers.DTOs;

namespace ReadyStackGo.Infrastructure.Docker;

public class DockerService : IDockerService
{
    private readonly DockerClient _dockerClient;

    public DockerService()
    {
        // Connect to Docker using named pipe on Windows or Unix socket on Linux
        var dockerUri = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        _dockerClient = new DockerClientConfiguration(new Uri(dockerUri))
            .CreateClient();
    }

    public async Task<IEnumerable<ContainerDto>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        var containers = await _dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true // Show all containers, not just running ones
            },
            cancellationToken);

        return containers.Select(MapToContainerDto);
    }

    public async Task StartContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        await _dockerClient.Containers.StartContainerAsync(
            containerId,
            new ContainerStartParameters(),
            cancellationToken);
    }

    public async Task StopContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        await _dockerClient.Containers.StopContainerAsync(
            containerId,
            new ContainerStopParameters
            {
                WaitBeforeKillSeconds = 10 // Give container 10 seconds to gracefully shut down
            },
            cancellationToken);
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
}
