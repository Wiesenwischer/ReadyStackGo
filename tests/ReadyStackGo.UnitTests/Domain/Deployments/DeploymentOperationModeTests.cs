namespace ReadyStackGo.UnitTests.Domain.Deployments;

using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;

/// <summary>
/// Unit tests for Deployment operation mode behavior.
/// Operation mode is only valid when deployment is in Running status.
/// Tests the simplified Normal/Maintenance mode transitions with trigger tracking.
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
        Assert.Null(deployment.MaintenanceTrigger);
    }

    [Fact]
    public void RunningDeployment_HasNormalOperationMode()
    {
        // Arrange & Act
        var deployment = CreateRunningDeployment();

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
        Assert.Null(deployment.MaintenanceTrigger);
    }

    #endregion

    #region Enter Maintenance Tests

    [Fact]
    public void EnterMaintenance_Manual_FromRunningNormal_Succeeds()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Scheduled maintenance"));

        // Assert
        Assert.Equal(OperationMode.Maintenance, deployment.OperationMode);
        Assert.Equal(DeploymentStatus.Running, deployment.Status);
        Assert.NotNull(deployment.MaintenanceTrigger);
        Assert.True(deployment.MaintenanceTrigger.IsManual);
        Assert.Equal("Scheduled maintenance", deployment.MaintenanceTrigger.Reason);
    }

    [Fact]
    public void EnterMaintenance_Observer_FromRunningNormal_Succeeds()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.EnterMaintenance(MaintenanceTrigger.Observer("External source reports maintenance", "HttpObserver"));

        // Assert
        Assert.Equal(OperationMode.Maintenance, deployment.OperationMode);
        Assert.NotNull(deployment.MaintenanceTrigger);
        Assert.True(deployment.MaintenanceTrigger.IsObserver);
        Assert.Equal("HttpObserver", deployment.MaintenanceTrigger.TriggeredBy);
    }

    [Fact]
    public void EnterMaintenance_WithoutReason_Succeeds()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.EnterMaintenance(MaintenanceTrigger.Manual());

        // Assert
        Assert.Equal(OperationMode.Maintenance, deployment.OperationMode);
        Assert.NotNull(deployment.MaintenanceTrigger);
        Assert.Null(deployment.MaintenanceTrigger.Reason);
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
            deployment.EnterMaintenance(MaintenanceTrigger.Manual("Scheduled maintenance")));
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
            deployment.EnterMaintenance(MaintenanceTrigger.Manual("Scheduled maintenance")));
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
            deployment.EnterMaintenance(MaintenanceTrigger.Manual("Scheduled maintenance")));
        Assert.Contains("running deployment", ex.Message);
    }

    [Fact]
    public void EnterMaintenance_WhenAlreadyInMaintenance_Throws()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("First maintenance"));

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.EnterMaintenance(MaintenanceTrigger.Manual("Second maintenance")));
        Assert.Contains("already in maintenance", ex.Message);
    }

    [Fact]
    public void EnterMaintenance_NullTrigger_Throws()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            deployment.EnterMaintenance(null!));
    }

    #endregion

    #region Exit Maintenance Tests

    [Fact]
    public void ExitMaintenance_Manual_FromManualMaintenance_ReturnsToNormal()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Test"));

        // Act
        deployment.ExitMaintenance(MaintenanceTriggerSource.Manual);

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
        Assert.Equal(DeploymentStatus.Running, deployment.Status);
        Assert.Null(deployment.MaintenanceTrigger);
    }

    [Fact]
    public void ExitMaintenance_Observer_FromObserverMaintenance_ReturnsToNormal()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Observer("External maintenance"));

        // Act
        deployment.ExitMaintenance(MaintenanceTriggerSource.Observer);

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
        Assert.Null(deployment.MaintenanceTrigger);
    }

    [Fact]
    public void ExitMaintenance_Manual_FromObserverMaintenance_Throws()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Observer("External maintenance"));

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            deployment.ExitMaintenance(MaintenanceTriggerSource.Manual));
        Assert.Contains("observer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExitMaintenance_Observer_FromManualMaintenance_Throws()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Manual maintenance"));

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            deployment.ExitMaintenance(MaintenanceTriggerSource.Observer));
        Assert.Contains("manually activated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExitMaintenance_WhenNotInMaintenance_Throws()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.ExitMaintenance(MaintenanceTriggerSource.Manual));
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
            deployment.ExitMaintenance(MaintenanceTriggerSource.Manual));
        Assert.Contains("running deployment", ex.Message);
    }

    #endregion

    #region Domain Event Tests

    [Fact]
    public void EnterMaintenance_RaisesOperationModeChangedEvent_WithTrigger()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.ClearDomainEvents();

        // Act
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Test reason"));

        // Assert
        var events = deployment.DomainEvents;
        var modeChangedEvent = events.OfType<OperationModeChanged>().FirstOrDefault();
        Assert.NotNull(modeChangedEvent);
        Assert.Equal(OperationMode.Maintenance, modeChangedEvent.NewMode);
        Assert.Equal("Test reason", modeChangedEvent.Reason);
        Assert.NotNull(modeChangedEvent.Trigger);
        Assert.True(modeChangedEvent.Trigger.IsManual);
    }

    [Fact]
    public void ExitMaintenance_RaisesOperationModeChangedEvent()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Some reason"));
        deployment.ClearDomainEvents();

        // Act
        deployment.ExitMaintenance(MaintenanceTriggerSource.Manual);

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
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Pre-upgrade"));
        deployment.ExitMaintenance(MaintenanceTriggerSource.Manual);
        deployment.StartUpgradeProcess("2.0.0");

        // Act - complete the upgrade (remove old, add new)
        deployment.RemoveService("service1");
        deployment.AddService("service1", "test:2.0", "starting");
        deployment.SetServiceContainerInfo("service1", "container2", "test-stack-service1", "running");
        deployment.MarkAsRunning();

        // Assert - should be Normal after upgrade with no trigger
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
        Assert.Null(deployment.MaintenanceTrigger);
        Assert.Equal(DeploymentStatus.Running, deployment.Status);
    }

    [Fact]
    public void MaintenanceMode_DoesNotAffectStatus()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Maintenance window"));

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
        deployment.EnterMaintenance(MaintenanceTrigger.Manual("Preparing for removal"));

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
        Assert.Null(deployment.MaintenanceTrigger);
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
        Assert.Null(deployment.MaintenanceTrigger);
    }

    #endregion
}
