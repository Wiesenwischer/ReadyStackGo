using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Volumes.RemoveVolume;

namespace ReadyStackGo.UnitTests.Application.Volumes;

public class RemoveVolumeHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly RemoveVolumeHandler _handler;

    public RemoveVolumeHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _handler = new RemoveVolumeHandler(_dockerServiceMock.Object);
    }

    private static ContainerVolumeMount MakeMount(string container, string volume, string path = "/data") =>
        new() { ContainerName = container, VolumeName = volume, MountPath = path };

    [Fact]
    public async Task Handle_ValidUnreferencedVolume_RemovesSuccessfully()
    {
        _dockerServiceMock
            .Setup(s => s.GetContainerVolumeMountsAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerVolumeMount>());

        var result = await _handler.Handle(
            new RemoveVolumeCommand("env-1", "my-vol"), CancellationToken.None);

        result.Success.Should().BeTrue();
        _dockerServiceMock.Verify(s => s.RemoveVolumeAsync("env-1", "my-vol", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReferencedVolume_WithoutForce_ReturnsFailure()
    {
        _dockerServiceMock
            .Setup(s => s.GetContainerVolumeMountsAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerVolumeMount> { MakeMount("app-1", "my-vol") });

        var result = await _handler.Handle(
            new RemoveVolumeCommand("env-1", "my-vol"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("still referenced");
        _dockerServiceMock.Verify(
            s => s.RemoveVolumeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ReferencedVolume_WithForce_RemovesSuccessfully()
    {
        var result = await _handler.Handle(
            new RemoveVolumeCommand("env-1", "my-vol", Force: true), CancellationToken.None);

        result.Success.Should().BeTrue();
        _dockerServiceMock.Verify(s => s.RemoveVolumeAsync("env-1", "my-vol", true, It.IsAny<CancellationToken>()), Times.Once);
        _dockerServiceMock.Verify(s => s.GetContainerVolumeMountsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyVolumeName_ReturnsFailure(string? volumeName)
    {
        var result = await _handler.Handle(
            new RemoveVolumeCommand("env-1", volumeName!), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Volume name cannot be empty");
    }

    [Fact]
    public async Task Handle_DockerServiceThrows_ReturnsFailure()
    {
        _dockerServiceMock
            .Setup(s => s.GetContainerVolumeMountsAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerVolumeMount>());
        _dockerServiceMock
            .Setup(s => s.RemoveVolumeAsync("env-1", "vol", false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Volume is in use"));

        var result = await _handler.Handle(
            new RemoveVolumeCommand("env-1", "vol"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Volume is in use");
    }

    [Fact]
    public async Task Handle_CaseInsensitiveReferenceCheck_DetectsMatch()
    {
        _dockerServiceMock
            .Setup(s => s.GetContainerVolumeMountsAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerVolumeMount> { MakeMount("app-1", "My-Vol") });

        var result = await _handler.Handle(
            new RemoveVolumeCommand("env-1", "my-vol"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("still referenced");
    }
}
