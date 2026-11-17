using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Default <see cref="IEdgeReconciler"/>. Iterates active product deployments that have an
/// edge configured, ensures the edge container exists, and pushes a fresh Caddy config only
/// when the desired config changed. Caches the last successfully pushed config per
/// deployment to keep reloads connection-preserving and avoid churn.
/// </summary>
public class EdgeReconciler : IEdgeReconciler
{
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IEdgeProvisioner _provisioner;
    private readonly ICaddyAdminClient _adminClient;
    private readonly IEdgeCertificateProvider _certificateProvider;
    private readonly IEdgeConfigCache _configCache;
    private readonly ILogger<EdgeReconciler> _logger;

    public EdgeReconciler(
        IProductDeploymentRepository productDeploymentRepository,
        IEdgeProvisioner provisioner,
        ICaddyAdminClient adminClient,
        IEdgeCertificateProvider certificateProvider,
        IEdgeConfigCache configCache,
        ILogger<EdgeReconciler> logger)
    {
        _productDeploymentRepository = productDeploymentRepository;
        _provisioner = provisioner;
        _adminClient = adminClient;
        _certificateProvider = certificateProvider;
        _configCache = configCache;
        _logger = logger;
    }

    public async Task ReconcileAllAsync(CancellationToken cancellationToken = default)
    {
        var active = _productDeploymentRepository.GetAllActive().ToList();

        foreach (var pd in active)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (pd.EdgeConfig is null)
                continue;

            try
            {
                await ReconcileOneAsync(pd, pd.EdgeConfig, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Edge reconcile failed for product deployment {ProductDeploymentId} ({ProductName})",
                    pd.Id, pd.ProductName);
            }
        }
    }

    private async Task ReconcileOneAsync(ProductDeployment pd, EdgeConfig edgeConfig, CancellationToken ct)
    {
        var environmentId = pd.EnvironmentId.Value.ToString();

        // 1. Make sure the edge container exists and the admin API is reachable.
        var adminBaseUrl = await _provisioner.EnsureEdgeAsync(
            environmentId, pd.DeploymentName, pd.ProductGroupId, edgeConfig, ct);

        // 2. Compute the desired state purely from authoritative deploy state + flag.
        var desired = EdgeStateResolver.Resolve(pd.Status, pd.OperationMode, pd.MaintenanceTrigger, pd.ProductVersion);

        // 3. Resolve the TLS material (null when TLS is disabled → plain HTTP).
        var tls = await _certificateProvider.GetCertificateAsync(edgeConfig, ct);

        // 4. Build the Caddy config and push it only when it actually changed. Since the cert
        //    PEM is embedded in the config, a renewed cert changes the config and is reloaded
        //    via the admin API without restarting the edge.
        var configJson = CaddyConfigBuilder.Build(edgeConfig, desired, tls);

        if (_configCache.Get(pd.Id.Value) == configJson)
            return;

        var ok = await _adminClient.LoadConfigAsync(adminBaseUrl, configJson, ct);
        if (ok)
        {
            _configCache.Set(pd.Id.Value, configJson);
            _logger.LogInformation(
                "Edge for product {ProductName} switched to {Mode} ({State})",
                pd.ProductName, desired.Mode, desired.StatusStateToken);
        }
        else
        {
            _logger.LogWarning(
                "Edge config push to {AdminBaseUrl} failed for product {ProductName} — will retry next cycle",
                adminBaseUrl, pd.ProductName);
        }
    }
}
