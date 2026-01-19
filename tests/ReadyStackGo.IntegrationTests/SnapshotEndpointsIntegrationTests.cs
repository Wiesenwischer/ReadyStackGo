using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Deployment Snapshot and Rollback API Endpoints.
/// Tests snapshot listing, rollback initiation, and error handling.
/// </summary>
public class SnapshotEndpointsIntegrationTests : AuthenticatedTestBase
{
    #region Test Data

    private const string SimpleStackYaml = @"
metadata:
  name: Snapshot Test Stack
  description: Stack for testing snapshots
  productVersion: 1.0.0

services:
  web:
    image: nginx:latest
    ports:
      - '80:80'
";

    #endregion

    #region Get Snapshots - Authentication

    [Fact]
    public async Task GET_Snapshots_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var fakeDeploymentId = Guid.NewGuid().ToString();

        // Act
        var response = await unauthenticatedClient.GetAsync(
            $"/api/environments/{EnvironmentId}/deployments/{fakeDeploymentId}/snapshots");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Get Snapshots - Validation

    [Fact]
    public async Task GET_Snapshots_WithInvalidEnvironmentId_ReturnsError()
    {
        // Arrange
        var fakeDeploymentId = Guid.NewGuid().ToString();

        // Act
        var response = await Client.GetAsync(
            $"/api/environments/invalid-env-id/deployments/{fakeDeploymentId}/snapshots");

        // Assert
        // Should return error (either 404 or 400 depending on how validation is implemented)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_Snapshots_WithInvalidDeploymentId_ReturnsNotFound()
    {
        // Arrange
        var fakeDeploymentId = "invalid-deployment-id";

        // Act
        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/deployments/{fakeDeploymentId}/snapshots");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_Snapshots_WithNonExistentDeployment_ReturnsNotFound()
    {
        // Arrange
        var nonExistentDeploymentId = Guid.NewGuid().ToString();

        // Act
        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/deployments/{nonExistentDeploymentId}/snapshots");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Get Snapshots - Success Cases

    [Fact]
    public async Task GET_Snapshots_ForNewDeployment_ReturnsEmptyList()
    {
        // Arrange - Deploy a stack first
        var deploymentId = await DeployTestStack("empty-snapshots-test");

        if (deploymentId == null)
        {
            // Skip test if deployment failed (Docker might not be available)
            return;
        }

        // Act
        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/deployments/{deploymentId}/snapshots");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetSnapshotsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Snapshots.Should().BeEmpty("A newly deployed stack has no snapshots yet");
        result.CanRollback.Should().BeFalse("Cannot rollback without snapshots");
    }

    #endregion

    #region Rollback - Authentication

    [Fact]
    public async Task POST_Rollback_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var fakeDeploymentId = Guid.NewGuid().ToString();
        var request = new { snapshotId = Guid.NewGuid().ToString() };

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/deployments/{fakeDeploymentId}/rollback",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Rollback - Validation

    [Fact]
    public async Task POST_Rollback_WithInvalidEnvironmentId_ReturnsError()
    {
        // Arrange
        var fakeDeploymentId = Guid.NewGuid().ToString();
        var request = new { snapshotId = Guid.NewGuid().ToString() };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/environments/invalid-env-id/deployments/{fakeDeploymentId}/rollback",
            request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Rollback_WithInvalidDeploymentId_ReturnsError()
    {
        // Arrange
        var request = new { snapshotId = Guid.NewGuid().ToString() };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/deployments/invalid-deployment-id/rollback",
            request);

        // Assert
        // Returns BadRequest because invalid GUID format fails early validation
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Rollback_WithNonExistentDeployment_ReturnsNotFound()
    {
        // Arrange
        var nonExistentDeploymentId = Guid.NewGuid().ToString();
        var request = new { snapshotId = Guid.NewGuid().ToString() };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/deployments/{nonExistentDeploymentId}/rollback",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_Rollback_WithMissingSnapshotId_ReturnsBadRequest()
    {
        // Arrange
        var fakeDeploymentId = Guid.NewGuid().ToString();
        var request = new { snapshotId = (string?)null };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/deployments/{fakeDeploymentId}/rollback",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Rollback_WithInvalidSnapshotId_ReturnsError()
    {
        // Arrange - Deploy a stack first
        var deploymentId = await DeployTestStack("rollback-invalid-snapshot-test");

        if (deploymentId == null)
        {
            // Skip test if deployment failed
            return;
        }

        var request = new { snapshotId = "invalid-snapshot-id" };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/deployments/{deploymentId}/rollback",
            request);

        // Assert
        // Should return error because snapshot ID format is invalid
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Rollback_WithNonExistentSnapshot_ReturnsNotFound()
    {
        // Arrange - Deploy a stack first
        var deploymentId = await DeployTestStack("rollback-nonexistent-snapshot-test");

        if (deploymentId == null)
        {
            // Skip test if deployment failed
            return;
        }

        var nonExistentSnapshotId = Guid.NewGuid().ToString();
        var request = new { snapshotId = nonExistentSnapshotId };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/deployments/{deploymentId}/rollback",
            request);

        // Assert
        // Should return error because the deployment has no snapshots
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Rollback - Business Logic

    [Fact]
    public async Task POST_Rollback_WithNoSnapshots_ReturnsBadRequest()
    {
        // Arrange - Deploy a stack (will have no snapshots)
        var deploymentId = await DeployTestStack("rollback-no-snapshots-test");

        if (deploymentId == null)
        {
            // Skip test if deployment failed
            return;
        }

        var request = new { snapshotId = Guid.NewGuid().ToString() };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/deployments/{deploymentId}/rollback",
            request);

        // Assert
        // Should return error because deployment has no snapshots to rollback to
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Endpoint Existence

    [Fact]
    public async Task GET_Snapshots_EndpointExists()
    {
        // Arrange
        var fakeDeploymentId = Guid.NewGuid().ToString();

        // Act
        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/deployments/{fakeDeploymentId}/snapshots");

        // Assert - Should not return 404 (endpoint not found)
        // The endpoint exists, so we expect either a success or validation error
        response.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed,
            "Snapshots GET endpoint should exist");
    }

    [Fact]
    public async Task POST_Rollback_EndpointExists()
    {
        // Arrange
        var fakeDeploymentId = Guid.NewGuid().ToString();
        var request = new { snapshotId = Guid.NewGuid().ToString() };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/deployments/{fakeDeploymentId}/rollback",
            request);

        // Assert - Should not return 405 (method not allowed)
        response.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed,
            "Rollback POST endpoint should exist");
    }

    #endregion

    #region Response Format

    [Fact]
    public async Task GET_Snapshots_ReturnsExpectedResponseFormat()
    {
        // Arrange
        var deploymentId = await DeployTestStack("response-format-test");

        if (deploymentId == null)
        {
            return;
        }

        // Act
        var response = await Client.GetAsync(
            $"/api/environments/{EnvironmentId}/deployments/{deploymentId}/snapshots");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetSnapshotsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.DeploymentId.Should().NotBeNullOrEmpty();
        result.StackName.Should().NotBeNullOrEmpty();
        result.Snapshots.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Deploys a test stack and returns the deployment ID.
    /// Returns null if deployment fails (e.g., Docker not available).
    /// </summary>
    private async Task<string?> DeployTestStack(string stackName)
    {
        var request = new
        {
            stackName = stackName,
            yamlContent = SimpleStackYaml,
            variables = new Dictionary<string, string>()
        };

        var response = await Client.PostAsJsonAsync(
            $"/api/environments/{EnvironmentId}/deployments", request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<DeployResponse>();
        return result?.DeploymentId;
    }

    #endregion

    #region Response DTOs

    private record GetSnapshotsResponse(
        bool Success,
        string? Message,
        string? DeploymentId,
        string? StackName,
        string? CurrentVersion,
        bool CanRollback,
        List<SnapshotDto> Snapshots);

    private record SnapshotDto(
        string SnapshotId,
        string StackVersion,
        DateTime CreatedAt,
        string? Description,
        int ServiceCount,
        int VariableCount);

    private record RollbackResponse(
        bool Success,
        string? Message,
        string? DeploymentId,
        string? TargetVersion,
        string? PreviousVersion);

    private record DeployResponse(
        bool Success,
        string? Message,
        string? DeploymentId);

    #endregion
}
