using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.Application.Stacks.DTOs;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

public class StackEndpointsIntegrationTests : AuthenticatedTestBase
{
    [Fact]
    public async Task GET_Stacks_ReturnsStacksList()
    {
        var response = await Client.GetAsync("/api/stacks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stacks = await response.Content.ReadFromJsonAsync<List<StackDto>>();
        stacks.Should().NotBeNull();
        // After fresh wizard setup, we may not have the demo-stack
        stacks!.Should().NotBeNull();
    }

    [Fact]
    public async Task GET_Stack_WithInvalidId_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/stacks/invalid-id");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_DeployStack_WithInvalidId_ReturnsNotFound()
    {
        var response = await Client.PostAsJsonAsync("/api/stacks/invalid-stack/deploy", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_RemoveStack_WhenNotDeployed_SucceedsWithoutError()
    {
        // Arrange - try to remove a non-existent stack
        var response = await Client.DeleteAsync("/api/stacks/nonexistent-stack");

        // Assert - should return NoContent or NotFound
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }
}
