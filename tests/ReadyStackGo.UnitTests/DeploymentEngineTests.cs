using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Configuration;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.Repositories;
using ReadyStackGo.Domain.Identity.ValueObjects;
using ReadyStackGo.Domain.StackManagement.Repositories;
using ReadyStackGo.Domain.StackManagement.ValueObjects;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Deployment;
using Xunit;
using DomainEnvironment = ReadyStackGo.Domain.StackManagement.Aggregates.Environment;

namespace ReadyStackGo.UnitTests;

/// <summary>
/// Unit tests for DeploymentEngine
/// Tests image pull error handling and local image fallback behavior
/// </summary>
public class DeploymentEngineTests
{
    private readonly Mock<IConfigStore> _configStoreMock;
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IOrganizationRepository> _organizationRepositoryMock;
    private readonly Mock<IEnvironmentRepository> _environmentRepositoryMock;
    private readonly Mock<ILogger<DeploymentEngine>> _loggerMock;
    private readonly DeploymentEngine _sut;

    private readonly OrganizationId _testOrgId = OrganizationId.Create();
    private readonly EnvironmentId _testEnvId = EnvironmentId.Create();

    public DeploymentEngineTests()
    {
        _configStoreMock = new Mock<IConfigStore>();
        _dockerServiceMock = new Mock<IDockerService>();
        _organizationRepositoryMock = new Mock<IOrganizationRepository>();
        _environmentRepositoryMock = new Mock<IEnvironmentRepository>();
        _loggerMock = new Mock<ILogger<DeploymentEngine>>();

        _sut = new DeploymentEngine(
            _configStoreMock.Object,
            _dockerServiceMock.Object,
            _organizationRepositoryMock.Object,
            _environmentRepositoryMock.Object,
            _loggerMock.Object);

        // Setup default config responses
        SetupDefaultConfigs();
    }

    private void SetupDefaultConfigs()
    {
        // Setup organization
        var organization = Organization.Provision(_testOrgId, "Test Organization", "Test Description");
        organization.Activate();

        _organizationRepositoryMock.Setup(x => x.GetAll())
            .Returns(new List<Organization> { organization });

        // Setup environment
        var environment = DomainEnvironment.CreateDockerSocket(
            _testEnvId,
            _testOrgId,
            "Test",
            "Test Environment",
            "/var/run/docker.sock");
        environment.SetAsDefault();

        _environmentRepositoryMock.Setup(x => x.GetDefault(_testOrgId))
            .Returns(environment);

#pragma warning disable CS0618 // ContextsConfig is obsolete
        _configStoreMock.Setup(x => x.GetContextsConfigAsync()).ReturnsAsync(new ContextsConfig());
        _configStoreMock.Setup(x => x.GetFeaturesConfigAsync()).ReturnsAsync(new FeaturesConfig());
        _configStoreMock.Setup(x => x.GetReleaseConfigAsync()).ReturnsAsync(new ReleaseConfig());
#pragma warning restore CS0618
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WhenImagePullFails_AndLocalImageExists_ShouldSucceedWithWarning()
    {
        // Arrange
        var plan = new DeploymentPlan
        {
            StackVersion = "1.0.0",
            EnvironmentId = _testEnvId.ToString(),
            Steps = new List<DeploymentStep>
            {
                new()
                {
                    ContextName = "api",
                    Image = "myregistry.com/api",
                    Version = "1.0.0",
                    ContainerName = "rsgo-api",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string>()
                }
            }
        };

        // Mock image pull failure
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection refused - registry unreachable"));

        // Mock local image exists
        _dockerServiceMock
            .Setup(x => x.ImageExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Mock container creation
        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("container-id-123");

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeTrue("deployment should succeed when local image is available");
        result.Warnings.Should().ContainSingle("there should be one warning about using local image");
        result.Warnings[0].Should().Contain("could not be pulled");
        result.Warnings[0].Should().Contain("using existing local image");
        result.DeployedContexts.Should().Contain("api");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WhenImagePullFails_AndNoLocalImage_ShouldFail()
    {
        // Arrange
        var plan = new DeploymentPlan
        {
            StackVersion = "1.0.0",
            EnvironmentId = _testEnvId.ToString(),
            Steps = new List<DeploymentStep>
            {
                new()
                {
                    ContextName = "api",
                    Image = "myregistry.com/api",
                    Version = "1.0.0",
                    ContainerName = "rsgo-api",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string>()
                }
            }
        };

        // Mock image pull failure
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection refused - registry unreachable"));

        // Mock no local image
        _dockerServiceMock
            .Setup(x => x.ImageExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeFalse("deployment should fail when image cannot be pulled and no local copy exists");
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("Failed to pull image");
        result.Errors[0].Should().Contain("no local copy exists");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WhenImagePullSucceeds_ShouldSucceedWithoutWarning()
    {
        // Arrange
        var plan = new DeploymentPlan
        {
            StackVersion = "1.0.0",
            EnvironmentId = _testEnvId.ToString(),
            Steps = new List<DeploymentStep>
            {
                new()
                {
                    ContextName = "api",
                    Image = "myregistry.com/api",
                    Version = "1.0.0",
                    ContainerName = "rsgo-api",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string>()
                }
            }
        };

        // Mock successful image pull
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock container creation
        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("container-id-123");

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeTrue();
        result.Warnings.Should().BeEmpty("no warnings when image pull succeeds");
        result.DeployedContexts.Should().Contain("api");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WithMultipleServices_WhenOneImagePullFails_ShouldIncludeWarningForThatService()
    {
        // Arrange
        var plan = new DeploymentPlan
        {
            StackVersion = "1.0.0",
            EnvironmentId = _testEnvId.ToString(),
            Steps = new List<DeploymentStep>
            {
                new()
                {
                    ContextName = "db",
                    Image = "postgres",
                    Version = "15",
                    ContainerName = "rsgo-db",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string>(),
                    Order = 0
                },
                new()
                {
                    ContextName = "api",
                    Image = "myregistry.com/api",
                    Version = "1.0.0",
                    ContainerName = "rsgo-api",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string> { "db" },
                    Order = 1
                }
            }
        };

        // Mock successful pull for postgres
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), "postgres", "15", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock failed pull for api
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), "myregistry.com/api", "1.0.0", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection refused"));

        // Mock local image exists for api
        _dockerServiceMock
            .Setup(x => x.ImageExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Mock container creation for both
        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("container-id-123");

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeTrue();
        result.Warnings.Should().ContainSingle("only one service should have a warning");
        result.Warnings[0].Should().Contain("myregistry.com/api:1.0.0");
        result.DeployedContexts.Should().HaveCount(2);
        result.DeployedContexts.Should().Contain("db");
        result.DeployedContexts.Should().Contain("api");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WithNoEnvironmentSpecified_ShouldUseDefaultEnvironment()
    {
        // Arrange
        var plan = new DeploymentPlan
        {
            StackVersion = "1.0.0",
            EnvironmentId = null, // No environment specified
            Steps = new List<DeploymentStep>
            {
                new()
                {
                    ContextName = "api",
                    Image = "nginx",
                    Version = "latest",
                    ContainerName = "rsgo-api",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string>()
                }
            }
        };

        // Mock successful image pull
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock container creation
        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("container-id-123");

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeTrue("deployment should succeed using default environment");

        // Verify that GetDefault was called on the environment repository
        _environmentRepositoryMock.Verify(x => x.GetDefault(_testOrgId), Times.Once);
    }
}
