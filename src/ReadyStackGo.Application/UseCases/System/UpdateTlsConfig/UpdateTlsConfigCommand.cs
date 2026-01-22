using MediatR;

namespace ReadyStackGo.Application.UseCases.System.UpdateTlsConfig;

public record UpdateTlsConfigCommand : IRequest<UpdateTlsConfigResponse>
{
    /// <summary>
    /// Base64-encoded PFX certificate data.
    /// </summary>
    public string? PfxBase64 { get; init; }

    /// <summary>
    /// Password for the PFX certificate.
    /// </summary>
    public string? PfxPassword { get; init; }

    /// <summary>
    /// PEM-encoded certificate.
    /// </summary>
    public string? CertificatePem { get; init; }

    /// <summary>
    /// PEM-encoded private key.
    /// </summary>
    public string? PrivateKeyPem { get; init; }

    /// <summary>
    /// Enable or disable HTTP access.
    /// </summary>
    public bool? HttpEnabled { get; init; }

    /// <summary>
    /// Reset to self-signed certificate.
    /// </summary>
    public bool ResetToSelfSigned { get; init; }

    /// <summary>
    /// Reverse proxy configuration updates.
    /// </summary>
    public ReverseProxyUpdate? ReverseProxy { get; init; }
}

/// <summary>
/// Reverse proxy configuration update (for running behind nginx, Traefik, etc.)
/// </summary>
public record ReverseProxyUpdate
{
    /// <summary>
    /// Enable or disable reverse proxy mode.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// SSL handling mode: Termination, Passthrough, or ReEncryption.
    /// </summary>
    public string? SslMode { get; init; }

    /// <summary>
    /// Trust the X-Forwarded-For header.
    /// </summary>
    public bool? TrustForwardedFor { get; init; }

    /// <summary>
    /// Trust the X-Forwarded-Proto header.
    /// </summary>
    public bool? TrustForwardedProto { get; init; }

    /// <summary>
    /// Trust the X-Forwarded-Host header.
    /// </summary>
    public bool? TrustForwardedHost { get; init; }

    /// <summary>
    /// List of known proxy IP addresses.
    /// </summary>
    public List<string>? KnownProxies { get; init; }

    /// <summary>
    /// Number of proxies in front of the application.
    /// </summary>
    public int? ForwardLimit { get; init; }

    /// <summary>
    /// Base path if the app is hosted at a subpath.
    /// </summary>
    public string? PathBase { get; init; }
}
