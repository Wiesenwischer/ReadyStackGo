namespace ReadyStackGo.Application.UseCases.Deployments.GetDeploymentSnapshots;

/// <summary>
/// Response containing rollback information for a deployment.
/// With the Point of No Return semantics, there's at most one snapshot (PendingUpgradeSnapshot).
/// </summary>
public class GetDeploymentSnapshotsResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? DeploymentId { get; set; }
    public string? StackName { get; set; }
    public string? CurrentVersion { get; set; }

    /// <summary>
    /// Whether rollback is currently available.
    /// True only if: deployment is in Failed status AND has a PendingUpgradeSnapshot.
    /// </summary>
    public bool CanRollback { get; set; }

    /// <summary>
    /// The version that would be restored on rollback.
    /// Null if rollback is not available.
    /// </summary>
    public string? RollbackTargetVersion { get; set; }

    /// <summary>
    /// When the snapshot was created (start of upgrade).
    /// Null if no snapshot exists.
    /// </summary>
    public DateTime? SnapshotCreatedAt { get; set; }

    /// <summary>
    /// Description of the snapshot (e.g., "Before upgrade to 2.0.0").
    /// </summary>
    public string? SnapshotDescription { get; set; }

    // Keep for backwards compatibility - will contain 0 or 1 snapshot
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
