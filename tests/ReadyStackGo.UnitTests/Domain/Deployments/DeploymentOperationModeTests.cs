namespace ReadyStackGo.UnitTests.Domain.Deployments;

using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

/// <summary>
/// Unit tests for Deployment operation mode behavior.
/// Operation mode is only valid when deployment is in Running status.
/// Tests the simplified Normal/Maintenance mode transitions.
/// </summary>
public class DeploymentOperationModeTests
{
    private readonly DeploymentId _deploymentId = new(Guid.NewGuid());
    private readonly EnvironmentId _environmentId = new(Guid.NewGuid());
    private readonly UserId _userId = UserId.NewId();

    private Deployment CreateRunningDeployment()
    {
        var deployment = Deployment.StartInstallation(
            _deploymentId,
            _environmentId,
            "test-stack", // stackId
            "test-stack",
            "test-project",
            _userId);

        deployment.AddService("service1", "test:latest", "starting");
        deployment.SetServiceContainerInfo("service1", "container1", "test-stack-service1", "running");
        deployment.MarkAsRunning();

        return deployment;
    }

    #region Initial State Tests

    [Fact]
    public void NewDeployment_HasNormalOperationMode()
    {
        // Arrange & Act
        var deployment = Deployment.StartInstallation(
            _deploymentId,
            _environmentId,
            "test-stack", // stackId
            "test-stack",
            "test-project",
            _userId);

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
    }

    [Fact]
    public void RunningDeployment_HasNormalOperationMode()
    {
        // Arrange & Act
        var deployment = CreateRunningDeployment();

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
    }

    #endregion

    #region Enter Maintenance Tests

    [Fact]
    public void EnterMaintenance_FromRunningNormal_Succeeds()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.EnterMaintenance("Scheduled maintenance");

        // Assert
        Assert.Equal(OperationMode.Maintenance, deployment.OperationMode);
        Assert.Equal(DeploymentStatus.Running, deployment.Status);
    }

    [Fact]
    public void EnterMaintenance_WithoutReason_Succeeds()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.EnterMaintenance();

        // Assert
        Assert.Equal(OperationMode.Maintenance, deployment.OperationMode);
    }

    [Fact]
    public void EnterMaintenance_FromInstalling_Throws()
    {
        // Arrange
        var deployment = Deployment.StartInstallation(
            _deploymentId,
            _environmentId,
            "test-stack", // stackId
            "test-stack",
            "test-project",
            _userId);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.EnterMaintenance("Scheduled maintenance"));
        Assert.Contains("running deployment", ex.Message);
    }

    [Fact]
    public void EnterMaintenance_FromUpgrading_Throws()
    {
        // Arrange
        var deployment = Deployment.StartUpgrade(
            _deploymentId,
            _environmentId,
            "test-stack",
            "test-stack",
            "test-project",
            _userId,
            "1.0.0");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.EnterMaintenance("Scheduled maintenance"));
        Assert.Contains("running deployment", ex.Message);
    }

    [Fact]
    public void EnterMaintenance_FromFailed_Throws()
    {
        // Arrange
        var deployment = Deployment.StartInstallation(
            _deploymentId,
            _environmentId,
            "test-stack",
            "test-stack",
            "test-project",
            _userId);
        deployment.MarkAsFailed("Installation failed");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.EnterMaintenance("Scheduled maintenance"));
        Assert.Contains("running deployment", ex.Message);
    }

    [Fact]
    public void EnterMaintenance_WhenAlreadyInMaintenance_Throws()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance("First maintenance");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.EnterMaintenance("Second maintenance"));
        Assert.Contains("already in maintenance", ex.Message);
    }

    #endregion

    #region Exit Maintenance Tests

    [Fact]
    public void ExitMaintenance_FromMaintenance_ReturnsToNormal()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance();

        // Act
        deployment.ExitMaintenance();

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
        Assert.Equal(DeploymentStatus.Running, deployment.Status);
    }

    [Fact]
    public void ExitMaintenance_WhenNotInMaintenance_Throws()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.ExitMaintenance());
        Assert.Contains("not in maintenance mode", ex.Message);
    }

    [Fact]
    public void ExitMaintenance_FromInstalling_Throws()
    {
        // Arrange
        var deployment = Deployment.StartInstallation(
            _deploymentId,
            _environmentId,
            "test-stack",
            "test-stack",
            "test-project",
            _userId);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.ExitMaintenance());
        Assert.Contains("running deployment", ex.Message);
    }

    #endregion

    #region Domain Event Tests

    [Fact]
    public void EnterMaintenance_RaisesOperationModeChangedEvent()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.ClearDomainEvents();

        // Act
        deployment.EnterMaintenance("Test reason");

        // Assert
        var events = deployment.DomainEvents;
        var modeChangedEvent = events.OfType<OperationModeChanged>().FirstOrDefault();
        Assert.NotNull(modeChangedEvent);
        Assert.Equal(OperationMode.Maintenance, modeChangedEvent.NewMode);
        Assert.Equal("Test reason", modeChangedEvent.Reason);
    }

    [Fact]
    public void ExitMaintenance_RaisesOperationModeChangedEvent()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance("Some reason");
        deployment.ClearDomainEvents();

        // Act
        deployment.ExitMaintenance();

        // Assert
        var events = deployment.DomainEvents;
        var modeChangedEvent = events.OfType<OperationModeChanged>().FirstOrDefault();
        Assert.NotNull(modeChangedEvent);
        Assert.Equal(OperationMode.Normal, modeChangedEvent.NewMode);
    }

    #endregion

    #region Operation Mode Preservation Tests

    [Fact]
    public void OperationMode_AfterUpgradeComplete_IsNormal()
    {
        // Arrange - simulate upgrade completion
        var deployment = CreateRunningDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.EnterMaintenance("Pre-upgrade");
        deployment.ExitMaintenance();
        deployment.StartUpgradeProcess("2.0.0");

        // Act - complete the upgrade (remove old, add new)
        deployment.RemoveService("service1");
        deployment.AddService("service1", "test:2.0", "starting");
        deployment.SetServiceContainerInfo("service1", "container2", "test-stack-service1", "running");
        deployment.MarkAsRunning();

        // Assert - should be Normal after upgrade
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
        Assert.Equal(DeploymentStatus.Running, deployment.Status);
    }

    [Fact]
    public void MaintenanceMode_DoesNotAffectStatus()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.EnterMaintenance("Maintenance window");

        // Assert - Status should remain Running
        Assert.Equal(DeploymentStatus.Running, deployment.Status);
        Assert.Equal(OperationMode.Maintenance, deployment.OperationMode);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EnterMaintenance_ThenRemove_ClearsServices()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance("Preparing for removal");

        // Act
        deployment.MarkAsRemoved();

        // Assert
        Assert.Equal(DeploymentStatus.Removed, deployment.Status);
        Assert.All(deployment.Services, s => Assert.Equal("removed", s.Status));
    }

    [Fact]
    public void OperationMode_IsNormal_AfterInstallationCompletes()
    {
        // Arrange
        var deployment = Deployment.StartInstallation(
            _deploymentId,
            _environmentId,
            "test-stack",
            "test-stack",
            "test-project",
            _userId);

        // Act
        deployment.AddService("service1", "test:latest", "starting");
        deployment.SetServiceContainerInfo("service1", "container1", "test-stack-service1", "running");
        deployment.MarkAsRunning();

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
    }

    [Fact]
    public void OperationMode_IsNormal_AfterUpgradeCompletes()
    {
        // Arrange
        var deployment = Deployment.StartUpgrade(
            _deploymentId,
            _environmentId,
            "test-stack",
            "test-stack",
            "test-project",
            _userId,
            "1.0.0");

        // Act
        deployment.AddService("service1", "test:2.0", "starting");
        deployment.SetServiceContainerInfo("service1", "container1", "test-stack-service1", "running");
        deployment.MarkAsRunning();

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
    }

    #endregion
}
