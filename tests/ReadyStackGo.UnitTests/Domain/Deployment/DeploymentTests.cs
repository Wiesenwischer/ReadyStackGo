using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

using Deployment = ReadyStackGo.Domain.Deployment.Deployments.Deployment;

/// <summary>
/// Unit tests for Deployment aggregate root.
/// </summary>
public class DeploymentTests
{
    #region Creation Tests

    [Fact]
    public void Start_WithValidData_CreatesDeployment()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();
        var environmentId = EnvironmentId.NewId();
        var userId = UserId.NewId();

        // Act
        var deployment = Deployment.Start(
            deploymentId,
            environmentId,
            "wordpress",
            "wordpress-prod",
            userId);

        // Assert
        deployment.Id.Should().Be(deploymentId);
        deployment.EnvironmentId.Should().Be(environmentId);
        deployment.StackName.Should().Be("wordpress");
        deployment.ProjectName.Should().Be("wordpress-prod");
        deployment.DeployedBy.Should().Be(userId);
        deployment.Status.Should().Be(DeploymentStatus.Pending);
        deployment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        deployment.CompletedAt.Should().BeNull();
        deployment.ErrorMessage.Should().BeNull();
        deployment.DomainEvents.Should().ContainSingle(e => e is DeploymentStarted);
    }

    [Fact]
    public void Start_WithEmptyStackName_ThrowsArgumentException()
    {
        // Act
        var act = () => Deployment.Start(
            DeploymentId.NewId(),
            EnvironmentId.NewId(),
            "",
            "project",
            UserId.NewId());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Start_WithEmptyProjectName_ThrowsArgumentException()
    {
        // Act
        var act = () => Deployment.Start(
            DeploymentId.NewId(),
            EnvironmentId.NewId(),
            "wordpress",
            "",
            UserId.NewId());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Start_WithNullEnvironmentId_ThrowsArgumentException()
    {
        // Act
        var act = () => Deployment.Start(
            DeploymentId.NewId(),
            null!,
            "wordpress",
            "project",
            UserId.NewId());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Start_WithNullDeployedBy_ThrowsArgumentException()
    {
        // Act
        var act = () => Deployment.Start(
            DeploymentId.NewId(),
            EnvironmentId.NewId(),
            "wordpress",
            "project",
            null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Start_RaisesDeploymentStartedEvent()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();
        var environmentId = EnvironmentId.NewId();

        // Act
        var deployment = Deployment.Start(
            deploymentId,
            environmentId,
            "wordpress",
            "wordpress-prod",
            UserId.NewId());

        // Assert
        var domainEvent = deployment.DomainEvents.OfType<DeploymentStarted>().Single();
        domainEvent.DeploymentId.Should().Be(deploymentId);
        domainEvent.EnvironmentId.Should().Be(environmentId);
        domainEvent.StackName.Should().Be("wordpress");
    }

    #endregion

    #region Stack Version Tests

    [Fact]
    public void SetStackVersion_SetsVersion()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        deployment.SetStackVersion("1.0.0");

        // Assert
        deployment.StackVersion.Should().Be("1.0.0");
    }

    #endregion

    #region Status Transition Tests

    [Fact]
    public void MarkAsRunning_FromPending_ChangesStatusAndAddsServices()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        var services = new[]
        {
            new DeployedService("wordpress-db", "abc123", "wordpress-db-1", "mysql:8.0", "running"),
            new DeployedService("wordpress-app", "def456", "wordpress-app-1", "wordpress:latest", "running")
        };

        // Act
        deployment.MarkAsRunning(services);

        // Assert
        deployment.Status.Should().Be(DeploymentStatus.Running);
        deployment.Services.Should().HaveCount(2);
        deployment.Services.Should().Contain(s => s.ServiceName == "wordpress-db");
        deployment.Services.Should().Contain(s => s.ServiceName == "wordpress-app");
        deployment.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsRunning_FromRunning_ThrowsInvalidOperationException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        var act = () => deployment.MarkAsRunning(CreateTestServices());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsFailed_SetsStatusAndErrorMessage()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        deployment.MarkAsFailed("Docker compose failed: connection refused");

        // Assert
        deployment.Status.Should().Be(DeploymentStatus.Failed);
        deployment.ErrorMessage.Should().Be("Docker compose failed: connection refused");
        deployment.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsFailed_RaisesDeploymentCompletedEvent()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.ClearDomainEvents();

        // Act
        deployment.MarkAsFailed("Connection error");

        // Assert
        var domainEvent = deployment.DomainEvents.OfType<DeploymentCompleted>().Single();
        domainEvent.DeploymentId.Should().Be(deployment.Id);
        domainEvent.Status.Should().Be(DeploymentStatus.Failed);
    }

    [Fact]
    public void MarkAsStopped_FromRunning_ChangesStatus()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        deployment.MarkAsStopped();

        // Assert
        deployment.Status.Should().Be(DeploymentStatus.Stopped);
        deployment.Services.Should().OnlyContain(s => s.Status == "stopped");
    }

    [Fact]
    public void MarkAsStopped_FromPending_ThrowsInvalidOperationException()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        var act = () => deployment.MarkAsStopped();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsRemoved_ChangesStatus()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        deployment.MarkAsRemoved();

        // Assert
        deployment.Status.Should().Be(DeploymentStatus.Removed);
        deployment.Services.Should().OnlyContain(s => s.Status == "removed");
    }

    #endregion

    #region Service Management Tests

    [Fact]
    public void Services_IsReadOnlyCollection()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Assert
        deployment.Services.Should().BeAssignableTo<IReadOnlyCollection<DeployedService>>();
    }

    [Fact]
    public void UpdateServiceStatus_UpdatesSpecificService()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        deployment.UpdateServiceStatus("wordpress-db", "exited");

        // Assert
        var dbService = deployment.Services.First(s => s.ServiceName == "wordpress-db");
        dbService.Status.Should().Be("exited");
    }

    [Fact]
    public void UpdateServiceStatus_NonExistentService_DoesNothing()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Act - Should not throw
        deployment.UpdateServiceStatus("nonexistent", "running");

        // Assert - Other services unchanged
        deployment.Services.Should().HaveCount(2);
    }

    #endregion

    #region DeploymentId Tests

    [Fact]
    public void DeploymentId_NewId_CreatesUniqueId()
    {
        // Act
        var id1 = DeploymentId.NewId();
        var id2 = DeploymentId.NewId();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void DeploymentId_Create_CreatesUniqueId()
    {
        // Act
        var id1 = DeploymentId.Create();
        var id2 = DeploymentId.Create();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void DeploymentId_FromGuid_CreatesCorrectId()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = DeploymentId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void DeploymentId_EmptyGuid_ThrowsException()
    {
        // Act
        var act = () => new DeploymentId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DeploymentId_Equality_WorksCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new DeploymentId(guid);
        var id2 = new DeploymentId(guid);

        // Assert
        id1.Should().Be(id2);
        id1.Equals(id2).Should().BeTrue();
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    #endregion

    #region DeployedService Tests

    [Fact]
    public void DeployedService_Create_CreatesWithAllProperties()
    {
        // Act
        var service = new DeployedService(
            "wordpress-db",
            "abc123container",
            "wordpress-db-1",
            "mysql:8.0",
            "running");

        // Assert
        service.ServiceName.Should().Be("wordpress-db");
        service.ContainerId.Should().Be("abc123container");
        service.ContainerName.Should().Be("wordpress-db-1");
        service.Image.Should().Be("mysql:8.0");
        service.Status.Should().Be("running");
        service.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void DeployedService_UpdateStatus_ChangesStatus()
    {
        // Arrange
        var service = new DeployedService("app", "id", "name", "image", "running");

        // Act
        service.UpdateStatus("stopped");

        // Assert
        service.Status.Should().Be("stopped");
    }

    [Fact]
    public void DeployedService_UpdateContainerInfo_ChangesContainerInfo()
    {
        // Arrange
        var service = new DeployedService("app", null, null, "image", "created");

        // Act
        service.UpdateContainerInfo("newContainerId", "newContainerName");

        // Assert
        service.ContainerId.Should().Be("newContainerId");
        service.ContainerName.Should().Be("newContainerName");
    }

    #endregion

    #region State Machine Tests

    [Fact]
    public void CanTransitionTo_FromPending_CanGoToRunningOrFailed()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Assert
        deployment.CanTransitionTo(DeploymentStatus.Running).Should().BeTrue();
        deployment.CanTransitionTo(DeploymentStatus.Failed).Should().BeTrue();
        deployment.CanTransitionTo(DeploymentStatus.Stopped).Should().BeFalse();
        deployment.CanTransitionTo(DeploymentStatus.Removed).Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_FromRunning_CanGoToStoppedOrFailed()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Assert
        deployment.CanTransitionTo(DeploymentStatus.Stopped).Should().BeTrue();
        deployment.CanTransitionTo(DeploymentStatus.Failed).Should().BeTrue();
        deployment.CanTransitionTo(DeploymentStatus.Running).Should().BeFalse();
        deployment.CanTransitionTo(DeploymentStatus.Pending).Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_FromStopped_CanGoToRunningOrRemoved()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.MarkAsStopped();

        // Assert
        deployment.CanTransitionTo(DeploymentStatus.Running).Should().BeTrue();
        deployment.CanTransitionTo(DeploymentStatus.Removed).Should().BeTrue();
        deployment.CanTransitionTo(DeploymentStatus.Stopped).Should().BeFalse();
        deployment.CanTransitionTo(DeploymentStatus.Pending).Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_FromFailed_CannotGoAnywhere()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsFailed("Error");

        // Assert - Terminal state
        deployment.CanTransitionTo(DeploymentStatus.Running).Should().BeFalse();
        deployment.CanTransitionTo(DeploymentStatus.Stopped).Should().BeFalse();
        deployment.CanTransitionTo(DeploymentStatus.Removed).Should().BeFalse();
        deployment.CanTransitionTo(DeploymentStatus.Pending).Should().BeFalse();
    }

    [Fact]
    public void CanTransitionTo_FromRemoved_CannotGoAnywhere()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.MarkAsRemoved();

        // Assert - Terminal state
        deployment.CanTransitionTo(DeploymentStatus.Running).Should().BeFalse();
        deployment.CanTransitionTo(DeploymentStatus.Stopped).Should().BeFalse();
        deployment.CanTransitionTo(DeploymentStatus.Failed).Should().BeFalse();
        deployment.CanTransitionTo(DeploymentStatus.Pending).Should().BeFalse();
    }

    [Fact]
    public void GetValidNextStates_FromPending_ReturnsRunningAndFailed()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        var validStates = deployment.GetValidNextStates().ToList();

        // Assert
        validStates.Should().HaveCount(2);
        validStates.Should().Contain(DeploymentStatus.Running);
        validStates.Should().Contain(DeploymentStatus.Failed);
    }

    [Fact]
    public void IsTerminal_ForFailedDeployment_ReturnsTrue()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsFailed("Error");

        // Assert
        deployment.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_ForRemovedDeployment_ReturnsTrue()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.MarkAsRemoved();

        // Assert
        deployment.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_ForPendingDeployment_ReturnsFalse()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Assert
        deployment.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void IsActive_ForPendingAndRunning_ReturnsTrue()
    {
        // Arrange
        var pendingDeployment = CreateTestDeployment();
        var runningDeployment = CreateTestDeployment();
        runningDeployment.MarkAsRunning(CreateTestServices());

        // Assert
        pendingDeployment.IsActive.Should().BeTrue();
        runningDeployment.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_ForStoppedAndFailed_ReturnsFalse()
    {
        // Arrange
        var stoppedDeployment = CreateTestDeployment();
        stoppedDeployment.MarkAsRunning(CreateTestServices());
        stoppedDeployment.MarkAsStopped();

        var failedDeployment = CreateTestDeployment();
        failedDeployment.MarkAsFailed("Error");

        // Assert
        stoppedDeployment.IsActive.Should().BeFalse();
        failedDeployment.IsActive.Should().BeFalse();
    }

    #endregion

    #region Progress Tracking Tests

    [Fact]
    public void UpdateProgress_WithValidData_UpdatesProgress()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.ClearDomainEvents();

        // Act
        deployment.UpdateProgress(DeploymentPhase.PullingImages, 50, "Pulling images...");

        // Assert
        deployment.CurrentPhase.Should().Be(DeploymentPhase.PullingImages);
        deployment.ProgressPercentage.Should().Be(50);
        deployment.ProgressMessage.Should().Be("Pulling images...");
        deployment.DomainEvents.Should().ContainSingle(e => e is DeploymentProgressUpdated);
    }

    [Fact]
    public void UpdateProgress_WithInvalidPercentage_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        var act = () => deployment.UpdateProgress(DeploymentPhase.Starting, 150, "Invalid");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateProgress_OnTerminalDeployment_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsFailed("Error");

        // Act
        var act = () => deployment.UpdateProgress(DeploymentPhase.Starting, 50, "Message");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PhaseHistory_TracksAllPhaseChanges()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        deployment.UpdateProgress(DeploymentPhase.ValidatingPrerequisites, 10, "Validating");
        deployment.UpdateProgress(DeploymentPhase.PullingImages, 50, "Pulling");
        deployment.MarkAsRunning(CreateTestServices());

        // Assert - Initializing + 2 updates + Completed
        deployment.PhaseHistory.Should().HaveCountGreaterThanOrEqualTo(4);
        deployment.PhaseHistory.Should().Contain(p => p.Phase == DeploymentPhase.Initializing);
        deployment.PhaseHistory.Should().Contain(p => p.Phase == DeploymentPhase.ValidatingPrerequisites);
        deployment.PhaseHistory.Should().Contain(p => p.Phase == DeploymentPhase.PullingImages);
        deployment.PhaseHistory.Should().Contain(p => p.Phase == DeploymentPhase.Completed);
    }

    #endregion

    #region Restart Tests

    [Fact]
    public void Restart_FromStopped_TransitionsToRunning()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.MarkAsStopped();
        deployment.ClearDomainEvents();

        // Act
        deployment.Restart();

        // Assert
        deployment.Status.Should().Be(DeploymentStatus.Running);
        deployment.CurrentPhase.Should().Be(DeploymentPhase.Starting);
        deployment.DomainEvents.Should().ContainSingle(e => e is DeploymentRestarted);
    }

    [Fact]
    public void Restart_FromPending_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        var act = () => deployment.Restart();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Restart_FromFailed_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsFailed("Error");

        // Act
        var act = () => deployment.Restart();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public void RequestCancellation_FromPending_SetsCancellationFlag()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.ClearDomainEvents();

        // Act
        deployment.RequestCancellation("User requested cancellation");

        // Assert
        deployment.IsCancellationRequested.Should().BeTrue();
        deployment.CancellationReason.Should().Be("User requested cancellation");
        deployment.DomainEvents.Should().ContainSingle(e => e is DeploymentCancellationRequested);
    }

    [Fact]
    public void RequestCancellation_FromRunning_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        var act = () => deployment.RequestCancellation("Reason");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RequestCancellation_WithEmptyReason_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        var act = () => deployment.RequestCancellation("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConfirmCancellation_WhenRequested_FailsDeployment()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.RequestCancellation("User cancelled");
        deployment.ClearDomainEvents();

        // Act
        deployment.ConfirmCancellation();

        // Assert
        deployment.Status.Should().Be(DeploymentStatus.Failed);
        deployment.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public void ConfirmCancellation_WhenNotRequested_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        var act = () => deployment.ConfirmCancellation();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Service Health Tests

    [Fact]
    public void AreAllServicesHealthy_WhenAllRunning_ReturnsTrue()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Assert
        deployment.AreAllServicesHealthy().Should().BeTrue();
    }

    [Fact]
    public void AreAllServicesHealthy_WhenSomeNotRunning_ReturnsFalse()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.UpdateServiceStatus("wordpress-db", "exited");

        // Assert
        deployment.AreAllServicesHealthy().Should().BeFalse();
    }

    [Fact]
    public void GetUnhealthyServices_ReturnsNonRunningServices()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.UpdateServiceStatus("wordpress-db", "exited");

        // Act
        var unhealthy = deployment.GetUnhealthyServices().ToList();

        // Assert
        unhealthy.Should().HaveCount(1);
        unhealthy.First().ServiceName.Should().Be("wordpress-db");
    }

    [Fact]
    public void GetRunningServiceCount_ReturnsCorrectCount()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.UpdateServiceStatus("wordpress-db", "exited");

        // Assert
        deployment.GetRunningServiceCount().Should().Be(1);
    }

    [Fact]
    public void UpdateServiceStatus_RaisesServiceStatusChangedEvent()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.ClearDomainEvents();

        // Act
        deployment.UpdateServiceStatus("wordpress-db", "exited");

        // Assert
        var domainEvent = deployment.DomainEvents.OfType<ServiceStatusChanged>().SingleOrDefault();
        domainEvent.Should().NotBeNull();
        domainEvent!.ServiceName.Should().Be("wordpress-db");
        domainEvent.PreviousStatus.Should().Be("running");
        domainEvent.NewStatus.Should().Be("exited");
    }

    #endregion

    #region Duration & Metrics Tests

    [Fact]
    public void GetDuration_WhenCompleted_ReturnsDuration()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        var duration = deployment.GetDuration();

        // Assert
        duration.Should().NotBeNull();
        duration!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void GetDuration_WhenNotCompleted_ReturnsNull()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Assert
        deployment.GetDuration().Should().BeNull();
    }

    [Fact]
    public void GetElapsedTime_ReturnsTimeSinceCreation()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        var elapsed = deployment.GetElapsedTime();

        // Assert
        elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void IsOverdue_WhenPendingAndExceedsExpectedDuration_ReturnsTrue()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        var veryShortExpectedDuration = TimeSpan.FromTicks(1);

        // Act - With such a short expected duration, any measurable time makes it overdue
        var result = deployment.IsOverdue(veryShortExpectedDuration);

        // Assert - Should be true because elapsed time (even tiny) > 1 tick
        result.Should().BeTrue();
    }

    [Fact]
    public void IsOverdue_WhenRunning_ReturnsFalse()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Assert
        deployment.IsOverdue(TimeSpan.FromTicks(1)).Should().BeFalse();
    }

    #endregion

    #region Domain Events Tests

    [Fact]
    public void MarkAsStopped_RaisesDeploymentStoppedEvent()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.ClearDomainEvents();

        // Act
        deployment.MarkAsStopped();

        // Assert
        deployment.DomainEvents.Should().ContainSingle(e => e is DeploymentStopped);
    }

    [Fact]
    public void MarkAsRemoved_RaisesDeploymentRemovedEvent()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.ClearDomainEvents();

        // Act
        deployment.MarkAsRemoved();

        // Assert
        deployment.DomainEvents.Should().ContainSingle(e => e is DeploymentRemoved);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        var result = deployment.ToString();

        // Assert
        result.Should().Contain("wordpress");
        result.Should().Contain("Pending");
        result.Should().Contain("Initializing");
    }

    #endregion

    #region Helper Methods

    private static Deployment CreateTestDeployment()
    {
        return Deployment.Start(
            DeploymentId.NewId(),
            EnvironmentId.NewId(),
            "wordpress",
            "wordpress-prod",
            UserId.NewId());
    }

    private static IEnumerable<DeployedService> CreateTestServices()
    {
        return new[]
        {
            new DeployedService("wordpress-db", "abc123", "wordpress-db-1", "mysql:8.0", "running"),
            new DeployedService("wordpress-app", "def456", "wordpress-app-1", "wordpress:latest", "running")
        };
    }

    #endregion
}
