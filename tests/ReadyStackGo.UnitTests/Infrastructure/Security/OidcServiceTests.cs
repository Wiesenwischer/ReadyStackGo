using System.Net;
using System.Security.Cryptography;
using System.Web;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Infrastructure.Security.Authentication;
using ReadyStackGo.UnitTests.TestSupport;

namespace ReadyStackGo.UnitTests.Infrastructure.Security;

public class OidcServiceTests : IDisposable
{
    private const string RedirectUri = "https://rsgo.example.com/api/auth/oidc/test/callback";

    private readonly FakeOidcProvider _idp = new();
    private readonly OidcService _sut;

    public OidcServiceTests()
    {
        _sut = new OidcService(_idp.Factory, NullLogger<OidcService>.Instance);
    }

    public void Dispose() => _idp.Dispose();

    private Task<OidcAuthorizeResult> Authorize(OidcProviderSettings? provider = null) =>
        _sut.BuildAuthorizeUrlAsync(provider ?? FakeOidcProvider.Settings(), RedirectUri, "state-1", "nonce-1", "challenge-1");

    private Task<OidcExchangeResult> Exchange(OidcProviderSettings? provider = null, string nonce = "nonce-1") =>
        _sut.ExchangeCodeAsync(provider ?? FakeOidcProvider.Settings(), "code-1", RedirectUri, "verifier-1", nonce);

    private static System.Collections.Specialized.NameValueCollection Query(string url) =>
        HttpUtility.ParseQueryString(new Uri(url).Query);

    #region Authorize without PAR

    [Fact]
    public async Task BuildAuthorizeUrl_ParNotRequired_RedirectsWithAllParametersAndNoPar()
    {
        _idp.PublishDiscovery(parEndpoint: true, requirePar: false);

        var result = await Authorize();

        result.Succeeded.Should().BeTrue();
        result.Url.Should().StartWith(FakeOidcProvider.AuthorizationEndpoint + "?");
        var query = Query(result.Url!);
        query["client_id"].Should().Be(FakeOidcProvider.ClientId);
        query["response_type"].Should().Be("code");
        query["scope"].Should().Be("openid profile email");
        query["redirect_uri"].Should().Be(RedirectUri);
        query["state"].Should().Be("state-1");
        query["nonce"].Should().Be("nonce-1");
        query["code_challenge"].Should().Be("challenge-1");
        query["code_challenge_method"].Should().Be("S256");
        query.AllKeys.Should().NotContain("client_secret");
        _idp.Handler.RequestsTo(FakeOidcProvider.ParEndpoint).Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAuthorizeUrl_UsesNamedHttpClient()
    {
        _idp.PublishDiscovery();

        await Authorize();

        _idp.Factory.RequestedNames.Should().NotBeEmpty().And.OnlyContain(n => n == IOidcService.HttpClientName);
    }

    #endregion

    #region Authorize with PAR

    [Fact]
    public async Task BuildAuthorizeUrl_DiscoveryRequiresPar_PushesRequestAndRedirectsWithRequestUriOnly()
    {
        _idp.PublishDiscovery(parEndpoint: true, requirePar: true);
        _idp.PublishParSuccess("urn:ietf:params:oauth:request_uri:xyz");

        var result = await Authorize();

        result.Succeeded.Should().BeTrue();
        result.Url.Should().StartWith(FakeOidcProvider.AuthorizationEndpoint + "?");
        var query = Query(result.Url!);
        query.AllKeys.Should().BeEquivalentTo("client_id", "request_uri");
        query["client_id"].Should().Be(FakeOidcProvider.ClientId);
        query["request_uri"].Should().Be("urn:ietf:params:oauth:request_uri:xyz");

        var par = _idp.Handler.RequestsTo(FakeOidcProvider.ParEndpoint).Should().ContainSingle().Subject;
        par.Method.Should().Be(HttpMethod.Post);
        var form = par.Form;
        form["client_id"].Should().Be(FakeOidcProvider.ClientId);
        form["client_secret"].Should().Be("client-secret");
        form["response_type"].Should().Be("code");
        form["redirect_uri"].Should().Be(RedirectUri);
        form["state"].Should().Be("state-1");
        form["nonce"].Should().Be("nonce-1");
        form["code_challenge"].Should().Be("challenge-1");
        form["code_challenge_method"].Should().Be("S256");
        form["scope"].Should().Be("openid profile email");
    }

    [Fact]
    public async Task BuildAuthorizeUrl_ProviderRequiresParButDiscoveryDoesNot_StillUsesPar()
    {
        _idp.PublishDiscovery(parEndpoint: true, requirePar: false);
        _idp.PublishParSuccess();

        var result = await Authorize(FakeOidcProvider.Settings(requirePar: true));

        result.Succeeded.Should().BeTrue();
        Query(result.Url!).AllKeys.Should().BeEquivalentTo("client_id", "request_uri");
        _idp.Handler.RequestsTo(FakeOidcProvider.ParEndpoint).Should().ContainSingle();
    }

    [Fact]
    public async Task BuildAuthorizeUrl_ParWithoutSecret_SendsNoSecret()
    {
        _idp.PublishDiscovery(requirePar: true);
        _idp.PublishParSuccess();

        await Authorize(FakeOidcProvider.Settings(secret: null));

        _idp.Handler.RequestsTo(FakeOidcProvider.ParEndpoint).Single().Form.Should().NotContainKey("client_secret");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task BuildAuthorizeUrl_ParRequiredWithoutEndpoint_FailsWithParNotSupported(bool discoveryRequires, bool providerRequires)
    {
        _idp.PublishDiscovery(parEndpoint: false, requirePar: discoveryRequires);

        var result = await Authorize(FakeOidcProvider.Settings(requirePar: providerRequires));

        result.Succeeded.Should().BeFalse();
        result.Url.Should().BeNull();
        result.Error.Should().Be(OidcErrorCodes.ParNotSupported);
        _idp.Handler.Requests.Should().NotContain(r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task BuildAuthorizeUrl_ParInvalidClient_FailsWithInvalidClient()
    {
        _idp.PublishDiscovery(requirePar: true);
        _idp.Handler.MapJson(FakeOidcProvider.ParEndpoint, """{"error":"invalid_client","error_description":"bad secret"}""", HttpStatusCode.BadRequest);

        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.InvalidClient);
        result.ErrorDescription.Should().Be("bad secret");
    }

    [Fact]
    public async Task BuildAuthorizeUrl_ParUnauthorizedWithoutBody_FailsWithInvalidClient()
    {
        _idp.PublishDiscovery(requirePar: true);
        _idp.Handler.Map(FakeOidcProvider.ParEndpoint, _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.InvalidClient);
    }

    [Fact]
    public async Task BuildAuthorizeUrl_ParRejectsRedirect_FailsWithRejected()
    {
        _idp.PublishDiscovery(requirePar: true);
        _idp.Handler.MapJson(FakeOidcProvider.ParEndpoint, """{"error":"invalid_request","error_description":"Invalid redirect_uri"}""", HttpStatusCode.BadRequest);

        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.Rejected);
        result.ErrorDescription.Should().Be("Invalid redirect_uri");
    }

    [Fact]
    public async Task BuildAuthorizeUrl_ParServerError_FailsWithUnreachable()
    {
        _idp.PublishDiscovery(requirePar: true);
        _idp.Handler.MapJson(FakeOidcProvider.ParEndpoint, "{}", HttpStatusCode.ServiceUnavailable);

        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.Unreachable);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"request_uri":""}""")]
    [InlineData("not json")]
    public async Task BuildAuthorizeUrl_ParSuccessWithoutRequestUri_FailsWithRejected(string body)
    {
        _idp.PublishDiscovery(requirePar: true);
        _idp.Handler.MapJson(FakeOidcProvider.ParEndpoint, body, HttpStatusCode.Created);

        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.Rejected);
    }

    [Fact]
    public async Task BuildAuthorizeUrl_ParNetworkError_FailsWithUnreachableWithoutException()
    {
        _idp.PublishDiscovery(requirePar: true);
        _idp.Handler.MapThrow(FakeOidcProvider.ParEndpoint, new HttpRequestException("connection refused"));

        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.Unreachable);
    }

    [Fact]
    public async Task BuildAuthorizeUrl_ParTimeout_FailsWithUnreachable()
    {
        _idp.PublishDiscovery(requirePar: true);
        _idp.Handler.MapThrow(FakeOidcProvider.ParEndpoint, new TaskCanceledException("timeout"));

        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.Unreachable);
    }

    #endregion

    #region Discovery errors

    [Fact]
    public async Task BuildAuthorizeUrl_DiscoveryNetworkError_FailsWithUnreachable()
    {
        _idp.Handler.MapThrow(FakeOidcProvider.DiscoveryUrl, new HttpRequestException("no route"));

        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.Unreachable);
    }

    [Fact]
    public async Task BuildAuthorizeUrl_DiscoveryNotJson_FailsWithInvalidDiscovery()
    {
        _idp.Handler.MapJson(FakeOidcProvider.DiscoveryUrl, "<html>login page</html>");

        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.InvalidDiscovery);
    }

    [Fact]
    public async Task BuildAuthorizeUrl_DiscoveryNotFound_FailsWithInvalidDiscovery()
    {
        // No route for the discovery URL: the fake answers 404.
        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.InvalidDiscovery);
    }

    [Fact]
    public async Task BuildAuthorizeUrl_DiscoveryServerError_FailsWithUnreachable()
    {
        _idp.Handler.MapJson(FakeOidcProvider.DiscoveryUrl, "{}", HttpStatusCode.BadGateway);

        var result = await Authorize();

        result.Error.Should().Be(OidcErrorCodes.Unreachable);
    }

    [Fact]
    public async Task BuildAuthorizeUrl_DiscoveryFailedOnce_RetriesOnNextCall()
    {
        _idp.Handler.MapThrow(FakeOidcProvider.DiscoveryUrl, new HttpRequestException("down"));
        (await Authorize()).Error.Should().Be(OidcErrorCodes.Unreachable);

        _idp.PublishDiscovery(requirePar: false);
        var second = await Authorize();

        second.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task BuildAuthorizeUrl_AuthorityWithTrailingSlash_UsesSameDiscoveryAddress()
    {
        _idp.PublishDiscovery(requirePar: false);
        var provider = FakeOidcProvider.Settings();
        provider.Authority = FakeOidcProvider.Authority + "/";

        var result = await Authorize(provider);

        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Token exchange

    [Fact]
    public async Task ExchangeCode_ValidIdToken_ReturnsUserInfoAndPostsPkce()
    {
        _idp.PublishDiscovery();
        _idp.PublishToken(_idp.CreateIdToken(new Dictionary<string, object?>
        {
            ["email"] = "alice@example.com",
            ["email_verified"] = true,
            ["preferred_username"] = "alice",
            ["name"] = "Alice Example"
        }));

        var result = await Exchange();

        result.Succeeded.Should().BeTrue(result.ErrorDescription);
        result.UserInfo.Should().Be(new OidcUserInfo("subject-1", "alice@example.com", true, "alice", "Alice Example"));
        var form = _idp.Handler.RequestsTo(FakeOidcProvider.TokenEndpoint).Should().ContainSingle().Subject.Form;
        form["grant_type"].Should().Be("authorization_code");
        form["code"].Should().Be("code-1");
        form["redirect_uri"].Should().Be(RedirectUri);
        form["client_id"].Should().Be(FakeOidcProvider.ClientId);
        form["client_secret"].Should().Be("client-secret");
        form["code_verifier"].Should().Be("verifier-1");
    }

    [Fact]
    public async Task ExchangeCode_WithoutSecret_SendsNoSecret()
    {
        _idp.PublishDiscovery();
        _idp.PublishToken(_idp.CreateIdToken());

        await Exchange(FakeOidcProvider.Settings(secret: ""));

        _idp.Handler.RequestsTo(FakeOidcProvider.TokenEndpoint).Single().Form.Should().NotContainKey("client_secret");
    }

    [Fact]
    public async Task ExchangeCode_CustomClaimNames_AreUsed()
    {
        _idp.PublishDiscovery();
        _idp.PublishToken(_idp.CreateIdToken(new Dictionary<string, object?>
        {
            ["name"] = "Full Name",
            ["nickname"] = "Nick",
            ["preferred_username"] = "standard",
            ["login"] = "custom_login",
            ["mail"] = "custom@example.com",
            ["email"] = "standard@example.com"
        }));
        var claims = new OidcClaimNames { DisplayName = "nickname", Username = "login", Email = "mail" };

        var result = await Exchange(FakeOidcProvider.Settings(claims: claims));

        result.UserInfo!.DisplayName.Should().Be("Nick");
        result.UserInfo.Username.Should().Be("custom_login");
        result.UserInfo.Email.Should().Be("custom@example.com");
    }

    [Fact]
    public async Task ExchangeCode_ClaimsMissingOrEmpty_AreNull()
    {
        _idp.PublishDiscovery();
        _idp.PublishToken(_idp.CreateIdToken(new Dictionary<string, object?> { ["email"] = "" }));

        var result = await Exchange();

        result.UserInfo!.Email.Should().BeNull();
        result.UserInfo.Username.Should().BeNull();
        result.UserInfo.DisplayName.Should().BeNull();
        result.UserInfo.EmailVerified.Should().BeFalse();
    }

    public static TheoryData<object?, bool> EmailVerifiedValues => new()
    {
        { true, true },
        { "true", true },
        { "True", true },
        { false, false },
        { "false", false },
        { "yes", false },
        { null, false }
    };

    [Theory]
    [MemberData(nameof(EmailVerifiedValues))]
    public async Task ExchangeCode_EmailVerifiedClaim_IsInterpreted(object? value, bool expected)
    {
        _idp.PublishDiscovery();
        _idp.PublishToken(_idp.CreateIdToken(new Dictionary<string, object?>
        {
            ["email"] = "alice@example.com",
            ["email_verified"] = value
        }));

        var result = await Exchange();

        result.Succeeded.Should().BeTrue(result.ErrorDescription);
        result.UserInfo!.EmailVerified.Should().Be(expected);
    }

    [Fact]
    public async Task ExchangeCode_InvalidClient_FailsWithInvalidClient()
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapJson(FakeOidcProvider.TokenEndpoint, """{"error":"invalid_client"}""", HttpStatusCode.Unauthorized);

        var result = await Exchange();

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(OidcErrorCodes.InvalidClient);
    }

    [Fact]
    public async Task ExchangeCode_InvalidGrant_FailsWithRejected()
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapJson(FakeOidcProvider.TokenEndpoint, """{"error":"invalid_grant","error_description":"Code expired"}""", HttpStatusCode.BadRequest);

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.Rejected);
        result.ErrorDescription.Should().Be("Code expired");
    }

    [Fact]
    public async Task ExchangeCode_ServerError_FailsWithUnreachable()
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapJson(FakeOidcProvider.TokenEndpoint, "oops", HttpStatusCode.InternalServerError);

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.Unreachable);
    }

    [Fact]
    public async Task ExchangeCode_NetworkError_FailsWithUnreachableWithoutException()
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapThrow(FakeOidcProvider.TokenEndpoint, new HttpRequestException("reset"));

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.Unreachable);
    }

    [Fact]
    public async Task ExchangeCode_Timeout_FailsWithUnreachable()
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapThrow(FakeOidcProvider.TokenEndpoint, new TaskCanceledException("timeout"));

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.Unreachable);
    }

    [Fact]
    public async Task ExchangeCode_DiscoveryUnreachable_FailsWithUnreachable()
    {
        _idp.Handler.MapThrow(FakeOidcProvider.DiscoveryUrl, new HttpRequestException("down"));

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.Unreachable);
        _idp.Handler.RequestsTo(FakeOidcProvider.TokenEndpoint).Should().BeEmpty();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"access_token":"at"}""")]
    [InlineData("""{"id_token":""}""")]
    public async Task ExchangeCode_ResponseWithoutIdToken_FailsWithInvalidToken(string body)
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapJson(FakeOidcProvider.TokenEndpoint, body);

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.InvalidToken);
    }

    [Fact]
    public async Task ExchangeCode_NonceMismatch_FailsWithInvalidToken()
    {
        _idp.PublishDiscovery();
        _idp.PublishToken(_idp.CreateIdToken());

        var result = await Exchange(nonce: "other-nonce");

        result.Error.Should().Be(OidcErrorCodes.InvalidToken);
    }

    [Fact]
    public async Task ExchangeCode_NonceMissing_FailsWithInvalidToken()
    {
        _idp.PublishDiscovery();
        _idp.PublishToken(_idp.CreateIdToken(new Dictionary<string, object?> { ["nonce"] = null }));

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.InvalidToken);
    }

    [Fact]
    public async Task ExchangeCode_SubjectMissing_FailsWithInvalidToken()
    {
        _idp.PublishDiscovery();
        _idp.PublishToken(_idp.CreateIdToken(new Dictionary<string, object?> { ["sub"] = null }));

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.InvalidToken);
    }

    [Fact]
    public async Task ExchangeCode_WrongAudience_FailsWithInvalidToken()
    {
        _idp.PublishDiscovery();
        _idp.PublishToken(_idp.CreateIdToken(new Dictionary<string, object?> { ["aud"] = "other-client" }));

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.InvalidToken);
    }

    [Fact]
    public async Task ExchangeCode_WrongIssuer_FailsWithInvalidToken()
    {
        _idp.PublishDiscovery();
        _idp.PublishToken(_idp.CreateIdToken(new Dictionary<string, object?> { ["iss"] = "https://evil.test" }));

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.InvalidToken);
    }

    [Fact]
    public async Task ExchangeCode_Expired_FailsWithInvalidToken()
    {
        _idp.PublishDiscovery();
        var now = DateTime.UtcNow;
        _idp.PublishToken(_idp.CreateIdToken(new Dictionary<string, object?>
        {
            ["iat"] = EpochTime.GetIntDate(now.AddHours(-2)),
            ["nbf"] = EpochTime.GetIntDate(now.AddHours(-2))
        }, expires: now.AddHours(-1)));

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.InvalidToken);
    }

    [Fact]
    public async Task ExchangeCode_SignedWithUnknownKey_FailsWithInvalidToken()
    {
        _idp.PublishDiscovery();
        using var otherRsa = RSA.Create(2048);
        var otherKey = new RsaSecurityKey(otherRsa) { KeyId = FakeOidcProvider.KeyId };
        _idp.PublishToken(_idp.CreateIdToken(signingKey: otherKey));

        var result = await Exchange();

        result.Error.Should().Be(OidcErrorCodes.InvalidToken);
    }

    #endregion

    [Theory]
    [InlineData("https://idp.test", "https://idp.test/.well-known/openid-configuration")]
    [InlineData("https://idp.test/", "https://idp.test/.well-known/openid-configuration")]
    [InlineData("https://idp.test/realms/main//", "https://idp.test/realms/main/.well-known/openid-configuration")]
    public void DiscoveryAddress_TrimsTrailingSlashes(string authority, string expected)
    {
        OidcService.DiscoveryAddress(authority).Should().Be(expected);
    }
}
