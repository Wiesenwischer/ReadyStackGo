using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.Infrastructure.Security.Authentication;

/// <summary>
/// Generic OIDC authorization-code + PKCE client. Uses each provider's discovery document
/// for endpoints and signing keys, and issues no session of its own — the caller mints the
/// ReadyStackGo JWT after a successful exchange.
/// </summary>
public class OidcService : IOidcService
{
    private static readonly HttpClient HttpClient = new();

    // One cached discovery manager per authority (refreshes signing keys automatically).
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _configManagers = new();

    public async Task<string> BuildAuthorizeUrlAsync(
        OidcProviderSettings provider,
        string redirectUri,
        string state,
        string nonce,
        string codeChallenge,
        CancellationToken cancellationToken = default)
    {
        var config = await GetConfigurationAsync(provider, cancellationToken);

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = provider.ClientId,
            ["response_type"] = "code",
            ["scope"] = provider.Scopes,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var queryString = string.Join("&", query
            .Where(kvp => kvp.Value != null)
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

        return $"{config.AuthorizationEndpoint}?{queryString}";
    }

    public async Task<OidcUserInfo?> ExchangeCodeAsync(
        OidcProviderSettings provider,
        string code,
        string redirectUri,
        string codeVerifier,
        string expectedNonce,
        CancellationToken cancellationToken = default)
    {
        var config = await GetConfigurationAsync(provider, cancellationToken);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = provider.ClientId,
            ["code_verifier"] = codeVerifier
        };
        if (!string.IsNullOrEmpty(provider.ClientSecret))
        {
            form["client_secret"] = provider.ClientSecret;
        }

        using var response = await HttpClient.PostAsync(
            config.TokenEndpoint,
            new FormUrlEncodedContent(form),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("id_token", out var idTokenElement))
        {
            return null;
        }

        var idToken = idTokenElement.GetString();
        if (string.IsNullOrEmpty(idToken))
        {
            return null;
        }

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = config.Issuer,
            ValidAudience = provider.ClientId,
            IssuerSigningKeys = config.SigningKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        try
        {
            var principal = handler.ValidateToken(idToken, validationParameters, out _);

            // Replay protection: the nonce must match the one we sent in the challenge.
            var nonce = principal.FindFirst("nonce")?.Value;
            if (nonce != expectedNonce)
            {
                return null;
            }

            var subject = principal.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(subject))
            {
                return null;
            }

            var email = principal.FindFirst("email")?.Value;
            var emailVerified = string.Equals(
                principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);

            return new OidcUserInfo(subject, email, emailVerified);
        }
        catch
        {
            return null;
        }
    }

    private async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        OidcProviderSettings provider,
        CancellationToken cancellationToken)
    {
        var metadataAddress = $"{provider.Authority.TrimEnd('/')}/.well-known/openid-configuration";

        var manager = _configManagers.GetOrAdd(metadataAddress, address =>
            new ConfigurationManager<OpenIdConnectConfiguration>(
                address,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(HttpClient) { RequireHttps = false }));

        return await manager.GetConfigurationAsync(cancellationToken);
    }
}
