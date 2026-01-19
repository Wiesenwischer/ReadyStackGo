using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.RuntimeConfig;

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
            "wordpress", // stackId
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
            "stackId",
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
            "wordpress", // stackId
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
            "wordpress", // stackId
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
            "wordpress", // stackId
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
            "wordpress", // stackId
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

    #region Variables Tests

    [Fact]
    public void SetVariables_WithValidDictionary_StoresVariables()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        var variables = new Dictionary<string, string>
        {
            ["DB_HOST"] = "localhost",
            ["DB_PORT"] = "5432",
            ["DB_NAME"] = "mydb"
        };

        // Act
        deployment.SetVariables(variables);

        // Assert
        deployment.Variables.Should().HaveCount(3);
        deployment.Variables["DB_HOST"].Should().Be("localhost");
        deployment.Variables["DB_PORT"].Should().Be("5432");
        deployment.Variables["DB_NAME"].Should().Be("mydb");
    }

    [Fact]
    public void SetVariables_WithEmptyDictionary_ClearsVariables()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetVariables(new Dictionary<string, string> { ["OLD"] = "value" });

        // Act
        deployment.SetVariables(new Dictionary<string, string>());

        // Assert
        deployment.Variables.Should().BeEmpty();
    }

    [Fact]
    public void SetVariables_OverwritesExistingVariables()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetVariables(new Dictionary<string, string>
        {
            ["VAR1"] = "old1",
            ["VAR2"] = "old2"
        });

        // Act
        deployment.SetVariables(new Dictionary<string, string>
        {
            ["VAR1"] = "new1",
            ["VAR3"] = "new3"
        });

        // Assert
        deployment.Variables.Should().HaveCount(2);
        deployment.Variables["VAR1"].Should().Be("new1");
        deployment.Variables["VAR3"].Should().Be("new3");
        deployment.Variables.Should().NotContainKey("VAR2");
    }

    [Fact]
    public void Variables_DefaultsToEmpty()
    {
        // Arrange & Act
        var deployment = CreateTestDeployment();

        // Assert
        deployment.Variables.Should().NotBeNull();
        deployment.Variables.Should().BeEmpty();
    }

    [Fact]
    public void Variables_IsReadOnly()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetVariables(new Dictionary<string, string> { ["KEY"] = "value" });

        // Assert - Variables should be IReadOnlyDictionary
        deployment.Variables.Should().BeAssignableTo<IReadOnlyDictionary<string, string>>();
    }

    [Fact]
    public void SetVariables_WithConnectionString_StoresCorrectly()
    {
        // Arrange - Real-world scenario with connection string containing special characters
        var deployment = CreateTestDeployment();
        var connectionString = "Server=sqldev2017;Database=dev-amsproject;User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true";
        var variables = new Dictionary<string, string>
        {
            ["AMS_DB"] = connectionString
        };

        // Act
        deployment.SetVariables(variables);

        // Assert
        deployment.Variables["AMS_DB"].Should().Be(connectionString);
    }

    [Fact]
    public void SetVariables_PreservesAfterStatusTransition()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        var variables = new Dictionary<string, string>
        {
            ["ENV_VAR"] = "test_value"
        };
        deployment.SetVariables(variables);

        // Act - Transition status
        deployment.MarkAsRunning(CreateTestServices());

        // Assert - Variables should still be accessible
        deployment.Variables.Should().ContainKey("ENV_VAR");
        deployment.Variables["ENV_VAR"].Should().Be("test_value");
    }

    #endregion

    #region HealthCheckConfigs Tests

    [Fact]
    public void SetHealthCheckConfigs_WithValidConfigs_StoresConfigs()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        var configs = new[]
        {
            new ServiceHealthCheckConfig
            {
                ServiceName = "api",
                Type = "http",
                Path = "/health",
                Port = 8080,
                Https = false
            },
            new ServiceHealthCheckConfig
            {
                ServiceName = "db",
                Type = "tcp",
                Port = 5432
            }
        };

        // Act
        deployment.SetHealthCheckConfigs(configs);

        // Assert
        deployment.HealthCheckConfigs.Should().HaveCount(2);
        deployment.HealthCheckConfigs.Should().Contain(c => c.ServiceName == "api");
        deployment.HealthCheckConfigs.Should().Contain(c => c.ServiceName == "db");
    }

    [Fact]
    public void SetHealthCheckConfigs_WithNull_ClearsConfigs()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetHealthCheckConfigs(new[]
        {
            new ServiceHealthCheckConfig { ServiceName = "api", Type = "http" }
        });

        // Act
        deployment.SetHealthCheckConfigs(null);

        // Assert
        deployment.HealthCheckConfigs.Should().BeEmpty();
    }

    [Fact]
    public void SetHealthCheckConfigs_OverwritesExistingConfigs()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetHealthCheckConfigs(new[]
        {
            new ServiceHealthCheckConfig { ServiceName = "old-service", Type = "docker" }
        });

        // Act
        deployment.SetHealthCheckConfigs(new[]
        {
            new ServiceHealthCheckConfig { ServiceName = "new-service", Type = "http", Path = "/hc" }
        });

        // Assert
        deployment.HealthCheckConfigs.Should().HaveCount(1);
        deployment.HealthCheckConfigs.First().ServiceName.Should().Be("new-service");
    }

    [Fact]
    public void GetHealthCheckConfig_WithExistingService_ReturnsConfig()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetHealthCheckConfigs(new[]
        {
            new ServiceHealthCheckConfig
            {
                ServiceName = "api",
                Type = "http",
                Path = "/health",
                Port = 8080
            }
        });

        // Act
        var config = deployment.GetHealthCheckConfig("api");

        // Assert
        config.Should().NotBeNull();
        config!.Type.Should().Be("http");
        config.Path.Should().Be("/health");
        config.Port.Should().Be(8080);
    }

    [Fact]
    public void GetHealthCheckConfig_WithNonExistingService_ReturnsNull()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetHealthCheckConfigs(new[]
        {
            new ServiceHealthCheckConfig { ServiceName = "api", Type = "http" }
        });

        // Act
        var config = deployment.GetHealthCheckConfig("nonexistent");

        // Assert
        config.Should().BeNull();
    }

    [Fact]
    public void GetHealthCheckConfig_IsCaseInsensitive()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetHealthCheckConfigs(new[]
        {
            new ServiceHealthCheckConfig { ServiceName = "Api", Type = "http" }
        });

        // Act
        var config = deployment.GetHealthCheckConfig("api");

        // Assert
        config.Should().NotBeNull();
    }

    [Fact]
    public void HealthCheckConfigs_DefaultsToEmpty()
    {
        // Arrange & Act
        var deployment = CreateTestDeployment();

        // Assert
        deployment.HealthCheckConfigs.Should().NotBeNull();
        deployment.HealthCheckConfigs.Should().BeEmpty();
    }

    [Fact]
    public void HealthCheckConfigs_IsReadOnly()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetHealthCheckConfigs(new[]
        {
            new ServiceHealthCheckConfig { ServiceName = "api", Type = "http" }
        });

        // Assert
        deployment.HealthCheckConfigs.Should().BeAssignableTo<IReadOnlyCollection<ServiceHealthCheckConfig>>();
    }

    [Fact]
    public void SetHealthCheckConfigs_PreservesAfterStatusTransition()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetHealthCheckConfigs(new[]
        {
            new ServiceHealthCheckConfig
            {
                ServiceName = "api",
                Type = "http",
                Path = "/health"
            }
        });

        // Act
        deployment.MarkAsRunning(CreateTestServices());

        // Assert
        deployment.HealthCheckConfigs.Should().HaveCount(1);
        deployment.HealthCheckConfigs.First().ServiceName.Should().Be("api");
    }

    [Fact]
    public void SetHealthCheckConfigs_WithAllProperties_StoresAllProperties()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        var config = new ServiceHealthCheckConfig
        {
            ServiceName = "api",
            Type = "http",
            Path = "/hc",
            Port = 8080,
            ExpectedStatusCodes = new[] { 200, 204 },
            Https = true,
            Interval = "30s",
            Timeout = "10s",
            Retries = 3
        };

        // Act
        deployment.SetHealthCheckConfigs(new[] { config });

        // Assert
        var storedConfig = deployment.HealthCheckConfigs.First();
        storedConfig.ServiceName.Should().Be("api");
        storedConfig.Type.Should().Be("http");
        storedConfig.Path.Should().Be("/hc");
        storedConfig.Port.Should().Be(8080);
        storedConfig.ExpectedStatusCodes.Should().BeEquivalentTo(new[] { 200, 204 });
        storedConfig.Https.Should().BeTrue();
        storedConfig.Interval.Should().Be("30s");
        storedConfig.Timeout.Should().Be("10s");
        storedConfig.Retries.Should().Be(3);
    }

    #endregion

    #region Snapshot Tests (Point of No Return Semantics)

    [Fact]
    public void CreateSnapshot_FromRunning_CreatesPendingUpgradeSnapshot()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.SetVariables(new Dictionary<string, string> { ["DB_HOST"] = "localhost" });
        deployment.MarkAsRunning(CreateTestServices());
        deployment.ClearDomainEvents();

        // Act
        var snapshot = deployment.CreateSnapshot("Before upgrade to v2.0");

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.DeploymentId.Should().Be(deployment.Id);
        snapshot.StackVersion.Should().Be("1.0.0");
        snapshot.Description.Should().Be("Before upgrade to v2.0");
        snapshot.Variables.Should().ContainKey("DB_HOST");
        snapshot.Services.Should().HaveCount(2);
        deployment.PendingUpgradeSnapshot.Should().NotBeNull();
        deployment.PendingUpgradeSnapshot.Should().Be(snapshot);
        deployment.DomainEvents.Should().ContainSingle(e => e is DeploymentSnapshotCreated);
    }

    [Fact]
    public void CreateSnapshot_FromPending_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");

        // Act
        var act = () => deployment.CreateSnapshot();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*running*");
    }

    [Fact]
    public void CreateSnapshot_WithoutStackVersion_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        var act = () => deployment.CreateSnapshot();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*version*");
    }

    [Fact]
    public void CreateSnapshot_FromStopped_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.MarkAsStopped();

        // Act
        var act = () => deployment.CreateSnapshot();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*running*");
    }

    [Fact]
    public void CreateSnapshot_WhenSnapshotAlreadyExists_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.CreateSnapshot("First snapshot");

        // Act
        var act = () => deployment.CreateSnapshot("Second snapshot");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public void CreateSnapshot_CapturesServiceImages()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        var services = new[]
        {
            new DeployedService("db", "c1", "db-1", "mysql:8.0", "running"),
            new DeployedService("app", "c2", "app-1", "myapp:1.5.0", "running")
        };
        deployment.MarkAsRunning(services);

        // Act
        var snapshot = deployment.CreateSnapshot()!;

        // Assert
        snapshot.Services.Should().HaveCount(2);
        snapshot.Services.Should().Contain(s => s.Name == "db" && s.Image == "mysql:8.0");
        snapshot.Services.Should().Contain(s => s.Name == "app" && s.Image == "myapp:1.5.0");
    }

    [Fact]
    public void ClearSnapshot_RemovesPendingUpgradeSnapshot()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.CreateSnapshot("Before upgrade");
        deployment.PendingUpgradeSnapshot.Should().NotBeNull();

        // Act
        deployment.ClearSnapshot();

        // Assert
        deployment.PendingUpgradeSnapshot.Should().BeNull();
    }

    [Fact]
    public void GetRollbackTargetVersion_WithSnapshot_ReturnsSnapshotVersion()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.CreateSnapshot();

        // Act
        var targetVersion = deployment.GetRollbackTargetVersion();

        // Assert
        targetVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void GetRollbackTargetVersion_WithoutSnapshot_ReturnsNull()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        var targetVersion = deployment.GetRollbackTargetVersion();

        // Assert
        targetVersion.Should().BeNull();
    }

    #endregion

    #region Rollback Tests (Point of No Return Semantics)

    [Fact]
    public void RollbackToPrevious_WhenFailedWithSnapshot_RestoresState()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.SetVariables(new Dictionary<string, string> { ["VAR1"] = "old" });
        deployment.MarkAsRunning(CreateTestServices());
        deployment.CreateSnapshot("Before upgrade to v2.0");

        // Simulate upgrade that changed state and then failed (before container start)
        deployment.SetStackVersion("2.0.0");
        deployment.SetVariables(new Dictionary<string, string> { ["VAR1"] = "new", ["VAR2"] = "added" });
        deployment.MarkAsFailed("Image pull failed");
        deployment.ClearDomainEvents();

        // Act
        deployment.RollbackToPrevious();

        // Assert
        deployment.StackVersion.Should().Be("1.0.0");
        deployment.Variables.Should().HaveCount(1);
        deployment.Variables["VAR1"].Should().Be("old");
        deployment.Status.Should().Be(DeploymentStatus.Pending);
        deployment.CurrentPhase.Should().Be(DeploymentPhase.Initializing);
        deployment.PendingUpgradeSnapshot.Should().BeNull(); // Consumed by rollback
        deployment.DomainEvents.Should().Contain(e => e is DeploymentRolledBack);
    }

    [Fact]
    public void RollbackToPrevious_WithoutSnapshot_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsFailed("Some error");

        // Act
        var act = () => deployment.RollbackToPrevious();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*No snapshot available*");
    }

    [Fact]
    public void RollbackToPrevious_WhenNotFailed_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.CreateSnapshot();

        // Act - Try to rollback without being in Failed state
        var act = () => deployment.RollbackToPrevious();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*failed upgrade*");
    }

    [Fact]
    public void CanRollback_WhenFailedWithSnapshot_ReturnsTrue()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.CreateSnapshot();

        // Simulate failed upgrade before container start
        deployment.MarkAsFailed("Image pull failed");

        // Assert
        deployment.CanRollback().Should().BeTrue();
    }

    [Fact]
    public void CanRollback_WhenFailedWithoutSnapshot_ReturnsFalse()
    {
        // Arrange - Simulates upgrade that passed Point of No Return (snapshot was cleared)
        var deployment = CreateTestDeployment();
        deployment.MarkAsFailed("Container health check failed");

        // Assert
        deployment.CanRollback().Should().BeFalse();
    }

    [Fact]
    public void CanRollback_WhenRunningWithSnapshot_ReturnsFalse()
    {
        // Arrange - Running deployment can have snapshot (mid-upgrade), but can't rollback
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.CreateSnapshot();

        // Assert - Must be Failed to rollback
        deployment.CanRollback().Should().BeFalse();
    }

    [Fact]
    public void CanRollback_WhenRemoved_ReturnsFalse()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.MarkAsRemoved();

        // Assert
        deployment.CanRollback().Should().BeFalse();
    }

    [Fact]
    public void RollbackToPrevious_ClearsErrorState()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.CreateSnapshot();
        deployment.MarkAsFailed("Upgrade failed");

        // Act
        deployment.RollbackToPrevious();

        // Assert
        deployment.ErrorMessage.Should().BeNull();
        deployment.IsCancellationRequested.Should().BeFalse();
        deployment.CancellationReason.Should().BeNull();
        deployment.ProgressPercentage.Should().Be(0);
    }

    [Fact]
    public void RollbackToPrevious_RaisesDeploymentRolledBackEvent()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        var snapshot = deployment.CreateSnapshot()!;
        deployment.MarkAsFailed("Upgrade failed");
        deployment.ClearDomainEvents();

        // Act
        deployment.RollbackToPrevious();

        // Assert
        var domainEvent = deployment.DomainEvents.OfType<DeploymentRolledBack>().Single();
        domainEvent.DeploymentId.Should().Be(deployment.Id);
        domainEvent.SnapshotId.Should().Be(snapshot.Id);
        domainEvent.RestoredVersion.Should().Be("1.0.0");
    }

    [Fact]
    public void RollbackToPrevious_ConsumesSnapshot()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.CreateSnapshot();
        deployment.MarkAsFailed("Upgrade failed");

        deployment.PendingUpgradeSnapshot.Should().NotBeNull();

        // Act
        deployment.RollbackToPrevious();

        // Assert - Snapshot is consumed (deleted)
        deployment.PendingUpgradeSnapshot.Should().BeNull();
        deployment.CanRollback().Should().BeFalse();
    }

    #endregion

    #region Upgrade Tracking Tests (RecordUpgrade, CanUpgrade)

    [Fact]
    public void RecordUpgrade_WithValidVersions_RecordsUpgradeHistory()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.ClearDomainEvents();

        // Act
        deployment.RecordUpgrade("1.0.0", "2.0.0");

        // Assert
        deployment.StackVersion.Should().Be("2.0.0");
        deployment.PreviousVersion.Should().Be("1.0.0");
        deployment.UpgradeCount.Should().Be(1);
        deployment.LastUpgradedAt.Should().NotBeNull();
        deployment.LastUpgradedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        deployment.DomainEvents.Should().ContainSingle(e => e is DeploymentUpgraded);
    }

    [Fact]
    public void RecordUpgrade_MultipleTimes_IncrementsUpgradeCount()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        deployment.RecordUpgrade("1.0.0", "2.0.0");
        deployment.RecordUpgrade("2.0.0", "3.0.0");
        deployment.RecordUpgrade("3.0.0", "4.0.0");

        // Assert
        deployment.StackVersion.Should().Be("4.0.0");
        deployment.PreviousVersion.Should().Be("3.0.0");
        deployment.UpgradeCount.Should().Be(3);
    }

    [Fact]
    public void RecordUpgrade_RaisesDeploymentUpgradedEvent()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());
        deployment.ClearDomainEvents();

        // Act
        deployment.RecordUpgrade("1.0.0", "2.0.0");

        // Assert
        var domainEvent = deployment.DomainEvents.OfType<DeploymentUpgraded>().Single();
        domainEvent.DeploymentId.Should().Be(deployment.Id);
        domainEvent.PreviousVersion.Should().Be("1.0.0");
        domainEvent.NewVersion.Should().Be("2.0.0");
        domainEvent.UpgradedAt.Should().Be(deployment.LastUpgradedAt);
    }

    [Fact]
    public void RecordUpgrade_WithEmptyPreviousVersion_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        var act = () => deployment.RecordUpgrade("", "2.0.0");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordUpgrade_WithEmptyNewVersion_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        var act = () => deployment.RecordUpgrade("1.0.0", "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CanUpgrade_WhenRunning_ReturnsTrue()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());

        // Assert
        deployment.CanUpgrade().Should().BeTrue();
    }

    [Fact]
    public void CanUpgrade_WhenPending_ReturnsFalse()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Assert
        deployment.CanUpgrade().Should().BeFalse();
    }

    [Fact]
    public void CanUpgrade_WhenStopped_ReturnsFalse()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.MarkAsStopped();

        // Assert
        deployment.CanUpgrade().Should().BeFalse();
    }

    [Fact]
    public void CanUpgrade_WhenFailed_ReturnsFalse()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsFailed("Error");

        // Assert
        deployment.CanUpgrade().Should().BeFalse();
    }

    [Fact]
    public void CanUpgrade_WhenRemoved_ReturnsFalse()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.MarkAsRemoved();

        // Assert
        deployment.CanUpgrade().Should().BeFalse();
    }

    [Fact]
    public void UpgradeCount_DefaultsToZero()
    {
        // Arrange & Act
        var deployment = CreateTestDeployment();

        // Assert
        deployment.UpgradeCount.Should().Be(0);
    }

    [Fact]
    public void LastUpgradedAt_DefaultsToNull()
    {
        // Arrange & Act
        var deployment = CreateTestDeployment();

        // Assert
        deployment.LastUpgradedAt.Should().BeNull();
    }

    [Fact]
    public void PreviousVersion_DefaultsToNull()
    {
        // Arrange & Act
        var deployment = CreateTestDeployment();

        // Assert
        deployment.PreviousVersion.Should().BeNull();
    }

    #endregion

    #region UpdateServicesAfterUpgrade Tests

    [Fact]
    public void UpdateServicesAfterUpgrade_ReplacesServices()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(new[]
        {
            new DeployedService("old-service", "c1", "old-1", "old:1.0", "running")
        });
        deployment.ClearDomainEvents();

        // Act
        deployment.UpdateServicesAfterUpgrade(new[]
        {
            new DeployedService("new-service", "c2", "new-1", "new:2.0", "running")
        });

        // Assert
        deployment.Services.Should().HaveCount(1);
        deployment.Services.First().ServiceName.Should().Be("new-service");
        deployment.DomainEvents.Should().ContainSingle(e => e is DeploymentProgressUpdated);
    }

    [Fact]
    public void UpdateServicesAfterUpgrade_FromPending_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();

        // Act
        var act = () => deployment.UpdateServicesAfterUpgrade(CreateTestServices());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*running*");
    }

    [Fact]
    public void UpdateServicesAfterUpgrade_FromStopped_ThrowsArgumentException()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.MarkAsRunning(CreateTestServices());
        deployment.MarkAsStopped();

        // Act
        var act = () => deployment.UpdateServicesAfterUpgrade(CreateTestServices());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*running*");
    }

    [Fact]
    public void UpdateServicesAfterUpgrade_RecordsPhaseHistory()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        deployment.SetStackVersion("2.0.0");
        deployment.MarkAsRunning(CreateTestServices());

        // Act
        deployment.UpdateServicesAfterUpgrade(CreateTestServices());

        // Assert
        deployment.PhaseHistory.Should().Contain(p =>
            p.Phase == DeploymentPhase.Completed &&
            p.Message.Contains("2.0.0"));
    }

    #endregion

    #region Helper Methods

    private static Deployment CreateTestDeployment()
    {
        return Deployment.Start(
            DeploymentId.NewId(),
            EnvironmentId.NewId(),
            "wordpress", // stackId
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
