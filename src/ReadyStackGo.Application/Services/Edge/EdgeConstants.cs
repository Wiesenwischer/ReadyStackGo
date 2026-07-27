namespace ReadyStackGo.Application.Services.Edge;

using ReadyStackGo.Domain.Deployment.Edge;

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
    /// Namespaced sysctl that controls kernel path-MTU probing (PLPMTUD, RFC 4821) in the
    /// edge container's network namespace. Used by the adaptive <c>pmtu</c> MSS mode.
    /// </summary>
    public const string TcpMtuProbingSysctl = "net.ipv4.tcp_mtu_probing";

    /// <summary>
    /// Namespaced sysctl for the base MSS the kernel probes up from once a blackhole is
    /// detected. Paired with <see cref="TcpMtuProbingSysctl"/> in the adaptive mode.
    /// </summary>
    public const string TcpBaseMssSysctl = "net.ipv4.tcp_base_mss";

    /// <summary>Value for <see cref="TcpMtuProbingSysctl"/> enabling blackhole-triggered probing.</summary>
    public const string AdaptiveMtuProbing = "1";

    /// <summary>Safe floor the kernel drops to when a blackhole is detected (adaptive mode).</summary>
    public const string AdaptiveBaseMss = "1024";

    /// <summary>
    /// IPv4 + TCP header overhead (20 + 20 bytes). A fixed MSS <c>n</c> is enforced by setting
    /// the edge network MTU to <c>n + MssHeaderOverhead</c>.
    /// </summary>
    public const int MssHeaderOverhead = 40;

    /// <summary>
    /// Label recording the client-facing MSS tuning the edge container was <em>created</em> with.
    /// Sysctls and the network MTU cannot be changed on a live container, so this label is the
    /// drift fingerprint: when it no longer matches <see cref="MssFingerprint"/> of the current
    /// config, the container has to be recreated for the setting to take effect.
    /// </summary>
    public const string MssLabel = "rsgo.edge.mss";

    /// <summary>
    /// Fingerprint of the configured client-facing MSS tuning: <c>pmtu</c>, the plain number for
    /// a fixed MSS, or <c>off</c>. Stored in <see cref="MssLabel"/> at container creation.
    /// </summary>
    public static string MssFingerprint(EdgeConfig config) => config.MssMode switch
    {
        EdgeMssMode.Pmtu => "pmtu",
        EdgeMssMode.Fixed => config.MssValue!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => MssFingerprintOff
    };

    /// <summary>
    /// Fingerprint of an edge container that applies no MSS tuning at all. Also the assumed
    /// fingerprint of a container created before the <c>mss</c> option existed (no label): such a
    /// container carries neither sysctls nor a lowered MTU, which is exactly <c>off</c>.
    /// </summary>
    public const string MssFingerprintOff = "off";

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
