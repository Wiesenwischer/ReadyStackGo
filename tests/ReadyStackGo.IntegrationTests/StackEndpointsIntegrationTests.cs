using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ReadyStackGo.Api;
using ReadyStackGo.Application.Auth.DTOs;
using ReadyStackGo.Application.Stacks.DTOs;
using System.Linq;

namespace ReadyStackGo.IntegrationTests;

public class StackEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public StackEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var token = await TestAuthHelper.GetAdminTokenAsync(_client);
        TestAuthHelper.AddAuthToken(_client, token);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GET_Stacks_ReturnsStacksList()
    {
        var response = await _client.GetAsync("/api/stacks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stacks = await response.Content.ReadFromJsonAsync<List<StackDto>>();
        stacks.Should().NotBeNull();
        stacks!.Should().NotBeEmpty();
        stacks.Should().ContainSingle(s => s.Id == "demo-stack");
    }

    [Fact]
    public async Task GET_Stack_ById_ReturnsStack()
    {
        var response = await _client.GetAsync("/api/stacks/demo-stack");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stack = await response.Content.ReadFromJsonAsync<StackDto>();
        stack.Should().NotBeNull();
        stack!.Id.Should().Be("demo-stack");
        stack.Name.Should().Be("Demo Stack");
        stack.Services.Should().HaveCount(2);
    }

    [Fact]
    public async Task GET_Stack_WithInvalidId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/stacks/invalid-id");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_DeployStack_DeploysSuccessfully()
    {
        // Arrange - ensure stack is not deployed first
        await _client.DeleteAsync("/api/stacks/demo-stack");

        // Act
        var response = await _client.PostAsJsonAsync("/api/stacks/demo-stack/deploy", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stack = await response.Content.ReadFromJsonAsync<StackDto>();
        stack.Should().NotBeNull();
        stack!.Id.Should().Be("demo-stack");
        stack.Status.Should().Be("Running");
        stack.DeployedAt.Should().NotBeNull();
        stack.Services.Should().HaveCount(2);
        stack.Services.Should().AllSatisfy(s =>
        {
            s.ContainerId.Should().NotBeNullOrEmpty();
            s.ContainerStatus.Should().Be("running");
        });

        // Cleanup
        await _client.DeleteAsync("/api/stacks/demo-stack");
    }

    [Fact]
    public async Task POST_DeployStack_WithInvalidId_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/stacks/invalid-stack/deploy", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_DeployStack_WhenAlreadyDeployed_RedeploysSuccessfully()
    {
        // Arrange - deploy first time
        await _client.DeleteAsync("/api/stacks/demo-stack");
        await _client.PostAsJsonAsync("/api/stacks/demo-stack/deploy", new { });

        // Act - deploy again (should clean up old containers and redeploy)
        var response = await _client.PostAsJsonAsync("/api/stacks/demo-stack/deploy", new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stack = await response.Content.ReadFromJsonAsync<StackDto>();
        stack.Should().NotBeNull();
        stack!.Status.Should().Be("Running");

        // Cleanup
        await _client.DeleteAsync("/api/stacks/demo-stack");
    }

    [Fact]
    public async Task DELETE_RemoveStack_RemovesSuccessfully()
    {
        // Arrange - deploy stack first
        await _client.PostAsJsonAsync("/api/stacks/demo-stack/deploy", new { });

        // Act
        var response = await _client.DeleteAsync("/api/stacks/demo-stack");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify stack is now NotDeployed
        var getResponse = await _client.GetAsync("/api/stacks/demo-stack");
        var stack = await getResponse.Content.ReadFromJsonAsync<StackDto>();
        stack.Should().NotBeNull();
        stack!.Status.Should().Be("NotDeployed");
        stack.DeployedAt.Should().BeNull();
        stack.Services.Should().AllSatisfy(s =>
        {
            s.ContainerId.Should().BeNullOrEmpty();
            s.ContainerStatus.Should().BeNullOrEmpty();
        });
    }

    [Fact]
    public async Task DELETE_RemoveStack_WhenNotDeployed_SucceedsWithoutError()
    {
        // Arrange - ensure stack is not deployed
        await _client.DeleteAsync("/api/stacks/demo-stack");

        // Act - remove again (should succeed idempotently)
        var response = await _client.DeleteAsync("/api/stacks/demo-stack");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullStackLifecycle_DeployAndRemove_WorksCorrectly()
    {
        // Start with clean state
        await _client.DeleteAsync("/api/stacks/demo-stack");

        // 1. Verify initial state is NotDeployed
        var initialResponse = await _client.GetAsync("/api/stacks/demo-stack");
        var initialStack = await initialResponse.Content.ReadFromJsonAsync<StackDto>();
        initialStack!.Status.Should().Be("NotDeployed");

        // 2. Deploy the stack
        var deployResponse = await _client.PostAsJsonAsync("/api/stacks/demo-stack/deploy", new { });
        deployResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deployedStack = await deployResponse.Content.ReadFromJsonAsync<StackDto>();
        deployedStack!.Status.Should().Be("Running");
        deployedStack.Services.Should().AllSatisfy(s => s.ContainerId.Should().NotBeNullOrEmpty());

        // 3. Verify stack is listed as Running
        var listResponse = await _client.GetAsync("/api/stacks");
        var stacks = await listResponse.Content.ReadFromJsonAsync<List<StackDto>>();
        var runningStack = stacks!.FirstOrDefault(s => s.Id == "demo-stack");
        runningStack.Should().NotBeNull();
        runningStack!.Status.Should().Be("Running");

        // 4. Remove the stack
        var removeResponse = await _client.DeleteAsync("/api/stacks/demo-stack");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Verify final state is NotDeployed
        var finalResponse = await _client.GetAsync("/api/stacks/demo-stack");
        var finalStack = await finalResponse.Content.ReadFromJsonAsync<StackDto>();
        finalStack!.Status.Should().Be("NotDeployed");
        finalStack.DeployedAt.Should().BeNull();
    }
}
