using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Containers.StartContainer;

public class StartContainerHandler : IRequestHandler<StartContainerCommand, StartContainerResult>
{
    private readonly IDockerService _dockerService;

    public StartContainerHandler(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<StartContainerResult> Handle(StartContainerCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _dockerService.StartContainerAsync(request.EnvironmentId, request.ContainerId, cancellationToken);
            return new StartContainerResult(true);
        }
        catch (InvalidOperationException ex)
        {
            return new StartContainerResult(false, ex.Message);
        }
    }
}
