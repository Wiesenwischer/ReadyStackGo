using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Containers.GetContainerLogs;

public class GetContainerLogsHandler : IRequestHandler<GetContainerLogsQuery, GetContainerLogsResult>
{
    private readonly IDockerService _dockerService;

    public GetContainerLogsHandler(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<GetContainerLogsResult> Handle(GetContainerLogsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var logs = await _dockerService.GetContainerLogsAsync(
                request.EnvironmentId,
                request.ContainerId,
                request.Tail,
                cancellationToken);

            return new GetContainerLogsResult(true, logs);
        }
        catch (Exception ex)
        {
            return new GetContainerLogsResult(false, string.Empty, ex.Message);
        }
    }
}
