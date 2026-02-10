using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for API Key Management Endpoints.
/// Tests CRUD operations for CI/CD API key management (v0.19 feature).
/// </summary>
public class ApiKeyEndpointsIntegrationTests : AuthenticatedTestBase
{
    #region List API Keys

    [Fact]
    public async Task GET_ListApiKeys_ReturnsSuccess()
    {
        var response = await Client.GetAsync("/api/api-keys");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListApiKeysResponse>();
        result.Should().NotBeNull();
        result!.ApiKeys.Should().NotBeNull();
    }

    [Fact]
    public async Task GET_ListApiKeys_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthenticatedClient = CreateUnauthenticatedClient();

        var response = await unauthenticatedClient.GetAsync("/api/api-keys");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_ListApiKeys_ReturnsEmptyList_WhenNoKeysExist()
    {
        var response = await Client.GetAsync("/api/api-keys");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListApiKeysResponse>();
        result!.ApiKeys.Should().BeEmpty();
    }

    #endregion

    #region Create API Key

    [Fact]
    public async Task POST_CreateApiKey_WithValidData_ReturnsCreated()
    {
        var request = new
        {
            name = "Test CI/CD Key",
            permissions = new[] { "Hooks.Redeploy", "Hooks.Upgrade" }
        };

        var response = await Client.PostAsJsonAsync("/api/api-keys", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.ApiKey.Should().NotBeNull();
        result.ApiKey!.Name.Should().Be("Test CI/CD Key");
        result.ApiKey.FullKey.Should().StartWith("rsgo_");
        result.ApiKey.FullKey.Should().HaveLength(37);
        result.ApiKey.KeyPrefix.Should().HaveLength(12);
    }

    [Fact]
    public async Task POST_CreateApiKey_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var request = new
        {
            name = "Unauthorized Key",
            permissions = new[] { "Hooks.Redeploy" }
        };

        var response = await unauthenticatedClient.PostAsJsonAsync("/api/api-keys", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_CreateApiKey_CreatedKeyAppearsInList()
    {
        // Create
        var request = new
        {
            name = "Listed Key",
            permissions = new[] { "Hooks.Redeploy" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/api-keys", request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // List
        var listResponse = await Client.GetAsync("/api/api-keys");
        var result = await listResponse.Content.ReadFromJsonAsync<ListApiKeysResponse>();
        result!.ApiKeys.Should().Contain(k => k.Name == "Listed Key");
    }

    [Fact]
    public async Task POST_CreateApiKey_WithEmptyName_ReturnsBadRequest()
    {
        var request = new
        {
            name = "",
            permissions = new[] { "Hooks.Redeploy" }
        };

        var response = await Client.PostAsJsonAsync("/api/api-keys", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_CreateApiKey_WithNoPermissions_ReturnsBadRequest()
    {
        var request = new
        {
            name = "No Perms Key",
            permissions = Array.Empty<string>()
        };

        var response = await Client.PostAsJsonAsync("/api/api-keys", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_CreateApiKey_DuplicateName_ReturnsBadRequest()
    {
        var request = new
        {
            name = "Duplicate Name Test",
            permissions = new[] { "Hooks.Redeploy" }
        };

        var first = await Client.PostAsJsonAsync("/api/api-keys", request);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Client.PostAsJsonAsync("/api/api-keys", request);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Revoke API Key

    [Fact]
    public async Task DELETE_RevokeApiKey_ExistingKey_ReturnsSuccess()
    {
        // Create key first
        var createRequest = new
        {
            name = "Key To Revoke",
            permissions = new[] { "Hooks.Redeploy" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/api-keys", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var keyId = created!.ApiKey!.Id;

        // Revoke
        var response = await Client.DeleteAsync($"/api/api-keys/{keyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RevokeApiKeyResponse>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DELETE_RevokeApiKey_AppearAsRevokedInList()
    {
        // Create key first
        var createRequest = new
        {
            name = "Key To Check Revoked",
            permissions = new[] { "Hooks.Redeploy" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/api-keys", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        var keyId = created!.ApiKey!.Id;

        // Revoke
        await Client.DeleteAsync($"/api/api-keys/{keyId}");

        // List
        var listResponse = await Client.GetAsync("/api/api-keys");
        var result = await listResponse.Content.ReadFromJsonAsync<ListApiKeysResponse>();
        var revokedKey = result!.ApiKeys.FirstOrDefault(k => k.Id == keyId);
        revokedKey.Should().NotBeNull();
        revokedKey!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task DELETE_RevokeApiKey_NonExistentId_ReturnsNotFound()
    {
        var response = await Client.DeleteAsync($"/api/api-keys/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_RevokeApiKey_WithoutAuth_ReturnsUnauthorized()
    {
        using var unauthenticatedClient = CreateUnauthenticatedClient();

        var response = await unauthenticatedClient.DeleteAsync($"/api/api-keys/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Response DTOs

    private record ListApiKeysResponse(ApiKeyDto[] ApiKeys);

    private record CreateApiKeyResponse(bool Success, string? Message, ApiKeyCreatedDto? ApiKey);

    private record ApiKeyCreatedDto(string Id, string Name, string KeyPrefix, string FullKey);

    private record ApiKeyDto(
        string Id,
        string Name,
        string KeyPrefix,
        string OrganizationId,
        string? EnvironmentId,
        string[] Permissions,
        string CreatedAt,
        string? LastUsedAt,
        string? ExpiresAt,
        bool IsRevoked);

    private record RevokeApiKeyResponse(bool Success, string? Message);

    #endregion
}
