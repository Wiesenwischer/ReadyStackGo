using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Containers.StopContainer;

public class StopContainerHandler : IRequestHandler<StopContainerCommand, StopContainerResult>
{
    private readonly IDockerService _dockerService;

    public StopContainerHandler(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<StopContainerResult> Handle(StopContainerCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _dockerService.StopContainerAsync(request.EnvironmentId, request.ContainerId, cancellationToken);
            return new StopContainerResult(true);
        }
        catch (InvalidOperationException ex)
        {
            return new StopContainerResult(false, ex.Message);
        }
    }
}
