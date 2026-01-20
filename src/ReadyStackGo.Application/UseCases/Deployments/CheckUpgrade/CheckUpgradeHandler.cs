using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.UseCases.Deployments.CheckUpgrade;

/// <summary>
/// Handler for checking if an upgrade is available for a deployment.
/// Compares the deployed version with all available versions in the catalog.
/// Supports multi-version catalog with cross-source upgrades via GroupId.
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

        // 4. Parse the stack ID into structured components
        if (!StackId.TryParse(deployment.StackId, out var parsedStackId) || parsedStackId == null)
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

        // 5. Look up product using sourceId:productId format
        var productLookupKey = $"{parsedStackId.SourceId}:{parsedStackId.ProductId.Value}";
        var product = await _productSourceService.GetProductAsync(productLookupKey, cancellationToken);
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

        // 6. Get all available upgrades using the product's GroupId
        var currentVersion = deployment.StackVersion ?? "0.0.0";
        var groupId = product.GroupId;

        var availableUpgrades = (await _productSourceService.GetAvailableUpgradesAsync(
            groupId, currentVersion, cancellationToken)).ToList();

        var upgradeAvailable = availableUpgrades.Count > 0;
        var latestUpgrade = availableUpgrades.FirstOrDefault();

        // 7. If upgrade available, analyze changes
        List<string>? newVars = null;
        List<string>? removedVars = null;

        if (upgradeAvailable && latestUpgrade != null)
        {
            try
            {
                var currentStack = await _productSourceService.GetStackAsync(parsedStackId.Value, cancellationToken);
                var latestStack = latestUpgrade.DefaultStack;

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

        // 8. Build response with all available versions
        var latestVersion = latestUpgrade?.ProductVersion;
        var latestStackId = latestUpgrade?.DefaultStack?.Id.Value;

        // Build list of all available upgrade versions
        var availableVersions = availableUpgrades
            .Select(p => new AvailableVersion
            {
                Version = p.ProductVersion ?? "unknown",
                StackId = p.DefaultStack?.Id.Value ?? p.Id,
                SourceId = p.SourceId
            })
            .ToList();

        return new CheckUpgradeResponse
        {
            Success = true,
            UpgradeAvailable = upgradeAvailable,
            CurrentVersion = deployment.StackVersion,
            LatestVersion = latestVersion,
            LatestStackId = latestStackId,
            AvailableVersions = availableVersions.Count > 0 ? availableVersions : null,
            NewVariables = newVars,
            RemovedVariables = removedVars,
            CanUpgrade = true,
            Message = upgradeAvailable
                ? $"Upgrade available: {deployment.StackVersion} -> {latestVersion} ({availableUpgrades.Count} version(s) available)"
                : "Already running the latest version"
        };
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
