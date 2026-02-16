using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Containers.RemoveContainer;

public class RemoveContainerHandler : IRequestHandler<RemoveContainerCommand, RemoveContainerResult>
{
    private readonly IDockerService _dockerService;

    public RemoveContainerHandler(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<RemoveContainerResult> Handle(RemoveContainerCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (!request.Force)
            {
                var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);
                var container = containers.FirstOrDefault(c => c.Id == request.ContainerId);

                if (container == null)
                    return new RemoveContainerResult(false, "Container not found.");

                if (container.State.Equals("running", StringComparison.OrdinalIgnoreCase))
                    return new RemoveContainerResult(false, "Cannot remove a running container. Stop it first or use force.");
            }

            await _dockerService.RemoveContainerAsync(request.EnvironmentId, request.ContainerId, request.Force, cancellationToken);
            return new RemoveContainerResult(true);
        }
        catch (InvalidOperationException ex)
        {
            return new RemoveContainerResult(false, ex.Message);
        }
    }
}
