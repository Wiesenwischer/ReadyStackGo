using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;
using DeploymentUserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Operation Mode API Endpoint.
/// Tests the ability to change deployment operation mode (Normal, Maintenance).
/// Note: Migrating mode is no longer supported - use DeploymentStatus.Upgrading instead.
/// </summary>
public class OperationModeEndpointsIntegrationTests : AuthenticatedTestBase
{

    #region Change Operation Mode

    [Fact]
    public async Task PUT_ChangeOperationMode_WithValidDeployment_EntersMaintenance()
    {
        // Arrange
        var deploymentId = await CreateTestDeploymentAsync("maintenance-test");

        var request = new
        {
            mode = "Maintenance",
            reason = "Scheduled maintenance window"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ChangeOperationModeResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.NewMode.Should().Be("Maintenance");
        result.PreviousMode.Should().Be("Normal");
    }

    [Fact]
    public async Task PUT_ChangeOperationMode_ExitMaintenance_ReturnsToNormal()
    {
        // Arrange
        var deploymentId = await CreateTestDeploymentAsync("exit-maintenance-test");

        // First enter maintenance
        var enterRequest = new { mode = "Maintenance" };
        var enterResponse = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", enterRequest);
        enterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Exit maintenance
        var exitRequest = new { mode = "Normal" };
        var response = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", exitRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ChangeOperationModeResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.NewMode.Should().Be("Normal");
        result.PreviousMode.Should().Be("Maintenance");
    }

    [Fact]
    public async Task PUT_ChangeOperationMode_MigratingMode_ReturnsBadRequest()
    {
        // Migrating mode is no longer supported as an OperationMode
        // Upgrades are now handled via DeploymentStatus.Upgrading

        // Arrange
        var deploymentId = await CreateTestDeploymentAsync("migration-test");

        var request = new
        {
            mode = "Migrating", // This mode no longer exists
            targetVersion = "2.0.0"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PUT_ChangeOperationMode_InvalidMode_ReturnsBadRequest()
    {
        // Arrange
        var deploymentId = await CreateTestDeploymentAsync("invalid-mode-test");

        var request = new
        {
            mode = "InvalidMode"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PUT_ChangeOperationMode_NonexistentDeployment_ReturnsBadRequest()
    {
        // Arrange
        var fakeDeploymentId = Guid.NewGuid().ToString();

        var request = new
        {
            mode = "Maintenance"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{fakeDeploymentId}/operation-mode", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PUT_ChangeOperationMode_InvalidDeploymentId_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            mode = "Maintenance"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/not-a-guid/operation-mode", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PUT_ChangeOperationMode_SameMode_ReturnsSuccess()
    {
        // Arrange
        var deploymentId = await CreateTestDeploymentAsync("same-mode-test");

        var request = new
        {
            mode = "Normal" // Already in Normal mode
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ChangeOperationModeResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.NewMode.Should().Be("Normal");
        result.PreviousMode.Should().Be("Normal");
    }

    [Fact]
    public async Task PUT_ChangeOperationMode_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var fakeDeploymentId = Guid.NewGuid().ToString();

        var request = new
        {
            mode = "Maintenance"
        };

        // Act
        var fakeEnvironmentId = Guid.NewGuid().ToString();
        var response = await unauthenticatedClient.PutAsJsonAsync($"/api/environments/{fakeEnvironmentId}/deployments/{fakeDeploymentId}/operation-mode", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Get Deployment - OperationMode in Response

    [Fact]
    public async Task GET_GetDeployment_IncludesOperationMode()
    {
        // Arrange
        var stackName = "get-opmode-test";
        _ = await CreateTestDeploymentAsync(stackName);

        // Act - GetDeployment now uses stackName, not deploymentId
        var response = await Client.GetAsync($"/api/environments/{EnvironmentId}/deployments/{stackName}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetDeploymentResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.OperationMode.Should().Be("Normal");
    }

    [Fact]
    public async Task GET_GetDeployment_ReflectsChangedOperationMode()
    {
        // Arrange
        var stackName = "reflect-opmode-test";
        var deploymentId = await CreateTestDeploymentAsync(stackName);

        // Change to maintenance mode (still uses deploymentId)
        var modeRequest = new { mode = "Maintenance" };
        var modeResponse = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", modeRequest);
        modeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - GetDeployment now uses stackName, not deploymentId
        var response = await Client.GetAsync($"/api/environments/{EnvironmentId}/deployments/{stackName}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetDeploymentResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.OperationMode.Should().Be("Maintenance");
    }

    #endregion

    #region List Deployments - OperationMode in Response

    [Fact]
    public async Task GET_ListDeployments_IncludesOperationMode()
    {
        // Arrange
        var stackName = "list-opmode-test";
        await CreateTestDeploymentAsync(stackName);

        // Act
        var response = await Client.GetAsync($"/api/environments/{EnvironmentId}/deployments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ListDeploymentsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Deployments.Should().NotBeEmpty();

        var deployment = result.Deployments.FirstOrDefault(d => d.StackName == stackName);
        deployment.Should().NotBeNull();
        deployment!.OperationMode.Should().Be("Normal");
    }

    #endregion

    #region Complete Flow

    [Fact]
    public async Task OperationModeFlow_MaintenanceCycle_WorksCorrectly()
    {
        // Arrange
        var stackName = "flow-maintenance-test";
        var deploymentId = await CreateTestDeploymentAsync(stackName);

        // Step 1: Verify initial state is Normal (GetDeployment now uses stackName)
        var getResponse1 = await Client.GetAsync($"/api/environments/{EnvironmentId}/deployments/{stackName}");
        var deployment1 = await getResponse1.Content.ReadFromJsonAsync<GetDeploymentResponse>();
        deployment1!.OperationMode.Should().Be("Normal");

        // Step 2: Enter maintenance (operation-mode uses deploymentId)
        var enterRequest = new { mode = "Maintenance", reason = "Scheduled maintenance" };
        var enterResponse = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", enterRequest);
        enterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 3: Verify maintenance state
        var getResponse2 = await Client.GetAsync($"/api/environments/{EnvironmentId}/deployments/{stackName}");
        var deployment2 = await getResponse2.Content.ReadFromJsonAsync<GetDeploymentResponse>();
        deployment2!.OperationMode.Should().Be("Maintenance");

        // Step 4: Exit maintenance
        var exitRequest = new { mode = "Normal" };
        var exitResponse = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", exitRequest);
        exitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 5: Verify back to normal
        var getResponse3 = await Client.GetAsync($"/api/environments/{EnvironmentId}/deployments/{stackName}");
        var deployment3 = await getResponse3.Content.ReadFromJsonAsync<GetDeploymentResponse>();
        deployment3!.OperationMode.Should().Be("Normal");
    }

    [Fact]
    public async Task OperationModeFlow_MaintenanceToggle_WorksCorrectly()
    {
        // Note: Migration is no longer an OperationMode - it's now a DeploymentStatus
        // This test verifies the maintenance toggle flow

        // Arrange
        var stackName = "flow-toggle-test";
        var deploymentId = await CreateTestDeploymentAsync(stackName);

        // Step 1: Enter maintenance
        var enterRequest = new { mode = "Maintenance", reason = "Maintenance window" };
        var enterResponse = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", enterRequest);
        enterResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var enterResult = await enterResponse.Content.ReadFromJsonAsync<ChangeOperationModeResponse>();
        enterResult!.NewMode.Should().Be("Maintenance");

        // Step 2: Exit maintenance (back to normal)
        var exitRequest = new { mode = "Normal" };
        var exitResponse = await Client.PutAsJsonAsync($"/api/environments/{EnvironmentId}/deployments/{deploymentId}/operation-mode", exitRequest);
        exitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var exitResult = await exitResponse.Content.ReadFromJsonAsync<ChangeOperationModeResponse>();
        exitResult!.NewMode.Should().Be("Normal");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test deployment directly in the database.
    /// This bypasses Docker which is not available in the test environment.
    /// </summary>
    private string CreateTestDeploymentInDb(string stackName)
    {
        using var scope = Factory.Services.CreateScope();
        var deploymentRepository = scope.ServiceProvider.GetRequiredService<IDeploymentRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        // Get the first user (admin created during test setup)
        var user = userRepository.GetAll().First();

        // Parse environment ID
        var environmentId = new EnvironmentId(Guid.Parse(EnvironmentId));

        // Create deployment
        var deploymentId = deploymentRepository.NextIdentity();
        var deployment = Deployment.StartInstallation(
            deploymentId,
            environmentId,
            stackName, // stackId (same as stackName for test deployments)
            stackName,
            stackName, // projectName
            DeploymentUserId.FromIdentityAccess(user.Id));

        deployment.SetStackVersion("1.0.0");

        // Mark as running (with services - no Docker needed)
        deployment.AddService("web", "nginx:latest", "starting");
        deployment.SetServiceContainerInfo("web", "container1", $"{stackName}-web-1", "running");
        deployment.MarkAsRunning();

        deploymentRepository.Add(deployment);
        deploymentRepository.SaveChanges();

        return deploymentId.Value.ToString();
    }

    private Task<string> CreateTestDeploymentAsync(string stackName)
    {
        return Task.FromResult(CreateTestDeploymentInDb(stackName));
    }

    #endregion

    #region Response DTOs

    private record ChangeOperationModeResponse(
        bool Success,
        string? Message,
        string? DeploymentId,
        string? PreviousMode,
        string? NewMode);

    private record DeployComposeResponse(
        bool Success,
        string? Message,
        string? DeploymentId,
        string? StackName);

    private record GetDeploymentResponse(
        bool Success,
        string? Message,
        string? DeploymentId,
        string? StackName,
        string? Status,
        string? OperationMode);

    private record DeploymentSummary(
        string? DeploymentId,
        string StackName,
        string? Status,
        string? OperationMode);

    private record ListDeploymentsResponse(
        bool Success,
        List<DeploymentSummary> Deployments);

    #endregion
}
