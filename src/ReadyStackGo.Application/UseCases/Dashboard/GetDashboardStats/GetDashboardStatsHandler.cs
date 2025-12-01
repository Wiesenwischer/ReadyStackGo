using System.Text.Json;
using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Dashboard.GetDashboardStats;

public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, GetDashboardStatsResult>
{
    private readonly IStackSourceService _stackSourceService;
    private readonly IDockerService _dockerService;

    public GetDashboardStatsHandler(IStackSourceService stackSourceService, IDockerService dockerService)
    {
        _stackSourceService = stackSourceService;
        _dockerService = dockerService;
    }

    public async Task<GetDashboardStatsResult> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EnvironmentId))
        {
            return new GetDashboardStatsResult(true, new DashboardStatsDto());
        }

        try
        {
            var stacks = await _stackSourceService.GetStacksAsync(cancellationToken);
            var containers = await _dockerService.ListContainersAsync(request.EnvironmentId, cancellationToken);

            var stats = new DashboardStatsDto
            {
                TotalStacks = stacks.Count(),
                DeployedStacks = 0,
                NotDeployedStacks = stacks.Count(),
                TotalContainers = containers.Count(),
                RunningContainers = containers.Count(c => c.State == "running"),
                StoppedContainers = containers.Count(c => c.State != "running")
            };

            return new GetDashboardStatsResult(true, stats);
        }
        catch (JsonException ex)
        {
            return new GetDashboardStatsResult(false, new DashboardStatsDto(),
                $"Configuration error: Unable to read configuration file. Details: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return new GetDashboardStatsResult(false, new DashboardStatsDto(), ex.Message);
        }
        catch (Exception ex)
        {
            return new GetDashboardStatsResult(false, new DashboardStatsDto(), $"Unexpected error: {ex.Message}");
        }
    }
}
