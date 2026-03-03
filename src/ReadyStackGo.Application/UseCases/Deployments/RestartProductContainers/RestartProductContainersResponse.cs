namespace ReadyStackGo.Application.UseCases.Deployments.RestartProductContainers;

/// <summary>
/// Response from restarting containers of a product deployment.
/// </summary>
public class RestartProductContainersResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ProductDeploymentId { get; set; }
    public string? ProductName { get; set; }
    public int TotalStacks { get; set; }
    public int RestartedStacks { get; set; }
    public int FailedStacks { get; set; }
    public List<StackRestartResult> Results { get; set; } = new();

    public static RestartProductContainersResponse Failed(string message) => new()
    {
        Success = false,
        Message = message
    };
}

/// <summary>
/// Result of restarting containers for a single stack within a product.
/// </summary>
public class StackRestartResult
{
    public required string StackName { get; set; }
    public required string StackDisplayName { get; set; }
    public bool Success { get; set; }
    public int ContainersStopped { get; set; }
    public int ContainersStarted { get; set; }
    public string? Error { get; set; }
}
