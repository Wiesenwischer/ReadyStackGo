using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.CheckProductUpgrade;

/// <summary>
/// Query to check if an upgrade is available for a product deployment.
/// Returns version comparison, available versions, and per-stack change analysis.
/// </summary>
public record CheckProductUpgradeQuery(
    string EnvironmentId,
    string ProductDeploymentId) : IRequest<CheckProductUpgradeResponse>;

/// <summary>
/// Response containing product upgrade availability and version information.
/// </summary>
public record CheckProductUpgradeResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    /// <summary>
    /// Whether an upgrade is available (newer version exists in catalog).
    /// </summary>
    public bool UpgradeAvailable { get; init; }

    /// <summary>
    /// Currently deployed product version.
    /// </summary>
    public string? CurrentVersion { get; init; }

    /// <summary>
    /// Latest available version in the catalog.
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>
    /// Product ID of the latest version (for use in upgrade request).
    /// </summary>
    public string? LatestProductId { get; init; }

    /// <summary>
    /// All available upgrade versions, sorted by version (newest first).
    /// </summary>
    public List<AvailableProductVersion>? AvailableVersions { get; init; }

    /// <summary>
    /// Stacks that are new in the latest version (not in current deployment).
    /// </summary>
    public List<string>? NewStacks { get; init; }

    /// <summary>
    /// Stacks from the current deployment that are absent in the latest version.
    /// </summary>
    public List<string>? RemovedStacks { get; init; }

    /// <summary>
    /// Whether the deployment can be upgraded (must be Running or PartiallyRunning).
    /// </summary>
    public bool CanUpgrade { get; init; }

    /// <summary>
    /// Reason why upgrade is not available (if CanUpgrade is false).
    /// </summary>
    public string? CannotUpgradeReason { get; init; }

    public static CheckProductUpgradeResponse Failed(string message) => new()
    {
        Success = false,
        Message = message,
        UpgradeAvailable = false,
        CanUpgrade = false
    };
}

/// <summary>
/// Represents an available product version for upgrade.
/// </summary>
public record AvailableProductVersion
{
    public required string Version { get; init; }
    public required string ProductId { get; init; }
    public required string SourceId { get; init; }
    public int StackCount { get; init; }
}
