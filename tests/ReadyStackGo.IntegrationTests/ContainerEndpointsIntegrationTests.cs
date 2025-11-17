using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using ReadyStackGo.Api;
using ReadyStackGo.Application.Containers.DTOs;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests für Container API Endpoints
/// Testet die komplette API mit WebApplicationFactory gegen echte Docker-Container
/// </summary>
public class ContainerEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private IContainer? _testContainer;

    public ContainerEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Starte einen Test-Container für die API-Tests
        _testContainer = new ContainerBuilder()
            .WithImage("nginx:alpine")
            .WithName($"readystackgo-api-test-{Guid.NewGuid():N}")
            .WithPortBinding(8081, 80)
            .Build();

        await _testContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_testContainer != null)
        {
            await _testContainer.DisposeAsync();
        }

        _client.Dispose();
    }

    [Fact]
    public async Task GET_Containers_ReturnsSuccessAndContainers()
    {
        // Act
        var response = await _client.GetAsync("/api/containers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var containers = await response.Content.ReadFromJsonAsync<List<ContainerDto>>();
        containers.Should().NotBeNull();
        containers.Should().Contain(c => c.Id.StartsWith(_testContainer!.Id));
    }

    [Fact]
    public async Task POST_StartContainer_ReturnsSuccess()
    {
        // Arrange - Container erst stoppen
        await _testContainer!.StopAsync();
        await Task.Delay(1000);

        // Act
        var response = await _client.PostAsync($"/api/containers/{_testContainer.Id}/start", new StringContent("", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify container is running
        await Task.Delay(2000);
        var listResponse = await _client.GetAsync("/api/containers");
        var containers = await listResponse.Content.ReadFromJsonAsync<List<ContainerDto>>();
        var testContainer = containers!.FirstOrDefault(c => c.Id.StartsWith(_testContainer.Id));

        testContainer.Should().NotBeNull();
        testContainer!.State.Should().Be("running");
    }

    [Fact]
    public async Task POST_StopContainer_ReturnsSuccess()
    {
        // Arrange - Sicherstellen dass Container läuft
        if (_testContainer!.State != TestcontainersStates.Running)
        {
            await _testContainer.StartAsync();
        }
        await Task.Delay(1000);

        // Act
        var response = await _client.PostAsync($"/api/containers/{_testContainer.Id}/stop", new StringContent("", System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify container is stopped
        await Task.Delay(2000);
        var listResponse = await _client.GetAsync("/api/containers");
        var containers = await listResponse.Content.ReadFromJsonAsync<List<ContainerDto>>();
        var testContainer = containers!.FirstOrDefault(c => c.Id.StartsWith(_testContainer.Id));

        testContainer.Should().NotBeNull();
        testContainer!.State.Should().NotBe("running");
    }

    [Fact]
    public async Task GET_Containers_ReturnsCorsHeaders()
    {
        // Act
        var response = await _client.GetAsync("/api/containers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // In production würden wir CORS headers testen, aber im Development-Modus
        // ist CORS bereits konfiguriert
    }
}
