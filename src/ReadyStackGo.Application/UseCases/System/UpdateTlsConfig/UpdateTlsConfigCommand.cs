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
}
