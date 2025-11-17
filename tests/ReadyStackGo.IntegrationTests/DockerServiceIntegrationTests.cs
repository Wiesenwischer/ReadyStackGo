using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
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

    public async Task InitializeAsync()
    {
        // Starte einen Test-Container (nginx) den wir für die Tests verwenden können
        _testContainer = new ContainerBuilder()
            .WithImage("nginx:alpine")
            .WithName($"readystackgo-test-{Guid.NewGuid():N}")
            .WithPortBinding(8080, 80)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(80))
            .Build();

        await _testContainer.StartAsync();

        _dockerService = new DockerService();
    }

    public async Task DisposeAsync()
    {
        if (_testContainer != null)
        {
            await _testContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task ListContainersAsync_ShouldReturnContainers()
    {
        // Act
        var containers = await _dockerService!.ListContainersAsync();

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
        await _dockerService!.StartContainerAsync(_testContainer.Id);

        // Wait a bit for container to start
        await Task.Delay(2000);

        // Assert
        var containers = await _dockerService.ListContainersAsync();
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
        await _dockerService!.StopContainerAsync(_testContainer.Id);

        // Wait a bit for container to stop
        await Task.Delay(2000);

        // Assert
        var containers = await _dockerService.ListContainersAsync();
        var testContainer = containers.FirstOrDefault(c => c.Id.StartsWith(_testContainer.Id));

        testContainer.Should().NotBeNull();
        testContainer!.State.Should().NotBe("running");
    }

    [Fact]
    public async Task ListContainersAsync_ShouldReturnContainerWithCorrectProperties()
    {
        // Act
        var containers = await _dockerService!.ListContainersAsync();
        var testContainer = containers.FirstOrDefault(c => c.Id.StartsWith(_testContainer!.Id));

        // Assert
        testContainer.Should().NotBeNull();
        testContainer!.Id.Should().NotBeNullOrEmpty();
        testContainer.Name.Should().NotBeNullOrEmpty();
        testContainer.Image.Should().Contain("nginx");
        testContainer.State.Should().NotBeNullOrEmpty();
        testContainer.Status.Should().NotBeNullOrEmpty();
    }
}
