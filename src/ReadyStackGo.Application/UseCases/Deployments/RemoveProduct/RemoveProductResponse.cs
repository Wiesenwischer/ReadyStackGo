namespace ReadyStackGo.Application.UseCases.Deployments.RemoveProduct;

/// <summary>
/// Response from removing an entire product deployment.
/// </summary>
public class RemoveProductResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ProductDeploymentId { get; set; }
    public string? ProductName { get; set; }
    public string? Status { get; set; }
    public string? SessionId { get; set; }
    public List<RemoveProductStackResult> StackResults { get; set; } = new();

    public static RemoveProductResponse Failed(string message) => new()
    {
        Success = false,
        Message = message
    };
}

/// <summary>
/// Result of removing a single stack within a product removal.
/// </summary>
public class RemoveProductStackResult
{
    public required string StackName { get; set; }
    public required string StackDisplayName { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ServiceCount { get; set; }
}
