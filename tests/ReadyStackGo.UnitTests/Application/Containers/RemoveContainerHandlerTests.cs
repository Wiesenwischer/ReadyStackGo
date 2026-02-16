using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Containers.RemoveContainer;

namespace ReadyStackGo.UnitTests.Application.Containers;

public class RemoveContainerHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly RemoveContainerHandler _handler;

    public RemoveContainerHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _handler = new RemoveContainerHandler(_dockerServiceMock.Object);
    }

    private static ContainerDto MakeContainer(string id, string name, string state = "exited") =>
        new() { Id = id, Name = name, Image = "test:latest", State = state, Status = $"Exited (0)" };

    [Fact]
    public async Task Handle_StoppedContainer_ReturnsSuccess()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "exited") });

        var result = await _handler.Handle(
            new RemoveContainerCommand("env-1", "c1"), CancellationToken.None);

        result.Success.Should().BeTrue();
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync("env-1", "c1", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RunningContainer_WithoutForce_ReturnsError()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "running") });

        var result = await _handler.Handle(
            new RemoveContainerCommand("env-1", "c1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("running");
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_RunningContainer_WithForce_ReturnsSuccess()
    {
        var result = await _handler.Handle(
            new RemoveContainerCommand("env-1", "c1", Force: true), CancellationToken.None);

        result.Success.Should().BeTrue();
        _dockerServiceMock.Verify(
            s => s.RemoveContainerAsync("env-1", "c1", true, It.IsAny<CancellationToken>()), Times.Once);
        // Force skips the container list check entirely
        _dockerServiceMock.Verify(
            s => s.ListContainersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NonExistentContainer_ReturnsError()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("other", "other-app") });

        var result = await _handler.Handle(
            new RemoveContainerCommand("env-1", "c1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_DockerServiceThrows_ReturnsError()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", "exited") });
        _dockerServiceMock
            .Setup(s => s.RemoveContainerAsync("env-1", "c1", false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Docker daemon error"));

        var result = await _handler.Handle(
            new RemoveContainerCommand("env-1", "c1"), CancellationToken.None);

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
            .Setup(s => s.ListContainersAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeContainer("c1", "app", state) });

        var result = await _handler.Handle(
            new RemoveContainerCommand("env-1", "c1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("running");
    }
}
