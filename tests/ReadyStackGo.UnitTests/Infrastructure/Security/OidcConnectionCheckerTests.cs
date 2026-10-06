using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Infrastructure.Security.Authentication;
using ReadyStackGo.UnitTests.TestSupport;

namespace ReadyStackGo.UnitTests.Infrastructure.Security;

public class OidcConnectionCheckerTests : IDisposable
{
    private const string RedirectUri = "https://rsgo.example.com/api/auth/oidc/test/callback";

    private readonly FakeOidcProvider _idp = new();
    private readonly OidcConnectionChecker _sut;

    public OidcConnectionCheckerTests()
    {
        _sut = new OidcConnectionChecker(_idp.Factory, NullLogger<OidcConnectionChecker>.Instance);
    }

    public void Dispose() => _idp.Dispose();

    private static OidcCheckRequest Request(
        string authority = FakeOidcProvider.Authority,
        string? clientId = FakeOidcProvider.ClientId,
        string? secret = "client-secret",
        string? redirectUri = RedirectUri,
        bool requirePar = false) =>
        new(authority, clientId, secret, "openid profile email", redirectUri, requirePar);

    private Task<OidcCheckReport> Check(OidcCheckRequest? request = null, OidcCheckScope scope = OidcCheckScope.Full) =>
        _sut.CheckAsync(request ?? Request(), scope);

    private static OidcCheckItem Item(OidcCheckReport report, string id) =>
        report.Items.Should().ContainSingle(i => i.Id == id).Subject;

    private void PublishRawDiscovery(object document) =>
        _idp.Handler.MapJson(FakeOidcProvider.DiscoveryUrl, JsonSerializer.Serialize(document));

    #region Discovery

    [Fact]
    public async Task Check_AllGoodWithPar_AllFivePassed()
    {
        _idp.PublishDiscovery(parEndpoint: true, requirePar: true);
        _idp.PublishParSuccess();

        var report = await Check();

        report.Items.Select(i => i.Id).Should().Equal(
            OidcCheckIds.Discovery, OidcCheckIds.Issuer, OidcCheckIds.Endpoints, OidcCheckIds.Par, OidcCheckIds.Client);
        report.Items.Should().OnlyContain(i => i.Status == OidcCheckStatus.Passed);
        report.Passed.Should().BeTrue();
        report.ExecutedCount.Should().Be(5);
        report.FailedCount.Should().Be(0);
        report.Issuer.Should().Be(FakeOidcProvider.Authority);
        report.ParOffered.Should().BeTrue();
        report.ParRequired.Should().BeTrue();
        _idp.Factory.RequestedNames.Should().OnlyContain(n => n == IOidcService.HttpClientName);
    }

    [Fact]
    public async Task Check_Discovery404_FailsWithNotFoundAndStops()
    {
        var report = await Check();

        var item = report.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(OidcCheckIds.Discovery);
        item.Status.Should().Be(OidcCheckStatus.Failed);
        item.Code.Should().Be("not_found");
        item.Detail.Should().Contain("404");
        report.Passed.Should().BeFalse();
        report.ExecutedCount.Should().Be(1);
        report.Issuer.Should().BeNull();
        report.ParOffered.Should().BeFalse();
    }

    [Fact]
    public async Task Check_DiscoveryNotFound_KeepsTemplateParRequirementInReport()
    {
        var report = await Check(Request(requirePar: true));

        report.ParRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Check_DiscoveryTimeout_FailsWithUnreachable()
    {
        _idp.Handler.MapThrow(FakeOidcProvider.DiscoveryUrl, new TaskCanceledException("timeout"));

        var report = await Check();

        var item = Item(report, OidcCheckIds.Discovery);
        item.Status.Should().Be(OidcCheckStatus.Failed);
        item.Code.Should().Be(OidcErrorCodes.Unreachable);
        item.Detail.Should().Contain("did not answer in time").And.Contain("idp.test");
    }

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError, "could not be resolved")]
    [InlineData(HttpRequestError.ConnectionError, "refused")]
    [InlineData(HttpRequestError.SecureConnectionError, "TLS")]
    [InlineData(HttpRequestError.Unknown, "connection failed")]
    public async Task Check_DiscoveryNetworkError_FailsWithUnreachableAndReason(HttpRequestError error, string reason)
    {
        _idp.Handler.MapThrow(FakeOidcProvider.DiscoveryUrl, new HttpRequestException(error, "boom"));

        var report = await Check();

        var item = Item(report, OidcCheckIds.Discovery);
        item.Code.Should().Be(OidcErrorCodes.Unreachable);
        item.Detail.Should().Contain(reason);
    }

    [Fact]
    public async Task Check_DiscoveryServerError_FailsWithUnreachable()
    {
        _idp.Handler.MapJson(FakeOidcProvider.DiscoveryUrl, "{}", HttpStatusCode.ServiceUnavailable);

        var report = await Check();

        Item(report, OidcCheckIds.Discovery).Code.Should().Be(OidcErrorCodes.Unreachable);
    }

    [Fact]
    public async Task Check_DiscoveryForbidden_FailsWithHttpError()
    {
        _idp.Handler.MapJson(FakeOidcProvider.DiscoveryUrl, "{}", HttpStatusCode.Forbidden);

        var report = await Check();

        Item(report, OidcCheckIds.Discovery).Code.Should().Be("http_error");
    }

    [Theory]
    [InlineData("<html>Login</html>")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("")]
    public async Task Check_DiscoveryNotAJsonObject_FailsWithInvalidDiscovery(string body)
    {
        _idp.Handler.MapJson(FakeOidcProvider.DiscoveryUrl, body);

        var report = await Check();

        var item = report.Items.Should().ContainSingle().Subject;
        item.Status.Should().Be(OidcCheckStatus.Failed);
        item.Code.Should().Be(OidcErrorCodes.InvalidDiscovery);
    }

    [Fact]
    public async Task Check_AuthorityWithTrailingSlash_FetchesSameDiscoveryAddress()
    {
        _idp.PublishDiscovery(parEndpoint: false);

        var report = await Check(Request(authority: FakeOidcProvider.Authority + "/"), OidcCheckScope.Discovery);

        Item(report, OidcCheckIds.Discovery).Status.Should().Be(OidcCheckStatus.Passed);
    }

    #endregion

    #region Issuer

    [Theory]
    [InlineData(FakeOidcProvider.Authority, FakeOidcProvider.Authority)]
    [InlineData(FakeOidcProvider.Authority + "/", FakeOidcProvider.Authority)]
    [InlineData(FakeOidcProvider.Authority, FakeOidcProvider.Authority + "/")]
    public async Task Check_IssuerEqualIgnoringOneTrailingSlash_Passes(string issuer, string authority)
    {
        _idp.PublishDiscovery(issuer: issuer);

        var report = await Check(Request(authority: authority), OidcCheckScope.Discovery);

        Item(report, OidcCheckIds.Issuer).Status.Should().Be(OidcCheckStatus.Passed);
        report.Issuer.Should().Be(issuer);
    }

    [Theory]
    [InlineData("https://IDP.test/realms/main")]
    [InlineData("https://idp.test/realms/Main")]
    [InlineData("https://idp.test/realms/other")]
    [InlineData("https://idp.test")]
    [InlineData("http://idp.test/realms/main")]
    public async Task Check_IssuerDiffers_FailsWithIssuerMismatchNamingBoth(string issuer)
    {
        _idp.PublishDiscovery(issuer: issuer);

        var report = await Check(scope: OidcCheckScope.Discovery);

        var item = Item(report, OidcCheckIds.Issuer);
        item.Status.Should().Be(OidcCheckStatus.Failed);
        item.Code.Should().Be("issuer_mismatch");
        item.Detail.Should().Contain(issuer).And.Contain(FakeOidcProvider.Authority);
        report.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Check_IssuerMissing_FailsWithIssuerMissing()
    {
        PublishRawDiscovery(new
        {
            authorization_endpoint = FakeOidcProvider.AuthorizationEndpoint,
            token_endpoint = FakeOidcProvider.TokenEndpoint,
            jwks_uri = FakeOidcProvider.JwksUrl
        });

        var report = await Check(scope: OidcCheckScope.Discovery);

        Item(report, OidcCheckIds.Issuer).Code.Should().Be("issuer_missing");
        Item(report, OidcCheckIds.Endpoints).Status.Should().Be(OidcCheckStatus.Passed);
    }

    #endregion

    #region Endpoints

    [Fact]
    public async Task Check_AllEndpointsMissing_FailsNamingAllThree()
    {
        PublishRawDiscovery(new { issuer = FakeOidcProvider.Authority });

        var report = await Check(scope: OidcCheckScope.Discovery);

        var item = Item(report, OidcCheckIds.Endpoints);
        item.Status.Should().Be(OidcCheckStatus.Failed);
        item.Code.Should().Be("endpoints_missing");
        item.Detail.Should().Contain("authorization, token and JWKS");
    }

    [Fact]
    public async Task Check_OneEndpointMissing_NamesOnlyThatOne()
    {
        PublishRawDiscovery(new
        {
            issuer = FakeOidcProvider.Authority,
            authorization_endpoint = FakeOidcProvider.AuthorizationEndpoint,
            token_endpoint = FakeOidcProvider.TokenEndpoint
        });

        var report = await Check(scope: OidcCheckScope.Discovery);

        Item(report, OidcCheckIds.Endpoints).Detail.Should().Be("The discovery document names no JWKS endpoint.");
    }

    [Fact]
    public async Task Check_EndpointNotAString_CountsAsMissing()
    {
        PublishRawDiscovery(new
        {
            issuer = FakeOidcProvider.Authority,
            authorization_endpoint = 42,
            token_endpoint = FakeOidcProvider.TokenEndpoint,
            jwks_uri = FakeOidcProvider.JwksUrl
        });

        var report = await Check(scope: OidcCheckScope.Discovery);

        Item(report, OidcCheckIds.Endpoints).Detail.Should().Contain("authorization");
    }

    #endregion

    #region Scope

    [Fact]
    public async Task Check_DiscoveryScope_RunsOnlyFirstThreeChecksAndNoPar()
    {
        _idp.PublishDiscovery(parEndpoint: true, requirePar: true);
        _idp.PublishParSuccess();

        var report = await Check(scope: OidcCheckScope.Discovery);

        report.Items.Select(i => i.Id).Should().Equal(OidcCheckIds.Discovery, OidcCheckIds.Issuer, OidcCheckIds.Endpoints);
        report.ExecutedCount.Should().Be(3);
        report.Passed.Should().BeTrue();
        report.ParOffered.Should().BeTrue();
        report.ParRequired.Should().BeTrue();
        _idp.Handler.RequestsTo(FakeOidcProvider.ParEndpoint).Should().BeEmpty();
    }

    #endregion

    #region PAR

    [Fact]
    public async Task Check_TemplateRequiresParAndProviderOffersNone_FailsWithParNotSupported()
    {
        _idp.PublishDiscovery(parEndpoint: false, requirePar: false);

        var report = await Check(Request(requirePar: true));

        var par = Item(report, OidcCheckIds.Par);
        par.Status.Should().Be(OidcCheckStatus.Failed);
        par.Code.Should().Be(OidcErrorCodes.ParNotSupported);
        par.Detail.Should().Contain("the template");
        report.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Check_ProviderRequiresParButOffersNone_FailsNamingProvider()
    {
        _idp.PublishDiscovery(parEndpoint: false, requirePar: true);

        var report = await Check();

        var par = Item(report, OidcCheckIds.Par);
        par.Status.Should().Be(OidcCheckStatus.Failed);
        par.Detail.Should().Contain("the provider");
    }

    [Fact]
    public async Task Check_RequireParFlagNotBooleanTrue_IsNotRequired()
    {
        PublishRawDiscovery(new
        {
            issuer = FakeOidcProvider.Authority,
            authorization_endpoint = FakeOidcProvider.AuthorizationEndpoint,
            token_endpoint = FakeOidcProvider.TokenEndpoint,
            jwks_uri = FakeOidcProvider.JwksUrl,
            require_pushed_authorization_requests = "true"
        });

        var report = await Check();

        report.ParRequired.Should().BeFalse();
        Item(report, OidcCheckIds.Par).Status.Should().Be(OidcCheckStatus.Skipped);
    }

    [Fact]
    public async Task Check_NoParOfferedNorRequired_SkipsParAndClientAndCountsOnlyExecuted()
    {
        _idp.PublishDiscovery(parEndpoint: false, requirePar: false);

        var report = await Check();

        Item(report, OidcCheckIds.Par).Status.Should().Be(OidcCheckStatus.Skipped);
        var client = Item(report, OidcCheckIds.Client);
        client.Status.Should().Be(OidcCheckStatus.Skipped);
        client.Detail.Should().Contain("test sign-in");
        report.Items.Should().HaveCount(5);
        report.ExecutedCount.Should().Be(3);
        report.FailedCount.Should().Be(0);
        report.Passed.Should().BeTrue();
        _idp.Handler.Requests.Should().NotContain(r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Check_ParOfferedButNotRequired_SkipsParCheckButStillChecksClient()
    {
        _idp.PublishDiscovery(parEndpoint: true, requirePar: false);
        _idp.PublishParSuccess();

        var report = await Check();

        var par = Item(report, OidcCheckIds.Par);
        par.Status.Should().Be(OidcCheckStatus.Skipped);
        par.Detail.Should().Contain("not used");
        Item(report, OidcCheckIds.Client).Status.Should().Be(OidcCheckStatus.Passed);
        report.ExecutedCount.Should().Be(4);
    }

    #endregion

    #region Client credentials

    [Fact]
    public async Task Check_ClientCheck_PostsRealRedirectUriSecretAndPkce()
    {
        _idp.PublishDiscovery();
        _idp.PublishParSuccess();

        await Check();

        var form = _idp.Handler.RequestsTo(FakeOidcProvider.ParEndpoint).Should().ContainSingle().Subject.Form;
        form["client_id"].Should().Be(FakeOidcProvider.ClientId);
        form["client_secret"].Should().Be("client-secret");
        form["redirect_uri"].Should().Be(RedirectUri);
        form["response_type"].Should().Be("code");
        form["scope"].Should().Be("openid profile email");
        form["code_challenge_method"].Should().Be("S256");
        form["code_challenge"].Should().NotBeNullOrEmpty();
        form["state"].Should().NotBeNullOrEmpty();
        form["nonce"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Check_ClientWithoutSecret_SendsNoSecret()
    {
        _idp.PublishDiscovery();
        _idp.PublishParSuccess();

        await Check(Request(secret: null));

        _idp.Handler.RequestsTo(FakeOidcProvider.ParEndpoint).Single().Form.Should().NotContainKey("client_secret");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_client"}""")]
    [InlineData(HttpStatusCode.Unauthorized, "")]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"unauthorized_client"}""")]
    public async Task Check_SecretRejected_FailsWithInvalidClient(HttpStatusCode status, string body)
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapJson(FakeOidcProvider.ParEndpoint, body, status);

        var report = await Check();

        var client = Item(report, OidcCheckIds.Client);
        client.Status.Should().Be(OidcCheckStatus.Failed);
        client.Code.Should().Be(OidcErrorCodes.InvalidClient);
        client.Detail.Should().NotContain("client-secret");
        report.Passed.Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"error":"invalid_redirect_uri"}""")]
    [InlineData("""{"error":"invalid_request","error_description":"Invalid redirect_uri"}""")]
    [InlineData("""{"error":"invalid_request","error_description":"The REDIRECT URI is not registered"}""")]
    public async Task Check_RedirectUriRejected_FailsNamingRedirectUri(string body)
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapJson(FakeOidcProvider.ParEndpoint, body, HttpStatusCode.BadRequest);

        var report = await Check();

        var client = Item(report, OidcCheckIds.Client);
        client.Code.Should().Be("redirect_uri_rejected");
        client.Detail.Should().Contain(RedirectUri);
    }

    [Fact]
    public async Task Check_InvalidRequestUnrelatedToRedirect_FailsWithProviderError()
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapJson(FakeOidcProvider.ParEndpoint, """{"error":"invalid_request","error_description":"Missing code_challenge"}""", HttpStatusCode.BadRequest);

        var report = await Check();

        var client = Item(report, OidcCheckIds.Client);
        client.Code.Should().Be("invalid_request");
        client.Detail.Should().Contain("400").And.Contain("Missing code_challenge");
    }

    [Fact]
    public async Task Check_ParRejectedWithoutJson_FailsWithRejected()
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapJson(FakeOidcProvider.ParEndpoint, "<html>error</html>", HttpStatusCode.BadRequest);

        var report = await Check();

        Item(report, OidcCheckIds.Client).Code.Should().Be(OidcErrorCodes.Rejected);
    }

    [Fact]
    public async Task Check_ParEndpointUnreachable_FailsWithUnreachableWithoutException()
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapThrow(FakeOidcProvider.ParEndpoint, new HttpRequestException("refused"));

        var report = await Check();

        Item(report, OidcCheckIds.Client).Code.Should().Be(OidcErrorCodes.Unreachable);
    }

    [Fact]
    public async Task Check_ParEndpointTimeout_FailsWithUnreachable()
    {
        _idp.PublishDiscovery();
        _idp.Handler.MapThrow(FakeOidcProvider.ParEndpoint, new TaskCanceledException("timeout"));

        var report = await Check();

        Item(report, OidcCheckIds.Client).Code.Should().Be(OidcErrorCodes.Unreachable);
    }

    [Theory]
    [InlineData(null, RedirectUri)]
    [InlineData("", RedirectUri)]
    [InlineData(FakeOidcProvider.ClientId, null)]
    public async Task Check_ClientIdOrRedirectMissing_FailsWithoutCallingPar(string? clientId, string? redirectUri)
    {
        _idp.PublishDiscovery();
        _idp.PublishParSuccess();

        var report = await Check(Request(clientId: clientId, redirectUri: redirectUri));

        Item(report, OidcCheckIds.Client).Code.Should().Be("client_missing");
        _idp.Handler.RequestsTo(FakeOidcProvider.ParEndpoint).Should().BeEmpty();
    }

    [Fact]
    public async Task Check_IssuerAndClientFail_CountsBothButNotSkippedPar()
    {
        _idp.PublishDiscovery(issuer: "https://other.test", requirePar: false);
        _idp.Handler.MapJson(FakeOidcProvider.ParEndpoint, """{"error":"invalid_client"}""", HttpStatusCode.BadRequest);

        var report = await Check();

        report.Items.Should().HaveCount(5);
        report.FailedCount.Should().Be(2);
        report.ExecutedCount.Should().Be(4);
        report.Passed.Should().BeFalse();
    }

    #endregion

    [Fact]
    public void Report_WithOnlySkippedChecks_IsNotPassed()
    {
        var report = new OidcCheckReport(
            [new OidcCheckItem(OidcCheckIds.Par, OidcCheckStatus.Skipped, "t", null)], null, false, false);

        report.ExecutedCount.Should().Be(0);
        report.Passed.Should().BeFalse();
    }
}
