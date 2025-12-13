using Docker.DotNet;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Infrastructure.Docker;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;
using DomainEnvironment = ReadyStackGo.Domain.Deployment.Environments.Environment;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for DockerService using Testcontainers
/// Diese Tests verwenden echte Docker-Container um die Docker-Integration zu testen
/// Requires Docker to be running and accessible via Testcontainers.
/// </summary>
[Trait("Category", "Docker")]
[Collection("Docker")]
public class DockerServiceIntegrationTests : IAsyncLifetime, IClassFixture<DockerTestFixture>
{
    private readonly DockerTestFixture _fixture;
    private IContainer? _testContainer;
    private DockerService? _dockerService;
    private static readonly EnvironmentId TestEnvironmentId = EnvironmentId.Create();
    private static readonly OrganizationId TestOrganizationId = OrganizationId.Create();

    public DockerServiceIntegrationTests(DockerTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.IsDockerAvailable)
        {
            return; // Skip initialization when Docker is not available
        }

        // Configure Docker endpoint for Windows Docker Desktop if needed
        var dockerEndpoint = GetDockerEndpoint();

        // Starte einen Test-Container (nginx) den wir für die Tests verwenden können
        // Verwende WithPortBinding(80, true) um einen zufälligen freien Port zu bekommen
        var containerBuilder = new ContainerBuilder()
            .WithImage("nginx:alpine")
            .WithName($"readystackgo-test-{Guid.NewGuid():N}")
            .WithPortBinding(80, true) // true = assign random free host port
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80)));

        // Configure Docker endpoint if we detected a custom one
        if (!string.IsNullOrEmpty(dockerEndpoint))
        {
            containerBuilder = containerBuilder.WithDockerEndpoint(dockerEndpoint);
        }

        _testContainer = containerBuilder.Build();

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

    [SkippableFact]
    public async Task ListContainersAsync_ShouldReturnContainers()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

        // Act
        var containers = await _dockerService!.ListContainersAsync(TestEnvironmentId.ToString());

        // Assert
        containers.Should().NotBeNull();
        containers.Should().Contain(c => c.Id.StartsWith(_testContainer!.Id));
    }

    [SkippableFact]
    public async Task StartContainerAsync_ShouldStartStoppedContainer()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

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

    [SkippableFact]
    public async Task StopContainerAsync_ShouldStopRunningContainer()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

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

    [SkippableFact]
    public async Task ListContainersAsync_ShouldReturnContainerWithCorrectProperties()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

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

    [SkippableFact]
    public async Task TestConnectionAsync_ShouldSucceedWithValidDockerHost()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

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

    [SkippableFact]
    public async Task TestConnectionAsync_ShouldFailWithInvalidDockerHost()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

        // Arrange
        var dockerHost = "tcp://invalid-host:9999";

        // Act
        var result = await _dockerService!.TestConnectionAsync(dockerHost);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Connection failed");
    }

    [SkippableFact]
    public async Task ListContainersAsync_ShouldThrowForInvalidEnvironment()
    {
        Skip.IfNot(_fixture.IsDockerAvailable, "Docker is not available");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _dockerService!.ListContainersAsync("non-existent-env"));
    }

    /// <summary>
    /// Detects the Docker endpoint for the current platform.
    /// On Windows with Docker Desktop, uses the named pipe endpoint.
    /// On Linux/Mac, returns null to use the default socket.
    /// </summary>
    private static string? GetDockerEndpoint()
    {
        // Check environment variable first
        var envEndpoint = System.Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrEmpty(envEndpoint))
            return envEndpoint;

        // On Windows, try Docker Desktop named pipes
        if (OperatingSystem.IsWindows())
        {
            // Docker Desktop Linux containers (most common)
            if (File.Exists(@"\\.\pipe\dockerDesktopLinuxEngine"))
                return "npipe://./pipe/dockerDesktopLinuxEngine";

            // Docker Desktop Windows containers
            if (File.Exists(@"\\.\pipe\docker_engine"))
                return "npipe://./pipe/docker_engine";
        }

        // On Linux/Mac, use default (null means use default socket)
        return null;
    }
}
