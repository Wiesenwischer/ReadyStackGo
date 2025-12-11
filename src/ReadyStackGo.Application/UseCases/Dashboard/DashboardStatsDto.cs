namespace ReadyStackGo.Application.UseCases.Dashboard;

public class DashboardStatsDto
{
    /// <summary>
    /// Total number of products in the catalog.
    /// A product is a grouping of one or more stacks (e.g., "Business Services").
    /// </summary>
    public int TotalProducts { get; set; }

    /// <summary>
    /// Total number of stacks (variants) in the catalog.
    /// A stack is a deployable configuration (e.g., "Business Services - Standard").
    /// </summary>
    public int TotalStacks { get; set; }

    /// <summary>
    /// Number of active deployments in the environment.
    /// </summary>
    public int DeployedStacks { get; set; }

    /// <summary>
    /// Number of stacks that are not deployed.
    /// </summary>
    public int NotDeployedStacks { get; set; }

    public int TotalContainers { get; set; }
    public int RunningContainers { get; set; }
    public int StoppedContainers { get; set; }

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
