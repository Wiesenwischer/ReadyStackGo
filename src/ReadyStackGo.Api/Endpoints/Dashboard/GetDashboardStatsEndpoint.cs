using FastEndpoints;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Application.Dashboard.DTOs;
using ReadyStackGo.Application.Stacks;

namespace ReadyStackGo.API.Endpoints.Dashboard;

public class GetDashboardStatsEndpoint : Endpoint<EmptyRequest, DashboardStatsDto>
{
    public IStackService StackService { get; set; } = null!;
    public IDockerService DockerService { get; set; } = null!;

    public override void Configure()
    {
        Get("/api/dashboard/stats");
        Roles("admin", "operator");
        Options(x => x.WithTags("Dashboard"));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var stacks = await StackService.ListStacksAsync(ct);
        var containers = await DockerService.ListContainersAsync(ct);

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
}
