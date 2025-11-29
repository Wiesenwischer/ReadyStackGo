using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Domain.Configuration;
using ReadyStackGo.Domain.Organizations;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Docker;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for DockerService using Testcontainers
/// Diese Tests verwenden echte Docker-Container um die Docker-Integration zu testen
/// </summary>
public class DockerServiceIntegrationTests : IAsyncLifetime
{
    private IContainer? _testContainer;
    private DockerService? _dockerService;
    private const string TestEnvironmentId = "test-env";

    public async Task InitializeAsync()
    {
        // Starte einen Test-Container (nginx) den wir für die Tests verwenden können
        // Verwende WithPortBinding(80, true) um einen zufälligen freien Port zu bekommen
        _testContainer = new ContainerBuilder()
            .WithImage("nginx:alpine")
            .WithName($"readystackgo-test-{Guid.NewGuid():N}")
            .WithPortBinding(80, true) // true = assign random free host port
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(80))
            .Build();

        await _testContainer.StartAsync();

        // Create mock config store that returns a test environment
        var configStore = Substitute.For<IConfigStore>();
        var logger = Substitute.For<ILogger<DockerService>>();

        // Setup system config with test environment
        var socketPath = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        var testEnv = new DockerSocketEnvironment
        {
            Id = TestEnvironmentId,
            Name = "Test Environment",
            SocketPath = socketPath,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        var org = new Organization
        {
            Id = "test-org",
            Name = "Test Organization",
            CreatedAt = DateTime.UtcNow
        };
        org.AddEnvironment(testEnv);

        var systemConfig = new SystemConfig
        {
            Organization = org
        };

        configStore.GetSystemConfigAsync().Returns(systemConfig);

        // Create empty configuration for tests
        var configuration = new ConfigurationBuilder().Build();

        _dockerService = new DockerService(configStore, configuration, logger);
    }

    public async Task DisposeAsync()
    {
        if (_testContainer != null)
        {
            await _testContainer.DisposeAsync();
        }

        _dockerService?.Dispose();
    }

    [Fact]
    public async Task ListContainersAsync_ShouldReturnContainers()
    {
        // Act
        var containers = await _dockerService!.ListContainersAsync(TestEnvironmentId);

        // Assert
        containers.Should().NotBeNull();
        containers.Should().Contain(c => c.Id.StartsWith(_testContainer!.Id));
    }

    [Fact]
    public async Task StartContainerAsync_ShouldStartStoppedContainer()
    {
        // Arrange - Container erst stoppen
        await _testContainer!.StopAsync();

        // Act
        await _dockerService!.StartContainerAsync(TestEnvironmentId, _testContainer.Id);

        // Wait a bit for container to start
        await Task.Delay(2000);

        // Assert
        var containers = await _dockerService.ListContainersAsync(TestEnvironmentId);
        var testContainer = containers.FirstOrDefault(c => c.Id.StartsWith(_testContainer.Id));

        testContainer.Should().NotBeNull();
        testContainer!.State.Should().Be("running");
    }

    [Fact]
    public async Task StopContainerAsync_ShouldStopRunningContainer()
    {
        // Arrange - Sicherstellen dass Container läuft
        if (_testContainer!.State != TestcontainersStates.Running)
        {
            await _testContainer.StartAsync();
        }

        // Act
        await _dockerService!.StopContainerAsync(TestEnvironmentId, _testContainer.Id);

        // Wait a bit for container to stop
        await Task.Delay(2000);

        // Assert
        var containers = await _dockerService.ListContainersAsync(TestEnvironmentId);
        var testContainer = containers.FirstOrDefault(c => c.Id.StartsWith(_testContainer.Id));

        testContainer.Should().NotBeNull();
        testContainer!.State.Should().NotBe("running");
    }

    [Fact]
    public async Task ListContainersAsync_ShouldReturnContainerWithCorrectProperties()
    {
        // Act
        var containers = await _dockerService!.ListContainersAsync(TestEnvironmentId);
        var testContainer = containers.FirstOrDefault(c => c.Id.StartsWith(_testContainer!.Id));

        // Assert
        testContainer.Should().NotBeNull();
        testContainer!.Id.Should().NotBeNullOrEmpty();
        testContainer.Name.Should().NotBeNullOrEmpty();
        testContainer.Image.Should().Contain("nginx");
        testContainer.State.Should().NotBeNullOrEmpty();
        testContainer.Status.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldSucceedWithValidDockerHost()
    {
        // Arrange
        var dockerHost = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        // Act
        var result = await _dockerService!.TestConnectionAsync(dockerHost);

        // Assert
        result.Success.Should().BeTrue();
        result.DockerVersion.Should().NotBeNullOrWhiteSpace();
        result.Message.Should().Contain("Connected to Docker");
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldFailWithInvalidDockerHost()
    {
        // Arrange
        var dockerHost = "tcp://invalid-host:9999";

        // Act
        var result = await _dockerService!.TestConnectionAsync(dockerHost);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Connection failed");
    }

    [Fact]
    public async Task ListContainersAsync_ShouldThrowForInvalidEnvironment()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _dockerService!.ListContainersAsync("non-existent-env"));
    }
}
