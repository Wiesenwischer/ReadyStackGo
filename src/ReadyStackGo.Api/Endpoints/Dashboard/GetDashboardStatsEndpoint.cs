using System.Text.Json;
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
    [BindFrom("environment")]
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
        // Manually bind from query string since this is a GET request
        var environment = Query<string>("environment", false);
        if (string.IsNullOrWhiteSpace(environment))
        {
            // Return empty stats if no environment specified
            Response = new DashboardStatsDto();
            return;
        }

        try
        {
            var stacks = await StackService.ListStacksAsync(ct);
            var containers = await DockerService.ListContainersAsync(environment, ct);

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
        catch (JsonException ex)
        {
            ThrowError($"Configuration error: Unable to read configuration file. Please check that all configuration files contain valid JSON. Details: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message);
        }
        catch (Exception ex)
        {
            ThrowError($"Unexpected error: {ex.Message}");
        }
    }
}
