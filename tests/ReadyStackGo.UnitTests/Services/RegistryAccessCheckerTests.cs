using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Services;

namespace ReadyStackGo.UnitTests.Services;

public class RegistryAccessCheckerTests
{
    private readonly Mock<ILogger<RegistryAccessChecker>> _loggerMock = new();

    private RegistryAccessChecker CreateChecker(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(5);

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("RegistryCheck")).Returns(client);

        return new RegistryAccessChecker(factoryMock.Object, _loggerMock.Object);
    }

    private static Mock<HttpMessageHandler> CreateHandler(
        HttpStatusCode statusCode,
        AuthenticationHeaderValue? wwwAuthenticate = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var response = new HttpResponseMessage(statusCode);
        if (wwwAuthenticate != null)
            response.Headers.WwwAuthenticate.Add(wwwAuthenticate);

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        return handlerMock;
    }

    private static Mock<HttpMessageHandler> CreateSequenceHandler(
        params (HttpStatusCode Status, AuthenticationHeaderValue? Auth, string? Content)[] responses)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var sequence = handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        foreach (var (status, auth, content) in responses)
        {
            sequence = sequence.ReturnsAsync(() =>
            {
                var resp = new HttpResponseMessage(status);
                if (auth != null)
                    resp.Headers.WwwAuthenticate.Add(auth);
                if (content != null)
                    resp.Content = new StringContent(content);
                return resp;
            });
        }

        return handlerMock;
    }

    private static AuthenticationHeaderValue BearerChallenge(string realm = "https://auth.example.io/token",
        string service = "registry.example.io")
        => new("Bearer", $"realm=\"{realm}\",service=\"{service}\"");

    #region Fully open registry (v2 returns 200)

    [Fact]
    public async Task CheckAccess_V2Returns200_ReturnsPublic()
    {
        var handler = CreateHandler(HttpStatusCode.OK);
        var checker = CreateChecker(handler.Object);

        var result = await checker.CheckAccessAsync("mcr.microsoft.com", "dotnet", "runtime");

        result.Should().Be(RegistryAccessLevel.Public);
    }

    #endregion

    #region Bearer token flow — truly public (token + tags both succeed)

    [Fact]
    public async Task CheckAccess_TokenAndTagsSucceed_ReturnsPublic()
    {
        // v2 → 401, token → 200 with token, tags/list → 200
        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, BearerChallenge(), null),
            (HttpStatusCode.OK, null, "{\"token\":\"abc123\"}"),
            (HttpStatusCode.OK, null, "{\"name\":\"myorg/myapp\",\"tags\":[\"latest\"]}"));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "myorg", "myapp");

        result.Should().Be(RegistryAccessLevel.Public);
    }

    [Fact]
    public async Task CheckAccess_TokenWithAccessTokenField_ReturnsPublic()
    {
        // Some registries use "access_token" instead of "token"
        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, BearerChallenge(), null),
            (HttpStatusCode.OK, null, "{\"access_token\":\"xyz789\"}"),
            (HttpStatusCode.OK, null, "{\"tags\":[\"v1\"]}"));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "myorg", "myapp");

        result.Should().Be(RegistryAccessLevel.Public);
    }

    #endregion

    #region Bearer token flow — auth required

    [Fact]
    public async Task CheckAccess_TokenSucceedsButTagsDenied_ReturnsAuthRequired()
    {
        // Token is handed out but doesn't have pull access (e.g., private Docker Hub repo)
        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, BearerChallenge(), null),
            (HttpStatusCode.OK, null, "{\"token\":\"limited\"}"),
            (HttpStatusCode.Unauthorized, null, null));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "private", "app");

        result.Should().Be(RegistryAccessLevel.AuthRequired);
    }

    [Fact]
    public async Task CheckAccess_TokenSucceedsButTagsForbidden_ReturnsAuthRequired()
    {
        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, BearerChallenge(), null),
            (HttpStatusCode.OK, null, "{\"token\":\"limited\"}"),
            (HttpStatusCode.Forbidden, null, null));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "private", "app");

        result.Should().Be(RegistryAccessLevel.AuthRequired);
    }

    [Fact]
    public async Task CheckAccess_AnonymousTokenDenied_ReturnsAuthRequired()
    {
        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, BearerChallenge(), null),
            (HttpStatusCode.Unauthorized, null, null));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "private", "app");

        result.Should().Be(RegistryAccessLevel.AuthRequired);
    }

    [Fact]
    public async Task CheckAccess_TokenDenied403_ReturnsAuthRequired()
    {
        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, BearerChallenge(), null),
            (HttpStatusCode.Forbidden, null, null));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "private", "app");

        result.Should().Be(RegistryAccessLevel.AuthRequired);
    }

    #endregion

    #region Token parsing edge cases

    [Fact]
    public async Task CheckAccess_TokenResponseEmpty_ReturnsUnknown()
    {
        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, BearerChallenge(), null),
            (HttpStatusCode.OK, null, "{}"));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "ns", "repo");

        result.Should().Be(RegistryAccessLevel.Unknown);
    }

    [Fact]
    public async Task CheckAccess_TokenResponseMalformed_ReturnsUnknown()
    {
        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, BearerChallenge(), null),
            (HttpStatusCode.OK, null, "not json at all"));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "ns", "repo");

        result.Should().Be(RegistryAccessLevel.Unknown);
    }

    #endregion

    #region Docker Hub special handling

    [Fact]
    public async Task CheckAccess_DockerHub_UsesRegistryOneDomain()
    {
        var bearerAuth = new AuthenticationHeaderValue("Bearer",
            "realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\"");

        // v2 → 401, token → 200, tags → 200
        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, bearerAuth, null),
            (HttpStatusCode.OK, null, "{\"token\":\"abc\"}"),
            (HttpStatusCode.OK, null, "{\"tags\":[\"latest\"]}"));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("docker.io", "library", "nginx");

        result.Should().Be(RegistryAccessLevel.Public);
    }

    [Fact]
    public async Task CheckAccess_DockerHubPrivateRepo_TokenSucceedsButTagsDenied()
    {
        var bearerAuth = new AuthenticationHeaderValue("Bearer",
            "realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\"");

        // Docker Hub gives out tokens for private repos too — but tags will fail
        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, bearerAuth, null),
            (HttpStatusCode.OK, null, "{\"token\":\"limited_scope\"}"),
            (HttpStatusCode.Unauthorized, null, null));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("docker.io", "privateuser", "privateapp");

        result.Should().Be(RegistryAccessLevel.AuthRequired);
    }

    #endregion

    #region Error handling

    [Fact]
    public async Task CheckAccess_UnexpectedStatusCode_ReturnsUnknown()
    {
        var handler = CreateHandler(HttpStatusCode.InternalServerError);
        var checker = CreateChecker(handler.Object);

        var result = await checker.CheckAccessAsync("broken.example.com", "ns", "repo");

        result.Should().Be(RegistryAccessLevel.Unknown);
    }

    [Fact]
    public async Task CheckAccess_401WithoutBearerChallenge_ReturnsUnknown()
    {
        var handler = CreateHandler(HttpStatusCode.Unauthorized);
        var checker = CreateChecker(handler.Object);

        var result = await checker.CheckAccessAsync("weird.registry.io", "ns", "repo");

        result.Should().Be(RegistryAccessLevel.Unknown);
    }

    [Fact]
    public async Task CheckAccess_401WithBasicChallenge_ReturnsUnknown()
    {
        var basicAuth = new AuthenticationHeaderValue("Basic", "realm=\"registry\"");
        var handler = CreateHandler(HttpStatusCode.Unauthorized, basicAuth);
        var checker = CreateChecker(handler.Object);

        var result = await checker.CheckAccessAsync("basic.registry.io", "ns", "repo");

        result.Should().Be(RegistryAccessLevel.Unknown);
    }

    [Fact]
    public async Task CheckAccess_BearerWithoutRealm_ReturnsUnknown()
    {
        var bearerAuth = new AuthenticationHeaderValue("Bearer",
            "service=\"registry.example.io\"");

        var handler = CreateHandler(HttpStatusCode.Unauthorized, bearerAuth);
        var checker = CreateChecker(handler.Object);

        var result = await checker.CheckAccessAsync("norealm.registry.io", "ns", "repo");

        result.Should().Be(RegistryAccessLevel.Unknown);
    }

    [Fact]
    public async Task CheckAccess_HttpRequestException_ReturnsUnknown()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("unreachable.example.com", "ns", "repo");

        result.Should().Be(RegistryAccessLevel.Unknown);
    }

    [Fact]
    public async Task CheckAccess_TaskCanceled_ReturnsUnknown()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("slow.registry.io", "ns", "repo");

        result.Should().Be(RegistryAccessLevel.Unknown);
    }

    #endregion
}
