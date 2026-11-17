namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Options for the optional host-level SNI passthrough router (Phase 4). Default OFF — when
/// disabled nothing is provisioned and per-product edges behave exactly as before.
///
/// When enabled, RSGO runs a single shared Layer-4 router that listens on one public port and
/// routes TLS connections by SNI hostname to the matching product edge — purely at L4
/// (<c>SslMode=Passthrough</c>): it never terminates TLS, so each edge keeps its own certificate.
///
/// The router image must include the Caddy <c>layer4</c> module (the official caddy image does
/// not); supply a caddy-l4-capable image via <see cref="Image"/>.
/// </summary>
public class SniRouterOptions
{
    public const string SectionName = "Edge:SniRouter";

    /// <summary>Master switch. Default false → feature inert.</summary>
    public bool Enabled { get; set; }

    /// <summary>A caddy-l4-capable image (must contain the <c>layer4</c> module).</summary>
    public string Image { get; set; } = "caddy:2.8.4";

    /// <summary>Public port the shared router listens on. Default 443.</summary>
    public int ListenPort { get; set; } = 443;
}
