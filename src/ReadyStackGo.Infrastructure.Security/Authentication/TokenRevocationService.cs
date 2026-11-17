using System.Collections.Concurrent;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Security.Authentication;

/// <summary>
/// In-memory token revocation store keyed by jti, with each entry holding the token's
/// expiry (unix seconds). Expired entries are pruned lazily, so the set stays bounded by
/// the number of unexpired revoked tokens.
/// </summary>
public class TokenRevocationService : ITokenRevocationService
{
    private readonly ConcurrentDictionary<string, long> _revoked = new();

    public void Revoke(string jti, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrEmpty(jti))
            return;

        Prune();
        _revoked[jti] = expiresAt.ToUnixTimeSeconds();
    }

    public bool IsRevoked(string jti)
    {
        if (string.IsNullOrEmpty(jti))
            return false;

        if (!_revoked.TryGetValue(jti, out var expiresAtUnix))
            return false;

        if (expiresAtUnix <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            // Token would be rejected by lifetime validation anyway; drop the entry.
            _revoked.TryRemove(jti, out _);
            return false;
        }

        return true;
    }

    private void Prune()
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var entry in _revoked)
        {
            if (entry.Value <= nowUnix)
                _revoked.TryRemove(entry.Key, out _);
        }
    }
}
