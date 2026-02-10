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
using ReadyStackGo.Domain.StackManagement.Manifests;
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

        DeploymentProgressCallback progressCallback = (phase, message, percent, _, _, _, _, _) =>
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

    #region Init Container Counting

    [Fact]
    public async Task ExecuteDeploymentAsync_WithInitContainers_ShouldReportSeparateInitCounts()
    {
        // Arrange
        var progressUpdates = new List<(string Phase, int TotalServices, int CompletedServices, int TotalInitContainers, int CompletedInitContainers)>();

        var plan = new DeploymentPlan
        {
            StackVersion = "1.0.0",
            EnvironmentId = _testEnvId.ToString(),
            StackName = "test-stack",
            Steps = new List<DeploymentStep>
            {
                new()
                {
                    ContextName = "db-init",
                    Image = "flyway",
                    Version = "latest",
                    ContainerName = "rsgo-db-init",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string>(),
                    Lifecycle = ServiceLifecycle.Init,
                    Order = 0
                },
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
                    Order = 1
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
                    Order = 2
                }
            }
        };

        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("container-id");

        // Mock init container completion (exited with code 0)
        _dockerServiceMock
            .Setup(x => x.GetContainerByNameAsync(It.IsAny<string>(), "rsgo-db-init", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerDto { Id = "init-container-id", Name = "rsgo-db-init", Image = "flyway:latest", State = "exited", Status = "exited(0)", Labels = new Dictionary<string, string>() });

        _dockerServiceMock
            .Setup(x => x.GetContainerExitCodeAsync(It.IsAny<string>(), "init-container-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        DeploymentProgressCallback progressCallback = (phase, message, percent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers) =>
        {
            progressUpdates.Add((phase, totalServices, completedServices, totalInitContainers, completedInitContainers));
            return Task.CompletedTask;
        };

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan, progressCallback, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Verify init container phase reports correct counts
        var initPhaseUpdates = progressUpdates.Where(p => p.Phase == "InitializingContainers").ToList();
        initPhaseUpdates.Should().NotBeEmpty("init container phase should be reported");
        initPhaseUpdates.Should().OnlyContain(p => p.TotalInitContainers == 1,
            "there is 1 init container");
        initPhaseUpdates.Should().OnlyContain(p => p.TotalServices == 2,
            "there are 2 regular services");

        // Verify service phase reports correct counts
        var servicePhaseUpdates = progressUpdates.Where(p => p.Phase == "StartingServices").ToList();
        servicePhaseUpdates.Should().NotBeEmpty("service phase should be reported");
        servicePhaseUpdates.Should().OnlyContain(p => p.TotalServices == 2,
            "there are 2 regular services");
        servicePhaseUpdates.Should().OnlyContain(p => p.TotalInitContainers == 1,
            "init container count should be passed through");

        // Verify completed init containers increments
        var lastInitUpdate = initPhaseUpdates.Last();
        lastInitUpdate.CompletedInitContainers.Should().Be(1, "the init container should be completed");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WithoutInitContainers_ShouldReportZeroInitCounts()
    {
        // Arrange
        var progressUpdates = new List<(string Phase, int TotalInitContainers, int CompletedInitContainers)>();

        var plan = new DeploymentPlan
        {
            StackVersion = "1.0.0",
            EnvironmentId = _testEnvId.ToString(),
            StackName = "test-stack",
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
                    DependsOn = new List<string>(),
                    Order = 0
                }
            }
        };

        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("container-id");

        DeploymentProgressCallback progressCallback = (phase, message, percent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers) =>
        {
            progressUpdates.Add((phase, totalInitContainers, completedInitContainers));
            return Task.CompletedTask;
        };

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan, progressCallback, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // All updates should have zero init container counts
        progressUpdates.Should().OnlyContain(p => p.TotalInitContainers == 0,
            "there are no init containers");
        progressUpdates.Should().OnlyContain(p => p.CompletedInitContainers == 0,
            "no init containers to complete");

        // No InitializingContainers phase should be reported
        progressUpdates.Should().NotContain(p => p.Phase == "InitializingContainers",
            "no init containers means no init phase");
    }

    [Fact]
    public async Task ExecuteDeploymentAsync_WithMultipleInitContainers_ShouldIncrementCompletedCount()
    {
        // Arrange
        var initPhaseUpdates = new List<(int CompletedInitContainers, int TotalInitContainers)>();

        var plan = new DeploymentPlan
        {
            StackVersion = "1.0.0",
            EnvironmentId = _testEnvId.ToString(),
            StackName = "test-stack",
            Steps = new List<DeploymentStep>
            {
                new()
                {
                    ContextName = "migration-1",
                    Image = "flyway",
                    Version = "latest",
                    ContainerName = "rsgo-migration-1",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string>(),
                    Lifecycle = ServiceLifecycle.Init,
                    Order = 0
                },
                new()
                {
                    ContextName = "migration-2",
                    Image = "liquibase",
                    Version = "latest",
                    ContainerName = "rsgo-migration-2",
                    EnvVars = new Dictionary<string, string>(),
                    Ports = new List<string>(),
                    Volumes = new Dictionary<string, string>(),
                    DependsOn = new List<string>(),
                    Lifecycle = ServiceLifecycle.Init,
                    Order = 1
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
                    DependsOn = new List<string>(),
                    Order = 2
                }
            }
        };

        _dockerServiceMock
            .Setup(x => x.PullImageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dockerServiceMock
            .Setup(x => x.CreateAndStartContainerAsync(It.IsAny<string>(), It.IsAny<CreateContainerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("container-id");

        // Mock both init containers completing successfully
        _dockerServiceMock
            .Setup(x => x.GetContainerByNameAsync(It.IsAny<string>(), "rsgo-migration-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerDto { Id = "init-1-id", Name = "rsgo-migration-1", Image = "flyway:latest", State = "exited", Status = "exited(0)", Labels = new Dictionary<string, string>() });

        _dockerServiceMock
            .Setup(x => x.GetContainerByNameAsync(It.IsAny<string>(), "rsgo-migration-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerDto { Id = "init-2-id", Name = "rsgo-migration-2", Image = "liquibase:latest", State = "exited", Status = "exited(0)", Labels = new Dictionary<string, string>() });

        _dockerServiceMock
            .Setup(x => x.GetContainerExitCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        DeploymentProgressCallback progressCallback = (phase, message, percent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers) =>
        {
            if (phase == "InitializingContainers")
            {
                initPhaseUpdates.Add((completedInitContainers, totalInitContainers));
            }
            return Task.CompletedTask;
        };

        // Act
        var result = await _sut.ExecuteDeploymentAsync(plan, progressCallback, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // Should have progress updates showing 0/2, then 1/2, then 2/2
        initPhaseUpdates.Should().Contain(u => u.TotalInitContainers == 2,
            "total should always be 2");
        initPhaseUpdates.Should().Contain(u => u.CompletedInitContainers == 0,
            "should report 0 completed at start");
        initPhaseUpdates.Should().Contain(u => u.CompletedInitContainers == 1,
            "should report 1 completed after first init");
        initPhaseUpdates.Should().Contain(u => u.CompletedInitContainers == 2,
            "should report 2 completed after both inits");
    }

    [Fact]
    public async Task RemoveStackAsync_WithProgress_ShouldReportZeroInitCounts()
    {
        // Arrange
        var progressUpdates = new List<(string Phase, int TotalInitContainers, int CompletedInitContainers)>();

        _dockerServiceMock
            .Setup(x => x.ListContainersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerDto>
            {
                new()
                {
                    Id = "container-1",
                    Name = "rsgo-api",
                    Image = "nginx:latest",
                    State = "running",
                    Status = "running",
                    Labels = new Dictionary<string, string> { ["rsgo.stack"] = "test-stack", ["rsgo.context"] = "api" }
                }
            });

        _dockerServiceMock
            .Setup(x => x.RemoveContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _configStoreMock
            .Setup(x => x.GetReleaseConfigAsync())
            .ReturnsAsync(new ReleaseConfig { InstalledStackVersion = "test-stack" });

        _configStoreMock
            .Setup(x => x.SaveReleaseConfigAsync(It.IsAny<ReleaseConfig>()))
            .Returns(Task.CompletedTask);

        DeploymentProgressCallback progressCallback = (phase, message, percent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers) =>
        {
            progressUpdates.Add((phase, totalInitContainers, completedInitContainers));
            return Task.CompletedTask;
        };

        // Act
        var result = await _sut.RemoveStackAsync(_testEnvId.ToString(), "test-stack", progressCallback);

        // Assert
        result.Success.Should().BeTrue();

        // Removal never has init containers
        progressUpdates.Should().OnlyContain(p => p.TotalInitContainers == 0 && p.CompletedInitContainers == 0,
            "removal operations should always report zero init container counts");
    }

    #endregion
}
