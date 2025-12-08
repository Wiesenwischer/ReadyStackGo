namespace ReadyStackGo.UnitTests.Domain.Deployments;

using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.IdentityAccess.Users;

public class DeploymentOperationModeTests
{
    private readonly DeploymentId _deploymentId = new(Guid.NewGuid());
    private readonly EnvironmentId _environmentId = new(Guid.NewGuid());
    private readonly UserId _userId = new(Guid.NewGuid());

    private Deployment CreateRunningDeployment()
    {
        var deployment = Deployment.Start(
            _deploymentId,
            _environmentId,
            "test-stack",
            "test-project",
            _userId);

        deployment.MarkAsRunning(new[]
        {
            new DeployedService("service1", "container1", "test-stack-service1", "test:latest", "running")
        });

        return deployment;
    }

    [Fact]
    public void NewDeployment_HasNormalOperationMode()
    {
        // Arrange & Act
        var deployment = Deployment.Start(
            _deploymentId,
            _environmentId,
            "test-stack",
            "test-project",
            _userId);

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
    }

    [Fact]
    public void EnterMaintenance_FromNormal_Succeeds()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.EnterMaintenance("Scheduled maintenance");

        // Assert
        Assert.Equal(OperationMode.Maintenance, deployment.OperationMode);
    }

    [Fact]
    public void EnterMaintenance_FromNonRunning_Throws()
    {
        // Arrange
        var deployment = Deployment.Start(
            _deploymentId,
            _environmentId,
            "test-stack",
            "test-project",
            _userId);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.EnterMaintenance("Scheduled maintenance"));
        Assert.Contains("running deployment", ex.Message);
    }

    [Fact]
    public void ExitMaintenance_ReturnToNormal()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.EnterMaintenance();

        // Act
        deployment.ExitMaintenance();

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
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
    public void StartMigration_FromNormal_Succeeds()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.StartMigration("2.0.0");

        // Assert
        Assert.Equal(OperationMode.Migrating, deployment.OperationMode);
    }

    [Fact]
    public void StartMigration_WithoutVersion_Throws()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.StartMigration(""));
        Assert.Contains("Target version is required", ex.Message);
    }

    [Fact]
    public void CompleteMigration_UpdatesVersionAndReturnsToNormal()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.StartMigration("2.0.0");

        // Act
        deployment.CompleteMigration("2.0.0");

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
        Assert.Equal("2.0.0", deployment.StackVersion);
    }

    [Fact]
    public void CompleteMigration_WhenNotMigrating_Throws()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.CompleteMigration("2.0.0"));
        Assert.Contains("not in migration mode", ex.Message);
    }

    [Fact]
    public void FailMigration_SetsFailedMode()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.StartMigration("2.0.0");

        // Act
        deployment.FailMigration("Migration script failed");

        // Assert
        Assert.Equal(OperationMode.Failed, deployment.OperationMode);
        Assert.Equal("Migration script failed", deployment.ErrorMessage);
    }

    [Fact]
    public void RecoverFromFailure_ReturnsToNormal()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.StartMigration("2.0.0");
        deployment.FailMigration("Migration script failed");

        // Act
        deployment.RecoverFromFailure();

        // Assert
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
        Assert.Null(deployment.ErrorMessage);
    }

    [Fact]
    public void RecoverFromFailure_WhenNotFailed_Throws()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            deployment.RecoverFromFailure());
        Assert.Contains("not in failed state", ex.Message);
    }

    [Fact]
    public void MarkAsStopped_SetsStoppedOperationMode()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.MarkAsStopped();

        // Assert
        Assert.Equal(DeploymentStatus.Stopped, deployment.Status);
        Assert.Equal(OperationMode.Stopped, deployment.OperationMode);
    }

    [Fact]
    public void Restart_SetsNormalOperationMode()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.MarkAsStopped();

        // Act
        deployment.Restart();

        // Assert
        Assert.Equal(DeploymentStatus.Running, deployment.Status);
        Assert.Equal(OperationMode.Normal, deployment.OperationMode);
    }

    [Fact]
    public void OperationModeChanged_EventIsRaised()
    {
        // Arrange
        var deployment = CreateRunningDeployment();

        // Act
        deployment.EnterMaintenance("Test reason");

        // Assert
        var events = deployment.DomainEvents;
        var modeChangedEvent = events.OfType<OperationModeChanged>().FirstOrDefault();
        Assert.NotNull(modeChangedEvent);
        Assert.Equal(OperationMode.Maintenance, modeChangedEvent.NewMode);
        Assert.Equal("Test reason", modeChangedEvent.Reason);
    }
}
