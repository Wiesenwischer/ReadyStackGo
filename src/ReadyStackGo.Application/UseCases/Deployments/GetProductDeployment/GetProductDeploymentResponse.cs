namespace ReadyStackGo.Application.UseCases.Deployments.GetProductDeployment;

/// <summary>
/// Detailed response for a product deployment.
/// </summary>
public class GetProductDeploymentResponse
{
    public required string ProductDeploymentId { get; set; }
    public required string EnvironmentId { get; set; }
    public required string ProductGroupId { get; set; }
    public required string ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string ProductDisplayName { get; set; }
    public required string ProductVersion { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ContinueOnError { get; set; }
    public int TotalStacks { get; set; }
    public int CompletedStacks { get; set; }
    public int FailedStacks { get; set; }
    public string? PreviousVersion { get; set; }
    public int UpgradeCount { get; set; }
    public bool CanUpgrade { get; set; }
    public bool CanRemove { get; set; }
    public double? DurationSeconds { get; set; }
    public List<ProductStackDeploymentDto> Stacks { get; set; } = new();
    public Dictionary<string, string> SharedVariables { get; set; } = new();
}

/// <summary>
/// DTO for a stack within a product deployment.
/// </summary>
public class ProductStackDeploymentDto
{
    public required string StackName { get; set; }
    public required string StackDisplayName { get; set; }
    public required string StackId { get; set; }
    public string? DeploymentId { get; set; }
    public string? DeploymentStackName { get; set; }
    public required string Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int Order { get; set; }
    public int ServiceCount { get; set; }
    public bool IsNewInUpgrade { get; set; }
}
