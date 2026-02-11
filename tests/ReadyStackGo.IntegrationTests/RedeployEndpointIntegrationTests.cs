using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for the Redeploy Webhook Endpoint.
/// Tests authentication, authorization, and request validation.
/// </summary>
public class RedeployEndpointIntegrationTests : AuthenticatedTestBase
{
    #region Authentication

    [Fact]
    public async Task POST_Redeploy_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthenticatedClient = CreateUnauthenticatedClient();

        var request = new { stackName = "my-stack", environmentId = Guid.NewGuid().ToString() };
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/hooks/redeploy", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Redeploy_WithJwtAuth_IsAllowed()
    {
        // JWT-authenticated admin has all permissions including Hooks.Redeploy
        var request = new { stackName = "nonexistent-stack", environmentId = EnvironmentId };
        var response = await Client.PostAsJsonAsync("/api/hooks/redeploy", request);

        // Should reach the handler (not 401/403), but fail because no deployment exists
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task POST_Redeploy_WithoutEnvironmentId_ReturnsError()
    {
        var request = new { stackName = "my-stack" };
        var response = await Client.PostAsJsonAsync("/api/hooks/redeploy", request);

        // Should fail because no environmentId and no env_id claim in JWT
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task POST_Redeploy_NonexistentStack_ReturnsBadRequest()
    {
        var request = new { stackName = "stack-that-does-not-exist", environmentId = EnvironmentId };
        var response = await Client.PostAsJsonAsync("/api/hooks/redeploy", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("No deployment found");
    }

    [Fact]
    public async Task POST_Redeploy_InvalidEnvironmentId_ReturnsBadRequest()
    {
        var request = new { stackName = "my-stack", environmentId = "not-a-guid" };
        var response = await Client.PostAsJsonAsync("/api/hooks/redeploy", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task POST_Redeploy_WithValidEnvironmentId_NoDeployment_ReturnsBadRequest()
    {
        var request = new { stackName = "some-stack", environmentId = EnvironmentId };
        var response = await Client.PostAsJsonAsync("/api/hooks/redeploy", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("No deployment found");
        body.Should().Contain("some-stack");
    }

    #endregion

    #region API Key Authentication

    [Fact]
    public async Task POST_Redeploy_WithApiKey_IsAllowed()
    {
        // Create an API key with Hooks.Redeploy permission
        var createKeyRequest = new
        {
            name = "Redeploy Test Key",
            permissions = new[] { "Hooks.Redeploy" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/api-keys", createKeyRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var keyResult = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var apiKey = keyResult!.ApiKey!.FullKey;

        // Use the API key to call the redeploy endpoint
        using var apiKeyClient = CreateUnauthenticatedClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var request = new { stackName = "test-stack", environmentId = EnvironmentId };
        var response = await apiKeyClient.PostAsJsonAsync("/api/hooks/redeploy", request);

        // Should reach handler (not 401/403), fail because no deployment exists
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("No deployment found");
    }

    [Fact]
    public async Task POST_Redeploy_WithApiKeyWithoutRedeployPermission_ReturnsForbidden()
    {
        // Create an API key with only SyncSources permission (not Redeploy)
        var createKeyRequest = new
        {
            name = "Sync Only Key",
            permissions = new[] { "Hooks.SyncSources" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/api-keys", createKeyRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var keyResult = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var apiKey = keyResult!.ApiKey!.FullKey;

        // Use the API key - should be forbidden since it lacks Hooks.Redeploy
        using var apiKeyClient = CreateUnauthenticatedClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var request = new { stackName = "test-stack", environmentId = EnvironmentId };
        var response = await apiKeyClient.PostAsJsonAsync("/api/hooks/redeploy", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region DTOs

    private record CreateApiKeyResponse(bool Success, string? Message, ApiKeyCreatedDto? ApiKey = null);
    private record ApiKeyCreatedDto(string Id, string Name, string KeyPrefix, string FullKey);

    #endregion
}
