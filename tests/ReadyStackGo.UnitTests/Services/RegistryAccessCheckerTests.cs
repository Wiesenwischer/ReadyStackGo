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

    #region Bearer token flow

    [Fact]
    public async Task CheckAccess_AnonymousTokenSucceeds_ReturnsPublic()
    {
        var bearerAuth = new AuthenticationHeaderValue("Bearer",
            "realm=\"https://auth.example.io/token\",service=\"registry.example.io\"");

        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, bearerAuth, null),
            (HttpStatusCode.OK, null, "{\"token\":\"abc\"}"));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "myorg", "myapp");

        result.Should().Be(RegistryAccessLevel.Public);
    }

    [Fact]
    public async Task CheckAccess_AnonymousTokenDenied_ReturnsAuthRequired()
    {
        var bearerAuth = new AuthenticationHeaderValue("Bearer",
            "realm=\"https://auth.example.io/token\",service=\"registry.example.io\"");

        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, bearerAuth, null),
            (HttpStatusCode.Unauthorized, null, null));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "private", "app");

        result.Should().Be(RegistryAccessLevel.AuthRequired);
    }

    [Fact]
    public async Task CheckAccess_TokenDenied403_ReturnsAuthRequired()
    {
        var bearerAuth = new AuthenticationHeaderValue("Bearer",
            "realm=\"https://auth.example.io/token\",service=\"registry.example.io\"");

        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, bearerAuth, null),
            (HttpStatusCode.Forbidden, null, null));

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("registry.example.io", "private", "app");

        result.Should().Be(RegistryAccessLevel.AuthRequired);
    }

    #endregion

    #region Docker Hub special handling

    [Fact]
    public async Task CheckAccess_DockerHub_UsesRegistryOneDomain()
    {
        var bearerAuth = new AuthenticationHeaderValue("Bearer",
            "realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\"");

        var handlerMock = CreateSequenceHandler(
            (HttpStatusCode.Unauthorized, bearerAuth, null),
            (HttpStatusCode.OK, null, "{\"token\":\"abc\"}"));

        handlerMock.Protected()
            .Verify<Task<HttpResponseMessage>>(
                "SendAsync",
                Times.Never(),
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri != null && r.RequestUri.Host == "docker.io"),
                ItExpr.IsAny<CancellationToken>());

        var checker = CreateChecker(handlerMock.Object);

        var result = await checker.CheckAccessAsync("docker.io", "library", "nginx");

        result.Should().Be(RegistryAccessLevel.Public);
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
