namespace ReadyStackGo.Application.Services.Oidc;

/// <summary>
/// Drives the OIDC authorization-code flow against a provider's discovery document:
/// builds the authorize URL and exchanges the authorization code for a validated id token.
/// </summary>
public interface IOidcService
{
    /// <summary>Builds the provider authorize URL for the authorization-code + PKCE flow.</summary>
    Task<string> BuildAuthorizeUrlAsync(
        OidcProviderSettings provider,
        string redirectUri,
        string state,
        string nonce,
        string codeChallenge,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges the authorization code for tokens, validates the id token (signature,
    /// issuer, audience, expiry, nonce) and returns the user info, or null on failure.
    /// </summary>
    Task<OidcUserInfo?> ExchangeCodeAsync(
        OidcProviderSettings provider,
        string code,
        string redirectUri,
        string codeVerifier,
        string expectedNonce,
        CancellationToken cancellationToken = default);
}
