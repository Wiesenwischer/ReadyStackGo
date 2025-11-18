using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ReadyStackGo.Api;
using ReadyStackGo.Application.Auth.DTOs;
using ReadyStackGo.Application.Stacks.DTOs;

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

    // Note: Deploy and Delete tests require Docker to be running and accessible
    // These are skipped in CI environments but work locally for manual testing

}
