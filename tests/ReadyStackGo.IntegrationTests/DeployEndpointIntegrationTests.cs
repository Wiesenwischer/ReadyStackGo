using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for the Deploy Webhook Endpoint (idempotent deploy/redeploy).
/// Tests authentication, authorization, and request validation.
/// </summary>
public class DeployEndpointIntegrationTests : AuthenticatedTestBase
{
    #region Authentication

    [Fact]
    public async Task POST_Deploy_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthenticatedClient = CreateUnauthenticatedClient();

        var request = new
        {
            stackId = "source:product:stack",
            stackName = "my-stack",
            environmentId = Guid.NewGuid().ToString()
        };
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/hooks/deploy", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Deploy_WithJwtAuth_IsAllowed()
    {
        // JWT-authenticated admin has all permissions including Hooks.Deploy
        var request = new
        {
            stackId = "source:product:stack",
            stackName = "nonexistent-stack",
            environmentId = EnvironmentId
        };
        var response = await Client.PostAsJsonAsync("/api/hooks/deploy", request);

        // Should reach the handler (not 401/403) — will fail during actual deployment
        // but the point is it gets past auth
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task POST_Deploy_InvalidEnvironmentId_ReturnsBadRequest()
    {
        var request = new
        {
            stackId = "source:product:stack",
            stackName = "my-stack",
            environmentId = "not-a-guid"
        };
        var response = await Client.PostAsJsonAsync("/api/hooks/deploy", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task POST_Deploy_EmptyStackId_ReturnsBadRequest()
    {
        var request = new
        {
            stackId = "",
            stackName = "my-stack",
            environmentId = EnvironmentId
        };
        var response = await Client.PostAsJsonAsync("/api/hooks/deploy", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("StackId is required");
    }

    [Fact]
    public async Task POST_Deploy_EmptyStackName_ReturnsBadRequest()
    {
        var request = new
        {
            stackId = "source:product:stack",
            stackName = "",
            environmentId = EnvironmentId
        };
        var response = await Client.PostAsJsonAsync("/api/hooks/deploy", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("StackName is required");
    }

    [Fact]
    public async Task POST_Deploy_WithoutEnvironmentId_ReturnsError()
    {
        var request = new
        {
            stackId = "source:product:stack",
            stackName = "my-stack"
        };
        var response = await Client.PostAsJsonAsync("/api/hooks/deploy", request);

        // Should fail because no environmentId and no env_id claim in JWT
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    #endregion

    #region API Key Authentication

    [Fact]
    public async Task POST_Deploy_WithApiKey_IsAllowed()
    {
        // Create an API key with Hooks.Deploy permission
        var createKeyRequest = new
        {
            name = "Deploy Test Key",
            permissions = new[] { "Hooks.Deploy" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/api-keys", createKeyRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var keyResult = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var apiKey = keyResult!.ApiKey!.FullKey;

        // Use the API key to call the deploy endpoint
        using var apiKeyClient = CreateUnauthenticatedClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var request = new
        {
            stackId = "source:product:stack",
            stackName = "test-stack",
            environmentId = EnvironmentId
        };
        var response = await apiKeyClient.PostAsJsonAsync("/api/hooks/deploy", request);

        // Should reach handler (not 401/403)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task POST_Deploy_WithApiKeyWithoutDeployPermission_ReturnsForbidden()
    {
        // Create an API key with only SyncSources permission (not Deploy)
        var createKeyRequest = new
        {
            name = "Sync Only Key",
            permissions = new[] { "Hooks.SyncSources" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/api-keys", createKeyRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var keyResult = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var apiKey = keyResult!.ApiKey!.FullKey;

        // Use the API key — should be forbidden since it lacks Hooks.Deploy
        using var apiKeyClient = CreateUnauthenticatedClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var request = new
        {
            stackId = "source:product:stack",
            stackName = "test-stack",
            environmentId = EnvironmentId
        };
        var response = await apiKeyClient.PostAsJsonAsync("/api/hooks/deploy", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region DTOs

    private record CreateApiKeyResponse(bool Success, string? Message, ApiKeyCreatedDto? ApiKey = null);
    private record ApiKeyCreatedDto(string Id, string Name, string KeyPrefix, string FullKey);

    #endregion
}
