using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.CheckProductUpgrade;

/// <summary>
/// Handler for checking if an upgrade is available for a product deployment.
/// Compares the deployed product version with all available versions in the catalog.
/// </summary>
public class CheckProductUpgradeHandler : IRequestHandler<CheckProductUpgradeQuery, CheckProductUpgradeResponse>
{
    private readonly IProductDeploymentRepository _repository;
    private readonly IProductSourceService _productSourceService;
    private readonly ILogger<CheckProductUpgradeHandler> _logger;

    public CheckProductUpgradeHandler(
        IProductDeploymentRepository repository,
        IProductSourceService productSourceService,
        ILogger<CheckProductUpgradeHandler> logger)
    {
        _repository = repository;
        _productSourceService = productSourceService;
        _logger = logger;
    }

    public async Task<CheckProductUpgradeResponse> Handle(
        CheckProductUpgradeQuery request, CancellationToken cancellationToken)
    {
        // 1. Parse and validate product deployment ID
        if (!Guid.TryParse(request.ProductDeploymentId, out var pdGuid))
        {
            return CheckProductUpgradeResponse.Failed("Invalid product deployment ID format.");
        }

        // 2. Load product deployment
        var productDeployment = _repository.Get(ProductDeploymentId.FromGuid(pdGuid));
        if (productDeployment == null)
        {
            return CheckProductUpgradeResponse.Failed("Product deployment not found.");
        }

        // 3. Check if deployment can be upgraded
        if (!productDeployment.CanUpgrade)
        {
            return new CheckProductUpgradeResponse
            {
                Success = true,
                UpgradeAvailable = false,
                CurrentVersion = productDeployment.ProductVersion,
                CanUpgrade = false,
                CannotUpgradeReason = $"Product deployment must be Running or PartiallyRunning to upgrade. Current status: {productDeployment.Status}"
            };
        }

        // 4. Look up product in catalog using ProductGroupId
        var currentVersion = productDeployment.ProductVersion;
        var groupId = productDeployment.ProductGroupId;

        _logger.LogInformation(
            "CheckProductUpgrade: Checking upgrades for product {ProductName} v{Version} (group: {GroupId})",
            productDeployment.ProductName, currentVersion, groupId);

        // 5. Get available upgrades
        var availableUpgrades = (await _productSourceService.GetAvailableUpgradesAsync(
            groupId, currentVersion, cancellationToken)).ToList();

        var upgradeAvailable = availableUpgrades.Count > 0;
        var latestUpgrade = availableUpgrades.FirstOrDefault();

        // 6. Analyze stack changes for latest version
        List<string>? newStacks = null;
        List<string>? removedStacks = null;

        if (upgradeAvailable && latestUpgrade != null)
        {
            var currentStackNames = productDeployment.Stacks
                .Select(s => s.StackName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var latestStackNames = latestUpgrade.Stacks
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newStackList = latestStackNames
                .Except(currentStackNames, StringComparer.OrdinalIgnoreCase).ToList();
            var removedStackList = currentStackNames
                .Except(latestStackNames, StringComparer.OrdinalIgnoreCase).ToList();

            newStacks = newStackList.Count > 0 ? newStackList : null;
            removedStacks = removedStackList.Count > 0 ? removedStackList : null;
        }

        // 7. Build response
        var availableVersions = availableUpgrades
            .Select(p => new AvailableProductVersion
            {
                Version = p.ProductVersion ?? "unknown",
                ProductId = p.Id,
                SourceId = p.SourceId,
                StackCount = p.Stacks.Count
            })
            .ToList();

        return new CheckProductUpgradeResponse
        {
            Success = true,
            UpgradeAvailable = upgradeAvailable,
            CurrentVersion = currentVersion,
            LatestVersion = latestUpgrade?.ProductVersion,
            LatestProductId = latestUpgrade?.Id,
            AvailableVersions = availableVersions.Count > 0 ? availableVersions : null,
            NewStacks = newStacks,
            RemovedStacks = removedStacks,
            CanUpgrade = true,
            Message = upgradeAvailable
                ? $"Upgrade available: {currentVersion} -> {latestUpgrade!.ProductVersion} ({availableUpgrades.Count} version(s) available)"
                : "Already running the latest version"
        };
    }
}
