using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Volumes.ListVolumes;

namespace ReadyStackGo.UnitTests.Application.Volumes;

public class ListVolumesHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly ListVolumesHandler _handler;

    public ListVolumesHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _handler = new ListVolumesHandler(_dockerServiceMock.Object);
    }

    private static DockerVolumeRaw MakeVolumeRaw(
        string name, string driver = "local", string? mountpoint = null,
        IDictionary<string, string>? labels = null) =>
        new()
        {
            Name = name,
            Driver = driver,
            Mountpoint = mountpoint ?? $"/mnt/{name}",
            Scope = "local",
            CreatedAt = DateTime.UtcNow,
            Labels = labels ?? new Dictionary<string, string>()
        };

    private static ContainerVolumeMount MakeMount(string container, string volume, string path = "/data") =>
        new() { ContainerName = container, VolumeName = volume, MountPath = path };

    [Fact]
    public async Task Handle_WithVolumes_ReturnsVolumeList()
    {
        _dockerServiceMock
            .Setup(s => s.ListVolumesRawAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DockerVolumeRaw> { MakeVolumeRaw("vol-1") });
        _dockerServiceMock
            .Setup(s => s.GetContainerVolumeMountsAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerVolumeMount> { MakeMount("app-1", "vol-1") });

        var result = await _handler.Handle(new ListVolumesQuery("env-1"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Volumes.Should().HaveCount(1);
        result.Volumes[0].Name.Should().Be("vol-1");
        result.Volumes[0].IsOrphaned.Should().BeFalse();
        result.Volumes[0].ContainerCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OrphanedVolume_MarksAsOrphaned()
    {
        _dockerServiceMock
            .Setup(s => s.ListVolumesRawAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DockerVolumeRaw> { MakeVolumeRaw("orphan-vol") });
        _dockerServiceMock
            .Setup(s => s.GetContainerVolumeMountsAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerVolumeMount>());

        var result = await _handler.Handle(new ListVolumesQuery("env-1"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Volumes.Should().HaveCount(1);
        result.Volumes[0].IsOrphaned.Should().BeTrue();
        result.Volumes[0].ContainerCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NoVolumes_ReturnsEmptyList()
    {
        _dockerServiceMock
            .Setup(s => s.ListVolumesRawAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DockerVolumeRaw>());
        _dockerServiceMock
            .Setup(s => s.GetContainerVolumeMountsAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerVolumeMount>());

        var result = await _handler.Handle(new ListVolumesQuery("env-1"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Volumes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DockerServiceThrows_ReturnsFailure()
    {
        _dockerServiceMock
            .Setup(s => s.ListVolumesRawAsync("env-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Docker not available"));

        var result = await _handler.Handle(new ListVolumesQuery("env-1"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Docker not available");
    }

    [Fact]
    public async Task Handle_VolumeWithLabels_MapsLabelsCorrectly()
    {
        var labels = new Dictionary<string, string> { ["com.docker.compose.project"] = "myapp" };
        _dockerServiceMock
            .Setup(s => s.ListVolumesRawAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DockerVolumeRaw> { MakeVolumeRaw("vol-1", labels: labels) });
        _dockerServiceMock
            .Setup(s => s.GetContainerVolumeMountsAsync("env-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerVolumeMount>());

        var result = await _handler.Handle(new ListVolumesQuery("env-1"), CancellationToken.None);

        result.Volumes[0].Labels.Should().ContainKey("com.docker.compose.project");
    }
}
