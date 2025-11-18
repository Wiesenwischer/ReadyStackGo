using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ReadyStackGo.Api;
using ReadyStackGo.Application.Auth.DTOs;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests für Authentication API Endpoints
/// Testet Login, Logout und Authorization
/// </summary>
public class AuthEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task POST_Login_WithValidCredentials_ReturnsSuccessAndToken()
    {
        // Arrange
        var loginRequest = new
        {
            username = "admin",
            password = "admin"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.Token.Should().NotBeNullOrEmpty();
        loginResponse.Username.Should().Be("admin");
        loginResponse.Role.Should().Be("admin");
    }

    [Fact]
    public async Task POST_Login_WithInvalidUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            username = "invaliduser",
            password = "admin"
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
            username = "admin",
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
        // Arrange
        var token = await TestAuthHelper.GetAdminTokenAsync(_client);
        var authenticatedClient = _factory.CreateClient();
        TestAuthHelper.AddAuthToken(authenticatedClient, token);

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
        TestAuthHelper.AddAuthToken(authenticatedClient, "invalid.token.here");

        // Act
        var response = await authenticatedClient.GetAsync("/api/containers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private record LoginResponse(string Token, string Username, string Role);
}
