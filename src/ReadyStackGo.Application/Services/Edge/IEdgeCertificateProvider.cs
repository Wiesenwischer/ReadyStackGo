using ReadyStackGo.Domain.Deployment.Edge;

namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Resolves (and, for self-signed, generates/renews) the TLS material a product edge should
/// terminate with. RSGO owns all edge certificates; the result is injected into Caddy inline
/// via the admin API (no second ACME instance in Caddy). Implemented in infrastructure.
/// </summary>
public interface IEdgeCertificateProvider
{
    /// <summary>
    /// Returns the certificate + key the edge should serve, or <c>null</c> when TLS is not
    /// enabled (<see cref="EdgeTlsMode.None"/>) or no certificate can be materialized — in
    /// which case the edge stays on plain HTTP. Idempotent and cheap to call every cycle:
    /// self-signed certs are persisted and only regenerated when missing or near expiry.
    /// </summary>
    Task<EdgeCertMaterial?> GetCertificateAsync(EdgeConfig config, CancellationToken cancellationToken = default);
}
