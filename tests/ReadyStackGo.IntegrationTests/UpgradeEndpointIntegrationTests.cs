using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for the Upgrade Webhook Endpoint.
/// Tests authentication, authorization, and request validation.
/// </summary>
public class UpgradeEndpointIntegrationTests : AuthenticatedTestBase
{
    #region Authentication

    [Fact]
    public async Task POST_Upgrade_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthenticatedClient = CreateUnauthenticatedClient();

        var request = new { stackName = "my-stack", targetVersion = "2.0.0", environmentId = EnvironmentId };
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/hooks/upgrade", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Upgrade_WithJwtAuth_IsAllowed()
    {
        var request = new { stackName = "nonexistent-stack", targetVersion = "2.0.0", environmentId = EnvironmentId };
        var response = await Client.PostAsJsonAsync("/api/hooks/upgrade", request);

        // Should reach the handler (not 401/403), but fail because no deployment exists
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task POST_Upgrade_NonexistentStack_ReturnsBadRequest()
    {
        var request = new { stackName = "stack-that-does-not-exist", targetVersion = "2.0.0", environmentId = EnvironmentId };
        var response = await Client.PostAsJsonAsync("/api/hooks/upgrade", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("No deployment found");
    }

    [Fact]
    public async Task POST_Upgrade_InvalidEnvironmentId_ReturnsBadRequest()
    {
        var request = new { stackName = "my-stack", targetVersion = "2.0.0", environmentId = "not-a-guid" };
        var response = await Client.PostAsJsonAsync("/api/hooks/upgrade", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task POST_Upgrade_WithoutEnvironmentId_ReturnsError()
    {
        var request = new { stackName = "my-stack", targetVersion = "2.0.0" };
        var response = await Client.PostAsJsonAsync("/api/hooks/upgrade", request);

        // Should fail because no environmentId and no env_id claim in JWT
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    #endregion

    #region API Key Authentication

    [Fact]
    public async Task POST_Upgrade_WithApiKey_IsAllowed()
    {
        var createKeyRequest = new
        {
            name = "Upgrade Test Key",
            permissions = new[] { "Hooks.Upgrade" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/api-keys", createKeyRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var keyResult = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var apiKey = keyResult!.ApiKey!.FullKey;

        using var apiKeyClient = CreateUnauthenticatedClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var request = new { stackName = "test-stack", targetVersion = "2.0.0", environmentId = EnvironmentId };
        var response = await apiKeyClient.PostAsJsonAsync("/api/hooks/upgrade", request);

        // Should reach handler (not 401/403), fail because no deployment exists
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("No deployment found");
    }

    [Fact]
    public async Task POST_Upgrade_WithApiKeyWithoutUpgradePermission_ReturnsForbidden()
    {
        var createKeyRequest = new
        {
            name = "Redeploy Only Key",
            permissions = new[] { "Hooks.Redeploy" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/api-keys", createKeyRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var keyResult = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var apiKey = keyResult!.ApiKey!.FullKey;

        using var apiKeyClient = CreateUnauthenticatedClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var request = new { stackName = "test-stack", targetVersion = "2.0.0", environmentId = EnvironmentId };
        var response = await apiKeyClient.PostAsJsonAsync("/api/hooks/upgrade", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region DTOs

    private record CreateApiKeyResponse(bool Success, string? Message, ApiKeyCreatedDto? ApiKey = null);
    private record ApiKeyCreatedDto(string Id, string Name, string KeyPrefix, string FullKey);

    #endregion
}
