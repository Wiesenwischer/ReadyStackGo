namespace ReadyStackGo.Application.UseCases.Deployments.GetDeploymentSnapshots;

/// <summary>
/// Response containing deployment snapshots.
/// </summary>
public class GetDeploymentSnapshotsResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? DeploymentId { get; set; }
    public string? StackName { get; set; }
    public string? CurrentVersion { get; set; }
    public bool CanRollback { get; set; }
    public List<DeploymentSnapshotDto> Snapshots { get; set; } = new();
}

/// <summary>
/// DTO for deployment snapshot information.
/// </summary>
public class DeploymentSnapshotDto
{
    public required string SnapshotId { get; set; }
    public required string StackVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Description { get; set; }
    public int ServiceCount { get; set; }
    public int VariableCount { get; set; }
}
