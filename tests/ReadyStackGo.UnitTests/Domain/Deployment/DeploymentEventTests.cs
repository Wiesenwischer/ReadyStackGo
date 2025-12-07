using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for Deployment domain events.
/// </summary>
public class DeploymentEventTests
{
    #region DeploymentStarted Tests

    [Fact]
    public void DeploymentStarted_Constructor_SetsAllProperties()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();
        var environmentId = EnvironmentId.NewId();
        var stackName = "wordpress";

        // Act
        var evt = new DeploymentStarted(deploymentId, environmentId, stackName);

        // Assert
        evt.DeploymentId.Should().Be(deploymentId);
        evt.EnvironmentId.Should().Be(environmentId);
        evt.StackName.Should().Be(stackName);
    }

    [Fact]
    public void DeploymentStarted_InheritsFromDomainEvent()
    {
        // Arrange & Act
        var evt = new DeploymentStarted(DeploymentId.NewId(), EnvironmentId.NewId(), "stack");

        // Assert
        evt.Should().BeAssignableTo<ReadyStackGo.Domain.SharedKernel.DomainEvent>();
    }

    #endregion

    #region DeploymentCompleted Tests

    [Fact]
    public void DeploymentCompleted_SuccessfulDeployment_SetsProperties()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();

        // Act
        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Running);

        // Assert
        evt.DeploymentId.Should().Be(deploymentId);
        evt.Status.Should().Be(DeploymentStatus.Running);
        evt.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void DeploymentCompleted_FailedDeployment_IncludesErrorMessage()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();
        var errorMessage = "Container failed to start";

        // Act
        var evt = new DeploymentCompleted(deploymentId, DeploymentStatus.Failed, errorMessage);

        // Assert
        evt.DeploymentId.Should().Be(deploymentId);
        evt.Status.Should().Be(DeploymentStatus.Failed);
        evt.ErrorMessage.Should().Be(errorMessage);
    }

    #endregion

    #region DeploymentProgressUpdated Tests

    [Fact]
    public void DeploymentProgressUpdated_Constructor_SetsAllProperties()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();
        var phase = DeploymentPhase.PullingImages;
        var progressPercentage = 50;
        var message = "Pulling image nginx:latest";

        // Act
        var evt = new DeploymentProgressUpdated(deploymentId, phase, progressPercentage, message);

        // Assert
        evt.DeploymentId.Should().Be(deploymentId);
        evt.Phase.Should().Be(phase);
        evt.ProgressPercentage.Should().Be(progressPercentage);
        evt.Message.Should().Be(message);
    }

    [Theory]
    [InlineData(DeploymentPhase.Initializing, 0)]
    [InlineData(DeploymentPhase.ValidatingPrerequisites, 10)]
    [InlineData(DeploymentPhase.PullingImages, 30)]
    [InlineData(DeploymentPhase.Starting, 60)]
    [InlineData(DeploymentPhase.WaitingForHealthChecks, 80)]
    [InlineData(DeploymentPhase.Completed, 100)]
    public void DeploymentProgressUpdated_VariousPhases_TracksCorrectly(DeploymentPhase phase, int percentage)
    {
        // Act
        var evt = new DeploymentProgressUpdated(DeploymentId.NewId(), phase, percentage, "Progress");

        // Assert
        evt.Phase.Should().Be(phase);
        evt.ProgressPercentage.Should().Be(percentage);
    }

    #endregion

    #region DeploymentStopped Tests

    [Fact]
    public void DeploymentStopped_Constructor_SetsDeploymentId()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();

        // Act
        var evt = new DeploymentStopped(deploymentId);

        // Assert
        evt.DeploymentId.Should().Be(deploymentId);
    }

    #endregion

    #region DeploymentRestarted Tests

    [Fact]
    public void DeploymentRestarted_Constructor_SetsDeploymentId()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();

        // Act
        var evt = new DeploymentRestarted(deploymentId);

        // Assert
        evt.DeploymentId.Should().Be(deploymentId);
    }

    #endregion

    #region DeploymentRemoved Tests

    [Fact]
    public void DeploymentRemoved_Constructor_SetsDeploymentId()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();

        // Act
        var evt = new DeploymentRemoved(deploymentId);

        // Assert
        evt.DeploymentId.Should().Be(deploymentId);
    }

    #endregion

    #region DeploymentCancellationRequested Tests

    [Fact]
    public void DeploymentCancellationRequested_Constructor_SetsAllProperties()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();
        var reason = "User requested cancellation";

        // Act
        var evt = new DeploymentCancellationRequested(deploymentId, reason);

        // Assert
        evt.DeploymentId.Should().Be(deploymentId);
        evt.Reason.Should().Be(reason);
    }

    #endregion

    #region ServiceStatusChanged Tests

    [Fact]
    public void ServiceStatusChanged_Constructor_SetsAllProperties()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();
        var serviceName = "web";
        var previousStatus = "starting";
        var newStatus = "running";

        // Act
        var evt = new ServiceStatusChanged(deploymentId, serviceName, previousStatus, newStatus);

        // Assert
        evt.DeploymentId.Should().Be(deploymentId);
        evt.ServiceName.Should().Be(serviceName);
        evt.PreviousStatus.Should().Be(previousStatus);
        evt.NewStatus.Should().Be(newStatus);
    }

    [Theory]
    [InlineData("starting", "running")]
    [InlineData("running", "stopped")]
    [InlineData("stopped", "removed")]
    [InlineData("running", "exited")]
    public void ServiceStatusChanged_VariousTransitions_TracksCorrectly(string from, string to)
    {
        // Act
        var evt = new ServiceStatusChanged(DeploymentId.NewId(), "service", from, to);

        // Assert
        evt.PreviousStatus.Should().Be(from);
        evt.NewStatus.Should().Be(to);
    }

    #endregion
}
