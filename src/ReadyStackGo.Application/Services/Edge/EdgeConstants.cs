namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Shared constants for the managed maintenance edge-proxy feature.
/// </summary>
public static class EdgeConstants
{
    /// <summary>
    /// Label marking a container as part of the "edge scope" — outside any product stack
    /// identity. <see cref="ScopeEdge"/> containers survive product redeploys
    /// (<c>RemoveStackAsync</c> excludes them).
    /// </summary>
    public const string ScopeLabel = "rsgo.scope";

    /// <summary>Value of <see cref="ScopeLabel"/> for the edge container.</summary>
    public const string ScopeEdge = "edge";

    /// <summary>
    /// Generic survival opt-out label. Any container carrying <c>rsgo.redeploy=ignore</c> is
    /// excluded from stack teardown, regardless of scope.
    /// </summary>
    public const string RedeployLabel = "rsgo.redeploy";

    /// <summary>Value of <see cref="RedeployLabel"/> that opts a container out of teardown.</summary>
    public const string RedeployIgnore = "ignore";

    /// <summary>Label marking a product-contributed maintenance-page container (Phase 3).</summary>
    public const string RoleLabel = "rsgo.role";

    /// <summary>Value of <see cref="RoleLabel"/> for a maintenance-page container.</summary>
    public const string RoleMaintenancePage = "maintenance-page";

    /// <summary>Label carrying the product group id the edge belongs to.</summary>
    public const string ProductLabel = "rsgo.product";

    /// <summary>Context label value used for the edge container.</summary>
    public const string ContextLabel = "rsgo.context";

    /// <summary>Context value for the edge container.</summary>
    public const string ContextEdge = "edge";

    /// <summary>
    /// Default Caddy edge image. Production deployments should override this with a
    /// digest-pinned reference via the manifest <c>edge.image</c> field; a tag pin is used
    /// as the out-of-the-box default so the feature works without extra configuration.
    /// </summary>
    public const string DefaultCaddyImage = "caddy:2.8.4";

    /// <summary>Port the Caddy admin API listens on inside the edge container.</summary>
    public const int CaddyAdminPort = 2019;

    /// <summary>Default public port when the manifest does not specify one.</summary>
    public const int DefaultPublicPort = 443;

    /// <summary>Default upstream port when the manifest does not specify one.</summary>
    public const int DefaultUpstreamPort = 8080;

    /// <summary>
    /// Derives the deterministic edge container name from the product deployment name.
    /// Idempotency anchor: the provisioner reuses an existing container with this name.
    /// </summary>
    public static string EdgeContainerName(string deploymentName)
        => $"{Sanitize(deploymentName)}-edge";

    /// <summary>
    /// Network alias the edge is reachable under on the management network (for the admin API).
    /// </summary>
    public static string EdgeNetworkAlias(string deploymentName)
        => EdgeContainerName(deploymentName);

    /// <summary>Caddy admin API base URL for an edge reachable under the given alias on rsgo-net.</summary>
    public static string AdminBaseUrl(string deploymentName)
        => $"http://{EdgeNetworkAlias(deploymentName)}:{CaddyAdminPort}";

    /// <summary>Container name / network alias of the optional shared SNI passthrough router.</summary>
    public const string SniRouterContainerName = "rsgo-sni-router";

    /// <summary>Caddy admin API base URL for the shared SNI router (reached over rsgo-net).</summary>
    public static string SniRouterAdminBaseUrl()
        => $"http://{SniRouterContainerName}:{CaddyAdminPort}";

    private static string Sanitize(string raw)
    {
        var lowered = raw.ToLowerInvariant();
        var chars = lowered.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }
}
