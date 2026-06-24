namespace ReadyStackGo.Domain.Deployment.Edge;

/// <summary>
/// Resolved TLS material for a product edge: the certificate chain and private key in PEM
/// form, ready to be handed to Caddy (inline, via the admin API). RSGO is the single owner
/// of edge certificates; the edge never runs its own ACME.
/// </summary>
public sealed record EdgeCertMaterial(
    string CertificatePem,
    string PrivateKeyPem,
    DateTime NotAfterUtc,
    string Thumbprint);
