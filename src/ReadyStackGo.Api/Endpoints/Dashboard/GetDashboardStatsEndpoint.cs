using FastEndpoints;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Application.Dashboard.DTOs;
using ReadyStackGo.Application.Stacks;

namespace ReadyStackGo.API.Endpoints.Dashboard;

public class GetDashboardStatsRequest
{
    /// <summary>
    /// The environment ID to get stats for.
    /// </summary>
    [QueryParam]
    public string Environment { get; set; } = null!;
}

public class GetDashboardStatsEndpoint : Endpoint<GetDashboardStatsRequest, DashboardStatsDto>
{
    public IStackService StackService { get; set; } = null!;
    public IDockerService DockerService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/dashboard/stats");
        Roles("admin", "operator");
        Description(b => b.WithTags("Dashboard"));
    }

    public override async Task HandleAsync(GetDashboardStatsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            // Return empty stats if no environment specified
            Response = new DashboardStatsDto();
            return;
        }

        try
        {
            var stacks = await StackService.ListStacksAsync(ct);
            var containers = await DockerService.ListContainersAsync(req.Environment, ct);

            var stats = new DashboardStatsDto
            {
                TotalStacks = stacks.Count(),
                DeployedStacks = stacks.Count(s => s.Status == "Running" || s.Status == "Deploying"),
                NotDeployedStacks = stacks.Count(s => s.Status == "NotDeployed"),
                TotalContainers = containers.Count(),
                RunningContainers = containers.Count(c => c.State == "running"),
                StoppedContainers = containers.Count(c => c.State != "running")
            };

            Response = stats;
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message);
        }
    }
}
