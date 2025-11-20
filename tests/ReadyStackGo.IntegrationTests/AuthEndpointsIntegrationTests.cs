using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests für Authentication API Endpoints
/// Testet Login, Logout und Authorization
/// Each test gets its own isolated factory to allow proper wizard setup.
/// </summary>
public class AuthEndpointsIntegrationTests : IAsyncLifetime
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _adminUsername = string.Empty;
    private string _adminPassword = string.Empty;

    public async Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();

        // Complete wizard setup to create admin user
        var testId = Guid.NewGuid().ToString("N")[..8];
        _adminUsername = $"admin_{testId}";
        _adminPassword = "TestPassword123!";

        // Step 1: Create admin
        var adminRequest = new { username = _adminUsername, password = _adminPassword };
        await _client.PostAsJsonAsync("/api/wizard/admin", adminRequest);

        // Step 2: Set organization
        var orgRequest = new { id = $"org-{testId}", name = $"Test Organization {testId}" };
        await _client.PostAsJsonAsync("/api/wizard/organization", orgRequest);

        // Step 3: Complete wizard
        var installRequest = new { manifestPath = (string?)null };
        await _client.PostAsJsonAsync("/api/wizard/install", installRequest);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task POST_Login_WithValidCredentials_ReturnsSuccessAndToken()
    {
        // Arrange
        var loginRequest = new
        {
            username = _adminUsername,
            password = _adminPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.Token.Should().NotBeNullOrEmpty();
        loginResponse.Username.Should().Be(_adminUsername);
        loginResponse.Role.Should().Be("admin");
    }

    [Fact]
    public async Task POST_Login_WithInvalidUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            username = "invaliduser",
            password = _adminPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            username = _adminUsername,
            password = "wrongpassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Logout_ReturnsSuccess()
    {
        // Act
        var response = await _client.PostAsync("/api/auth/logout", new StringContent("", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        // 204 No Content is correct for logout as there's no response body
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GET_ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/containers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_ProtectedEndpoint_WithValidToken_ReturnsSuccess()
    {
        // Arrange - login first to get token
        var loginRequest = new { username = _adminUsername, password = _adminPassword };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.Token);

        // Act
        var response = await authenticatedClient.GetAsync("/api/containers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_ProtectedEndpoint_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var authenticatedClient = _factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid.token.here");

        // Act
        var response = await authenticatedClient.GetAsync("/api/containers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record LoginResponse(string Token, string Username, string Role);
}
