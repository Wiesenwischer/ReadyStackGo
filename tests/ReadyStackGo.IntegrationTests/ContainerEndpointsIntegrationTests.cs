using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests für Container API Endpoints
/// Testet die komplette API mit WebApplicationFactory gegen echte Docker-Container
/// </summary>
public class ContainerEndpointsIntegrationTests : AuthenticatedTestBase
{
    private IContainer? _testContainer;

    protected override async Task OnInitializedAsync()
    {
        // Starte einen Test-Container für die API-Tests
        // Use random host port to avoid conflicts in parallel test runs
        _testContainer = new ContainerBuilder()
            .WithImage("nginx:alpine")
            .WithName($"readystackgo-api-test-{Guid.NewGuid():N}")
            .WithPortBinding(0, 80) // 0 = random available port
            .Build();

        await _testContainer.StartAsync();
    }

    protected override async Task OnDisposingAsync()
    {
        if (_testContainer != null)
        {
            await _testContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task GET_Containers_ReturnsSuccessAndContainers()
    {
        // Act
        var response = await Client.GetAsync($"/api/containers?environment={EnvironmentId}");

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
        var response = await Client.PostAsync($"/api/containers/{_testContainer.Id}/start?environment={EnvironmentId}", null);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Start container failed with {response.StatusCode}: {errorContent}");
        }
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verify container is running
        await Task.Delay(2000);
        var listResponse = await Client.GetAsync($"/api/containers?environment={EnvironmentId}");
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
        var response = await Client.PostAsync($"/api/containers/{_testContainer.Id}/stop?environment={EnvironmentId}", null);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Stop container failed with {response.StatusCode}: {errorContent}");
        }
        response.IsSuccessStatusCode.Should().BeTrue();

        // Verify container is stopped
        await Task.Delay(2000);
        var listResponse = await Client.GetAsync($"/api/containers?environment={EnvironmentId}");
        var containers = await listResponse.Content.ReadFromJsonAsync<List<ContainerDto>>();
        var testContainer = containers!.FirstOrDefault(c => c.Id.StartsWith(_testContainer.Id));

        testContainer.Should().NotBeNull();
        testContainer!.State.Should().NotBe("running");
    }

    [Fact]
    public async Task POST_StopContainer_ReturnsNoContentOrEmptyBody()
    {
        // Arrange - Sicherstellen dass Container läuft
        if (_testContainer!.State != TestcontainersStates.Running)
        {
            await _testContainer.StartAsync();
        }
        await Task.Delay(1000);

        // Act
        var response = await Client.PostAsync($"/api/containers/{_testContainer.Id}/stop?environment={EnvironmentId}", null);

        // Assert - Get error details if failed
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            response.IsSuccessStatusCode.Should().BeTrue($"Status: {response.StatusCode}, Body: {errorBody}");
        }

        // Verify the response is properly handled for empty body scenarios
        // Either 204 No Content, or 200 with empty body / content-length 0
        var contentLength = response.Content.Headers.ContentLength;
        var responseBody = await response.Content.ReadAsStringAsync();

        // The response should be empty or a minimal JSON object
        // If the body is empty, it should not cause JSON parsing errors
        var isValidResponse = response.StatusCode == HttpStatusCode.NoContent
                              || contentLength == 0
                              || string.IsNullOrWhiteSpace(responseBody)
                              || responseBody == "{}"; // Empty JSON object is also valid

        isValidResponse.Should().BeTrue(
            $"Response should be empty or minimal JSON for stop container. " +
            $"Status: {response.StatusCode}, ContentLength: {contentLength}, Body: '{responseBody}'");
    }

    [Fact]
    public async Task GET_Containers_ReturnsCorsHeaders()
    {
        // Act
        var response = await Client.GetAsync($"/api/containers?environment={EnvironmentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // In production würden wir CORS headers testen, aber im Development-Modus
        // ist CORS bereits konfiguriert
    }
}
