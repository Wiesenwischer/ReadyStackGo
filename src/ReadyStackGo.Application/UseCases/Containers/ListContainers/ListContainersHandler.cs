using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Containers.ListContainers;

public class ListContainersHandler : IRequestHandler<ListContainersQuery, ListContainersResult>
{
    private readonly IDockerService _dockerService;

    public ListContainersHandler(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<ListContainersResult> Handle(ListContainersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);
            return new ListContainersResult(true, containers);
        }
        catch (InvalidOperationException ex)
        {
            return new ListContainersResult(false, Enumerable.Empty<ContainerDto>(), ex.Message);
        }
    }
}
