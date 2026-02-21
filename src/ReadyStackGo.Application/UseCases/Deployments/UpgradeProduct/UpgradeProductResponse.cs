namespace ReadyStackGo.Application.UseCases.Deployments.UpgradeProduct;

/// <summary>
/// Response from upgrading an entire product deployment.
/// </summary>
public class UpgradeProductResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ProductDeploymentId { get; set; }
    public string? ProductName { get; set; }
    public string? PreviousVersion { get; set; }
    public string? NewVersion { get; set; }
    public string? Status { get; set; }
    public string? SessionId { get; set; }
    public List<UpgradeProductStackResult> StackResults { get; set; } = new();
    public List<string>? Warnings { get; set; }

    public static UpgradeProductResponse Failed(string message) => new()
    {
        Success = false,
        Message = message
    };
}

/// <summary>
/// Result of upgrading a single stack within a product upgrade.
/// </summary>
public class UpgradeProductStackResult
{
    public required string StackName { get; set; }
    public required string StackDisplayName { get; set; }
    public bool Success { get; set; }
    public string? DeploymentId { get; set; }
    public string? DeploymentStackName { get; set; }
    public string? ErrorMessage { get; set; }
    public int ServiceCount { get; set; }
    public bool IsNewInUpgrade { get; set; }
}
