using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Containers.RemoveOrphanedStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.UnitTests.Application.Containers;

public class RemoveOrphanedStackHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly RemoveOrphanedStackHandler _handler;

    private static readonly string EnvId = Guid.NewGuid().ToString();

    public RemoveOrphanedStackHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _handler = new RemoveOrphanedStackHandler(
            _dockerServiceMock.Object, _deploymentRepoMock.Object);
    }

    private static ContainerDto MakeContainer(
        string id, string name, string stackLabel, string state = "running") =>
        new()
        {
            Id = id,
            Name = name,
            Image = "test:latest",
            State = state,
            Status = "Up 5 minutes",
            Labels = new Dictionary<string, string> { ["rsgo.stack"] = stackLabel }
        };

    #region Happy Path

    [Fact]
    public async Task Handle_OrphanedStackWith3Containers_RemovesAllAndReturnsCount()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeContainer("c1", "app1", "my-stack"),
                MakeContainer("c2", "app2", "my-stack"),
                MakeContainer("c3", "db", "my-stack"),
            });

        var result = await _handler.Handle(
            new RemoveOrphanedStackCommand(EnvId, "my-stack"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RemovedCount.Should().Be(3);
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(EnvId, It.IsAny<string>(), true, It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_NoContainersFound_ReturnsSuccessWithZeroCount()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "ghost-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContainerDto>());

        var result = await _handler.Handle(
            new RemoveOrphanedStackCommand(EnvId, "ghost-stack"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RemovedCount.Should().Be(0);
    }

    #endregion

    #region Validation

    [Fact]
    public async Task Handle_InvalidEnvironmentId_ReturnsError()
    {
        var result = await _handler.Handle(
            new RemoveOrphanedStackCommand("not-a-guid", "my-stack"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task Handle_StackNotOrphaned_ReturnsError()
    {
        var deploymentId = DeploymentId.NewId();
        var environmentId = new EnvironmentId(Guid.Parse(EnvId));
        var deployment = Deployment.StartInstallation(
            deploymentId, environmentId, "stack:id", "my-stack", "my-stack",
            ReadyStackGo.Domain.Deployment.UserId.NewId());

        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns(deployment);

        var result = await _handler.Handle(
            new RemoveOrphanedStackCommand(EnvId, "my-stack"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not orphaned");
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Filtering

    [Fact]
    public async Task Handle_OnlyRemovesContainersWithMatchingStackLabel()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "target"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                MakeContainer("c1", "app", "target"),
                MakeContainer("c2", "other", "different-stack"),
                MakeContainer("c3", "db", "target"),
            });

        var result = await _handler.Handle(
            new RemoveOrphanedStackCommand(EnvId, "target"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RemovedCount.Should().Be(2);
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(EnvId, "c1", true, It.IsAny<CancellationToken>()), Times.Once);
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(EnvId, "c3", true, It.IsAny<CancellationToken>()), Times.Once);
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(EnvId, "c2", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("MY-STACK")]
    [InlineData("My-Stack")]
    [InlineData("my-stack")]
    public async Task Handle_StackLabelMatchIsCaseInsensitive(string labelValue)
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", labelValue) });

        var result = await _handler.Handle(
            new RemoveOrphanedStackCommand(EnvId, "my-stack"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RemovedCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ContainerWithoutStackLabel_IsIgnored()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        var unlabeled = new ContainerDto
        {
            Id = "c1",
            Name = "random",
            Image = "test:latest",
            State = "running",
            Status = "Up",
            Labels = new Dictionary<string, string>()
        };

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { unlabeled });

        var result = await _handler.Handle(
            new RemoveOrphanedStackCommand(EnvId, "my-stack"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RemovedCount.Should().Be(0);
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task Handle_DockerServiceThrows_ReturnsError()
    {
        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns((Deployment?)null);

        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "my-stack") });

        _dockerServiceMock
            .Setup(s => s.RemoveContainerAsync(EnvId, "c1", true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Docker daemon unavailable"));

        var result = await _handler.Handle(
            new RemoveOrphanedStackCommand(EnvId, "my-stack"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Docker daemon unavailable");
    }

    #endregion
}
