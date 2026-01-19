using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.UpgradeStack;

/// <summary>
/// Command to upgrade a deployment to a new version.
/// Uses the existing deployment flow with upgrade-specific semantics.
/// </summary>
public record UpgradeStackCommand(
    string EnvironmentId,
    string DeploymentId,
    string NewStackId,
    Dictionary<string, string>? Variables = null,
    string? SessionId = null) : IRequest<UpgradeStackResponse>;

/// <summary>
/// Response from upgrading a deployment.
/// </summary>
public record UpgradeStackResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? DeploymentId { get; init; }
    public string? PreviousVersion { get; init; }
    public string? NewVersion { get; init; }
    public string? SnapshotId { get; init; }
    public List<string>? Errors { get; init; }

    /// <summary>
    /// Whether rollback is available (only if upgrade failed before container start).
    /// </summary>
    public bool CanRollback { get; init; }

    /// <summary>
    /// Version to rollback to (if CanRollback is true).
    /// </summary>
    public string? RollbackVersion { get; init; }

    public static UpgradeStackResponse Failed(
        string message,
        List<string>? errors = null,
        bool canRollback = false,
        string? rollbackVersion = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors,
        CanRollback = canRollback,
        RollbackVersion = rollbackVersion
    };
}
