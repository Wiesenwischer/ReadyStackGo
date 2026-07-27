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

    /// <summary>
    /// Container entrypoint: materialise the bootstrap config, print one stable line stating
    /// whether the configured client-facing MSS tuning is actually in effect, then run Caddy.
    ///
    /// The banner is the field-support answer to "did this customer's edge really get the new
    /// setting?" — <c>docker logs &lt;deployment&gt;-edge | head -1</c> shows it, because sysctls
    /// and the interface MTU are read from the live container, not from RSGO's intent. A mismatch
    /// (verdict=INACTIVE) means the container predates the setting or the network MTU could not
    /// be applied.
    /// </summary>
    private const string StartupScript = """
        printf '%s' "$CADDY_BOOTSTRAP_CONFIG" > /etc/caddy/bootstrap.json
        mode="${RSGO_EDGE_MSS_MODE:-off}"
        probing="$(cat /proc/sys/net/ipv4/tcp_mtu_probing 2>/dev/null || echo '?')"
        base_mss="$(cat /proc/sys/net/ipv4/tcp_base_mss 2>/dev/null || echo '?')"
        ifmtus=""
        lowest=""
        for dev in /sys/class/net/*; do
          name="${dev##*/}"
          if [ "$name" != "lo" ]; then
            m="$(cat "$dev/mtu" 2>/dev/null || echo '?')"
            ifmtus="$ifmtus $name=$m"
            case "$m" in
              ''|*[!0-9]*) ;;
              *) if [ -z "$lowest" ] || [ "$m" -lt "$lowest" ]; then lowest="$m"; fi ;;
            esac
          fi
        done
        case "$mode" in
          pmtu)
            expected="tcp_mtu_probing=1"
            if [ "$probing" = "1" ]; then verdict=ACTIVE; else verdict=INACTIVE; fi ;;
          off)
            expected="none"
            verdict=DISABLED ;;
          ''|*[!0-9]*)
            expected="?"
            verdict=UNKNOWN ;;
          *)
            expected="edge network mtu<=$((mode + 40))"
            if [ -n "$lowest" ] && [ "$lowest" -le "$((mode + 40))" ]; then verdict=ACTIVE; else verdict=INACTIVE; fi ;;
        esac
        echo "rsgo-edge: client-facing MSS tuning mode=$mode verdict=$verdict (expected: $expected | tcp_mtu_probing=$probing tcp_base_mss=$base_mss iface_mtu:$ifmtus)"
        exec caddy run --config /etc/caddy/bootstrap.json
        """;

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

        // Client-facing VPN robustness (adaptive sysctls or a fixed-MSS network MTU).
        var (sysctls, networkMtu) = ResolveMssTuning(config);

        // Ensure both networks exist (idempotent; external networks are created once, shared).
        // The edge network carries the requested MTU in fixed-MSS mode; the management network
        // stays at the default MTU (internal, never traverses a VPN).
        await _dockerService.EnsureNetworkAsync(environmentId, config.Network, networkMtu, cancellationToken);
        await _dockerService.EnsureNetworkAsync(environmentId, ManagementNetwork, cancellationToken);

        // Idempotency: reuse an existing edge container; just make sure it is running.
        // Create-time settings (MSS tuning) of an existing container are NOT touched here —
        // that would restart the front door from a background loop. See ReconcileEdgeMssAsync.
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

        await CreateEdgeContainerAsync(
            environmentId, containerName, deploymentName, productGroupId, config, sysctls, cancellationToken);

        return adminBaseUrl;
    }

    public async Task<bool> ReconcileEdgeMssAsync(
        string environmentId,
        string deploymentName,
        string productGroupId,
        EdgeConfig config,
        CancellationToken cancellationToken = default)
    {
        var containerName = EdgeConstants.EdgeContainerName(deploymentName);
        var desiredFingerprint = EdgeConstants.MssFingerprint(config);

        var existing = await _dockerService.GetContainerByNameAsync(environmentId, containerName, cancellationToken);
        if (existing == null)
        {
            // Nothing to reconcile — the next EnsureEdgeAsync creates it with the current tuning.
            return false;
        }

        // A container created before the mss option existed carries no label and no tuning,
        // which is exactly what "off" means.
        var currentFingerprint = existing.Labels.TryGetValue(EdgeConstants.MssLabel, out var label)
            && !string.IsNullOrWhiteSpace(label)
                ? label
                : EdgeConstants.MssFingerprintOff;

        if (string.Equals(currentFingerprint, desiredFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Edge container {Name} already runs the configured MSS tuning ({Fingerprint}) — no recreation needed",
                containerName, desiredFingerprint);
            return false;
        }

        var (sysctls, networkMtu) = ResolveMssTuning(config);

        // Recreate: sysctls and the network MTU are fixed at creation time. The fresh container
        // boots with the maintenance bootstrap config; the reconciler pushes the live config on
        // its next cycle (the caller invalidates the config cache so that push actually happens).
        _logger.LogInformation(
            "Edge container {Name} was created with MSS tuning '{Current}' but '{Desired}' is configured — recreating it",
            containerName, currentFingerprint, desiredFingerprint);

        // Creates the edge network with the requested MTU when it is missing; logs a warning when
        // it exists with a different MTU (Docker cannot change that without recreating the network).
        await _dockerService.EnsureNetworkAsync(environmentId, config.Network, networkMtu, cancellationToken);
        await _dockerService.EnsureNetworkAsync(environmentId, ManagementNetwork, cancellationToken);

        await _dockerService.RemoveContainerAsync(environmentId, existing.Id, force: true, cancellationToken);

        await CreateEdgeContainerAsync(
            environmentId, containerName, deploymentName, productGroupId, config, sysctls, cancellationToken);

        return true;
    }

    /// <summary>
    /// Creates and starts a fresh edge container with the survival labels, the MSS fingerprint
    /// label and a bootstrap maintenance config. Shared by initial provisioning and recreation.
    /// </summary>
    private async Task CreateEdgeContainerAsync(
        string environmentId,
        string containerName,
        string deploymentName,
        string productGroupId,
        EdgeConfig config,
        Dictionary<string, string> sysctls,
        CancellationToken cancellationToken)
    {
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
                ["CADDY_BOOTSTRAP_CONFIG"] = bootstrapConfig,
                // Consumed by the startup banner below so the log states the intended mode.
                ["RSGO_EDGE_MSS_MODE"] = EdgeConstants.MssFingerprint(config)
            },
            // Override the image entrypoint (the official caddy image's ENTRYPOINT is "caddy",
            // so a plain Cmd would be appended to it). Write the bootstrap config from the env
            // var, log the effective MSS tuning, then run Caddy against it.
            Entrypoint = new List<string> { "sh", "-c", StartupScript },
            Sysctls = sysctls,
            Labels = new Dictionary<string, string>
            {
                [EdgeConstants.ScopeLabel] = EdgeConstants.ScopeEdge,
                [EdgeConstants.RedeployLabel] = EdgeConstants.RedeployIgnore,
                ["rsgo.maintenance"] = "ignore",
                [EdgeConstants.ProductLabel] = productGroupId,
                [EdgeConstants.ContextLabel] = EdgeConstants.ContextEdge,
                // Drift fingerprint of the create-time MSS tuning (see ReconcileEdgeMssAsync).
                [EdgeConstants.MssLabel] = EdgeConstants.MssFingerprint(config)
            },
            RestartPolicy = "unless-stopped"
        };

        var id = await _dockerService.CreateAndStartContainerAsync(environmentId, request, cancellationToken);
        _logger.LogInformation(
            "Provisioned managed edge container {Name} ({Id}) for product {Product} on {PublicPort} -> {Upstream}:{UpstreamPort} (MSS mode: {MssMode})",
            containerName, id, productGroupId, config.PublicPort, config.UpstreamService, config.UpstreamPort, config.MssMode);
    }

    public async Task<string> EnsureSniRouterAsync(
        string environmentId,
        SniRouterOptions options,
        CancellationToken cancellationToken = default)
    {
        var containerName = EdgeConstants.SniRouterContainerName;
        var adminBaseUrl = EdgeConstants.SniRouterAdminBaseUrl();

        await _dockerService.EnsureNetworkAsync(environmentId, ManagementNetwork, cancellationToken);

        var existing = await _dockerService.GetContainerByNameAsync(environmentId, containerName, cancellationToken);
        if (existing != null)
        {
            if (!existing.State.Equals("running", StringComparison.OrdinalIgnoreCase))
            {
                try { await _dockerService.StartContainerAsync(environmentId, existing.Id, cancellationToken); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to (re)start SNI router {Name}", containerName); }
            }
            return adminBaseUrl;
        }

        await EnsureImageAsync(environmentId, options.Image, cancellationToken);

        // Boot with an empty SNI server so the admin API is reachable; the reconciler fills routes.
        var bootstrap = Layer4ConfigBuilder.Build(Array.Empty<SniRoute>(), options.ListenPort, EdgeConstants.CaddyAdminPort);

        var request = new CreateContainerRequest
        {
            Name = containerName,
            Image = options.Image,
            Networks = new List<string> { ManagementNetwork },
            NetworkAliases = new List<string> { containerName },
            Ports = new List<string> { $"{options.ListenPort}:{options.ListenPort}" },
            EnvironmentVariables = new Dictionary<string, string> { ["CADDY_BOOTSTRAP_CONFIG"] = bootstrap },
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
                [EdgeConstants.ContextLabel] = "sni-router"
            },
            RestartPolicy = "unless-stopped"
        };

        var id = await _dockerService.CreateAndStartContainerAsync(environmentId, request, cancellationToken);
        _logger.LogInformation("Provisioned shared SNI passthrough router {Name} ({Id}) on port {Port}",
            containerName, id, options.ListenPort);

        return adminBaseUrl;
    }

    /// <summary>
    /// Translates the configured client-facing MSS mode into the concrete Docker knobs:
    /// namespaced sysctls for the adaptive <see cref="EdgeMssMode.Pmtu"/> mode (no elevated
    /// capability), a lowered edge-network MTU for <see cref="EdgeMssMode.Fixed"/>, and nothing
    /// for <see cref="EdgeMssMode.Off"/> (byte-for-byte the pre-feature behaviour).
    /// </summary>
    internal static (Dictionary<string, string> Sysctls, int? NetworkMtu) ResolveMssTuning(EdgeConfig config)
    {
        switch (config.MssMode)
        {
            case EdgeMssMode.Pmtu:
                return (new Dictionary<string, string>
                {
                    [EdgeConstants.TcpMtuProbingSysctl] = EdgeConstants.AdaptiveMtuProbing,
                    [EdgeConstants.TcpBaseMssSysctl] = EdgeConstants.AdaptiveBaseMss
                }, null);

            case EdgeMssMode.Fixed:
                // A fixed MSS n is enforced as an egress cap by the network MTU (n + headers).
                var mtu = config.MssValue!.Value + EdgeConstants.MssHeaderOverhead;
                return (new Dictionary<string, string>(), mtu);

            case EdgeMssMode.Off:
            default:
                return (new Dictionary<string, string>(), null);
        }
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
