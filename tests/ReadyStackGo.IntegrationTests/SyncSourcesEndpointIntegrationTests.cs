using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for the Sync Sources Webhook Endpoint.
/// Tests authentication, authorization, and basic sync functionality.
/// </summary>
public class SyncSourcesEndpointIntegrationTests : AuthenticatedTestBase
{
    #region Authentication

    [Fact]
    public async Task POST_SyncSources_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthenticatedClient = CreateUnauthenticatedClient();

        var response = await unauthenticatedClient.PostAsync("/api/hooks/sync-sources", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_SyncSources_WithJwtAuth_ReturnsSuccess()
    {
        var response = await Client.PostAsync("/api/hooks/sync-sources", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SyncSourcesHookResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().Contain("Synced");
    }

    #endregion

    #region Sync Result

    [Fact]
    public async Task POST_SyncSources_ReturnsStackAndSourceCounts()
    {
        var response = await Client.PostAsync("/api/hooks/sync-sources", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SyncSourcesHookResponse>();
        result.Should().NotBeNull();
        result!.SourcesSynced.Should().BeGreaterThanOrEqualTo(0);
        result.StacksLoaded.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region API Key Authentication

    [Fact]
    public async Task POST_SyncSources_WithApiKey_IsAllowed()
    {
        var createKeyRequest = new
        {
            name = "Sync Test Key",
            permissions = new[] { "Hooks.SyncSources" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/api-keys", createKeyRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var keyResult = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var apiKey = keyResult!.ApiKey!.FullKey;

        using var apiKeyClient = CreateUnauthenticatedClient();
        apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var response = await apiKeyClient.PostAsync("/api/hooks/sync-sources", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SyncSourcesHookResponse>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task POST_SyncSources_WithApiKeyWithoutSyncPermission_ReturnsForbidden()
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

        var response = await apiKeyClient.PostAsync("/api/hooks/sync-sources", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region DTOs

    private record SyncSourcesHookResponse(bool Success, int StacksLoaded, int SourcesSynced, string? Message);
    private record CreateApiKeyResponse(bool Success, string? Message, ApiKeyCreatedDto? ApiKey = null);
    private record ApiKeyCreatedDto(string Id, string Name, string KeyPrefix, string FullKey);

    #endregion
}
