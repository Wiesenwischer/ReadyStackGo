using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Deployments.CheckUpgrade;

/// <summary>
/// Handler for checking if an upgrade is available for a deployment.
/// Compares the deployed version with the latest version in the catalog.
/// </summary>
public class CheckUpgradeHandler : IRequestHandler<CheckUpgradeQuery, CheckUpgradeResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductSourceService _productSourceService;
    private readonly ILogger<CheckUpgradeHandler> _logger;

    public CheckUpgradeHandler(
        IDeploymentRepository deploymentRepository,
        IProductSourceService productSourceService,
        ILogger<CheckUpgradeHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _productSourceService = productSourceService;
        _logger = logger;
    }

    public async Task<CheckUpgradeResponse> Handle(CheckUpgradeQuery request, CancellationToken cancellationToken)
    {
        // 1. Parse and validate deployment ID
        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            return CheckUpgradeResponse.Failed("Invalid deployment ID format.");
        }

        // 2. Get deployment
        var deployment = _deploymentRepository.GetById(new DeploymentId(deploymentGuid));
        if (deployment == null)
        {
            return CheckUpgradeResponse.Failed("Deployment not found.");
        }

        // 3. Check if deployment can be upgraded
        if (!deployment.CanUpgrade())
        {
            return new CheckUpgradeResponse
            {
                Success = true,
                UpgradeAvailable = false,
                CurrentVersion = deployment.StackVersion,
                CanUpgrade = false,
                CannotUpgradeReason = $"Deployment must be in Running status to upgrade. Current status: {deployment.Status}"
            };
        }

        // 4. Check if deployment was created from catalog
        var stackId = deployment.StackId;
        if (string.IsNullOrEmpty(stackId) || !stackId.Contains(':'))
        {
            return new CheckUpgradeResponse
            {
                Success = true,
                UpgradeAvailable = false,
                CurrentVersion = deployment.StackVersion,
                CanUpgrade = true,
                Message = "Deployment was not created from catalog (manual YAML deployment). Upgrade detection not available."
            };
        }

        // 5. Extract product ID from stack ID (format: sourceId:productName:stackName)
        var productId = ExtractProductId(stackId);
        if (string.IsNullOrEmpty(productId))
        {
            return new CheckUpgradeResponse
            {
                Success = true,
                UpgradeAvailable = false,
                CurrentVersion = deployment.StackVersion,
                CanUpgrade = true,
                Message = "Could not determine product ID from stack ID."
            };
        }

        // 6. Get product from catalog
        var product = await _productSourceService.GetProductAsync(productId, cancellationToken);
        if (product == null)
        {
            return new CheckUpgradeResponse
            {
                Success = true,
                UpgradeAvailable = false,
                CurrentVersion = deployment.StackVersion,
                CanUpgrade = true,
                Message = "Product no longer available in catalog."
            };
        }

        // 7. Compare versions
        var currentVersion = deployment.StackVersion;
        var latestVersion = product.ProductVersion;

        var comparison = CompareVersions(currentVersion, latestVersion);
        var upgradeAvailable = comparison.HasValue && comparison.Value < 0;

        // 8. If upgrade available, analyze changes
        List<string>? newVars = null;
        List<string>? removedVars = null;

        if (upgradeAvailable)
        {
            try
            {
                var currentStack = await _productSourceService.GetStackAsync(stackId, cancellationToken);
                var latestStack = product.DefaultStack;

                if (currentStack != null && latestStack != null)
                {
                    (newVars, removedVars) = CompareVariables(
                        currentStack.Variables.Select(v => v.Name).ToList(),
                        latestStack.Variables.Select(v => v.Name).ToList());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not compare variables between versions");
            }
        }

        // 9. Build response
        var latestStackId = product.DefaultStack?.Id ?? $"{productId}:{product.DefaultStack?.Name ?? "default"}";

        return new CheckUpgradeResponse
        {
            Success = true,
            UpgradeAvailable = upgradeAvailable,
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            LatestStackId = latestStackId,
            NewVariables = newVars,
            RemovedVariables = removedVars,
            CanUpgrade = true,
            Message = upgradeAvailable
                ? $"Upgrade available: {currentVersion} -> {latestVersion}"
                : comparison == null
                    ? "Version comparison not possible (non-SemVer format)"
                    : "Already running the latest version"
        };
    }

    /// <summary>
    /// Extracts product ID from stack ID.
    /// Stack ID format: sourceId:productName:stackName
    /// Product ID format: sourceId:productName
    /// </summary>
    private static string? ExtractProductId(string stackId)
    {
        var parts = stackId.Split(':');
        if (parts.Length >= 2)
        {
            return $"{parts[0]}:{parts[1]}";
        }
        return null;
    }

    /// <summary>
    /// Compares two semantic versions.
    /// Returns: -1 (v1 &lt; v2), 0 (equal), 1 (v1 &gt; v2)
    /// Returns null if either version is not valid SemVer.
    /// </summary>
    private static int? CompareVersions(string? v1, string? v2)
    {
        if (string.IsNullOrEmpty(v1) && string.IsNullOrEmpty(v2)) return 0;
        if (string.IsNullOrEmpty(v1)) return -1;
        if (string.IsNullOrEmpty(v2)) return 1;

        // Normalize: remove 'v' prefix
        var normalized1 = v1.TrimStart('v', 'V');
        var normalized2 = v2.TrimStart('v', 'V');

        // Try semantic version comparison
        if (Version.TryParse(normalized1, out var ver1) &&
            Version.TryParse(normalized2, out var ver2))
        {
            return ver1.CompareTo(ver2);
        }

        // Return null if not valid SemVer
        return null;
    }

    /// <summary>
    /// Compares variables between two versions.
    /// Returns (newVariables, removedVariables).
    /// </summary>
    private static (List<string>?, List<string>?) CompareVariables(
        IReadOnlyList<string> currentVars,
        IReadOnlyList<string> latestVars)
    {
        var currentSet = new HashSet<string>(currentVars, StringComparer.OrdinalIgnoreCase);
        var latestSet = new HashSet<string>(latestVars, StringComparer.OrdinalIgnoreCase);

        var newVars = latestSet.Except(currentSet, StringComparer.OrdinalIgnoreCase).ToList();
        var removedVars = currentSet.Except(latestSet, StringComparer.OrdinalIgnoreCase).ToList();

        return (
            newVars.Count > 0 ? newVars : null,
            removedVars.Count > 0 ? removedVars : null);
    }
}
