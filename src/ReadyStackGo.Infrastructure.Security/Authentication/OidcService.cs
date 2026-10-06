using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.Infrastructure.Security.Authentication;

/// <summary>
/// Generic OIDC authorization-code + PKCE client with pushed authorization requests
/// (RFC 9126). Uses each provider's discovery document for endpoints and signing keys, and
/// issues no session of its own — the caller mints the ReadyStackGo JWT after a successful
/// exchange. All HTTP goes through the named client <see cref="IOidcService.HttpClientName"/>.
/// </summary>
public class OidcService : IOidcService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OidcService> _logger;

    // One cached discovery manager per metadata address (refreshes signing keys automatically).
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _configManagers = new();

    public OidcService(IHttpClientFactory httpClientFactory, ILogger<OidcService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OidcAuthorizeResult> BuildAuthorizeUrlAsync(
        OidcProviderSettings provider,
        string redirectUri,
        string state,
        string nonce,
        string codeChallenge,
        CancellationToken cancellationToken = default)
    {
        var discovery = await GetConfigurationAsync(provider, cancellationToken);
        if (discovery.Configuration is not { } config)
        {
            return OidcAuthorizeResult.Failure(discovery.Error!, discovery.Description);
        }

        var parameters = new Dictionary<string, string>
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

        var usePar = provider.RequirePar || config.RequirePushedAuthorizationRequests;
        if (!usePar)
        {
            return OidcAuthorizeResult.Success($"{config.AuthorizationEndpoint}?{ToQuery(parameters)}");
        }

        if (string.IsNullOrEmpty(config.PushedAuthorizationRequestEndpoint))
        {
            return OidcAuthorizeResult.Failure(
                OidcErrorCodes.ParNotSupported,
                "Pushed authorization requests are required, but the provider offers no PAR endpoint.");
        }

        var par = await PushAuthorizationRequestAsync(
            config.PushedAuthorizationRequestEndpoint, provider, parameters, cancellationToken);
        if (par.RequestUri == null)
        {
            return OidcAuthorizeResult.Failure(par.Error!, par.Description);
        }

        var query = ToQuery(new Dictionary<string, string>
        {
            ["client_id"] = provider.ClientId,
            ["request_uri"] = par.RequestUri
        });
        return OidcAuthorizeResult.Success($"{config.AuthorizationEndpoint}?{query}");
    }

    public async Task<OidcExchangeResult> ExchangeCodeAsync(
        OidcProviderSettings provider,
        string code,
        string redirectUri,
        string codeVerifier,
        string expectedNonce,
        CancellationToken cancellationToken = default)
    {
        var discovery = await GetConfigurationAsync(provider, cancellationToken);
        if (discovery.Configuration is not { } config)
        {
            return OidcExchangeResult.Failure(discovery.Error!, discovery.Description);
        }

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

        string json;
        try
        {
            using var response = await CreateClient().PostAsync(
                config.TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
            json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var (error, description) = ReadError(json);
                if ((int)response.StatusCode >= 500)
                {
                    return OidcExchangeResult.Failure(OidcErrorCodes.Unreachable, $"The token endpoint answered {(int)response.StatusCode}.");
                }
                return OidcExchangeResult.Failure(
                    error == "invalid_client" ? OidcErrorCodes.InvalidClient : OidcErrorCodes.Rejected,
                    description ?? error);
            }
        }
        catch (Exception ex) when (IsNetworkError(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "OIDC provider {Provider}: token endpoint not reachable", provider.Name);
            return OidcExchangeResult.Failure(OidcErrorCodes.Unreachable, "The token endpoint could not be reached.");
        }

        string? idToken;
        try
        {
            using var doc = JsonDocument.Parse(json);
            idToken = doc.RootElement.TryGetProperty("id_token", out var element) ? element.GetString() : null;
        }
        catch (JsonException)
        {
            return OidcExchangeResult.Failure(OidcErrorCodes.InvalidToken, "The token response is not valid JSON.");
        }

        if (string.IsNullOrEmpty(idToken))
        {
            return OidcExchangeResult.Failure(OidcErrorCodes.InvalidToken, "The token response contains no id_token.");
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
            if (principal.FindFirst("nonce")?.Value != expectedNonce)
            {
                return OidcExchangeResult.Failure(OidcErrorCodes.InvalidToken, "The nonce of the id token does not match.");
            }

            var subject = principal.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(subject))
            {
                return OidcExchangeResult.Failure(OidcErrorCodes.InvalidToken, "The id token has no subject.");
            }

            string? Claim(string name) =>
                principal.FindFirst(name)?.Value is { Length: > 0 } value ? value : null;

            var emailVerified = string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);

            return OidcExchangeResult.Success(new OidcUserInfo(
                subject,
                Claim(provider.Claims.Email),
                emailVerified,
                Claim(provider.Claims.Username),
                Claim(provider.Claims.DisplayName)));
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            _logger.LogWarning(ex, "OIDC provider {Provider}: id token rejected", provider.Name);
            return OidcExchangeResult.Failure(OidcErrorCodes.InvalidToken, "The id token could not be validated.");
        }
    }

    private async Task<ParResult> PushAuthorizationRequestAsync(
        string endpoint,
        OidcProviderSettings provider,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>(parameters);
        if (!string.IsNullOrEmpty(provider.ClientSecret))
        {
            form["client_secret"] = provider.ClientSecret;
        }

        try
        {
            using var response = await CreateClient().PostAsync(endpoint, new FormUrlEncodedContent(form), cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("request_uri", out var requestUri) &&
                        requestUri.GetString() is { Length: > 0 } value)
                    {
                        return new ParResult(value, null, null);
                    }
                }
                catch (JsonException)
                {
                    // Fall through to the error below.
                }
                return new ParResult(null, OidcErrorCodes.Rejected, "The PAR endpoint returned no request_uri.");
            }

            if ((int)response.StatusCode >= 500)
            {
                return new ParResult(null, OidcErrorCodes.Unreachable, $"The PAR endpoint answered {(int)response.StatusCode}.");
            }

            var (error, description) = ReadError(json);
            return new ParResult(
                null,
                error == "invalid_client" || response.StatusCode == HttpStatusCode.Unauthorized
                    ? OidcErrorCodes.InvalidClient
                    : OidcErrorCodes.Rejected,
                description ?? error);
        }
        catch (Exception ex) when (IsNetworkError(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "OIDC provider {Provider}: PAR endpoint not reachable", provider.Name);
            return new ParResult(null, OidcErrorCodes.Unreachable, "The PAR endpoint could not be reached.");
        }
    }

    private async Task<DiscoveryResult> GetConfigurationAsync(OidcProviderSettings provider, CancellationToken cancellationToken)
    {
        var metadataAddress = DiscoveryAddress(provider.Authority);

        var manager = _configManagers.GetOrAdd(metadataAddress, address =>
            new ConfigurationManager<OpenIdConnectConfiguration>(
                address,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(CreateClient()) { RequireHttps = false }));

        try
        {
            return new DiscoveryResult(await manager.GetConfigurationAsync(cancellationToken), null, null);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // A failed fetch must not stick: the next call retries.
            _configManagers.TryRemove(metadataAddress, out _);
            // An HTTP answer below 500 (e.g. 404) means the document is missing, not that the
            // provider is unreachable; the retriever reports the status in the exception data.
            var status = HttpStatusOf(ex);
            var unreachable = status.HasValue
                ? (int)status.Value >= 500
                : HasInner<HttpRequestException>(ex) || HasInner<TaskCanceledException>(ex) || HasInner<IOException>(ex);
            _logger.LogWarning(ex, "OIDC provider {Provider}: discovery {Address} failed", provider.Name, metadataAddress);
            return unreachable
                ? new DiscoveryResult(null, OidcErrorCodes.Unreachable, "The provider could not be reached.")
                : new DiscoveryResult(null, OidcErrorCodes.InvalidDiscovery, "The discovery document could not be read.");
        }
    }

    internal static string DiscoveryAddress(string authority) =>
        $"{authority.TrimEnd('/')}/.well-known/openid-configuration";

    private HttpClient CreateClient() => _httpClientFactory.CreateClient(IOidcService.HttpClientName);

    private static string ToQuery(Dictionary<string, string> parameters) =>
        string.Join("&", parameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

    private static (string? Error, string? Description) ReadError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return (
                root.TryGetProperty("error", out var error) ? error.GetString() : null,
                root.TryGetProperty("error_description", out var description) ? description.GetString() : null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static bool IsNetworkError(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or IOException ||
        (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static HttpStatusCode? HttpStatusOf(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current.Data.Contains(HttpDocumentRetriever.StatusCode) &&
                current.Data[HttpDocumentRetriever.StatusCode] is HttpStatusCode status)
            {
                return status;
            }
        }
        return null;
    }

    private static bool HasInner<T>(Exception ex) where T : Exception
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is T)
            {
                return true;
            }
        }
        return false;
    }

    private sealed record DiscoveryResult(OpenIdConnectConfiguration? Configuration, string? Error, string? Description);

    private sealed record ParResult(string? RequestUri, string? Error, string? Description);
}
