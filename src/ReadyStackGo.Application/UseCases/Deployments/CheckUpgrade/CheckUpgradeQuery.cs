using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.CheckUpgrade;

/// <summary>
/// Query to check if an upgrade is available for a deployment.
/// Returns version comparison and information about changes in the new version.
/// </summary>
public record CheckUpgradeQuery(
    string EnvironmentId,
    string DeploymentId) : IRequest<CheckUpgradeResponse>;

/// <summary>
/// Response containing upgrade availability and version information.
/// </summary>
public record CheckUpgradeResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    /// <summary>
    /// Whether an upgrade is available (newer version exists in catalog).
    /// </summary>
    public bool UpgradeAvailable { get; init; }

    /// <summary>
    /// Currently deployed version.
    /// </summary>
    public string? CurrentVersion { get; init; }

    /// <summary>
    /// Latest available version in the catalog.
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>
    /// Stack ID of the latest version (for use in upgrade request).
    /// </summary>
    public string? LatestStackId { get; init; }

    /// <summary>
    /// Variables that are new in the latest version.
    /// </summary>
    public List<string>? NewVariables { get; init; }

    /// <summary>
    /// Variables that were removed in the latest version.
    /// </summary>
    public List<string>? RemovedVariables { get; init; }

    /// <summary>
    /// Whether the deployment can be upgraded (must be Running status).
    /// </summary>
    public bool CanUpgrade { get; init; }

    /// <summary>
    /// Reason why upgrade is not available (if CanUpgrade is false).
    /// </summary>
    public string? CannotUpgradeReason { get; init; }

    public static CheckUpgradeResponse Failed(string message) => new()
    {
        Success = false,
        Message = message,
        UpgradeAvailable = false,
        CanUpgrade = false
    };

    public static CheckUpgradeResponse NoUpgradeAvailable(
        string currentVersion,
        string? reason = null) => new()
    {
        Success = true,
        UpgradeAvailable = false,
        CurrentVersion = currentVersion,
        CanUpgrade = true,
        Message = reason
    };
}
