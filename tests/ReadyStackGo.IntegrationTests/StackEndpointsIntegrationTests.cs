using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Response DTO for GET /api/stacks
/// </summary>
public class StackResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RelativePath { get; set; }
    public List<string> Services { get; set; } = new();
    public DateTime LastSyncedAt { get; set; }
    public string? Version { get; set; }
}

public class StackEndpointsIntegrationTests : AuthenticatedTestBase
{
    [Fact]
    public async Task GET_Stacks_ReturnsStacksList()
    {
        var response = await Client.GetAsync("/api/stacks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stacks = await response.Content.ReadFromJsonAsync<List<StackResponseDto>>();
        stacks.Should().NotBeNull();
    }

    [Fact]
    public async Task GET_Stack_WithInvalidId_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/stacks/invalid-id");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_StackSources_ReturnsSourcesList()
    {
        var response = await Client.GetAsync("/api/stack-sources");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task POST_SyncSources_ReturnsSuccessResult()
    {
        var response = await Client.PostAsync("/api/stack-sources/sync", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("success");
    }
}
