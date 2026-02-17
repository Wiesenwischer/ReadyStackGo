namespace ReadyStackGo.Application.UseCases.Deployments.DeployProduct;

/// <summary>
/// Response from deploying an entire product.
/// </summary>
public class DeployProductResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ProductDeploymentId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductVersion { get; set; }
    public string? Status { get; set; }
    public string? SessionId { get; set; }
    public List<DeployProductStackResult> StackResults { get; set; } = new();

    public static DeployProductResponse Failed(string message) => new()
    {
        Success = false,
        Message = message
    };
}

/// <summary>
/// Result of deploying a single stack within a product deployment.
/// </summary>
public class DeployProductStackResult
{
    public required string StackName { get; set; }
    public required string StackDisplayName { get; set; }
    public bool Success { get; set; }
    public string? DeploymentId { get; set; }
    public string? DeploymentStackName { get; set; }
    public string? ErrorMessage { get; set; }
    public int ServiceCount { get; set; }
}
