using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.UnitTests.TestSupport;

/// <summary>A request as seen by <see cref="FakeHttpHandler"/>, with the body already read.</summary>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body)
{
    public Dictionary<string, string> Form =>
        Body == null
            ? new Dictionary<string, string>()
            : Body.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(p => Uri.UnescapeDataString(p[0].Replace('+', ' ')), p => p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : string.Empty);
}

/// <summary>
/// HTTP handler that answers by absolute URL. Unknown URLs answer 404. A route can also throw
/// to simulate network errors.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<RecordedRequest, HttpResponseMessage>> _routes = new(StringComparer.Ordinal);

    public List<RecordedRequest> Requests { get; } = new();

    public void Map(string url, Func<RecordedRequest, HttpResponseMessage> respond) => _routes[url] = respond;

    public void MapJson(string url, string json, HttpStatusCode status = HttpStatusCode.OK) =>
        Map(url, _ => Json(json, status));

    public void MapThrow(string url, Exception exception) => Map(url, _ => throw exception);

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public IEnumerable<RecordedRequest> RequestsTo(string url) => Requests.Where(r => r.Uri.GetLeftPart(UriPartial.Path) == url);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(request.Method, request.RequestUri!, body);
        Requests.Add(recorded);

        var key = request.RequestUri!.GetLeftPart(UriPartial.Path);
        return _routes.TryGetValue(key, out var respond)
            ? respond(recorded)
            : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("not found") };
    }
}

internal sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public List<string> RequestedNames { get; } = new();

    public HttpClient CreateClient(string name)
    {
        RequestedNames.Add(name);
        return new HttpClient(handler, disposeHandler: false);
    }
}

/// <summary>
/// An OpenID provider on top of <see cref="FakeHttpHandler"/>: discovery, JWKS, PAR and token
/// endpoints, with an RSA key that signs id tokens.
/// </summary>
internal sealed class FakeOidcProvider : IDisposable
{
    public const string Authority = "https://idp.test/realms/main";
    public const string ClientId = "rsgo-client";
    public const string KeyId = "key-1";

    private readonly RSA _rsa = RSA.Create(2048);

    public FakeOidcProvider()
    {
        Handler = new FakeHttpHandler();
        Factory = new FakeHttpClientFactory(Handler);
        Handler.Map(JwksUrl, _ => FakeHttpHandler.Json(Jwks()));
    }

    public FakeHttpHandler Handler { get; }
    public FakeHttpClientFactory Factory { get; }

    public string Issuer { get; set; } = Authority;
    public static string DiscoveryUrl => Authority + "/.well-known/openid-configuration";
    public static string AuthorizationEndpoint => Authority + "/protocol/openid-connect/auth";
    public static string TokenEndpoint => Authority + "/protocol/openid-connect/token";
    public static string ParEndpoint => Authority + "/protocol/openid-connect/ext/par/request";
    public static string JwksUrl => Authority + "/protocol/openid-connect/certs";

    public SecurityKey SigningKey => new RsaSecurityKey(_rsa) { KeyId = KeyId };

    /// <summary>Publishes a discovery document with the given options.</summary>
    public void PublishDiscovery(bool parEndpoint = true, bool requirePar = false, string? issuer = null)
    {
        var doc = new Dictionary<string, object>
        {
            ["issuer"] = issuer ?? Issuer,
            ["authorization_endpoint"] = AuthorizationEndpoint,
            ["token_endpoint"] = TokenEndpoint,
            ["jwks_uri"] = JwksUrl,
            ["response_types_supported"] = new[] { "code" },
            ["subject_types_supported"] = new[] { "public" },
            ["id_token_signing_alg_values_supported"] = new[] { "RS256" }
        };
        if (parEndpoint)
        {
            doc["pushed_authorization_request_endpoint"] = ParEndpoint;
        }
        if (requirePar)
        {
            doc["require_pushed_authorization_requests"] = true;
        }
        Handler.MapJson(DiscoveryUrl, JsonSerializer.Serialize(doc));
    }

    public void PublishParSuccess(string requestUri = "urn:ietf:params:oauth:request_uri:abc") =>
        Handler.MapJson(ParEndpoint, JsonSerializer.Serialize(new { request_uri = requestUri, expires_in = 60 }), HttpStatusCode.Created);

    public void PublishToken(string idToken) =>
        Handler.MapJson(TokenEndpoint, JsonSerializer.Serialize(new { access_token = "at", token_type = "Bearer", id_token = idToken }));

    public static OidcProviderSettings Settings(bool requirePar = false, OidcClaimNames? claims = null, string? secret = "client-secret") => new()
    {
        Name = "test",
        DisplayName = "Test",
        Authority = Authority,
        ClientId = ClientId,
        ClientSecret = secret,
        Scopes = "openid profile email",
        RequirePar = requirePar,
        Claims = claims ?? new OidcClaimNames()
    };

    /// <summary>Creates a signed id token. <paramref name="claims"/> override or add payload entries (null removes).</summary>
    public string CreateIdToken(
        IDictionary<string, object?>? claims = null,
        SecurityKey? signingKey = null,
        DateTime? expires = null)
    {
        var now = DateTime.UtcNow;
        var payload = new JwtPayload
        {
            ["iss"] = Issuer,
            ["aud"] = ClientId,
            ["sub"] = "subject-1",
            ["nonce"] = "nonce-1",
            ["iat"] = EpochTime.GetIntDate(now.AddMinutes(-1)),
            ["nbf"] = EpochTime.GetIntDate(now.AddMinutes(-1)),
            ["exp"] = EpochTime.GetIntDate(expires ?? now.AddMinutes(10))
        };
        if (claims != null)
        {
            foreach (var (key, value) in claims)
            {
                if (value == null)
                {
                    payload.Remove(key);
                }
                else
                {
                    payload[key] = value;
                }
            }
        }

        var credentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(new JwtHeader(credentials), payload);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string Jwks()
    {
        var parameters = _rsa.ExportParameters(includePrivateParameters: false);
        return JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = KeyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus),
                    e = Base64UrlEncoder.Encode(parameters.Exponent)
                }
            }
        });
    }

    public void Dispose() => _rsa.Dispose();
}
