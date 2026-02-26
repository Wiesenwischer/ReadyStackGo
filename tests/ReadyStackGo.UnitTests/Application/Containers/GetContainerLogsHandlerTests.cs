using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers.GetContainerLogs;

namespace ReadyStackGo.UnitTests.Application.Containers;

public class GetContainerLogsHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly GetContainerLogsHandler _handler;

    private const string EnvironmentId = "env-1";
    private const string ContainerId = "container-abc123";

    public GetContainerLogsHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _handler = new GetContainerLogsHandler(_dockerServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WithLogs_ReturnsSuccessAndLogs()
    {
        var expectedLogs = "line1\nline2\nline3";
        _dockerServiceMock
            .Setup(s => s.GetContainerLogsAsync(EnvironmentId, ContainerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedLogs);

        var result = await _handler.Handle(
            new GetContainerLogsQuery(EnvironmentId, ContainerId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Logs.Should().Be(expectedLogs);
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public async Task Handle_PassesTailParameter(int tail)
    {
        _dockerServiceMock
            .Setup(s => s.GetContainerLogsAsync(EnvironmentId, ContainerId, tail, It.IsAny<CancellationToken>()))
            .ReturnsAsync("some logs");

        var result = await _handler.Handle(
            new GetContainerLogsQuery(EnvironmentId, ContainerId, tail),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        _dockerServiceMock.Verify(
            s => s.GetContainerLogsAsync(EnvironmentId, ContainerId, tail, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NullTail_PassesNullToService()
    {
        _dockerServiceMock
            .Setup(s => s.GetContainerLogsAsync(EnvironmentId, ContainerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("logs");

        await _handler.Handle(
            new GetContainerLogsQuery(EnvironmentId, ContainerId, null),
            CancellationToken.None);

        _dockerServiceMock.Verify(
            s => s.GetContainerLogsAsync(EnvironmentId, ContainerId, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyLogs_ReturnsSuccessWithEmptyString()
    {
        _dockerServiceMock
            .Setup(s => s.GetContainerLogsAsync(EnvironmentId, ContainerId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var result = await _handler.Handle(
            new GetContainerLogsQuery(EnvironmentId, ContainerId),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Logs.Should().BeEmpty();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DockerServiceThrows_ReturnsErrorResult()
    {
        _dockerServiceMock
            .Setup(s => s.GetContainerLogsAsync(EnvironmentId, ContainerId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Container not found"));

        var result = await _handler.Handle(
            new GetContainerLogsQuery(EnvironmentId, ContainerId),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Logs.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Container not found");
    }

    [Fact]
    public async Task Handle_DockerServiceThrowsGenericException_ReturnsErrorMessage()
    {
        _dockerServiceMock
            .Setup(s => s.GetContainerLogsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection refused"));

        var result = await _handler.Handle(
            new GetContainerLogsQuery("invalid-env", "invalid-container"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Logs.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Connection refused");
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _dockerServiceMock
            .Setup(s => s.GetContainerLogsAsync(EnvironmentId, ContainerId, null, token))
            .ReturnsAsync("logs");

        await _handler.Handle(new GetContainerLogsQuery(EnvironmentId, ContainerId), token);

        _dockerServiceMock.Verify(
            s => s.GetContainerLogsAsync(EnvironmentId, ContainerId, null, token),
            Times.Once);
    }
}
