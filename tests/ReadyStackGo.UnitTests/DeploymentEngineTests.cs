using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Services.Deployment;
using Xunit;
using DomainEnvironment = ReadyStackGo.Domain.Deployment.Environments.Environment;
using DeploymentOrganizationId = ReadyStackGo.Domain.Deployment.OrganizationId;
using IdentityOrganizationId = ReadyStackGo.Domain.IdentityAccess.Organizations.OrganizationId;

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

    private readonly IdentityOrganizationId _testIdentityOrgId = IdentityOrganizationId.Create();
    private readonly DeploymentOrganizationId _testOrgId;
    private readonly EnvironmentId _testEnvId = EnvironmentId.Create();

    public DeploymentEngineTests()
    {
        _testOrgId = DeploymentOrganizationId.FromIdentityAccess(_testIdentityOrgId);
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
        // Setup organization (using IdentityAccess ID)
        var organization = Organization.Provision(_testIdentityOrgId, "Test Organization", "Test Description");
        organization.Activate();

        _organizationRepositoryMock.Setup(x => x.GetAll())
            .Returns(new List<Organization> { organization });

        // Setup environment (using Deployment context ID)
        var environment = DomainEnvironment.CreateDockerSocket(
            _testEnvId,
            _testOrgId,
            "Test",
            "Test Environment",
            "/var/run/docker.sock");
        environment.SetAsDefault();

        _environmentRepositoryMock.Setup(x => x.GetDefault(_testOrgId))
            .Returns(environment);

        _configStoreMock.Setup(x => x.GetFeaturesConfigAsync()).ReturnsAsync(new FeaturesConfig());
        _configStoreMock.Setup(x => x.GetReleaseConfigAsync()).ReturnsAsync(new ReleaseConfig());
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

    #region Two-Phase Deployment Tests (Pull All, Then Start All)

    [Fact]
    public async Task ExecuteDeploymentAsync_TwoPhaseDeployment_ShouldPullAllImagesBeforeStartingContainers()
    {
        // Arrange
        var pullCallOrder = new List<string>();
        var startCallOrder = new List<string>();

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
                },
                new()
                {
                    ContextName = "web",
                    Image = "myregistry.com/web",
                    Version = "1.0.0",
                    ContainerName = "rsgo-web",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string> { "api" },
                    Order = 2
                }
            }
        };

        // Track pull order
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, image, _, _) => pullCallOrder.Add($"pull:{image}"))
            .Returns(Task.CompletedTask);

        // Track start order
        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, CreateContainerRequest, CancellationToken>((_, req, _) => startCallOrder.Add($"start:{req.Name}"))
            .ReturnsAsync("container-id");

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeTrue();

        // All pulls should happen before any starts
        pullCallOrder.Should().HaveCount(3);
        startCallOrder.Should().HaveCount(3);

        // Verify pull phase completes before start phase
        pullCallOrder.Should().ContainInOrder("pull:postgres", "pull:myregistry.com/api", "pull:myregistry.com/web");
        startCallOrder.Should().ContainInOrder("start:rsgo-db", "start:rsgo-api", "start:rsgo-web");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_TwoPhaseDeployment_WhenFirstImagePullFails_ShouldNotStartAnyContainers()
    {
        // Arrange
        var startCalled = false;

        var plan = new DeploymentPlan
        {
            StackVersion = "1.0.0",
            EnvironmentId = _testEnvId.ToString(),
            Steps = new List<DeploymentStep>
            {
                new()
                {
                    ContextName = "db",
                    Image = "nonexistent/image",
                    Version = "latest",
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
                    Image = "nginx",
                    Version = "latest",
                    ContainerName = "rsgo-api",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string> { "db" },
                    Order = 1
                }
            }
        };

        // First image pull fails
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), "nonexistent/image", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Image not found"));

        // No local image exists
        _dockerServiceMock
            .Setup(x => x.ImageExistsAsync(It.IsAny<string>(), "nonexistent/image", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Track if container creation is ever called
        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => startCalled = true)
            .ReturnsAsync("container-id");

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeFalse("deployment should fail when image pull fails and no local copy exists");
        startCalled.Should().BeFalse("no containers should be started when pull phase fails");
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("nonexistent/image");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_TwoPhaseDeployment_WhenMiddleImagePullFails_ShouldNotStartAnyContainers()
    {
        // Arrange
        var startCalled = false;

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
                    Image = "nonexistent/api",
                    Version = "1.0.0",
                    ContainerName = "rsgo-api",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string> { "db" },
                    Order = 1
                },
                new()
                {
                    ContextName = "web",
                    Image = "nginx",
                    Version = "latest",
                    ContainerName = "rsgo-web",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string> { "api" },
                    Order = 2
                }
            }
        };

        // First image pull succeeds
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), "postgres", "15", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Second image pull fails
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), "nonexistent/api", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Image not found"));

        // No local image exists for the failed image
        _dockerServiceMock
            .Setup(x => x.ImageExistsAsync(It.IsAny<string>(), "nonexistent/api", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Track if container creation is ever called
        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => startCalled = true)
            .ReturnsAsync("container-id");

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan);

        // Assert
        result.Success.Should().BeFalse("deployment should fail when any image pull fails");
        startCalled.Should().BeFalse("no containers should be started - even though postgres was pulled successfully");
        result.Errors.Should().ContainSingle();
        result.Errors[0].Should().Contain("nonexistent/api");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_TwoPhaseDeployment_ShouldReportProgressCorrectly()
    {
        // Arrange
        var progressUpdates = new List<(string Phase, string Message, int Percent)>();

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
                    Image = "nginx",
                    Version = "latest",
                    ContainerName = "rsgo-api",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string> { "db" },
                    Order = 1
                }
            }
        };

        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("container-id");

        DeploymentProgressCallback progressCallback = (phase, message, percent, _, _, _) =>
        {
            progressUpdates.Add((phase, message, percent));
            return Task.CompletedTask;
        };

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan, progressCallback, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify progress phases
        var phases = progressUpdates.Select(p => p.Phase).Distinct().ToList();
        phases.Should().Contain("PullingImages", "progress should include PullingImages phase");
        phases.Should().Contain("StartingServices", "progress should include StartingServices phase");

        // Verify progress percentages go from 0-50 for pulling, 50-100 for starting
        var pullingUpdates = progressUpdates.Where(p => p.Phase == "PullingImages").ToList();
        var startingUpdates = progressUpdates.Where(p => p.Phase == "StartingServices").ToList();

        pullingUpdates.Should().NotBeEmpty();
        startingUpdates.Should().NotBeEmpty();

        // Pulling phase should be in 5-70% range (based on phase weights - pulling is slow)
        pullingUpdates.Should().OnlyContain(p => p.Percent >= 5 && p.Percent <= 70,
            "PullingImages phase maps to overall 10-70%");
        // Starting phase should be in 80-100% range (InitializingContainers is 70-80%, StartingServices is 80-100%)
        startingUpdates.Should().OnlyContain(p => p.Percent >= 80 && p.Percent <= 100,
            "StartingServices phase maps to overall 80-100%");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_TwoPhaseDeployment_WhenCancelledDuringPull_ShouldNotStartContainers()
    {
        // Arrange
        var startCalled = false;
        var cts = new CancellationTokenSource();

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
                    Image = "nginx",
                    Version = "latest",
                    ContainerName = "rsgo-api",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string> { "db" },
                    Order = 1
                }
            }
        };

        // First pull succeeds, then cancel
        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), "postgres", "15", It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel()) // Cancel after first pull
            .Returns(Task.CompletedTask);

        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => startCalled = true)
            .ReturnsAsync("container-id");

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan, null, cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cancelled"));
        startCalled.Should().BeFalse("no containers should be started when cancelled during pull phase");
    }

    #endregion
}
