using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Services;
using System.Net;
using System.Text.Json;

namespace ReadyStackGo.UnitTests.Services;

/// <summary>
/// Unit tests for VersionCheckService.
/// </summary>
public class VersionCheckServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IMemoryCache> _memoryCacheMock;
    private readonly Mock<ILogger<VersionCheckService>> _loggerMock;

    public VersionCheckServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _memoryCacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<VersionCheckService>>();
    }

    private VersionCheckService CreateService(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClientFactoryMock
                .Setup(f => f.CreateClient("GitHub"))
                .Returns(httpClient);
        }

        return new VersionCheckService(
            _httpClientFactoryMock.Object,
            _memoryCacheMock.Object,
            _loggerMock.Object);
    }

    #region GetCurrentVersion Tests

    [Fact]
    public void GetCurrentVersion_ReturnsVersionString()
    {
        // Arrange
        var service = CreateService();

        // Act
        var version = service.GetCurrentVersion();

        // Assert
        version.Should().NotBeNullOrEmpty();
        // Should be a valid version format (x.y.z)
        version.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    #endregion

    #region GetLatestVersionAsync Tests

    [Fact]
    public async Task GetLatestVersionAsync_CacheHit_ReturnsCachedValue()
    {
        // Arrange
        var cachedInfo = new LatestVersionInfo("1.2.3", "https://example.com", DateTime.UtcNow, DateTime.UtcNow);
        object? cacheEntry = cachedInfo;

        _memoryCacheMock
            .Setup(c => c.TryGetValue("LatestVersionInfo", out cacheEntry!))
            .Returns(true);

        var service = CreateService();

        // Act
        var result = await service.GetLatestVersionAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("1.2.3");
        _httpClientFactoryMock.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetLatestVersionAsync_CacheMiss_FetchesFromGitHub()
    {
        // Arrange
        object? cacheEntry = null;
        _memoryCacheMock
            .Setup(c => c.TryGetValue("LatestVersionInfo", out cacheEntry!))
            .Returns(false);

        var cacheEntryMock = new Mock<ICacheEntry>();
        _memoryCacheMock
            .Setup(c => c.CreateEntry(It.IsAny<object>()))
            .Returns(cacheEntryMock.Object);

        var responseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v1.5.0",
            html_url = "https://github.com/test/releases/tag/v1.5.0",
            published_at = "2024-01-15T10:00:00Z"
        });

        var mockHandler = CreateMockHandler(responseJson, HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler.Object) { BaseAddress = new Uri("https://api.github.com") };

        var service = CreateService(httpClient);

        // Act
        var result = await service.GetLatestVersionAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("1.5.0"); // v prefix should be trimmed
        result.ReleaseUrl.Should().Contain("v1.5.0");
    }

    [Fact]
    public async Task GetLatestVersionAsync_GitHubReturnsError_ReturnsNull()
    {
        // Arrange
        object? cacheEntry = null;
        _memoryCacheMock
            .Setup(c => c.TryGetValue("LatestVersionInfo", out cacheEntry!))
            .Returns(false);

        var mockHandler = CreateMockHandler("", HttpStatusCode.NotFound);
        var httpClient = new HttpClient(mockHandler.Object) { BaseAddress = new Uri("https://api.github.com") };

        var service = CreateService(httpClient);

        // Act
        var result = await service.GetLatestVersionAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestVersionAsync_GitHubTimeout_ReturnsNull()
    {
        // Arrange
        object? cacheEntry = null;
        _memoryCacheMock
            .Setup(c => c.TryGetValue("LatestVersionInfo", out cacheEntry!))
            .Returns(false);

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHandler.Object) { BaseAddress = new Uri("https://api.github.com") };
        var service = CreateService(httpClient);

        // Act
        var result = await service.GetLatestVersionAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestVersionAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        object? cacheEntry = null;
        _memoryCacheMock
            .Setup(c => c.TryGetValue("LatestVersionInfo", out cacheEntry!))
            .Returns(false);

        var mockHandler = CreateMockHandler("{invalid json", HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler.Object) { BaseAddress = new Uri("https://api.github.com") };

        var service = CreateService(httpClient);

        // Act
        var result = await service.GetLatestVersionAsync();

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("v2.0.0", "2.0.0")]
    [InlineData("V2.0.0", "2.0.0")]
    [InlineData("2.0.0", "2.0.0")]
    public async Task GetLatestVersionAsync_TrimsVersionPrefix(string tagName, string expectedVersion)
    {
        // Arrange
        object? cacheEntry = null;
        _memoryCacheMock
            .Setup(c => c.TryGetValue("LatestVersionInfo", out cacheEntry!))
            .Returns(false);

        var cacheEntryMock = new Mock<ICacheEntry>();
        _memoryCacheMock
            .Setup(c => c.CreateEntry(It.IsAny<object>()))
            .Returns(cacheEntryMock.Object);

        var responseJson = JsonSerializer.Serialize(new
        {
            tag_name = tagName,
            html_url = "https://github.com/test/releases",
            published_at = "2024-01-15T10:00:00Z"
        });

        var mockHandler = CreateMockHandler(responseJson, HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler.Object) { BaseAddress = new Uri("https://api.github.com") };

        var service = CreateService(httpClient);

        // Act
        var result = await service.GetLatestVersionAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be(expectedVersion);
    }

    #endregion

    private Mock<HttpMessageHandler> CreateMockHandler(string content, HttpStatusCode statusCode)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });

        return mockHandler;
    }
}
