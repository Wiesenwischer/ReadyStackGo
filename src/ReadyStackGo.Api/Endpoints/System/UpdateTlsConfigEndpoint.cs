using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.System.UpdateTlsConfig;

namespace ReadyStackGo.Api.Endpoints.System;

/// <summary>
/// Request DTO for TLS configuration update.
/// </summary>
public class UpdateTlsConfigRequest
{
    /// <summary>
    /// Base64-encoded PFX certificate data.
    /// </summary>
    public string? PfxBase64 { get; set; }

    /// <summary>
    /// Password for the PFX certificate.
    /// </summary>
    public string? PfxPassword { get; set; }

    /// <summary>
    /// PEM-encoded certificate.
    /// </summary>
    public string? CertificatePem { get; set; }

    /// <summary>
    /// PEM-encoded private key.
    /// </summary>
    public string? PrivateKeyPem { get; set; }

    /// <summary>
    /// Enable or disable HTTP access.
    /// </summary>
    public bool? HttpEnabled { get; set; }

    /// <summary>
    /// Reset to self-signed certificate.
    /// </summary>
    public bool ResetToSelfSigned { get; set; }
}

/// <summary>
/// PUT /api/system/tls
/// Update TLS configuration - upload custom certificate or change settings.
/// Accessible only by SystemAdmin.
/// </summary>
[RequirePermission("System", "Write")]
public class UpdateTlsConfigEndpoint : Endpoint<UpdateTlsConfigRequest, UpdateTlsConfigResponse>
{
    private readonly IMediator _mediator;

    public UpdateTlsConfigEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/system/tls");
        Description(b => b.WithTags("System"));
        PreProcessor<RbacPreProcessor<UpdateTlsConfigRequest>>();
    }

    public override async Task HandleAsync(UpdateTlsConfigRequest req, CancellationToken ct)
    {
        var command = new UpdateTlsConfigCommand
        {
            PfxBase64 = req.PfxBase64,
            PfxPassword = req.PfxPassword,
            CertificatePem = req.CertificatePem,
            PrivateKeyPem = req.PrivateKeyPem,
            HttpEnabled = req.HttpEnabled,
            ResetToSelfSigned = req.ResetToSelfSigned
        };

        Response = await _mediator.Send(command, ct);

        if (!Response.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
