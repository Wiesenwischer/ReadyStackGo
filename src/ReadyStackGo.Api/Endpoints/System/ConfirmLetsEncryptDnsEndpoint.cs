using FastEndpoints;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.System;

/// <summary>
/// POST /api/system/tls/letsencrypt/confirm-dns
/// Confirm manual DNS challenges have been created.
/// </summary>
[RequirePermission("System", "Write")]
public class ConfirmLetsEncryptDnsEndpoint : EndpointWithoutRequest<ConfigureLetsEncryptResponse>
{
    private readonly ILetsEncryptService _letsEncryptService;
    private readonly ILogger<ConfirmLetsEncryptDnsEndpoint> _logger;

    public ConfirmLetsEncryptDnsEndpoint(
        ILetsEncryptService letsEncryptService,
        ILogger<ConfirmLetsEncryptDnsEndpoint> logger)
    {
        _letsEncryptService = letsEncryptService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/system/tls/letsencrypt/confirm-dns");
        Description(b => b
            .WithTags("System")
            .WithSummary("Confirm manual DNS challenges")
            .WithDescription("Confirms that manual DNS TXT records have been created and continues with certificate issuance."));
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        _logger.LogInformation("Confirming manual DNS challenges");

        var result = await _letsEncryptService.ConfirmManualDnsChallengesAsync(ct);

        Response = new ConfigureLetsEncryptResponse
        {
            Success = result.Success,
            Message = result.Message,
            ExpiresAt = result.ExpiresAt,
            RequiresRestart = result.RequiresRestart,
            AwaitingManualDnsChallenge = result.AwaitingManualDnsChallenge
        };

        if (!result.Success)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
