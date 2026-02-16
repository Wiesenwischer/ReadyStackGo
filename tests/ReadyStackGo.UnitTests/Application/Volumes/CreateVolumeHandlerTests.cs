using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Volumes.CreateVolume;

namespace ReadyStackGo.UnitTests.Application.Volumes;

public class CreateVolumeHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly CreateVolumeHandler _handler;

    public CreateVolumeHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _handler = new CreateVolumeHandler(_dockerServiceMock.Object);
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

    [Fact]
    public async Task Handle_ValidRequest_CreatesVolumeAndReturnsDto()
    {
        _dockerServiceMock
            .Setup(s => s.CreateVolumeAsync("env-1", "new-vol", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeVolumeRaw("new-vol"));

        var result = await _handler.Handle(
            new CreateVolumeCommand("env-1", "new-vol"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Volume.Should().NotBeNull();
        result.Volume!.Name.Should().Be("new-vol");
        result.Volume.Driver.Should().Be("local");
    }

    [Fact]
    public async Task Handle_WithDriver_PassesDriverToService()
    {
        _dockerServiceMock
            .Setup(s => s.CreateVolumeAsync("env-1", "vol", "nfs", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeVolumeRaw("vol", "nfs"));

        var result = await _handler.Handle(
            new CreateVolumeCommand("env-1", "vol", "nfs"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Volume!.Driver.Should().Be("nfs");
    }

    [Fact]
    public async Task Handle_WithLabels_PassesLabelsToService()
    {
        var labels = new Dictionary<string, string> { ["env"] = "test" };
        _dockerServiceMock
            .Setup(s => s.CreateVolumeAsync("env-1", "vol", null,
                It.Is<IDictionary<string, string>>(l => l.ContainsKey("env")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeVolumeRaw("vol", labels: labels));

        var result = await _handler.Handle(
            new CreateVolumeCommand("env-1", "vol", Labels: labels), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Volume!.Labels.Should().ContainKey("env");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyName_ReturnsFailure(string? name)
    {
        var result = await _handler.Handle(
            new CreateVolumeCommand("env-1", name!), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Volume name cannot be empty");
    }

    [Fact]
    public async Task Handle_DockerServiceThrows_ReturnsFailure()
    {
        _dockerServiceMock
            .Setup(s => s.CreateVolumeAsync("env-1", "vol", null, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Volume already exists"));

        var result = await _handler.Handle(
            new CreateVolumeCommand("env-1", "vol"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Volume already exists");
    }
}
