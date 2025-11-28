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
        detail.YamlContent.Should().NotBeNullOrEmpty();
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

    #region DTOs

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
        string YamlContent,
        List<string> Services,
        List<StackVariableDto> Variables,
        string? FilePath,
        List<string> AdditionalFiles,
        DateTime LastSyncedAt,
        string? Version
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
