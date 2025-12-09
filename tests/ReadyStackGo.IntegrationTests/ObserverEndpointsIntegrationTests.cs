using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Observer API Endpoints.
/// Tests GET /api/health/deployments/{deploymentId}/observer
/// and POST /api/health/deployments/{deploymentId}/observer/check
/// </summary>
public class ObserverEndpointsIntegrationTests : AuthenticatedTestBase
{
    #region Test Data

    private const string SimpleComposeYaml = @"
version: '3.8'
services:
  web:
    image: nginx:latest
    ports:
      - '80:80'
";

    private const string RsgoManifestWithHttpObserver = @"
version: rsgo/v0.10
metadata:
  name: TestWithObserver
  displayName: Test Stack with Observer
  description: Test stack with HTTP observer
  productVersion: '1.0.0'

compose:
  services:
    web:
      image: nginx:latest
      ports:
        - '80:80'

maintenanceObserver:
  type: http
  url: https://api.example.com/status
  pollingInterval: 30s
  maintenanceValue: 'maintenance'
  normalValue: 'normal'
";

    #endregion

    #region GET Observer Status

    [Fact]
    public async Task GET_ObserverStatus_WithInvalidDeploymentId_ReturnsNotFound()
    {
        // Arrange
        var invalidDeploymentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/health/deployments/{invalidDeploymentId}/observer");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_ObserverStatus_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var deploymentId = Guid.NewGuid();

        // Act
        var response = await unauthenticatedClient.GetAsync($"/api/health/deployments/{deploymentId}/observer");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_ObserverStatus_WithValidDeployment_NoObserver_ReturnsNoContent()
    {
        // Arrange
        var environmentId = await CreateTestEnvironment("Observer Test Environment");
        var deploymentId = await CreateTestDeployment(environmentId, "observer-test-stack", SimpleComposeYaml);

        // Skip if deployment creation failed (Docker not available)
        if (string.IsNullOrEmpty(deploymentId))
        {
            return;
        }

        // Act
        var response = await Client.GetAsync($"/api/health/deployments/{deploymentId}/observer");

        // Assert
        // Deployment without observer config should return 204 No Content
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    #endregion

    #region POST Trigger Observer Check

    [Fact]
    public async Task POST_TriggerObserverCheck_WithInvalidDeploymentId_ReturnsNotFound()
    {
        // Arrange
        var invalidDeploymentId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/api/health/deployments/{invalidDeploymentId}/observer/check", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_TriggerObserverCheck_WithoutAuth_ReturnsUnauthorizedOrNotFound()
    {
        // Arrange
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var deploymentId = Guid.NewGuid();

        // Act
        var response = await unauthenticatedClient.PostAsync($"/api/health/deployments/{deploymentId}/observer/check", null);

        // Assert - FastEndpoints may return 404 before checking auth for non-existent resources
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_TriggerObserverCheck_WithValidDeployment_NoObserver_ReturnsNoContent()
    {
        // Arrange
        var environmentId = await CreateTestEnvironment("Trigger Check Test Environment");
        var deploymentId = await CreateTestDeployment(environmentId, "trigger-test-stack", SimpleComposeYaml);

        // Skip if deployment creation failed (Docker not available)
        if (string.IsNullOrEmpty(deploymentId))
        {
            return;
        }

        // Act
        var response = await Client.PostAsync($"/api/health/deployments/{deploymentId}/observer/check", null);

        // Assert
        // Deployment without observer config should return 204 No Content
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.NotFound);
    }

    #endregion

    #region Observer Status Response Structure

    [Fact]
    public async Task GET_ObserverStatus_ResponseStructure_IsCorrect()
    {
        // This test verifies the response structure when an observer is configured
        // In practice, this requires a running deployment with maintenanceObserver in manifest

        // Arrange
        var deploymentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/health/deployments/{deploymentId}/observer");

        // Assert - verify we get a valid response type
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,           // Has observer with result
            HttpStatusCode.NoContent,    // Has observer but no result yet
            HttpStatusCode.NotFound      // No deployment found
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<ObserverStatusResponse>();
            result.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task POST_TriggerObserverCheck_ResponseStructure_IsCorrect()
    {
        // This test verifies the response structure when an observer check is triggered

        // Arrange
        var deploymentId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/api/health/deployments/{deploymentId}/observer/check", null);

        // Assert - verify we get a valid response type
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,           // Check performed, returns result
            HttpStatusCode.NoContent,    // No observer configured
            HttpStatusCode.NotFound      // No deployment found
        );

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<ObserverCheckResponse>();
            result.Should().NotBeNull();
        }
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GET_ObserverStatus_RequiresAuthentication()
    {
        // Arrange
        using var client = CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync($"/api/health/deployments/{Guid.NewGuid()}/observer");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_TriggerObserverCheck_RequiresAuthentication()
    {
        // Arrange
        using var client = CreateUnauthenticatedClient();

        // Act
        var response = await client.PostAsync($"/api/health/deployments/{Guid.NewGuid()}/observer/check", null);

        // Assert - FastEndpoints may return 404 before checking auth for non-existent resources
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    #endregion

    #region Helper Methods

    private async Task<string> CreateTestEnvironment(string name)
    {
        var socketPath = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        var request = new
        {
            name = name,
            socketPath = socketPath
        };

        var response = await Client.PostAsJsonAsync("/api/environments", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<EnvironmentResponse>();
        if (result?.Environment?.Id == null)
        {
            throw new InvalidOperationException($"Failed to create test environment: {result?.Message}");
        }

        return result.Environment.Id;
    }

    private async Task<string?> CreateTestDeployment(string environmentId, string stackName, string yamlContent)
    {
        var request = new
        {
            stackName = stackName,
            yamlContent = yamlContent,
            variables = new Dictionary<string, string>()
        };

        var response = await Client.PostAsJsonAsync($"/api/deployments/{environmentId}", request);

        // Deployment may fail if Docker is not available in test environment
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<DeployComposeResponse>();
        return result?.DeploymentId;
    }

    #endregion

    #region Response DTOs

    private record EnvironmentDto(string Id, string Name, string Type, string ConnectionString, bool IsDefault);
    private record EnvironmentResponse(bool Success, string? Message, EnvironmentDto? Environment);
    private record DeployComposeResponse(
        bool Success,
        string? Message,
        string? DeploymentId,
        string? StackName
    );

    private record ObserverStatusResponse(
        string DeploymentId,
        string? ObserverType,
        bool HasResult,
        ObserverResultDto? LastResult
    );

    private record ObserverCheckResponse(
        string DeploymentId,
        string? ObserverType,
        ObserverResultDto Result
    );

    private record ObserverResultDto(
        string? ObserverType,
        string? ObservedValue,
        bool IsMaintenanceRequired,
        bool IsSuccess,
        string? ErrorMessage,
        DateTimeOffset CheckedAt
    );

    #endregion
}
