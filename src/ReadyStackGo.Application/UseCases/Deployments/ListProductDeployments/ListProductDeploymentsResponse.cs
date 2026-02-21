namespace ReadyStackGo.Application.UseCases.Deployments.ListProductDeployments;

/// <summary>
/// Response containing a list of product deployments.
/// </summary>
public class ListProductDeploymentsResponse
{
    public bool Success { get; set; } = true;
    public List<ProductDeploymentSummaryDto> ProductDeployments { get; set; } = new();
}

/// <summary>
/// Summary DTO for a product deployment (used in lists).
/// </summary>
public class ProductDeploymentSummaryDto
{
    public required string ProductDeploymentId { get; set; }
    public required string ProductGroupId { get; set; }
    public required string ProductName { get; set; }
    public required string ProductDisplayName { get; set; }
    public required string ProductVersion { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalStacks { get; set; }
    public int CompletedStacks { get; set; }
    public int FailedStacks { get; set; }
    public bool CanUpgrade { get; set; }
    public bool CanRemove { get; set; }
}
