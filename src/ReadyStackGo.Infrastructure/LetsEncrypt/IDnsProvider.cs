using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.LetsEncrypt;

/// <summary>
/// DNS provider interface for ACME DNS-01 challenge
/// </summary>
public interface IDnsProvider
{
    /// <summary>
    /// Provider type identifier
    /// </summary>
    DnsProviderType ProviderType { get; }

    /// <summary>
    /// Create a TXT record for ACME challenge
    /// </summary>
    /// <param name="domain">The domain (e.g., _acme-challenge.example.com)</param>
    /// <param name="value">The challenge value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Record identifier for cleanup</returns>
    Task<string> CreateTxtRecordAsync(string domain, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a TXT record after challenge completion
    /// </summary>
    /// <param name="domain">The domain</param>
    /// <param name="recordId">Record identifier from CreateTxtRecordAsync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteTxtRecordAsync(string domain, string recordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate provider configuration
    /// </summary>
    Task<DnsProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default);
}

public record DnsProviderValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static DnsProviderValidationResult Success() => new() { IsValid = true };
    public static DnsProviderValidationResult Error(string message) => new() { IsValid = false, ErrorMessage = message };
}
