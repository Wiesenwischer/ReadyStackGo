using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.Edge;

namespace ReadyStackGo.Infrastructure.Services.Edge;

/// <summary>
/// Manages the managed edge (Caddy) container lifecycle on Docker. Modelled after the
/// self-update service's "RSGO creates and supervises a container" pattern.
///
/// The edge is created with the survival labels (<c>rsgo.scope=edge</c>,
/// <c>rsgo.redeploy=ignore</c>, <c>rsgo.maintenance=ignore</c>) so it outlives product
/// redeploys, removals and stop/start cycles. It is attached to the shared edge network
/// (to reach the upstream) and the management network (so RSGO can reach its admin API by
/// DNS alias). The container boots Caddy with an admin API bound to all interfaces and a
/// minimal maintenance config; the reconciler then pushes the live config via the admin API.
/// </summary>
public class EdgeProvisioner : IEdgeProvisioner
{
    private const string ManagementNetwork = "rsgo-net";

    private readonly IDockerService _dockerService;
    private readonly ILogger<EdgeProvisioner> _logger;

    public EdgeProvisioner(IDockerService dockerService, ILogger<EdgeProvisioner> logger)
    {
        _dockerService = dockerService;
        _logger = logger;
    }

    public async Task<string> EnsureEdgeAsync(
        string environmentId,
        string deploymentName,
        string productGroupId,
        EdgeConfig config,
        CancellationToken cancellationToken = default)
    {
        var containerName = EdgeConstants.EdgeContainerName(deploymentName);
        var adminBaseUrl = EdgeConstants.AdminBaseUrl(deploymentName);

        // Ensure both networks exist (idempotent; external networks are created once, shared).
        await _dockerService.EnsureNetworkAsync(environmentId, config.Network, cancellationToken);
        await _dockerService.EnsureNetworkAsync(environmentId, ManagementNetwork, cancellationToken);

        // Idempotency: reuse an existing edge container; just make sure it is running.
        var existing = await _dockerService.GetContainerByNameAsync(environmentId, containerName, cancellationToken);
        if (existing != null)
        {
            if (!existing.State.Equals("running", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Edge container {Name} exists but is {State} — starting it", containerName, existing.State);
                try { await _dockerService.StartContainerAsync(environmentId, existing.Id, cancellationToken); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to (re)start edge container {Name}", containerName); }
            }
            return adminBaseUrl;
        }

        // Pull the edge image (best-effort; container create surfaces a hard failure clearly).
        await EnsureImageAsync(environmentId, config.Image, cancellationToken);

        // Bootstrap config: a minimal "temporarily unavailable" maintenance page with the
        // admin API exposed, so the edge serves something and is reconcilable immediately.
        var bootstrapState = new EdgeDesiredState(
            EdgeMode.Maintenance, EdgeStatusState.Deploying,
            PlannedMaintenance: false, Reason: null, Until: null, ProductVersion: null);
        var bootstrapConfig = CaddyConfigBuilder.Build(config, bootstrapState);

        var request = new CreateContainerRequest
        {
            Name = containerName,
            Image = config.Image,
            // Primary network = shared edge<->upstream net (so the upstream DNS name resolves);
            // management network added so RSGO can reach the admin API by alias.
            Networks = new List<string> { config.Network, ManagementNetwork },
            NetworkAliases = new List<string> { EdgeConstants.EdgeNetworkAlias(deploymentName) },
            // Publish the public port to the host — the edge is the product's front door.
            Ports = new List<string> { $"{config.PublicPort}:{config.PublicPort}" },
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["CADDY_BOOTSTRAP_CONFIG"] = bootstrapConfig
            },
            // Override the image entrypoint (the official caddy image's ENTRYPOINT is "caddy",
            // so a plain Cmd would be appended to it). Write the bootstrap config from the env
            // var, then run Caddy against it.
            Entrypoint = new List<string>
            {
                "sh", "-c",
                "printf '%s' \"$CADDY_BOOTSTRAP_CONFIG\" > /etc/caddy/bootstrap.json && exec caddy run --config /etc/caddy/bootstrap.json"
            },
            Labels = new Dictionary<string, string>
            {
                [EdgeConstants.ScopeLabel] = EdgeConstants.ScopeEdge,
                [EdgeConstants.RedeployLabel] = EdgeConstants.RedeployIgnore,
                ["rsgo.maintenance"] = "ignore",
                [EdgeConstants.ProductLabel] = productGroupId,
                [EdgeConstants.ContextLabel] = EdgeConstants.ContextEdge
            },
            RestartPolicy = "unless-stopped"
        };

        var id = await _dockerService.CreateAndStartContainerAsync(environmentId, request, cancellationToken);
        _logger.LogInformation(
            "Provisioned managed edge container {Name} ({Id}) for product {Product} on {PublicPort} -> {Upstream}:{UpstreamPort}",
            containerName, id, productGroupId, config.PublicPort, config.UpstreamService, config.UpstreamPort);

        return adminBaseUrl;
    }

    private async Task EnsureImageAsync(string environmentId, string image, CancellationToken ct)
    {
        var (name, tag) = ParseImageRef(image);
        try
        {
            var exists = await _dockerService.ImageExistsAsync(environmentId, name, string.IsNullOrEmpty(tag) ? "latest" : tag, ct);
            if (exists)
                return;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Image existence check failed for {Image}; attempting pull", image);
        }

        await _dockerService.PullImageAsync(environmentId, name, string.IsNullOrEmpty(tag) ? "latest" : tag, ct);
    }

    /// <summary>
    /// Splits an image reference into (name, tag). Digest-pinned references
    /// (<c>name@sha256:...</c>) are passed through whole with an empty tag.
    /// </summary>
    internal static (string Name, string Tag) ParseImageRef(string image)
    {
        if (image.Contains('@'))
            return (image, string.Empty);

        var lastColon = image.LastIndexOf(':');
        var lastSlash = image.LastIndexOf('/');
        if (lastColon > lastSlash && lastColon >= 0 && lastColon < image.Length - 1)
            return (image[..lastColon], image[(lastColon + 1)..]);

        return (image, "latest");
    }
}
