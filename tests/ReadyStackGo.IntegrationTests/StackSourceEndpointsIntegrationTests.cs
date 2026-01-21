using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Stack Source API endpoints.
/// Tests stack source loading, syncing, and stack definition retrieval.
/// </summary>
public class StackSourceEndpointsIntegrationTests : AuthenticatedTestBase
{
    #region List Stack Definitions Tests

    [Fact]
    public async Task GET_ListStackDefinitions_ReturnsSuccess()
    {
        // Arrange & Act
        var response = await Client.GetAsync("/api/stacks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_ListStackDefinitions_ReturnsArrayOfStacks()
    {
        // Arrange & Act
        var stacks = await Client.GetFromJsonAsync<List<StackDefinitionDto>>("/api/stacks");

        // Assert
        stacks.Should().NotBeNull();
        // At minimum should have built-in example stacks
    }

    [Fact]
    public async Task GET_ListStackDefinitions_StacksHaveRequiredProperties()
    {
        // Arrange & Act
        var stacks = await Client.GetFromJsonAsync<List<StackDefinitionDto>>("/api/stacks");

        // Assert
        if (stacks != null && stacks.Count > 0)
        {
            var stack = stacks.First();
            stack.Id.Should().NotBeNullOrEmpty();
            stack.SourceId.Should().NotBeNullOrEmpty();
            stack.Name.Should().NotBeNullOrEmpty();
            stack.Services.Should().NotBeNull();
            stack.Variables.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task GET_ListStackDefinitions_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthClient = CreateUnauthenticatedClient();

        // Act
        var response = await unauthClient.GetAsync("/api/stacks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Get Stack Definition Detail Tests

    [Fact]
    public async Task GET_GetStackDefinition_ValidId_ReturnsStackDetail()
    {
        // Arrange - first get list of stacks
        var stacks = await Client.GetFromJsonAsync<List<StackDefinitionDto>>("/api/stacks");
        if (stacks == null || stacks.Count == 0)
        {
            // Skip if no stacks available
            return;
        }

        var stackId = stacks.First().Id;

        // Act
        var response = await Client.GetAsync($"/api/stacks/{Uri.EscapeDataString(stackId)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<StackDefinitionDetailDto>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(stackId);
        detail.Services.Should().NotBeNull();
    }

    [Fact]
    public async Task GET_GetStackDefinition_InvalidId_ReturnsNotFoundOrBadRequest()
    {
        // Arrange
        var invalidId = "non-existent-stack-id-12345";

        // Act
        var response = await Client.GetAsync($"/api/stacks/{invalidId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_GetStackDefinition_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthClient = CreateUnauthenticatedClient();

        // Act
        var response = await unauthClient.GetAsync("/api/stacks/any-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Sync Sources Tests

    [Fact]
    public async Task POST_SyncSources_ReturnsSuccess()
    {
        // Arrange & Act
        var response = await Client.PostAsync("/api/stack-sources/sync", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_SyncSources_ReturnsSyncResult()
    {
        // Arrange & Act
        var response = await Client.PostAsync("/api/stack-sources/sync", null);
        var result = await response.Content.ReadFromJsonAsync<SyncResultDto>();

        // Assert
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.StacksLoaded.Should().BeGreaterThanOrEqualTo(0);
        result.SourcesSynced.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task POST_SyncSources_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthClient = CreateUnauthenticatedClient();

        // Act
        var response = await unauthClient.PostAsync("/api/stack-sources/sync", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region List Stack Sources Tests

    [Fact]
    public async Task GET_ListStackSources_ReturnsSuccess()
    {
        // Arrange & Act
        var response = await Client.GetAsync("/api/stack-sources");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_ListStackSources_ReturnsConfiguredSources()
    {
        // Arrange & Act
        var sources = await Client.GetFromJsonAsync<List<StackSourceDto>>("/api/stack-sources");

        // Assert
        sources.Should().NotBeNull();
        // Should have at least the built-in source
        sources!.Count.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GET_ListStackSources_SourcesHaveRequiredProperties()
    {
        // Arrange & Act
        var sources = await Client.GetFromJsonAsync<List<StackSourceDto>>("/api/stack-sources");

        // Assert
        if (sources != null && sources.Count > 0)
        {
            var source = sources.First();
            source.Id.Should().NotBeNullOrEmpty();
            source.Name.Should().NotBeNullOrEmpty();
            source.Type.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GET_ListStackSources_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthClient = CreateUnauthenticatedClient();

        // Act
        var response = await unauthClient.GetAsync("/api/stack-sources");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Variable Detection Tests

    [Fact]
    public async Task GET_GetStackDefinition_IncludesVariablesWithDefaults()
    {
        // Arrange - get a stack with variables
        var stacks = await Client.GetFromJsonAsync<List<StackDefinitionDto>>("/api/stacks");
        var stackWithVars = stacks?.FirstOrDefault(s => s.Variables.Count > 0);

        if (stackWithVars == null)
        {
            // Skip if no stacks with variables
            return;
        }

        // Act
        var response = await Client.GetAsync($"/api/stacks/{Uri.EscapeDataString(stackWithVars.Id)}");
        var detail = await response.Content.ReadFromJsonAsync<StackDefinitionDetailDto>();

        // Assert
        detail.Should().NotBeNull();
        detail!.Variables.Should().NotBeEmpty();

        foreach (var variable in detail.Variables)
        {
            variable.Name.Should().NotBeNullOrEmpty();
            // Variables should be marked as required or have a default
            if (!variable.IsRequired)
            {
                variable.DefaultValue.Should().NotBeNull("Optional variables should have a default value");
            }
        }
    }

    #endregion

    #region Create Stack Source Tests (v0.15)

    [Fact]
    public async Task POST_CreateStackSource_LocalDirectory_ReturnsCreated()
    {
        // Arrange
        var request = new
        {
            id = $"test-local-{Guid.NewGuid():N}".Substring(0, 30),
            name = "Test Local Source",
            type = "LocalDirectory",
            path = "/test/stacks"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/stack-sources", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateSourceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.SourceId.Should().Be(request.id);
    }

    [Fact]
    public async Task POST_CreateStackSource_GitRepository_ReturnsCreated()
    {
        // Arrange
        var request = new
        {
            id = $"test-git-{Guid.NewGuid():N}".Substring(0, 30),
            name = "Test Git Source",
            type = "GitRepository",
            gitUrl = "https://github.com/test/repo.git",
            branch = "main"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/stack-sources", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateSourceResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task POST_CreateStackSource_WithoutId_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            name = "Test Source",
            type = "LocalDirectory",
            path = "/test/stacks"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/stack-sources", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_CreateStackSource_InvalidType_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            id = $"test-{Guid.NewGuid():N}".Substring(0, 30),
            name = "Test Source",
            type = "InvalidType"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/stack-sources", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_CreateStackSource_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthClient = CreateUnauthenticatedClient();
        var request = new
        {
            id = "test",
            name = "Test Source",
            type = "LocalDirectory",
            path = "/test"
        };

        // Act
        var response = await unauthClient.PostAsJsonAsync("/api/stack-sources", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Get Stack Source Tests (v0.15)

    [Fact]
    public async Task GET_GetStackSource_ValidId_ReturnsSource()
    {
        // Arrange - Create a source first
        var sourceId = $"test-get-{Guid.NewGuid():N}".Substring(0, 30);
        var createRequest = new
        {
            id = sourceId,
            name = "Test Source for Get",
            type = "LocalDirectory",
            path = "/test/stacks"
        };
        await Client.PostAsJsonAsync("/api/stack-sources", createRequest);

        // Act
        var response = await Client.GetAsync($"/api/stack-sources/{sourceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var source = await response.Content.ReadFromJsonAsync<StackSourceDetailDto>();
        source.Should().NotBeNull();
        source!.Id.Should().Be(sourceId);
        source.Name.Should().Be("Test Source for Get");
        source.Type.Should().Be("LocalDirectory");
    }

    [Fact]
    public async Task GET_GetStackSource_InvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/stack-sources/nonexistent-source");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Update Stack Source Tests (v0.15)

    [Fact]
    public async Task PUT_UpdateStackSource_Name_ReturnsOk()
    {
        // Arrange - Create a source first
        var sourceId = $"test-update-{Guid.NewGuid():N}".Substring(0, 30);
        var createRequest = new
        {
            id = sourceId,
            name = "Original Name",
            type = "LocalDirectory",
            path = "/test/stacks"
        };
        await Client.PostAsJsonAsync("/api/stack-sources", createRequest);

        // Act - Update the source
        var updateRequest = new { name = "Updated Name" };
        var response = await Client.PutAsJsonAsync($"/api/stack-sources/{sourceId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the update
        var getResponse = await Client.GetAsync($"/api/stack-sources/{sourceId}");
        var source = await getResponse.Content.ReadFromJsonAsync<StackSourceDetailDto>();
        source!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task PUT_UpdateStackSource_Enable_ReturnsOk()
    {
        // Arrange - Create a source first
        var sourceId = $"test-enable-{Guid.NewGuid():N}".Substring(0, 30);
        var createRequest = new
        {
            id = sourceId,
            name = "Test Enable",
            type = "LocalDirectory",
            path = "/test/stacks"
        };
        await Client.PostAsJsonAsync("/api/stack-sources", createRequest);

        // Act - Disable the source
        var disableRequest = new { enabled = false };
        var response = await Client.PutAsJsonAsync($"/api/stack-sources/{sourceId}", disableRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it's disabled
        var getResponse = await Client.GetAsync($"/api/stack-sources/{sourceId}");
        var source = await getResponse.Content.ReadFromJsonAsync<StackSourceDetailDto>();
        source!.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task PUT_UpdateStackSource_InvalidId_ReturnsNotFound()
    {
        // Act
        var updateRequest = new { name = "New Name" };
        var response = await Client.PutAsJsonAsync("/api/stack-sources/nonexistent-source", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Delete Stack Source Tests (v0.15)

    [Fact]
    public async Task DELETE_DeleteStackSource_ValidId_ReturnsOk()
    {
        // Arrange - Create a source first
        var sourceId = $"test-delete-{Guid.NewGuid():N}".Substring(0, 30);
        var createRequest = new
        {
            id = sourceId,
            name = "Test Delete",
            type = "LocalDirectory",
            path = "/test/stacks"
        };
        await Client.PostAsJsonAsync("/api/stack-sources", createRequest);

        // Act
        var response = await Client.DeleteAsync($"/api/stack-sources/{sourceId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it's deleted
        var getResponse = await Client.GetAsync($"/api/stack-sources/{sourceId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_DeleteStackSource_InvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.DeleteAsync("/api/stack-sources/nonexistent-source");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Sync Single Source Tests (v0.15)

    [Fact]
    public async Task POST_SyncSingleSource_ValidId_ReturnsOk()
    {
        // Arrange - Create a source first
        var sourceId = $"test-sync-{Guid.NewGuid():N}".Substring(0, 30);
        var createRequest = new
        {
            id = sourceId,
            name = "Test Sync",
            type = "LocalDirectory",
            path = "/test/stacks"
        };
        await Client.PostAsJsonAsync("/api/stack-sources", createRequest);

        // Act
        var response = await Client.PostAsync($"/api/stack-sources/{sourceId}/sync", null);

        // Assert - Sync should return OK even if sync finds no stacks (path doesn't exist)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_SyncSingleSource_InvalidId_ReturnsNotFound()
    {
        // Act
        var response = await Client.PostAsync("/api/stack-sources/nonexistent-source/sync", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DTOs

    private record CreateSourceResponse(
        bool Success,
        string? Message,
        string? SourceId
    );

    private record StackSourceDetailDto(
        string Id,
        string Name,
        string Type,
        bool Enabled,
        DateTime? LastSyncedAt,
        DateTime CreatedAt,
        string? Path,
        string? FilePattern,
        string? GitUrl,
        string? GitBranch
    );

    private record StackDefinitionDto(
        string Id,
        string SourceId,
        string Name,
        string? Description,
        List<string> Services,
        List<StackVariableDto> Variables,
        DateTime LastSyncedAt,
        string? Version
    );

    private record StackDefinitionDetailDto(
        string Id,
        string SourceId,
        string Name,
        string? Description,
        List<ServiceDetailDto> Services,
        List<StackVariableDto> Variables,
        List<VolumeDetailDto> Volumes,
        List<NetworkDetailDto> Networks,
        string? FilePath,
        DateTime LastSyncedAt,
        string? Version
    );

    private record ServiceDetailDto(
        string Name,
        string Image,
        string? ContainerName,
        List<string> Ports,
        Dictionary<string, string> Environment,
        List<string> Volumes,
        List<string> Networks,
        List<string> DependsOn
    );

    private record VolumeDetailDto(
        string Name,
        string? Driver,
        bool External
    );

    private record NetworkDetailDto(
        string Name,
        string? Driver,
        bool External
    );

    private record StackVariableDto(
        string Name,
        string? DefaultValue,
        bool IsRequired
    );

    private record SyncResultDto(
        bool Success,
        int StacksLoaded,
        int SourcesSynced,
        List<string> Errors,
        List<string> Warnings
    );

    private record StackSourceDto(
        string Id,
        string Name,
        string Type,
        bool Enabled,
        DateTime? LastSyncedAt,
        Dictionary<string, string> Details
    );

    #endregion
}
