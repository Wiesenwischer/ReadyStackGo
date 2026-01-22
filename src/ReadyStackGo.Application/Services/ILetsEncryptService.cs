namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for Let's Encrypt certificate management using ACME protocol
/// </summary>
public interface ILetsEncryptService
{
    /// <summary>
    /// Request a new certificate from Let's Encrypt
    /// </summary>
    Task<LetsEncryptResult> RequestCertificateAsync(
        LetsEncryptRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if certificate renewal is needed
    /// </summary>
    Task<bool> NeedsRenewalAsync(int daysBeforeExpiry = 30);

    /// <summary>
    /// Renew the current Let's Encrypt certificate
    /// </summary>
    Task<LetsEncryptResult> RenewCertificateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current Let's Encrypt status
    /// </summary>
    Task<LetsEncryptStatus> GetStatusAsync();

    /// <summary>
    /// Validate domains can be resolved
    /// </summary>
    Task<DomainValidationResult> ValidateDomainsAsync(
        IReadOnlyList<string> domains,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirm that manual DNS challenges have been completed
    /// </summary>
    Task<LetsEncryptResult> ConfirmManualDnsChallengesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to configure and obtain a Let's Encrypt certificate
/// </summary>
public record LetsEncryptRequest
{
    public required IReadOnlyList<string> Domains { get; init; }
    public required string Email { get; init; }
    public bool UseStaging { get; init; } = false;
    public LetsEncryptChallengeType ChallengeType { get; init; } = LetsEncryptChallengeType.Http01;
    public LetsEncryptDnsProviderConfig? DnsProvider { get; init; }
}

public enum LetsEncryptChallengeType
{
    Http01,
    Dns01
}

public record LetsEncryptDnsProviderConfig
{
    public LetsEncryptDnsProviderType Type { get; init; } = LetsEncryptDnsProviderType.Manual;
    public string? CloudflareApiToken { get; init; }
    public string? CloudflareZoneId { get; init; }
}

public enum LetsEncryptDnsProviderType
{
    Manual,
    Cloudflare
}

/// <summary>
/// Result of a Let's Encrypt certificate operation
/// </summary>
public record LetsEncryptResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? CertificatePath { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public bool RequiresRestart { get; init; }
    public bool AwaitingManualDnsChallenge { get; init; }

    public static LetsEncryptResult Ok(string path, DateTime expiresAt, string? message = null) =>
        new() { Success = true, CertificatePath = path, ExpiresAt = expiresAt, Message = message, RequiresRestart = true };

    public static LetsEncryptResult Error(string message) =>
        new() { Success = false, Message = message };

    public static LetsEncryptResult AwaitingDns(string message) =>
        new() { Success = false, Message = message, AwaitingManualDnsChallenge = true };
}

/// <summary>
/// Current Let's Encrypt status
/// </summary>
public record LetsEncryptStatus
{
    public bool IsConfigured { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<string> Domains { get; init; } = Array.Empty<string>();
    public DateTime? CertificateExpiresAt { get; init; }
    public DateTime? LastIssuedAt { get; init; }
    public DateTime? LastRenewalAttempt { get; init; }
    public string? LastError { get; init; }
    public bool IsUsingStaging { get; init; }
    public string ChallengeType { get; init; } = "Http01";
    public IReadOnlyList<PendingDnsChallengeInfo> PendingDnsChallenges { get; init; } = Array.Empty<PendingDnsChallengeInfo>();
}

public record PendingDnsChallengeInfo
{
    public required string Domain { get; init; }
    public required string TxtRecordName { get; init; }
    public required string TxtValue { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Result of domain validation
/// </summary>
public record DomainValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyDictionary<string, string?> DomainErrors { get; init; } = new Dictionary<string, string?>();
}
