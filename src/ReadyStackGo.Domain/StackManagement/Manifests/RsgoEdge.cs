namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Optional product-level edge configuration (manifest model).
///
/// Opts a product into a managed reverse-proxy ("edge") container that RSGO runs
/// alongside the product. The edge survives product redeploys and serves either a
/// transparent proxy to the upstream or a controlled maintenance page plus a
/// machine-readable status, driven by RSGO's authoritative deploy state and the
/// maintenance flag.
///
/// The whole feature is opt-in and dormant by default: a product manifest without an
/// <c>edge:</c> block (or with <c>edge.enabled: false</c>) behaves exactly as before —
/// no edge container is created and no deploy/teardown path is changed.
/// </summary>
public class RsgoEdge
{
    /// <summary>
    /// Master switch. When false (default) the edge feature is completely inert for
    /// this product and no edge container is provisioned.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Public hostname the edge serves (also used as the certificate CN in later phases).
    /// </summary>
    public string? PublicHostname { get; set; }

    /// <summary>
    /// Public port the edge listens on. Defaults to 443.
    /// </summary>
    public int? PublicPort { get; set; }

    /// <summary>
    /// Optional override for the Caddy edge image. Defaults to a digest-pinned official
    /// <c>caddy</c> image chosen by RSGO. Override only for air-gapped/mirror registries.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// Upstream the edge proxies to when the product is running.
    /// </summary>
    public RsgoEdgeUpstream? Upstream { get; set; }

    /// <summary>
    /// Name of the shared external network that connects edge and upstream.
    /// Must be declared <c>external: true</c> so it survives redeploys.
    /// </summary>
    public string? Network { get; set; }

    /// <summary>
    /// TLS configuration for the edge (consumed from Phase 2 onward).
    /// </summary>
    public RsgoEdgeTls? Tls { get; set; }

    /// <summary>
    /// Maintenance-page configuration (branding contract, consumed from Phase 3 onward;
    /// the default branding variables are already used in Phase 1).
    /// </summary>
    public RsgoEdgeMaintenancePage? MaintenancePage { get; set; }
}

/// <summary>
/// Upstream service the edge forwards to in proxy mode.
/// </summary>
public class RsgoEdgeUpstream
{
    /// <summary>
    /// Internal service name (DNS alias) of the product's public entry (e.g. its BFF).
    /// </summary>
    public string? Service { get; set; }

    /// <summary>
    /// Upstream port. Defaults to 8080.
    /// </summary>
    public int? Port { get; set; }
}

/// <summary>
/// TLS settings for the edge.
/// </summary>
public class RsgoEdgeTls
{
    /// <summary>
    /// TLS mode: <c>reuse</c> (use RSGO's own certificate — single-host only),
    /// <c>selfsigned</c>, <c>custom</c> (uploaded cert referenced by <see cref="CertRef"/>),
    /// or <c>letsencrypt</c>.
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// For <c>custom</c> mode: reference (name) of the uploaded certificate.
    /// </summary>
    public string? CertRef { get; set; }

    /// <summary>
    /// For <c>letsencrypt</c> mode: ACME settings.
    /// </summary>
    public RsgoEdgeLetsEncrypt? LetsEncrypt { get; set; }
}

/// <summary>
/// Let's Encrypt / ACME settings for the edge.
/// </summary>
public class RsgoEdgeLetsEncrypt
{
    public string? Email { get; set; }

    /// <summary>
    /// Optional DNS-01 challenge provider configuration token.
    /// </summary>
    public string? DnsChallenge { get; set; }
}

/// <summary>
/// Maintenance-page branding contract.
/// </summary>
public class RsgoEdgeMaintenancePage
{
    /// <summary>
    /// Resolution mode: <c>default</c> (RSGO standard page, themeable via
    /// <see cref="Branding"/>), <c>bundle</c> (asset directory), or <c>container</c>
    /// (product-contributed survivor-scoped container).
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// For <c>bundle</c> mode: asset directory inside the manifest repo.
    /// </summary>
    public string? BundlePath { get; set; }

    /// <summary>
    /// For <c>container</c> mode: the product-contributed maintenance container.
    /// </summary>
    public RsgoEdgeMaintenanceContainer? Container { get; set; }

    /// <summary>
    /// For <c>default</c> mode: themeable branding variables.
    /// </summary>
    public RsgoEdgeBranding? Branding { get; set; }
}

/// <summary>
/// Product-contributed maintenance-page container reference.
///
/// The product deploys this service as a normal container but labels it
/// <c>rsgo.role: maintenance-page</c> and <c>rsgo.redeploy: ignore</c> so it survives product
/// redeploys (same survival primitive as the edge), and attaches it to the shared edge network
/// so the edge can reach it by its service alias.
/// </summary>
public class RsgoEdgeMaintenanceContainer
{
    /// <summary>
    /// Service name (DNS alias on the edge network) the edge proxies to during maintenance.
    /// </summary>
    public string? Service { get; set; }

    /// <summary>
    /// Port the maintenance container serves on. Defaults to 80.
    /// </summary>
    public int? Port { get; set; }
}

/// <summary>
/// Themeable variables for the default maintenance page.
/// </summary>
public class RsgoEdgeBranding
{
    public string? ProductName { get; set; }
    public string? LogoUrl { get; set; }
    public string? SupportContact { get; set; }
    public List<string>? Locales { get; set; }
}
