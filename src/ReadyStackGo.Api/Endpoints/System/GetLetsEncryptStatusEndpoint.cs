using FastEndpoints;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.System;

/// <summary>
/// GET /api/system/tls/letsencrypt
/// Get Let's Encrypt status and configuration.
/// </summary>
[RequirePermission("System", "Read")]
public class GetLetsEncryptStatusEndpoint : EndpointWithoutRequest<LetsEncryptStatusResponse>
{
    private readonly ILetsEncryptService _letsEncryptService;

    public GetLetsEncryptStatusEndpoint(ILetsEncryptService letsEncryptService)
    {
        _letsEncryptService = letsEncryptService;
    }

    public override void Configure()
    {
        Get("/api/system/tls/letsencrypt");
        Description(b => b
            .WithTags("System")
            .WithSummary("Get Let's Encrypt status")
            .WithDescription("Returns the current Let's Encrypt configuration and certificate status."));
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var status = await _letsEncryptService.GetStatusAsync();

        Response = new LetsEncryptStatusResponse
        {
            IsConfigured = status.IsConfigured,
            IsActive = status.IsActive,
            Domains = status.Domains.ToList(),
            CertificateExpiresAt = status.CertificateExpiresAt,
            LastIssuedAt = status.LastIssuedAt,
            LastRenewalAttempt = status.LastRenewalAttempt,
            LastError = status.LastError,
            IsUsingStaging = status.IsUsingStaging,
            ChallengeType = status.ChallengeType,
            PendingDnsChallenges = status.PendingDnsChallenges
                .Select(c => new PendingDnsChallengeDto
                {
                    Domain = c.Domain,
                    TxtRecordName = c.TxtRecordName,
                    TxtValue = c.TxtValue,
                    CreatedAt = c.CreatedAt
                })
                .ToList()
        };
    }
}

public class LetsEncryptStatusResponse
{
    public bool IsConfigured { get; set; }
    public bool IsActive { get; set; }
    public List<string> Domains { get; set; } = new();
    public DateTime? CertificateExpiresAt { get; set; }
    public DateTime? LastIssuedAt { get; set; }
    public DateTime? LastRenewalAttempt { get; set; }
    public string? LastError { get; set; }
    public bool IsUsingStaging { get; set; }
    public string ChallengeType { get; set; } = "Http01";
    public List<PendingDnsChallengeDto> PendingDnsChallenges { get; set; } = new();
}

public class PendingDnsChallengeDto
{
    public string Domain { get; set; } = string.Empty;
    public string TxtRecordName { get; set; } = string.Empty;
    public string TxtValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
