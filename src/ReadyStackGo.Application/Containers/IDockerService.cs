using ReadyStackGo.Application.Containers.DTOs;

namespace ReadyStackGo.Application.Containers;

public interface IDockerService
{
    Task<IEnumerable<ContainerDto>> ListContainersAsync(CancellationToken cancellationToken = default);
    Task StartContainerAsync(string containerId, CancellationToken cancellationToken = default);
    Task StopContainerAsync(string containerId, CancellationToken cancellationToken = default);
}
