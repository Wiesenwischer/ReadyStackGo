using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services.IdentityProviders;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.Infrastructure.Security.IdentityProviders;

/// <summary>
/// Registration kind "manual": the user registers ReadyStackGo at the provider and enters
/// client ID and secret. Nothing to start or redeem.
/// </summary>
public class ManualRegistrationMethod : IClientRegistrationMethod
{
    public string Kind => RegistrationKinds.Manual;

    public RegistrationInteraction Interaction => RegistrationInteraction.ManualEntry;

    public Task<RegistrationStart> StartAsync(RegistrationContext context, CancellationToken cancellationToken) =>
        throw new ClientRegistrationException("not_supported", "Manual registration has no browser step.");

    public Task<RegisteredClient> CompleteAsync(RegistrationContext context, string code, CancellationToken cancellationToken) =>
        throw new ClientRegistrationException("not_supported", "Manual registration has no code to redeem.");
}

/// <summary>
/// Registration kind "pairing" after the WYSCH client pairing protocol (modelled on the GitHub
/// App manifest flow, Wiesenwischer/WYSCH docs/specs/client-kopplung.md and its plan):
/// <list type="number">
/// <item>The browser posts <c>manifest</c> (kind, name, url, redirect_uri, return_uri) and <c>state</c>
/// to <c>wysch_pairing_endpoint</c>.</item>
/// <item>The user signs in at the provider and confirms.</item>
/// <item>The provider returns to <c>return_uri</c> with <c>code</c> and <c>state</c>
/// (or <c>error=access_denied</c> / <c>limit_reached</c>).</item>
/// <item>The server redeems <c>code</c> and <c>state</c> at <c>wysch_pairing_token_endpoint</c> and
/// receives <c>client_id</c>, <c>client_secret</c> and <c>issuer</c>.</item>
/// </list>
/// ReadyStackGo does not need to be reachable from the provider; all returns go through the browser.
/// </summary>
public class PairingRegistrationMethod : IClientRegistrationMethod
{
    /// <summary>The application kind ReadyStackGo announces in the manifest.</summary>
    public const string ApplicationKind = "readystackgo";

    private const int MaxNameLength = 64;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PairingRegistrationMethod> _logger;

    public PairingRegistrationMethod(IHttpClientFactory httpClientFactory, ILogger<PairingRegistrationMethod> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Kind => RegistrationKinds.Pairing;

    public RegistrationInteraction Interaction => RegistrationInteraction.Connect;

    public async Task<RegistrationStart> StartAsync(RegistrationContext context, CancellationToken cancellationToken)
    {
        var endpoints = await GetEndpointsAsync(context.Authority, cancellationToken);

        var name = context.InstallationName.Trim();
        if (name.Length > MaxNameLength)
        {
            name = name[..MaxNameLength];
        }

        var manifest = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kind"] = ApplicationKind,
            ["name"] = name,
            ["url"] = context.BaseUrl,
            ["redirect_uri"] = context.RedirectUri,
            ["return_uri"] = context.ReturnUri
        });

        return RegistrationStart.FormPost(endpoints.Pairing, new Dictionary<string, string>
        {
            ["manifest"] = manifest,
            ["state"] = context.State
        });
    }

    public async Task<RegisteredClient> CompleteAsync(RegistrationContext context, string code, CancellationToken cancellationToken)
    {
        var endpoints = await GetEndpointsAsync(context.Authority, cancellationToken);
        var client = _httpClientFactory.CreateClient(IOidcService.HttpClientName);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(endpoints.Token, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["state"] = context.State
            }), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException ||
                                   (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Pairing token endpoint {Endpoint} not reachable", endpoints.Token);
            throw new ClientRegistrationException("unreachable", "The provider could not be reached to finish the connection.", ex);
        }

        using (response)
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new ClientRegistrationException("rate_limited", "The provider received too many connection requests. Try again later.");
            }

            if ((int)response.StatusCode >= 500)
            {
                throw new ClientRegistrationException("unreachable", $"The provider answered {(int)response.StatusCode} while finishing the connection.");
            }

            using var doc = TryParse(json);
            var root = doc?.RootElement;

            if (!response.IsSuccessStatusCode)
            {
                var error = Str(root, "error");
                throw error == "invalid_grant"
                    ? new ClientRegistrationException("invalid_grant", "The connection code expired or was already used. Connect again.")
                    : new ClientRegistrationException("invalid_request", Str(root, "error_description") ?? "The provider rejected the connection request.");
            }

            var clientId = Str(root, "client_id");
            var clientSecret = Str(root, "client_secret");
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new ClientRegistrationException("invalid_request", "The provider returned no client credentials.");
            }

            return new RegisteredClient(clientId, clientSecret, Str(root, "issuer"), DateTime.UtcNow, Str(root, "confirmed_by"));
        }
    }

    private async Task<PairingEndpoints> GetEndpointsAsync(string authority, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(IOidcService.HttpClientName);
        var address = OidcService.DiscoveryAddress(authority);

        string json;
        try
        {
            using var response = await client.GetAsync(address, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new ClientRegistrationException(
                    (int)response.StatusCode >= 500 ? "unreachable" : "not_supported",
                    $"The discovery document of {authority} answered {(int)response.StatusCode}.");
            }
            json = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException ||
                                   (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Discovery {Address} not reachable for pairing", address);
            throw new ClientRegistrationException("unreachable", $"{authority} could not be reached.", ex);
        }

        using var doc = TryParse(json);
        var pairing = Str(doc?.RootElement, "wysch_pairing_endpoint");
        var token = Str(doc?.RootElement, "wysch_pairing_token_endpoint");
        if (string.IsNullOrEmpty(pairing) || string.IsNullOrEmpty(token))
        {
            throw new ClientRegistrationException("not_supported", "The provider does not offer client pairing.");
        }

        return new PairingEndpoints(pairing, token);
    }

    private static JsonDocument? TryParse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Str(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed record PairingEndpoints(string Pairing, string Token);
}
