namespace ReadyStackGo.Application.UseCases.Deployments.StopProductContainers;

/// <summary>
/// Response from stopping containers of a product deployment.
/// </summary>
public class StopProductContainersResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ProductDeploymentId { get; set; }
    public string? ProductName { get; set; }
    public int TotalStacks { get; set; }
    public int StoppedStacks { get; set; }
    public int FailedStacks { get; set; }
    public List<StackContainerResult> Results { get; set; } = new();

    public static StopProductContainersResponse Failed(string message) => new()
    {
        Success = false,
        Message = message
    };
}

/// <summary>
/// Result of stopping containers for a single stack within a product.
/// </summary>
public class StackContainerResult
{
    public required string StackName { get; set; }
    public required string StackDisplayName { get; set; }
    public bool Success { get; set; }
    public int ContainersStopped { get; set; }
    public string? Error { get; set; }
}
