using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace ReadyStackGo.IntegrationTests.Sso;

/// <summary>
/// The test identity provider itself: discovery, PAR, authorize, token with PKCE, pairing and
/// the test controls. Other tests rely on these rules (and on their error answers).
/// </summary>
public class TestIdentityProviderTests : IAsyncLifetime
{
    private const string Issuer = TestIdentityProviderHost.Issuer;

    private TestIdentityProviderHost _idp = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _idp = await TestIdentityProviderHost.StartAsync();
        _client = _idp.CreateBrowserClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _idp.DisposeAsync();
    }

    [Fact]
    public async Task Discovery_RequiresPar_HasNoUserinfo_OffersPairing()
    {
        var doc = await _client.GetFromJsonAsync<JsonElement>(".well-known/openid-configuration");

        doc.GetProperty("issuer").GetString().Should().Be(Issuer);
        doc.GetProperty("require_pushed_authorization_requests").GetBoolean().Should().BeTrue();
        doc.GetProperty("pushed_authorization_request_endpoint").GetString().Should().Be($"{Issuer}par");
        doc.TryGetProperty("userinfo_endpoint", out _).Should().BeFalse();
        doc.GetProperty("wysch_pairing_endpoint").GetString().Should().Be($"{Issuer}pairing");
        doc.GetProperty("wysch_pairing_token_endpoint").GetString().Should().Be($"{Issuer}pairing/token");
    }

    [Fact]
    public async Task WrongIssuerPath_ReportsAnIssuerDifferentFromItsAddress()
    {
        var doc = await _client.GetFromJsonAsync<JsonElement>("wrong-issuer/.well-known/openid-configuration");

        doc.GetProperty("issuer").GetString().Should().Be(Issuer).And.NotStartWith($"{Issuer}wrong-issuer");
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Jwks_ContainsOnlyThePublicKey()
    {
        var jwks = await _client.GetStringAsync("jwks");
        var key = JsonDocument.Parse(jwks).RootElement.GetProperty("keys")[0];

        key.GetProperty("kty").GetString().Should().Be("RSA");
        key.TryGetProperty("d", out _).Should().BeFalse("the private exponent must never be published");
        key.TryGetProperty("p", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(TestIdentityProviderHost.ManualClientId, "wrong-secret")]
    [InlineData("unknown-client", TestIdentityProviderHost.ManualClientSecret)]
    [InlineData("", "")]
    public async Task Par_WithBadClientAuthentication_Returns401InvalidClient(string clientId, string secret)
    {
        var response = await PushAsync(clientId, secret, TestIdentityProviderHost.RedirectUri("oidc"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorOf(response)).Should().Be("invalid_client");
    }

    [Fact]
    public async Task Par_WithUnregisteredRedirectUri_ReturnsInvalidRequestNamingTheRedirect()
    {
        var response = await PushAsync(TestIdentityProviderHost.ManualClientId, TestIdentityProviderHost.ManualClientSecret,
            "http://localhost:5000/api/auth/oidc/other/callback");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("invalid_request");
        body.GetProperty("error_description").GetString().Should().Contain("redirect_uri");
    }

    [Fact]
    public async Task Par_WithoutPkce_IsRejected()
    {
        var response = await _client.PostAsync("par", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = TestIdentityProviderHost.ManualClientId,
            ["client_secret"] = TestIdentityProviderHost.ManualClientSecret,
            ["response_type"] = "code",
            ["scope"] = "openid",
            ["redirect_uri"] = TestIdentityProviderHost.RedirectUri("oidc")
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOf(response)).Should().Be("invalid_request");
    }

    [Fact]
    public async Task Authorize_WithParametersBesidesRequestUri_IsRejected()
    {
        var response = await _client.GetAsync($"authorize?client_id={TestIdentityProviderHost.ManualClientId}&redirect_uri=x&scope=openid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("only client_id and request_uri");
    }

    [Fact]
    public async Task Authorize_WithUnknownRequestUri_IsRejected()
    {
        var response = await _client.GetAsync($"authorize?client_id={TestIdentityProviderHost.ManualClientId}&request_uri=urn:unknown");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Authorize_ShowsTestUsersAndCancel()
    {
        var requestUri = await PushOkAsync("challenge");

        var html = await _client.GetStringAsync($"authorize?client_id={TestIdentityProviderHost.ManualClientId}&request_uri={Uri.EscapeDataString(requestUri)}");

        html.Should().Contain(">Sign in as Alex Verified</button>")
            .And.Contain(">Sign in as Uma Unverified</button>")
            .And.Contain(">Sign in as Noah No Username</button>")
            .And.Contain(">Cancel</button>");
    }

    [Fact]
    public async Task FullCodeFlow_IssuesSignedIdTokenWithClaimsAndNonce()
    {
        var (verifier, challenge) = Pkce();
        var requestUri = await PushOkAsync(challenge, nonce: "n-123", state: "s-1");

        var code = await DecideAsync(requestUri, "alex", expectedState: "s-1");
        var response = await RedeemAsync(code, verifier);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var idToken = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id_token").GetString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        jwt.Issuer.Should().Be(Issuer);
        jwt.Audiences.Should().Equal(TestIdentityProviderHost.ManualClientId);
        jwt.Claims.First(c => c.Type == "sub").Value.Should().Be("sub-alex-verified");
        jwt.Claims.First(c => c.Type == "nonce").Value.Should().Be("n-123");
        jwt.Claims.First(c => c.Type == "email_verified").Value.Should().Be("true");
        jwt.Claims.First(c => c.Type == "preferred_username").Value.Should().Be("alex");
        jwt.Claims.First(c => c.Type == "nickname").Value.Should().Be("alex");
    }

    [Fact]
    public async Task Token_ForUserWithoutUsername_HasNoPreferredUsername_AndUnverifiedUserIsMarked()
    {
        var (verifier, challenge) = Pkce();
        var code = await DecideAsync(await PushOkAsync(challenge), "noah");
        var idToken = (await (await RedeemAsync(code, verifier)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id_token").GetString()!;
        new JwtSecurityTokenHandler().ReadJwtToken(idToken).Claims.Should().NotContain(c => c.Type == "preferred_username");

        (verifier, challenge) = Pkce();
        code = await DecideAsync(await PushOkAsync(challenge), "uma");
        idToken = (await (await RedeemAsync(code, verifier)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id_token").GetString()!;
        new JwtSecurityTokenHandler().ReadJwtToken(idToken).Claims.First(c => c.Type == "email_verified").Value.Should().Be("false");
    }

    [Fact]
    public async Task Token_WithWrongVerifier_IsInvalidGrant()
    {
        var (_, challenge) = Pkce();
        var code = await DecideAsync(await PushOkAsync(challenge), "alex");

        var response = await RedeemAsync(code, "not-the-verifier");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOf(response)).Should().Be("invalid_grant");
    }

    [Fact]
    public async Task Token_CodeIsSingleUse()
    {
        var (verifier, challenge) = Pkce();
        var code = await DecideAsync(await PushOkAsync(challenge), "alex");

        (await RedeemAsync(code, verifier)).StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await RedeemAsync(code, verifier);

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOf(second)).Should().Be("invalid_grant");
    }

    [Fact]
    public async Task Token_WithWrongSecret_IsInvalidClient()
    {
        var (verifier, challenge) = Pkce();
        var code = await DecideAsync(await PushOkAsync(challenge), "alex");

        var response = await RedeemAsync(code, verifier, secret: "wrong");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorOf(response)).Should().Be("invalid_client");
    }

    [Fact]
    public async Task Cancel_RedirectsWithAccessDeniedAndState()
    {
        var requestUri = await PushOkAsync("challenge", state: "s-9");

        var response = await _client.PostAsync("authorize/decision", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["request_uri"] = requestUri,
            ["cancel"] = "true"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith(TestIdentityProviderHost.RedirectUri("oidc")).And.Contain("error=access_denied").And.Contain("state=s-9");
        location.Should().NotContain("code=");
    }

    [Fact]
    public async Task RevokedClient_IsRejectedAtPar()
    {
        (await _client.PostAsync($"test/clients/{TestIdentityProviderHost.ManualClientId}/revoke", null)).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var response = await PushAsync(TestIdentityProviderHost.ManualClientId, TestIdentityProviderHost.ManualClientSecret,
            TestIdentityProviderHost.RedirectUri("oidc"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.PostAsync("test/clients/unknown/revoke", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pairing_ConnectReturnsCode_RedeemedOnceForCredentials()
    {
        var (returnLocation, state) = await PairAsync("connect");
        returnLocation.Should().StartWith("http://localhost:5000/api/sso/registration/callback?code=");
        var code = System.Web.HttpUtility.ParseQueryString(new Uri(returnLocation).Query)["code"]!;

        // Wrong state first: rejected, and the code stays redeemable.
        var wrongState = await RedeemPairingAsync(code, "other-state");
        wrongState.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOf(wrongState)).Should().Be("invalid_grant");

        var ok = await RedeemPairingAsync(code, state);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("client_id").GetString().Should().StartWith("paired-");
        body.GetProperty("client_secret").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("issuer").GetString().Should().Be(Issuer);

        var again = await RedeemPairingAsync(code, state);
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOf(again)).Should().Be("invalid_grant");

        // The new client is registered with the manifest's redirect URI.
        var par = await PushAsync(body.GetProperty("client_id").GetString()!, body.GetProperty("client_secret").GetString()!,
            TestIdentityProviderHost.RedirectUri("wysch"));
        par.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Pairing_Cancel_ReturnsAccessDenied()
    {
        var (returnLocation, _) = await PairAsync("cancel");

        returnLocation.Should().Contain("error=access_denied").And.NotContain("code=");
    }

    [Fact]
    public async Task Pairing_WithInvalidManifest_ShowsError()
    {
        var response = await _client.PostAsync("pairing", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["manifest"] = "{\"kind\":\"readystackgo\",\"name\":\"x\",\"url\":\"not-a-url\"}",
            ["state"] = "s"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PairingToken_WithUnknownCode_IsInvalidGrant()
    {
        var response = await RedeemPairingAsync("unknown", "state");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorOf(response)).Should().Be("invalid_grant");
    }

    // ------------------------------------------------------------------ helpers

    private Task<HttpResponseMessage> PushAsync(string clientId, string secret, string redirectUri, string challenge = "challenge",
        string? nonce = null, string? state = null)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["response_type"] = "code",
            ["scope"] = "openid profile email",
            ["redirect_uri"] = redirectUri,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };
        if (nonce != null) form["nonce"] = nonce;
        if (state != null) form["state"] = state;
        return _client.PostAsync("par", new FormUrlEncodedContent(form));
    }

    private async Task<string> PushOkAsync(string challenge, string? nonce = null, string? state = null)
    {
        var response = await PushAsync(TestIdentityProviderHost.ManualClientId, TestIdentityProviderHost.ManualClientSecret,
            TestIdentityProviderHost.RedirectUri("oidc"), challenge, nonce, state);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("request_uri").GetString()!;
    }

    private async Task<string> DecideAsync(string requestUri, string user, string? expectedState = null)
    {
        var response = await _client.PostAsync("authorize/decision", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["request_uri"] = requestUri,
            ["user"] = user
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var query = System.Web.HttpUtility.ParseQueryString(response.Headers.Location!.Query);
        if (expectedState != null)
        {
            query["state"].Should().Be(expectedState);
        }
        return query["code"]!;
    }

    private Task<HttpResponseMessage> RedeemAsync(string code, string verifier, string secret = TestIdentityProviderHost.ManualClientSecret) =>
        _client.PostAsync("token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = TestIdentityProviderHost.RedirectUri("oidc"),
            ["client_id"] = TestIdentityProviderHost.ManualClientId,
            ["client_secret"] = secret,
            ["code_verifier"] = verifier
        }));

    private async Task<(string ReturnLocation, string State)> PairAsync(string decision)
    {
        const string state = "pairing-state-1";
        var manifest = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kind"] = "readystackgo",
            ["name"] = "ReadyStackGo (localhost:5000)",
            ["url"] = "http://localhost:5000",
            ["redirect_uri"] = TestIdentityProviderHost.RedirectUri("wysch"),
            ["return_uri"] = "http://localhost:5000/api/sso/registration/callback"
        });
        var page = await _client.PostAsync("pairing", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["manifest"] = manifest,
            ["state"] = state
        }));
        var html = await page.Content.ReadAsStringAsync();
        page.StatusCode.Should().Be(HttpStatusCode.OK, html);
        var pairingId = System.Text.RegularExpressions.Regex.Match(html, "name=\"pairing\" value=\"([^\"]+)\"").Groups[1].Value;

        var response = await _client.PostAsync("pairing/decision", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["pairing"] = pairingId,
            ["decision"] = decision
        }));
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain($"state={state}");
        return (response.Headers.Location!.ToString(), state);
    }

    private Task<HttpResponseMessage> RedeemPairingAsync(string code, string state) =>
        _client.PostAsync("pairing/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["state"] = state
        }));

    private static async Task<string?> ErrorOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();

    private static (string Verifier, string Challenge) Pkce()
    {
        var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (verifier, Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }
}
