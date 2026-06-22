namespace ReadyStackGo.Domain.Deployment.Edge;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// TLS mode for a product edge. Consumed from Phase 2 onward; stored in Phase 1.
/// </summary>
public enum EdgeTlsMode
{
    /// <summary>No TLS termination at the edge (plain HTTP). Phase 1 default.</summary>
    None,

    /// <summary>Reuse RSGO's own certificate (single-host special case).</summary>
    Reuse,

    /// <summary>Edge-managed self-signed certificate.</summary>
    SelfSigned,

    /// <summary>Operator-uploaded certificate, referenced by <see cref="EdgeConfig.TlsCertRef"/>.</summary>
    Custom,

    /// <summary>Let's Encrypt / ACME issued certificate.</summary>
    LetsEncrypt
}

/// <summary>
/// Maintenance-page resolution mode. Consumed from Phase 3 onward.
/// </summary>
public enum EdgeMaintenancePageMode
{
    /// <summary>RSGO standard page, themeable via <see cref="EdgeBranding"/>.</summary>
    Default,

    /// <summary>Manifest-provided asset bundle.</summary>
    Bundle,

    /// <summary>Product-contributed, survivor-scoped maintenance container.</summary>
    Container
}

/// <summary>
/// Themeable branding for the default maintenance page.
/// </summary>
public sealed class EdgeBranding : ValueObject
{
    public string? ProductName { get; }
    public string? LogoUrl { get; }
    public string? SupportContact { get; }
    public IReadOnlyList<string> Locales { get; }

    public EdgeBranding(string? productName, string? logoUrl, string? supportContact, IReadOnlyList<string>? locales)
    {
        ProductName = productName;
        LogoUrl = logoUrl;
        SupportContact = supportContact;
        Locales = locales ?? Array.Empty<string>();
    }

    public static EdgeBranding Empty => new(null, null, null, null);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProductName;
        yield return LogoUrl;
        yield return SupportContact;
        foreach (var locale in Locales)
            yield return locale;
    }
}

/// <summary>
/// Resolved edge configuration for a single product deployment.
///
/// This is the domain projection of the manifest <c>edge:</c> block after variable
/// resolution. It is stored on <see cref="ProductDeployments.ProductDeployment"/> so the
/// edge reconciler (a background service mirroring the maintenance observer) can compute
/// the edge's desired state without re-reading the catalog.
///
/// A non-null instance always means the edge is enabled — a disabled or absent
/// <c>edge:</c> block maps to <c>null</c>, keeping existing products completely unaffected.
/// </summary>
public sealed class EdgeConfig : ValueObject
{
    /// <summary>Public hostname the edge serves (certificate CN in later phases).</summary>
    public string PublicHostname { get; }

    /// <summary>Public port the edge listens on.</summary>
    public int PublicPort { get; }

    /// <summary>Upstream service (DNS alias) the edge proxies to when running.</summary>
    public string UpstreamService { get; }

    /// <summary>Upstream port.</summary>
    public int UpstreamPort { get; }

    /// <summary>Shared external network connecting edge and upstream.</summary>
    public string Network { get; }

    /// <summary>Caddy edge image reference (digest-pinned).</summary>
    public string Image { get; }

    /// <summary>TLS mode (Phase 2+).</summary>
    public EdgeTlsMode TlsMode { get; }

    /// <summary>For <see cref="EdgeTlsMode.Custom"/>: reference to the uploaded cert.</summary>
    public string? TlsCertRef { get; }

    /// <summary>For <see cref="EdgeTlsMode.LetsEncrypt"/>: ACME account email.</summary>
    public string? LetsEncryptEmail { get; }

    /// <summary>For <see cref="EdgeTlsMode.LetsEncrypt"/>: optional DNS-01 challenge token.</summary>
    public string? LetsEncryptDnsChallenge { get; }

    /// <summary>Maintenance-page resolution mode (Phase 3+).</summary>
    public EdgeMaintenancePageMode MaintenancePageMode { get; }

    /// <summary>For <see cref="EdgeMaintenancePageMode.Bundle"/>: asset directory path.</summary>
    public string? BundlePath { get; }

    /// <summary>For <see cref="EdgeMaintenancePageMode.Container"/>: the maintenance-page service name.</summary>
    public string? MaintenanceContainerService { get; }

    /// <summary>For <see cref="EdgeMaintenancePageMode.Container"/>: the maintenance-page service port.</summary>
    public int MaintenanceContainerPort { get; }

    /// <summary>
    /// For <see cref="EdgeMaintenancePageMode.Bundle"/>: the maintenance page HTML, resolved from
    /// the manifest bundle at deploy time and persisted so the reconciler stays manifest-free.
    /// </summary>
    public string? BundleHtml { get; }

    /// <summary>Branding variables for the default maintenance page.</summary>
    public EdgeBranding Branding { get; }

    private EdgeConfig(
        string publicHostname,
        int publicPort,
        string upstreamService,
        int upstreamPort,
        string network,
        string image,
        EdgeTlsMode tlsMode,
        string? tlsCertRef,
        string? letsEncryptEmail,
        string? letsEncryptDnsChallenge,
        EdgeMaintenancePageMode maintenancePageMode,
        string? bundlePath,
        string? maintenanceContainerService,
        int maintenanceContainerPort,
        string? bundleHtml,
        EdgeBranding branding)
    {
        PublicHostname = publicHostname;
        PublicPort = publicPort;
        UpstreamService = upstreamService;
        UpstreamPort = upstreamPort;
        Network = network;
        Image = image;
        TlsMode = tlsMode;
        TlsCertRef = tlsCertRef;
        LetsEncryptEmail = letsEncryptEmail;
        LetsEncryptDnsChallenge = letsEncryptDnsChallenge;
        MaintenancePageMode = maintenancePageMode;
        BundlePath = bundlePath;
        MaintenanceContainerService = maintenanceContainerService;
        MaintenanceContainerPort = maintenanceContainerPort;
        BundleHtml = bundleHtml;
        Branding = branding;
    }

    public static EdgeConfig Create(
        string publicHostname,
        int publicPort,
        string upstreamService,
        int upstreamPort,
        string network,
        string image,
        EdgeTlsMode tlsMode = EdgeTlsMode.None,
        string? tlsCertRef = null,
        string? letsEncryptEmail = null,
        string? letsEncryptDnsChallenge = null,
        EdgeMaintenancePageMode maintenancePageMode = EdgeMaintenancePageMode.Default,
        string? bundlePath = null,
        string? maintenanceContainerService = null,
        int maintenanceContainerPort = 80,
        string? bundleHtml = null,
        EdgeBranding? branding = null)
    {
        if (string.IsNullOrWhiteSpace(publicHostname))
            throw new ArgumentException("Edge publicHostname is required.", nameof(publicHostname));
        if (publicPort is <= 0 or > 65535)
            throw new ArgumentException("Edge publicPort must be in range 1-65535.", nameof(publicPort));
        if (string.IsNullOrWhiteSpace(upstreamService))
            throw new ArgumentException("Edge upstream service is required.", nameof(upstreamService));
        if (upstreamPort is <= 0 or > 65535)
            throw new ArgumentException("Edge upstream port must be in range 1-65535.", nameof(upstreamPort));
        if (string.IsNullOrWhiteSpace(network))
            throw new ArgumentException("Edge network is required.", nameof(network));
        if (string.IsNullOrWhiteSpace(image))
            throw new ArgumentException("Edge image is required.", nameof(image));

        return new EdgeConfig(
            publicHostname,
            publicPort,
            upstreamService,
            upstreamPort,
            network,
            image,
            tlsMode,
            tlsCertRef,
            letsEncryptEmail,
            letsEncryptDnsChallenge,
            maintenancePageMode,
            bundlePath,
            maintenanceContainerService,
            maintenanceContainerPort,
            bundleHtml,
            branding ?? EdgeBranding.Empty);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return PublicHostname;
        yield return PublicPort;
        yield return UpstreamService;
        yield return UpstreamPort;
        yield return Network;
        yield return Image;
        yield return TlsMode;
        yield return TlsCertRef;
        yield return LetsEncryptEmail;
        yield return LetsEncryptDnsChallenge;
        yield return MaintenancePageMode;
        yield return BundlePath;
        yield return MaintenanceContainerService;
        yield return MaintenanceContainerPort;
        yield return BundleHtml;
        yield return Branding;
    }
}
