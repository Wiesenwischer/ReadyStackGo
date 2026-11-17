using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Default <see cref="ISniRouterReconciler"/>. When enabled, provisions a shared Layer-4 router
/// per environment that routes TLS connections by SNI to the matching product edge (passthrough,
/// no termination — each edge keeps its own cert). When disabled it does nothing, leaving the
/// per-product edges as the public front doors.
/// </summary>
public class SniRouterReconciler : ISniRouterReconciler
{
    private readonly SniRouterOptions _options;
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IEdgeProvisioner _provisioner;
    private readonly ICaddyAdminClient _adminClient;
    private readonly IEdgeConfigCache _configCache;
    private readonly ILogger<SniRouterReconciler> _logger;

    public SniRouterReconciler(
        SniRouterOptions options,
        IProductDeploymentRepository productDeploymentRepository,
        IEdgeProvisioner provisioner,
        ICaddyAdminClient adminClient,
        IEdgeConfigCache configCache,
        ILogger<SniRouterReconciler> logger)
    {
        _options = options;
        _productDeploymentRepository = productDeploymentRepository;
        _provisioner = provisioner;
        _adminClient = adminClient;
        _configCache = configCache;
        _logger = logger;
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return; // feature inert — per-product edges remain the public entrypoints

        // Only TLS-terminating edges have an SNI to route on.
        var byEnvironment = _productDeploymentRepository.GetAllActive()
            .Where(pd => pd.EdgeConfig is { TlsMode: not EdgeTlsMode.None })
            .GroupBy(pd => pd.EnvironmentId.Value.ToString());

        foreach (var group in byEnvironment)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var environmentId = group.Key;
            try
            {
                var routes = group
                    .Select(pd => new SniRoute(
                        pd.EdgeConfig!.PublicHostname,
                        $"{EdgeConstants.EdgeNetworkAlias(pd.DeploymentName)}:{pd.EdgeConfig.PublicPort}"))
                    .ToList();

                var adminBaseUrl = await _provisioner.EnsureSniRouterAsync(environmentId, _options, cancellationToken);
                var config = Layer4ConfigBuilder.Build(routes, _options.ListenPort, EdgeConstants.CaddyAdminPort);

                var cacheKey = DeterministicKey(environmentId);
                if (_configCache.Get(cacheKey) == config)
                    continue;

                if (await _adminClient.LoadConfigAsync(adminBaseUrl, config, cancellationToken))
                {
                    _configCache.Set(cacheKey, config);
                    _logger.LogInformation("SNI router for environment {Env} updated with {Count} route(s)",
                        environmentId, routes.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SNI router reconcile failed for environment {Env}", environmentId);
            }
        }
    }

    /// <summary>Stable cache key (Guid) derived from the environment id.</summary>
    private static Guid DeterministicKey(string environmentId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("sni-router:" + environmentId));
        return new Guid(hash.AsSpan(0, 16));
    }
}
