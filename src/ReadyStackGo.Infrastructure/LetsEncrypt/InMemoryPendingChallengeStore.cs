using System.Collections.Concurrent;

namespace ReadyStackGo.Infrastructure.LetsEncrypt;

/// <summary>
/// In-memory store for pending ACME challenges.
/// For single-instance deployments. For multi-instance, use a distributed store.
/// </summary>
public class InMemoryPendingChallengeStore : IPendingChallengeStore
{
    private readonly ConcurrentDictionary<string, PendingDnsChallenge> _dnsChallenges = new();
    private readonly ConcurrentDictionary<string, string> _httpChallenges = new();

    public Task AddPendingDnsChallengeAsync(PendingDnsChallenge challenge)
    {
        _dnsChallenges[challenge.Id] = challenge;
        return Task.CompletedTask;
    }

    public Task RemovePendingDnsChallengeAsync(string id)
    {
        _dnsChallenges.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PendingDnsChallenge>> GetPendingDnsChallengesAsync()
    {
        return Task.FromResult<IReadOnlyList<PendingDnsChallenge>>(_dnsChallenges.Values.ToList());
    }

    public Task SetHttpChallengeAsync(string token, string keyAuthorization)
    {
        _httpChallenges[token] = keyAuthorization;
        return Task.CompletedTask;
    }

    public Task<string?> GetHttpChallengeAsync(string token)
    {
        _httpChallenges.TryGetValue(token, out var value);
        return Task.FromResult(value);
    }

    public Task RemoveHttpChallengeAsync(string token)
    {
        _httpChallenges.TryRemove(token, out _);
        return Task.CompletedTask;
    }

    public Task ClearAllAsync()
    {
        _dnsChallenges.Clear();
        _httpChallenges.Clear();
        return Task.CompletedTask;
    }
}
