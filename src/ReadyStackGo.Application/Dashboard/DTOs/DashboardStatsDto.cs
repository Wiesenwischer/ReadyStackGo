namespace ReadyStackGo.Application.Dashboard.DTOs;

public class DashboardStatsDto
{
    public int TotalStacks { get; set; }
    public int DeployedStacks { get; set; }
    public int NotDeployedStacks { get; set; }
    public int TotalContainers { get; set; }
    public int RunningContainers { get; set; }
    public int StoppedContainers { get; set; }

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
