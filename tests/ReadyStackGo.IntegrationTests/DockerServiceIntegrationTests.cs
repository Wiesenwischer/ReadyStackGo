using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Identity.ValueObjects;
using ReadyStackGo.Domain.StackManagement.Repositories;
using ReadyStackGo.Domain.StackManagement.ValueObjects;
using ReadyStackGo.Infrastructure.Docker;
using Xunit;
using DomainEnvironment = ReadyStackGo.Domain.StackManagement.Aggregates.Environment;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for DockerService using Testcontainers
/// Diese Tests verwenden echte Docker-Container um die Docker-Integration zu testen
/// </summary>
public class DockerServiceIntegrationTests : IAsyncLifetime
{
    private IContainer? _testContainer;
    private DockerService? _dockerService;
    private static readonly EnvironmentId TestEnvironmentId = EnvironmentId.Create();
    private static readonly OrganizationId TestOrganizationId = OrganizationId.Create();

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

        // Create mock environment repository
        var environmentRepository = Substitute.For<IEnvironmentRepository>();
        var logger = Substitute.For<ILogger<DockerService>>();

        // Setup environment with test Docker socket
        var socketPath = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        var testEnv = DomainEnvironment.CreateDockerSocket(
            TestEnvironmentId,
            TestOrganizationId,
            "Test Environment",
            "Test environment for integration tests",
            socketPath);
        testEnv.SetAsDefault();

        environmentRepository.Get(TestEnvironmentId).Returns(testEnv);

        // Create empty configuration for tests
        var configuration = new ConfigurationBuilder().Build();

        _dockerService = new DockerService(environmentRepository, configuration, logger);
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
        var containers = await _dockerService!.ListContainersAsync(TestEnvironmentId.ToString());

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
        await _dockerService!.StartContainerAsync(TestEnvironmentId.ToString(), _testContainer.Id);

        // Wait a bit for container to start
        await Task.Delay(2000);

        // Assert
        var containers = await _dockerService.ListContainersAsync(TestEnvironmentId.ToString());
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
        await _dockerService!.StopContainerAsync(TestEnvironmentId.ToString(), _testContainer.Id);

        // Wait a bit for container to stop
        await Task.Delay(2000);

        // Assert
        var containers = await _dockerService.ListContainersAsync(TestEnvironmentId.ToString());
        var testContainer = containers.FirstOrDefault(c => c.Id.StartsWith(_testContainer.Id));

        testContainer.Should().NotBeNull();
        testContainer!.State.Should().NotBe("running");
    }

    [Fact]
    public async Task ListContainersAsync_ShouldReturnContainerWithCorrectProperties()
    {
        // Act
        var containers = await _dockerService!.ListContainersAsync(TestEnvironmentId.ToString());
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
