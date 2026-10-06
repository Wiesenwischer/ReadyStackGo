namespace ReadyStackGo.Application.Services.Oidc;

/// <summary>
/// Drives the OIDC authorization-code flow against a provider's discovery document:
/// builds the authorize URL (with pushed authorization requests where required) and
/// exchanges the authorization code for a validated id token. Never throws for network or
/// provider errors; the results carry an <see cref="OidcErrorCodes"/> code instead.
/// </summary>
public interface IOidcService
{
    /// <summary>The name of the HttpClient used for all calls to identity providers.</summary>
    public const string HttpClientName = "Oidc";

    /// <summary>
    /// Builds the provider authorize URL for the authorization-code + PKCE flow. Uses PAR
    /// when the discovery requires it or the provider has <see cref="OidcProviderSettings.RequirePar"/>;
    /// the redirect then carries only client_id and request_uri.
    /// </summary>
    Task<OidcAuthorizeResult> BuildAuthorizeUrlAsync(
        OidcProviderSettings provider,
        string redirectUri,
        string state,
        string nonce,
        string codeChallenge,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges the authorization code for tokens, validates the id token (signature,
    /// issuer, audience, expiry, nonce) and returns the user info.
    /// </summary>
    Task<OidcExchangeResult> ExchangeCodeAsync(
        OidcProviderSettings provider,
        string code,
        string redirectUri,
        string codeVerifier,
        string expectedNonce,
        CancellationToken cancellationToken = default);
}

/// <summary>Machine-readable reasons of failed OIDC calls.</summary>
public static class OidcErrorCodes
{
    /// <summary>The provider could not be reached (DNS, connection, timeout, HTTP 5xx).</summary>
    public const string Unreachable = "unreachable";

    /// <summary>The discovery document is missing or invalid.</summary>
    public const string InvalidDiscovery = "invalid_discovery";

    /// <summary>PAR is required but the provider offers no PAR endpoint.</summary>
    public const string ParNotSupported = "par_not_supported";

    /// <summary>The provider rejected client ID or secret.</summary>
    public const string InvalidClient = "invalid_client";

    /// <summary>The provider rejected the request (PAR or token), for example the redirect URI.</summary>
    public const string Rejected = "rejected";

    /// <summary>The id token is missing or invalid (signature, issuer, audience, nonce).</summary>
    public const string InvalidToken = "invalid_token";
}

public sealed record OidcAuthorizeResult(string? Url, string? Error, string? ErrorDescription = null)
{
    public bool Succeeded => Url != null;

    public static OidcAuthorizeResult Success(string url) => new(url, null);

    public static OidcAuthorizeResult Failure(string error, string? description = null) => new(null, error, description);
}

public sealed record OidcExchangeResult(OidcUserInfo? UserInfo, string? Error, string? ErrorDescription = null)
{
    public bool Succeeded => UserInfo != null;

    public static OidcExchangeResult Success(OidcUserInfo userInfo) => new(userInfo, null);

    public static OidcExchangeResult Failure(string error, string? description = null) => new(null, error, description);
}
