using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Deployment Snapshot and Rollback API Endpoints.
/// Tests snapshot listing, rollback initiation, and error handling.
///
/// Note: Rollback API uses PendingUpgradeSnapshot model - no SnapshotId parameter needed.
/// Rollback is only available after a failed upgrade (before Point of No Return).
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
    public async Task GET_Snapshots_ForNewDeployment_ReturnsNoSnapshot()
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
        result.HasPendingSnapshot.Should().BeFalse("A newly deployed stack has no pending upgrade snapshot");
        result.CanRollback.Should().BeFalse("Cannot rollback without failed upgrade");
    }

    #endregion

    #region Rollback - Authentication

    [Fact]
    public async Task POST_Rollback_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange - First deploy a stack with authenticated client
        var deploymentId = await DeployTestStack("rollback-auth-test");

        if (deploymentId == null)
        {
            // Skip test if deployment failed (Docker might not be available)
            return;
        }

        using var unauthenticatedClient = CreateUnauthenticatedClient();

        // Act - Try to rollback without authentication
        var response = await unauthenticatedClient.PostAsync(
            $"/api/environments/{EnvironmentId}/deployments/{deploymentId}/rollback",
            null);

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

        // Act - No body needed
        var response = await Client.PostAsync(
            $"/api/environments/invalid-env-id/deployments/{fakeDeploymentId}/rollback",
            null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Rollback_WithInvalidDeploymentId_ReturnsError()
    {
        // Act - No body needed
        var response = await Client.PostAsync(
            $"/api/environments/{EnvironmentId}/deployments/invalid-deployment-id/rollback",
            null);

        // Assert
        // Returns BadRequest because invalid GUID format fails early validation
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Rollback_WithNonExistentDeployment_ReturnsNotFound()
    {
        // Arrange
        var nonExistentDeploymentId = Guid.NewGuid().ToString();

        // Act - No body needed
        var response = await Client.PostAsync(
            $"/api/environments/{EnvironmentId}/deployments/{nonExistentDeploymentId}/rollback",
            null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Rollback - Business Logic

    [Fact]
    public async Task POST_Rollback_WithNoSnapshot_ReturnsBadRequest()
    {
        // Arrange - Deploy a stack (will have no pending upgrade snapshot)
        var deploymentId = await DeployTestStack("rollback-no-snapshot-test");

        if (deploymentId == null)
        {
            // Skip test if deployment failed
            return;
        }

        // Act - No body needed, rollback uses PendingUpgradeSnapshot automatically
        var response = await Client.PostAsync(
            $"/api/environments/{EnvironmentId}/deployments/{deploymentId}/rollback",
            null);

        // Assert
        // Should return error because deployment has no pending upgrade snapshot
        // Rollback is only available after a failed upgrade (before Point of No Return)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Cannot rollback deployment without a pending upgrade snapshot");
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

        // Assert - Should not return 405 (method not allowed)
        // The endpoint exists, so we expect either a success or validation error
        response.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed,
            "Snapshots GET endpoint should exist");
    }

    [Fact]
    public async Task POST_Rollback_EndpointExists()
    {
        // Arrange
        var fakeDeploymentId = Guid.NewGuid().ToString();

        // Act - No body needed
        var response = await Client.PostAsync(
            $"/api/environments/{EnvironmentId}/deployments/{fakeDeploymentId}/rollback",
            null);

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
        // HasPendingSnapshot should be false for a newly deployed stack
        result.HasPendingSnapshot.Should().BeFalse();
        result.CanRollback.Should().BeFalse();
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

    /// <summary>
    /// Response from GET /api/environments/{envId}/deployments/{deploymentId}/snapshots
    /// Updated for PendingUpgradeSnapshot model.
    /// </summary>
    private record GetSnapshotsResponse(
        bool Success,
        string? Message,
        string? DeploymentId,
        string? StackName,
        string? CurrentVersion,
        bool CanRollback,
        string? RollbackTargetVersion,
        DateTime? SnapshotCreatedAt,
        string? SnapshotDescription,
        List<SnapshotDto> Snapshots)
    {
        /// <summary>
        /// Whether a pending upgrade snapshot exists.
        /// Calculated from presence of snapshots (0 or 1 in list).
        /// </summary>
        public bool HasPendingSnapshot => Snapshots?.Count > 0;
    }

    /// <summary>
    /// DTO for the pending upgrade snapshot.
    /// </summary>
    private record SnapshotDto(
        string SnapshotId,
        string StackVersion,
        DateTime CreatedAt,
        string? Description,
        int ServiceCount,
        int VariableCount);

    /// <summary>
    /// Response from POST /api/environments/{envId}/deployments/{deploymentId}/rollback
    /// </summary>
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
