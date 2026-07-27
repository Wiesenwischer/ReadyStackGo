using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Default <see cref="IEdgeSettingsReconciler"/>: delegates the drift check to the provisioner
/// and, when it recreated the container, drops the cached Caddy config so the fresh container
/// gets the live config on the next reconcile cycle instead of staying on its bootstrap
/// maintenance page.
/// </summary>
public class EdgeSettingsReconciler : IEdgeSettingsReconciler
{
    private readonly IEdgeProvisioner _provisioner;
    private readonly IEdgeConfigCache _configCache;
    private readonly ILogger<EdgeSettingsReconciler> _logger;

    public EdgeSettingsReconciler(
        IEdgeProvisioner provisioner,
        IEdgeConfigCache configCache,
        ILogger<EdgeSettingsReconciler> logger)
    {
        _provisioner = provisioner;
        _configCache = configCache;
        _logger = logger;
    }

    public async Task<bool> ApplyCreateTimeSettingsAsync(
        ProductDeployment productDeployment,
        CancellationToken cancellationToken = default)
    {
        var edgeConfig = productDeployment.EdgeConfig;
        if (edgeConfig is null)
            return false;

        try
        {
            var recreated = await _provisioner.ReconcileEdgeMssAsync(
                productDeployment.EnvironmentId.Value.ToString(),
                productDeployment.DeploymentName,
                productDeployment.ProductGroupId,
                edgeConfig,
                cancellationToken);

            if (!recreated)
                return false;

            // The replacement container only has the bootstrap config — force a fresh push.
            _configCache.Invalidate(productDeployment.Id.Value);

            _logger.LogInformation(
                "Edge container of product {ProductName} recreated to apply MSS mode {MssMode}",
                productDeployment.ProductName, edgeConfig.MssMode);

            return true;
        }
        catch (Exception ex)
        {
            // The front door keeps running with its previous tuning — worth a warning, not a
            // failed redeploy/upgrade.
            _logger.LogWarning(ex,
                "Could not re-apply create-time edge settings for product {ProductName} — the edge keeps its current tuning",
                productDeployment.ProductName);
            return false;
        }
    }
}
