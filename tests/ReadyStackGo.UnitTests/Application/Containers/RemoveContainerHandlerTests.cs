using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Containers.RemoveContainer;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.UnitTests.Application.Containers;

public class RemoveContainerHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IDeploymentRepository> _deploymentRepoMock;
    private readonly RemoveContainerHandler _handler;

    private static readonly string EnvId = Guid.NewGuid().ToString();

    public RemoveContainerHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _deploymentRepoMock = new Mock<IDeploymentRepository>();
        _handler = new RemoveContainerHandler(_dockerServiceMock.Object, _deploymentRepoMock.Object);
    }

    private static ContainerDto MakeContainer(string id, string name, string state = "exited",
        Dictionary<string, string>? labels = null) =>
        new()
        {
            Id = id, Name = name, Image = "test:latest", State = state, Status = "Exited (0)",
            Labels = labels ?? new Dictionary<string, string>()
        };

    [Fact]
    public async Task Handle_StoppedContainer_ReturnsSuccess()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "exited") });

        var result = await _handler.Handle(
            new RemoveContainerCommand(EnvId, "c1"), CancellationToken.None);

        result.Success.Should().BeTrue();
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(EnvId, "c1", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RunningContainer_WithoutForce_ReturnsError()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "running") });

        var result = await _handler.Handle(
            new RemoveContainerCommand(EnvId, "c1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("running");
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_RunningContainer_WithForce_ReturnsSuccess()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "running") });

        var result = await _handler.Handle(
            new RemoveContainerCommand(EnvId, "c1", Force: true), CancellationToken.None);

        result.Success.Should().BeTrue();
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(EnvId, "c1", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistentContainer_ReturnsError()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("other", "other-app") });

        var result = await _handler.Handle(
            new RemoveContainerCommand(EnvId, "c1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_DockerServiceThrows_ReturnsError()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "exited") });
        _dockerServiceMock
            .Setup(s => s.RemoveContainerAsync(EnvId, "c1", false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Docker daemon error"));

        var result = await _handler.Handle(
            new RemoveContainerCommand(EnvId, "c1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Docker daemon error");
    }

    [Theory]
    [InlineData("Running")]
    [InlineData("RUNNING")]
    [InlineData("running")]
    public async Task Handle_RunningStateIsCaseInsensitive(string state)
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", state) });

        var result = await _handler.Handle(
            new RemoveContainerCommand(EnvId, "c1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("running");
    }

    [Fact]
    public async Task Handle_ManagedContainer_RejectsRemoval()
    {
        var labels = new Dictionary<string, string> { ["rsgo.stack"] = "my-stack" };
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "exited", labels) });

        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns(Mock.Of<Deployment>());

        var result = await _handler.Handle(
            new RemoveContainerCommand(EnvId, "c1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("managed stack");
        result.ErrorMessage.Should().Contain("my-stack");
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ManagedContainer_WithForce_StillRejectsRemoval()
    {
        var labels = new Dictionary<string, string> { ["rsgo.stack"] = "my-stack" };
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "running", labels) });

        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "my-stack"))
            .Returns(Mock.Of<Deployment>());

        var result = await _handler.Handle(
            new RemoveContainerCommand(EnvId, "c1", Force: true), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("managed stack");
    }

    [Fact]
    public async Task Handle_OrphanedContainer_WithStackLabel_AllowsRemoval()
    {
        var labels = new Dictionary<string, string> { ["rsgo.stack"] = "orphaned-stack" };
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "exited", labels) });

        _deploymentRepoMock
            .Setup(r => r.GetByStackName(It.IsAny<EnvironmentId>(), "orphaned-stack"))
            .Returns((Deployment?)null);

        var result = await _handler.Handle(
            new RemoveContainerCommand(EnvId, "c1"), CancellationToken.None);

        result.Success.Should().BeTrue();
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(EnvId, "c1", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ContainerWithoutStackLabel_AllowsRemoval()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "unmanaged-app", "exited") });

        var result = await _handler.Handle(
            new RemoveContainerCommand(EnvId, "c1"), CancellationToken.None);

        result.Success.Should().BeTrue();
        _deploymentRepoMock.Verify(
            r => r.GetByStackName(It.IsAny<EnvironmentId>(), It.IsAny<string>()), Times.Never);
    }
}
