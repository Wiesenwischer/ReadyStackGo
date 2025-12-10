using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Services;

namespace ReadyStackGo.UnitTests.Services;

using Deployment = ReadyStackGo.Domain.Deployment.Deployments.Deployment;
using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;

/// <summary>
/// Unit tests for DeploymentService.
/// These tests focus on business logic edge cases that integration tests might miss.
/// </summary>
public class DeploymentServiceTests
{
    private readonly Mock<IDockerComposeParser> _composeParserMock;
    private readonly Mock<IDeploymentEngine> _deploymentEngineMock;
    private readonly Mock<IDeploymentRepository> _deploymentRepositoryMock;
    private readonly Mock<IEnvironmentRepository> _environmentRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILogger<DeploymentService>> _loggerMock;
    private readonly DeploymentService _sut;

    private readonly EnvironmentId _testEnvId = EnvironmentId.NewId();
    private readonly UserId _testUserId = UserId.NewId();

    public DeploymentServiceTests()
    {
        _composeParserMock = new Mock<IDockerComposeParser>();
        _deploymentEngineMock = new Mock<IDeploymentEngine>();
        _deploymentRepositoryMock = new Mock<IDeploymentRepository>();
        _environmentRepositoryMock = new Mock<IEnvironmentRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<DeploymentService>>();

        _sut = new DeploymentService(
            _composeParserMock.Object,
            _deploymentEngineMock.Object,
            _deploymentRepositoryMock.Object,
            _environmentRepositoryMock.Object,
            _userRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region ListDeploymentsAsync Tests

    [Fact]
    public async Task ListDeploymentsAsync_ShouldExcludeRemovedDeployments()
    {
        // Arrange
        var runningDeployment = CreateTestDeployment("stack-running", DeploymentStatus.Running);
        var stoppedDeployment = CreateTestDeployment("stack-stopped", DeploymentStatus.Stopped);
        var removedDeployment = CreateTestDeployment("stack-removed", DeploymentStatus.Removed);

        _deploymentRepositoryMock
            .Setup(x => x.GetByEnvironment(_testEnvId))
            .Returns(new[] { runningDeployment, stoppedDeployment, removedDeployment });

        // Act
        var result = await _sut.ListDeploymentsAsync(_testEnvId.ToString());

        // Assert
        result.Success.Should().BeTrue();
        result.Deployments.Should().HaveCount(2);
        result.Deployments.Should().NotContain(d => d.StackName == "stack-removed");
        result.Deployments.Should().Contain(d => d.StackName == "stack-running");
        result.Deployments.Should().Contain(d => d.StackName == "stack-stopped");
    }

    [Fact]
    public async Task ListDeploymentsAsync_ShouldIncludePendingAndFailedDeployments()
    {
        // Arrange
        var pendingDeployment = CreateTestDeployment("stack-pending", DeploymentStatus.Pending);
        var failedDeployment = CreateTestDeployment("stack-failed", DeploymentStatus.Failed);

        _deploymentRepositoryMock
            .Setup(x => x.GetByEnvironment(_testEnvId))
            .Returns(new[] { pendingDeployment, failedDeployment });

        // Act
        var result = await _sut.ListDeploymentsAsync(_testEnvId.ToString());

        // Assert
        result.Success.Should().BeTrue();
        result.Deployments.Should().HaveCount(2);
        result.Deployments.Should().Contain(d => d.StackName == "stack-pending");
        result.Deployments.Should().Contain(d => d.StackName == "stack-failed");
    }

    [Fact]
    public async Task ListDeploymentsAsync_WhenAllDeploymentsRemoved_ShouldReturnEmptyList()
    {
        // Arrange
        var removed1 = CreateTestDeployment("stack-1", DeploymentStatus.Removed);
        var removed2 = CreateTestDeployment("stack-2", DeploymentStatus.Removed);

        _deploymentRepositoryMock
            .Setup(x => x.GetByEnvironment(_testEnvId))
            .Returns(new[] { removed1, removed2 });

        // Act
        var result = await _sut.ListDeploymentsAsync(_testEnvId.ToString());

        // Assert
        result.Success.Should().BeTrue();
        result.Deployments.Should().BeEmpty();
    }

    #endregion

    #region RemoveDeploymentAsync Tests

    [Fact]
    public async Task RemoveDeploymentAsync_WhenDeploymentAlreadyRemoved_ShouldSucceedWithoutError()
    {
        // Arrange
        var removedDeployment = CreateTestDeployment("my-stack", DeploymentStatus.Removed);

        _deploymentRepositoryMock
            .Setup(x => x.GetByStackName(_testEnvId, "my-stack"))
            .Returns(removedDeployment);

        _deploymentEngineMock
            .Setup(x => x.RemoveStackAsync(_testEnvId.ToString(), "my-stack"))
            .ReturnsAsync(new DeploymentResult { Success = true });

        // Act
        var result = await _sut.RemoveDeploymentAsync(_testEnvId.ToString(), "my-stack");

        // Assert
        result.Success.Should().BeTrue("removing an already-removed deployment should succeed silently");

        // Should NOT try to call MarkAsRemoved again
        _deploymentRepositoryMock.Verify(x => x.Update(It.IsAny<Deployment>()), Times.Never);
    }

    [Fact]
    public async Task RemoveDeploymentAsync_WhenDeploymentRunning_ShouldMarkAsRemoved()
    {
        // Arrange
        var runningDeployment = CreateTestDeployment("my-stack", DeploymentStatus.Running);

        _deploymentRepositoryMock
            .Setup(x => x.GetByStackName(_testEnvId, "my-stack"))
            .Returns(runningDeployment);

        _deploymentEngineMock
            .Setup(x => x.RemoveStackAsync(_testEnvId.ToString(), "my-stack"))
            .ReturnsAsync(new DeploymentResult { Success = true });

        // Act
        var result = await _sut.RemoveDeploymentAsync(_testEnvId.ToString(), "my-stack");

        // Assert
        result.Success.Should().BeTrue();
        _deploymentRepositoryMock.Verify(x => x.Update(It.IsAny<Deployment>()), Times.Once);
        _deploymentRepositoryMock.Verify(x => x.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task RemoveDeploymentAsync_WhenDeploymentFailed_ShouldMarkAsRemoved()
    {
        // Arrange
        var failedDeployment = CreateTestDeployment("my-stack", DeploymentStatus.Failed);

        _deploymentRepositoryMock
            .Setup(x => x.GetByStackName(_testEnvId, "my-stack"))
            .Returns(failedDeployment);

        _deploymentEngineMock
            .Setup(x => x.RemoveStackAsync(_testEnvId.ToString(), "my-stack"))
            .ReturnsAsync(new DeploymentResult { Success = true });

        // Act
        var result = await _sut.RemoveDeploymentAsync(_testEnvId.ToString(), "my-stack");

        // Assert
        result.Success.Should().BeTrue();
        _deploymentRepositoryMock.Verify(x => x.Update(It.IsAny<Deployment>()), Times.Once);
    }

    [Fact]
    public async Task RemoveDeploymentAsync_WhenDeploymentNotFound_ShouldStillSucceed()
    {
        // Arrange - deployment record doesn't exist in DB but containers might
        _deploymentRepositoryMock
            .Setup(x => x.GetByStackName(_testEnvId, "orphan-stack"))
            .Returns((Deployment?)null);

        _deploymentEngineMock
            .Setup(x => x.RemoveStackAsync(_testEnvId.ToString(), "orphan-stack"))
            .ReturnsAsync(new DeploymentResult { Success = true });

        // Act
        var result = await _sut.RemoveDeploymentAsync(_testEnvId.ToString(), "orphan-stack");

        // Assert - should succeed (containers were removed, even if no DB record)
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveDeploymentAsync_WhenDockerEngineFails_ShouldReturnError()
    {
        // Arrange
        var runningDeployment = CreateTestDeployment("my-stack", DeploymentStatus.Running);

        _deploymentRepositoryMock
            .Setup(x => x.GetByStackName(_testEnvId, "my-stack"))
            .Returns(runningDeployment);

        _deploymentEngineMock
            .Setup(x => x.RemoveStackAsync(_testEnvId.ToString(), "my-stack"))
            .ReturnsAsync(new DeploymentResult
            {
                Success = false,
                Errors = new List<string> { "Container not found" }
            });

        // Act
        var result = await _sut.RemoveDeploymentAsync(_testEnvId.ToString(), "my-stack");

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Container not found");

        // Should NOT update DB when Docker removal fails
        _deploymentRepositoryMock.Verify(x => x.Update(It.IsAny<Deployment>()), Times.Never);
    }

    #endregion

    #region Helper Methods

    private Deployment CreateTestDeployment(string stackName, DeploymentStatus status)
    {
        var deployment = Deployment.Start(
            DeploymentId.NewId(),
            _testEnvId,
            stackName,
            $"{stackName}-project",
            _testUserId);

        // Transition to desired status
        switch (status)
        {
            case DeploymentStatus.Running:
                deployment.MarkAsRunning(CreateTestServices());
                break;
            case DeploymentStatus.Stopped:
                deployment.MarkAsRunning(CreateTestServices());
                deployment.MarkAsStopped();
                break;
            case DeploymentStatus.Failed:
                deployment.MarkAsFailed("Test failure");
                break;
            case DeploymentStatus.Removed:
                deployment.MarkAsRunning(CreateTestServices());
                deployment.MarkAsRemoved();
                break;
            case DeploymentStatus.Pending:
            default:
                // Already pending
                break;
        }

        return deployment;
    }

    private static IEnumerable<DeployedService> CreateTestServices()
    {
        return new[]
        {
            new DeployedService("service-1", "container-1", "service-1-container", "image:latest", "running")
        };
    }

    #endregion
}
