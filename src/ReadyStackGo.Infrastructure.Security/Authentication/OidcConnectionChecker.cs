using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.Infrastructure.Security.Authentication;

/// <summary>
/// Runs the connection checks of the setup flow from the server (see
/// <see cref="IOidcConnectionChecker"/>). Each HTTP call is bounded by the timeout of the
/// named client <see cref="IOidcService.HttpClientName"/> (10 seconds).
/// </summary>
public class OidcConnectionChecker : IOidcConnectionChecker
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OidcConnectionChecker> _logger;

    public OidcConnectionChecker(IHttpClientFactory httpClientFactory, ILogger<OidcConnectionChecker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OidcCheckReport> CheckAsync(OidcCheckRequest request, OidcCheckScope scope, CancellationToken cancellationToken = default)
    {
        var items = new List<OidcCheckItem>();
        var address = OidcService.DiscoveryAddress(request.Authority);
        var client = _httpClientFactory.CreateClient(IOidcService.HttpClientName);

        // 1. Discovery
        var discovery = await FetchDiscoveryAsync(client, address, cancellationToken);
        if (discovery.Document == null)
        {
            items.Add(new OidcCheckItem(OidcCheckIds.Discovery, OidcCheckStatus.Failed, discovery.FailureTitle!, discovery.FailureDetail, discovery.FailureCode));
            return new OidcCheckReport(items, null, false, request.RequirePar);
        }
        items.Add(new OidcCheckItem(OidcCheckIds.Discovery, OidcCheckStatus.Passed, "Discovery document reachable", address));

        var doc = discovery.Document;

        // 2. Issuer
        var issuer = doc.Issuer;
        if (string.IsNullOrEmpty(issuer))
        {
            items.Add(new OidcCheckItem(OidcCheckIds.Issuer, OidcCheckStatus.Failed, "Issuer missing",
                "The discovery document names no issuer.", "issuer_missing"));
        }
        else if (!string.Equals(issuer.TrimEnd('/'), request.Authority.Trim().TrimEnd('/'), StringComparison.Ordinal))
        {
            items.Add(new OidcCheckItem(OidcCheckIds.Issuer, OidcCheckStatus.Failed, "Issuer does not match the provider address",
                $"The provider reports {issuer}, but the provider address is {request.Authority.Trim()}. Use exactly the issuer the provider reports.",
                "issuer_mismatch"));
        }
        else
        {
            items.Add(new OidcCheckItem(OidcCheckIds.Issuer, OidcCheckStatus.Passed, "Issuer matches the provider address", issuer));
        }

        // 3. Endpoints
        var missing = new List<string>();
        // Endpoints are navigation and request targets: only absolute http(s) URLs count.
        if (!OidcEndpointUrls.IsHttp(doc.AuthorizationEndpoint)) missing.Add("authorization");
        if (!OidcEndpointUrls.IsHttp(doc.TokenEndpoint)) missing.Add("token");
        if (!OidcEndpointUrls.IsHttp(doc.JwksUri)) missing.Add("JWKS");
        items.Add(missing.Count == 0
            ? new OidcCheckItem(OidcCheckIds.Endpoints, OidcCheckStatus.Passed, "Endpoints present", "Authorization, token and JWKS endpoints found")
            : new OidcCheckItem(OidcCheckIds.Endpoints, OidcCheckStatus.Failed, "Endpoints missing",
                $"The discovery document names no {JoinWords(missing)} endpoint.", "endpoints_missing"));

        var parOffered = OidcEndpointUrls.IsHttp(doc.ParEndpoint);
        var parRequired = request.RequirePar || doc.RequirePar;

        if (scope == OidcCheckScope.Discovery)
        {
            return new OidcCheckReport(items, issuer, parOffered, parRequired);
        }

        // 4. PAR
        if (parRequired)
        {
            var requiredBy = request.RequirePar ? "the template" : "the provider";
            items.Add(parOffered
                ? new OidcCheckItem(OidcCheckIds.Par, OidcCheckStatus.Passed, "Pushed authorization requests (PAR)",
                    $"Required by {requiredBy}, PAR endpoint found")
                : new OidcCheckItem(OidcCheckIds.Par, OidcCheckStatus.Failed, "PAR endpoint missing",
                    $"Pushed authorization requests are required by {requiredBy}, but the provider offers no PAR endpoint.",
                    OidcErrorCodes.ParNotSupported));
        }
        else
        {
            items.Add(new OidcCheckItem(OidcCheckIds.Par, OidcCheckStatus.Skipped, "Pushed authorization requests (PAR)",
                parOffered ? "Offered by the provider, not used" : "Not offered by this provider and not required by the template"));
        }

        // 5. Client ID and secret, only through PAR (no sign-in needed).
        if (!parOffered)
        {
            items.Add(new OidcCheckItem(OidcCheckIds.Client, OidcCheckStatus.Skipped, "Client ID and secret",
                "Checked during the test sign-in"));
        }
        else if (string.IsNullOrEmpty(request.ClientId) || string.IsNullOrEmpty(request.RedirectUri))
        {
            items.Add(new OidcCheckItem(OidcCheckIds.Client, OidcCheckStatus.Failed, "Client ID missing",
                "Enter the client ID first.", "client_missing"));
        }
        else
        {
            items.Add(await CheckClientAsync(client, doc.ParEndpoint!, request, cancellationToken));
        }

        return new OidcCheckReport(items, issuer, parOffered, parRequired);
    }

    private async Task<OidcCheckItem> CheckClientAsync(HttpClient client, string parEndpoint, OidcCheckRequest request, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = request.ClientId!,
            ["response_type"] = "code",
            ["scope"] = request.Scopes,
            ["redirect_uri"] = request.RedirectUri!,
            ["state"] = RandomToken(),
            ["nonce"] = RandomToken(),
            ["code_challenge"] = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(RandomToken()))),
            ["code_challenge_method"] = "S256"
        };
        if (!string.IsNullOrEmpty(request.ClientSecret))
        {
            form["client_secret"] = request.ClientSecret;
        }

        try
        {
            using var response = await client.PostAsync(parEndpoint, new FormUrlEncodedContent(form), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new OidcCheckItem(OidcCheckIds.Client, OidcCheckStatus.Passed, "Client ID and secret accepted",
                    "Checked with a PAR request, nobody had to sign in");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var (error, description) = ReadError(json);

            if (error == "invalid_client" || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new OidcCheckItem(OidcCheckIds.Client, OidcCheckStatus.Failed, "Client ID and secret rejected",
                    "The provider answered invalid_client to a PAR request. Check the client ID and copy the client secret again from your provider.",
                    OidcErrorCodes.InvalidClient);
            }

            if (error is "invalid_redirect_uri" or "invalid_request" &&
                (error == "invalid_redirect_uri" || description?.Contains("redirect", StringComparison.OrdinalIgnoreCase) == true))
            {
                return new OidcCheckItem(OidcCheckIds.Client, OidcCheckStatus.Failed, "Redirect URI not accepted",
                    $"The provider does not accept {request.RedirectUri}. Register exactly this redirect URI at the provider.",
                    "redirect_uri_rejected");
            }

            return new OidcCheckItem(OidcCheckIds.Client, OidcCheckStatus.Failed, "PAR request rejected",
                $"The provider answered {(int)response.StatusCode}{(error != null ? $" ({error})" : "")}{(description != null ? $": {description}" : ".")}",
                error ?? OidcErrorCodes.Rejected);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException ||
                                   (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "PAR endpoint {Endpoint} not reachable during the connection check", parEndpoint);
            return new OidcCheckItem(OidcCheckIds.Client, OidcCheckStatus.Failed, "PAR endpoint not reachable",
                $"{parEndpoint} could not be reached.", OidcErrorCodes.Unreachable);
        }
    }

    private async Task<DiscoveryFetch> FetchDiscoveryAsync(HttpClient client, string address, CancellationToken cancellationToken)
    {
        Uri.TryCreate(address, UriKind.Absolute, out var uri);
        var host = uri?.Host ?? address;

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(address, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            var reason = ex.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "the name could not be resolved",
                HttpRequestError.ConnectionError => "the connection was refused",
                HttpRequestError.SecureConnectionError => "the TLS connection failed",
                _ => "the connection failed"
            };
            return DiscoveryFetch.Failed("Discovery document not reachable",
                $"{host} could not be reached: {reason}.", OidcErrorCodes.Unreachable);
        }
        catch (Exception ex) when (ex is IOException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return DiscoveryFetch.Failed("Discovery document not reachable",
                $"{host} did not answer in time.", OidcErrorCodes.Unreachable);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return DiscoveryFetch.Failed("Discovery document not found",
                    $"{address} answered 404 Not Found.", "not_found");
            }

            if (!response.IsSuccessStatusCode)
            {
                return DiscoveryFetch.Failed("Discovery document not reachable",
                    $"{address} answered {(int)response.StatusCode} {response.ReasonPhrase}.",
                    (int)response.StatusCode >= 500 ? OidcErrorCodes.Unreachable : "http_error");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new JsonException("Not an object.");
                }

                string? Str(string name) =>
                    root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

                var requirePar = root.TryGetProperty("require_pushed_authorization_requests", out var rp) &&
                                 rp.ValueKind == JsonValueKind.True;

                return new DiscoveryFetch(new DiscoveryDocument(
                    Str("issuer"),
                    Str("authorization_endpoint"),
                    Str("token_endpoint"),
                    Str("jwks_uri"),
                    Str("pushed_authorization_request_endpoint"),
                    requirePar), null, null, null);
            }
            catch (JsonException)
            {
                return DiscoveryFetch.Failed("Discovery document is not valid",
                    $"{address} did not return an OpenID Connect discovery document.", OidcErrorCodes.InvalidDiscovery);
            }
        }
    }

    private static string JoinWords(List<string> words) =>
        words.Count == 1 ? words[0] : $"{string.Join(", ", words.Take(words.Count - 1))} and {words[^1]}";

    /// <summary>A string property of a JSON object; null for other JSON values (an array, a number, ...).</summary>
    private static string? StringProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static (string? Error, string? Description) ReadError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return (StringProperty(root, "error"), StringProperty(root, "error_description"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string RandomToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record DiscoveryDocument(
        string? Issuer,
        string? AuthorizationEndpoint,
        string? TokenEndpoint,
        string? JwksUri,
        string? ParEndpoint,
        bool RequirePar);

    private sealed record DiscoveryFetch(DiscoveryDocument? Document, string? FailureTitle, string? FailureDetail, string? FailureCode)
    {
        public static DiscoveryFetch Failed(string title, string detail, string code) => new(null, title, detail, code);
    }
}
