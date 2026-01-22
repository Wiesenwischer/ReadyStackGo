using Microsoft.Extensions.Logging;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.LetsEncrypt;

/// <summary>
/// Manual DNS provider - user must create TXT records manually.
/// Stores pending challenges for UI display.
/// </summary>
public class ManualDnsProvider : IDnsProvider
{
    private readonly ILogger<ManualDnsProvider> _logger;
    private readonly IPendingChallengeStore _challengeStore;

    public DnsProviderType ProviderType => DnsProviderType.Manual;

    public ManualDnsProvider(
        ILogger<ManualDnsProvider> logger,
        IPendingChallengeStore challengeStore)
    {
        _logger = logger;
        _challengeStore = challengeStore;
    }

    public async Task<string> CreateTxtRecordAsync(string domain, string value, CancellationToken cancellationToken = default)
    {
        var recordId = Guid.NewGuid().ToString();

        // Extract the base domain for display (remove _acme-challenge. prefix)
        var baseDomain = domain.StartsWith("_acme-challenge.")
            ? domain.Substring("_acme-challenge.".Length)
            : domain;

        await _challengeStore.AddPendingDnsChallengeAsync(new PendingDnsChallenge
        {
            Id = recordId,
            Domain = baseDomain,
            TxtRecordName = domain,
            TxtValue = value,
            CreatedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "Manual DNS challenge created. Please add TXT record: Name={RecordName}, Value={Value}",
            domain, value);

        return recordId;
    }

    public async Task DeleteTxtRecordAsync(string domain, string recordId, CancellationToken cancellationToken = default)
    {
        await _challengeStore.RemovePendingDnsChallengeAsync(recordId);
        _logger.LogInformation("Manual DNS challenge cleanup completed for record {RecordId}", recordId);
    }

    public Task<DnsProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        // Manual provider is always valid - no configuration needed
        return Task.FromResult(DnsProviderValidationResult.Success());
    }
}
