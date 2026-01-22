namespace ReadyStackGo.Infrastructure.LetsEncrypt;

/// <summary>
/// Store for pending ACME challenges (HTTP-01 and DNS-01)
/// </summary>
public interface IPendingChallengeStore
{
    /// <summary>
    /// Add a pending DNS challenge for UI display (manual DNS provider)
    /// </summary>
    Task AddPendingDnsChallengeAsync(PendingDnsChallenge challenge);

    /// <summary>
    /// Remove a pending DNS challenge
    /// </summary>
    Task RemovePendingDnsChallengeAsync(string id);

    /// <summary>
    /// Get all pending DNS challenges
    /// </summary>
    Task<IReadOnlyList<PendingDnsChallenge>> GetPendingDnsChallengesAsync();

    /// <summary>
    /// Set HTTP-01 challenge response
    /// </summary>
    Task SetHttpChallengeAsync(string token, string keyAuthorization);

    /// <summary>
    /// Get HTTP-01 challenge response by token
    /// </summary>
    Task<string?> GetHttpChallengeAsync(string token);

    /// <summary>
    /// Remove HTTP-01 challenge
    /// </summary>
    Task RemoveHttpChallengeAsync(string token);

    /// <summary>
    /// Clear all pending challenges
    /// </summary>
    Task ClearAllAsync();
}

/// <summary>
/// Pending DNS challenge for manual DNS provider
/// </summary>
public class PendingDnsChallenge
{
    public string Id { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string TxtRecordName { get; set; } = string.Empty;
    public string TxtValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
