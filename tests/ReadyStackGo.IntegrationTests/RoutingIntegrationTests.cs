using System.Net;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Tests for verifying correct routing behavior for API and SPA routes.
/// Note: Static file serving tests (JS/CSS MIME types) are performed via E2E tests
/// against the Docker container, as the TestHost doesn't reliably serve static files
/// from custom wwwroot paths.
/// </summary>
public class RoutingIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RoutingIntegrationTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    #region SPA Fallback Tests

    [Theory]
    [InlineData("/nonexistent-page")]
    [InlineData("/settings/environments")]
    [InlineData("/some/deep/nested/route")]
    public async Task NonExistentFrontendRoute_ReturnsSpaFallback(string route)
    {
        // Arrange & Act
        var response = await _client.GetAsync(route);

        // Assert - SPA fallback returns 200 with index.html
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        // Verify it's HTML (the SPA entry point)
        Assert.Contains("<!DOCTYPE html>", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RootRoute_ReturnsSpaIndex()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("<!DOCTYPE html>", content, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region API Routing Tests

    [Fact]
    public async Task ApiRoute_DoesNotReturnSpaFallback()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/nonexistent");

        // Assert - API routes should NOT fall back to SPA
        // They should return a proper API error (404 or similar)
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion
}
