using FastEndpoints;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.System;

/// <summary>
/// POST /api/system/tls/letsencrypt
/// Configure and request a Let's Encrypt certificate.
/// </summary>
[RequirePermission("System", "Write")]
public class ConfigureLetsEncryptEndpoint : Endpoint<ConfigureLetsEncryptRequest, ConfigureLetsEncryptResponse>
{
    private readonly ILetsEncryptService _letsEncryptService;
    private readonly ILogger<ConfigureLetsEncryptEndpoint> _logger;

    public ConfigureLetsEncryptEndpoint(
        ILetsEncryptService letsEncryptService,
        ILogger<ConfigureLetsEncryptEndpoint> logger)
    {
        _letsEncryptService = letsEncryptService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/system/tls/letsencrypt");
        Description(b => b
            .WithTags("System")
            .WithSummary("Configure Let's Encrypt")
            .WithDescription("Configures Let's Encrypt and requests a new certificate. For DNS-01 with manual provider, returns pending challenges."));
        PreProcessor<RbacPreProcessor<ConfigureLetsEncryptRequest>>();
    }

    public override async Task HandleAsync(ConfigureLetsEncryptRequest req, CancellationToken ct)
    {
        // Validate domains
        if (req.Domains == null || req.Domains.Count == 0)
        {
            Response = new ConfigureLetsEncryptResponse
            {
                Success = false,
                Message = "At least one domain is required"
            };
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Validate email
        if (string.IsNullOrWhiteSpace(req.Email))
        {
            Response = new ConfigureLetsEncryptResponse
            {
                Success = false,
                Message = "Email address is required for Let's Encrypt account"
            };
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Validate DNS provider config for DNS-01 with Cloudflare
        var challengeType = ParseChallengeType(req.ChallengeType);
        if (challengeType == LetsEncryptChallengeType.Dns01 &&
            req.DnsProvider?.Type?.Equals("Cloudflare", StringComparison.OrdinalIgnoreCase) == true &&
            string.IsNullOrEmpty(req.DnsProvider.CloudflareApiToken))
        {
            Response = new ConfigureLetsEncryptResponse
            {
                Success = false,
                Message = "Cloudflare API token is required for Cloudflare DNS provider"
            };
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        _logger.LogInformation(
            "Configuring Let's Encrypt for domains: {Domains}",
            string.Join(", ", req.Domains));

        var result = await _letsEncryptService.RequestCertificateAsync(
            new LetsEncryptRequest
            {
                Domains = req.Domains,
                Email = req.Email,
                UseStaging = req.UseStaging,
                ChallengeType = challengeType,
                DnsProvider = MapDnsProviderConfig(req.DnsProvider)
            },
            ct);

        Response = new ConfigureLetsEncryptResponse
        {
            Success = result.Success,
            Message = result.Message,
            ExpiresAt = result.ExpiresAt,
            RequiresRestart = result.RequiresRestart,
            AwaitingManualDnsChallenge = result.AwaitingManualDnsChallenge
        };

        if (!result.Success && !result.AwaitingManualDnsChallenge)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    private static LetsEncryptChallengeType ParseChallengeType(string? type)
    {
        if (string.IsNullOrEmpty(type))
            return LetsEncryptChallengeType.Http01;

        return type.Equals("Dns01", StringComparison.OrdinalIgnoreCase)
            ? LetsEncryptChallengeType.Dns01
            : LetsEncryptChallengeType.Http01;
    }

    private static LetsEncryptDnsProviderConfig? MapDnsProviderConfig(DnsProviderConfigRequest? config)
    {
        if (config == null) return null;

        var providerType = config.Type?.Equals("Cloudflare", StringComparison.OrdinalIgnoreCase) == true
            ? LetsEncryptDnsProviderType.Cloudflare
            : LetsEncryptDnsProviderType.Manual;

        return new LetsEncryptDnsProviderConfig
        {
            Type = providerType,
            CloudflareApiToken = config.CloudflareApiToken,
            CloudflareZoneId = config.CloudflareZoneId
        };
    }
}

public class ConfigureLetsEncryptRequest
{
    public List<string> Domains { get; set; } = new();
    public string Email { get; set; } = string.Empty;
    public bool UseStaging { get; set; } = false;
    public string? ChallengeType { get; set; } = "Http01";
    public DnsProviderConfigRequest? DnsProvider { get; set; }
}

public class DnsProviderConfigRequest
{
    public string? Type { get; set; } = "Manual";
    public string? CloudflareApiToken { get; set; }
    public string? CloudflareZoneId { get; set; }
}

public class ConfigureLetsEncryptResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool RequiresRestart { get; set; }
    public bool AwaitingManualDnsChallenge { get; set; }
}
