namespace ReadyStackGo.Application.Services;

/// <summary>
/// Tracks revoked session tokens by their JWT id (jti) so that a logout takes effect
/// server-side before the token would naturally expire. In-memory and best-effort
/// (single-instance deployments); entries auto-expire at the token's own expiry.
/// </summary>
public interface ITokenRevocationService
{
    /// <summary>Marks a token (by jti) as revoked until its expiry.</summary>
    void Revoke(string jti, DateTimeOffset expiresAt);

    /// <summary>True if the token id has been revoked and has not yet expired.</summary>
    bool IsRevoked(string jti);
}
