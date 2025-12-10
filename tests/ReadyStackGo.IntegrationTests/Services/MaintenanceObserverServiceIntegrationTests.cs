using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.ChangeOperationMode;
using ReadyStackGo.Domain.Catalog.Manifests;
using ReadyStackGo.Domain.Catalog.Stacks;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Services;

namespace ReadyStackGo.IntegrationTests.Services;

/// <summary>
/// Integration tests for MaintenanceObserverService.
/// Tests the coordination of observer checks, caching, and result handling.
/// </summary>
public class MaintenanceObserverServiceIntegrationTests
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly IStackSourceService _stackSourceService;
    private readonly IHealthNotificationService _notificationService;
    private readonly IMaintenanceObserverFactory _observerFactory;
    private readonly ISender _mediator;
    private readonly ILogger<MaintenanceObserverService> _logger;

    public MaintenanceObserverServiceIntegrationTests()
    {
        _deploymentRepository = Substitute.For<IDeploymentRepository>();
        _healthSnapshotRepository = Substitute.For<IHealthSnapshotRepository>();
        _stackSourceService = Substitute.For<IStackSourceService>();
        _notificationService = Substitute.For<IHealthNotificationService>();
        _observerFactory = Substitute.For<IMaintenanceObserverFactory>();
        _mediator = Substitute.For<ISender>();
        _logger = Substitute.For<ILogger<MaintenanceObserverService>>();
    }

    private MaintenanceObserverService CreateService()
    {
        return new MaintenanceObserverService(
            _observerFactory,
            _deploymentRepository,
            _healthSnapshotRepository,
            _stackSourceService,
            _notificationService,
            _mediator,
            _logger);
    }

    private static Deployment CreateRunningDeployment(DeploymentId deploymentId, EnvironmentId environmentId, string stackName)
    {
        var userId = UserId.Create();
        var deployment = Deployment.Start(deploymentId, environmentId, stackName, stackName, userId);
        deployment.MarkAsRunning(new List<DeployedService>());
        return deployment;
    }

    private static Deployment CreatePendingDeployment(DeploymentId deploymentId, EnvironmentId environmentId, string stackName)
    {
        var userId = UserId.Create();
        return Deployment.Start(deploymentId, environmentId, stackName, stackName, userId);
    }

    private static StackDefinition CreateStackDefinition(string stackName, RsgoMaintenanceObserver? maintenanceObserver = null)
    {
        return new StackDefinition(
            sourceId: "test-source",
            name: stackName,
            yamlContent: "version: '3'\nservices:\n  web:\n    image: nginx",
            description: "Test Stack",
            maintenanceObserver: maintenanceObserver);
    }

    #region CheckDeploymentObserverAsync

    [Fact]
    public async Task CheckDeploymentObserverAsync_DeploymentNotFound_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();

        _deploymentRepository.Get(deploymentId).Returns((Deployment?)null);

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckDeploymentObserverAsync_DeploymentNotRunning_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreatePendingDeployment(deploymentId, environmentId, "test-stack");

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckDeploymentObserverAsync_NoObserverConfigured_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateRunningDeployment(deploymentId, environmentId, "test-stack");

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        // No stack definition with maintenanceObserver
        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition>()));

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckDeploymentObserverAsync_WithObserver_ReturnsResult()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateRunningDeployment(deploymentId, environmentId, "test-stack");

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        // Setup stack definition with MaintenanceObserver
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://api.example.com/status",
            PollingInterval = "30s",
            MaintenanceValue = "maintenance",
            NormalValue = "normal"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        // Setup mock observer
        var observer = Substitute.For<IMaintenanceObserver>();
        observer.Type.Returns(ObserverType.Http);
        observer.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ObserverResult.NormalOperation("normal")));

        _observerFactory.Create(Arg.Any<MaintenanceObserverConfig>()).Returns(observer);

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("normal");
        result.IsMaintenanceRequired.Should().BeFalse();
    }

    [Fact]
    public async Task CheckDeploymentObserverAsync_MaintenanceMode_SendsChangeOperationModeCommand()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        // Create deployment with services that have container IDs
        var userId = UserId.Create();
        var deployment = Deployment.Start(deploymentId, environmentId, "test-stack", "test-stack", userId);
        var services = new List<DeployedService>
        {
            new("web", "container-1", "test-stack-web", "nginx:latest", "running"),
            new("api", "container-2", "test-stack-api", "dotnet:8.0", "running")
        };
        deployment.MarkAsRunning(services);

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        // Setup stack definition with MaintenanceObserver
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://api.example.com/status",
            PollingInterval = "30s",
            MaintenanceValue = "maintenance",
            NormalValue = "normal"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        // Setup mock observer to return maintenance mode
        var observer = Substitute.For<IMaintenanceObserver>();
        observer.Type.Returns(ObserverType.Http);
        observer.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ObserverResult.MaintenanceRequired("maintenance")));

        _observerFactory.Create(Arg.Any<MaintenanceObserverConfig>()).Returns(observer);

        // No existing health snapshot (current mode is Normal by default)
        _healthSnapshotRepository.GetLatestForDeployment(deploymentId).Returns((HealthSnapshot?)null);

        // Setup mediator to return success response
        _mediator.Send(Arg.Any<ChangeOperationModeCommand>(), Arg.Any<CancellationToken>())
            .Returns(ChangeOperationModeResponse.Ok(deploymentId.Value.ToString(), "Normal", "Maintenance"));

        // Act
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert - ChangeOperationModeCommand should be sent via MediatR
        // Note: We verify that Send was called - the actual command content is tested in unit tests
        await _mediator.Received(1).Send(
            Arg.Any<ChangeOperationModeCommand>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetLastResultAsync

    [Fact]
    public async Task GetLastResultAsync_NoResultCached_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();

        // Act
        var result = await service.GetLastResultAsync(deploymentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLastResultAsync_AfterCheck_ReturnsCachedResult()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateRunningDeployment(deploymentId, environmentId, "test-stack");

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        // Setup stack definition with MaintenanceObserver
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://api.example.com/status",
            PollingInterval = "30s",
            MaintenanceValue = "maintenance",
            NormalValue = "normal"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var expectedResult = ObserverResult.NormalOperation("normal");
        var observer = Substitute.For<IMaintenanceObserver>();
        observer.Type.Returns(ObserverType.Http);
        observer.CheckAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(expectedResult));

        _observerFactory.Create(Arg.Any<MaintenanceObserverConfig>()).Returns(observer);

        // Perform initial check
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Act
        var cachedResult = await service.GetLastResultAsync(deploymentId);

        // Assert
        cachedResult.Should().NotBeNull();
        cachedResult!.ObservedValue.Should().Be("normal");
    }

    #endregion

    #region CheckAllObserversAsync

    [Fact]
    public async Task CheckAllObserversAsync_ChecksAllActiveDeployments()
    {
        // Arrange
        var service = CreateService();

        var deploymentId1 = DeploymentId.Create();
        var deploymentId2 = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment1 = CreateRunningDeployment(deploymentId1, environmentId, "stack-1");
        var deployment2 = CreateRunningDeployment(deploymentId2, environmentId, "stack-2");

        _deploymentRepository.GetAllActive().Returns(new List<Deployment> { deployment1, deployment2 });
        _deploymentRepository.Get(deploymentId1).Returns(deployment1);
        _deploymentRepository.Get(deploymentId2).Returns(deployment2);

        // No observer configured for these deployments
        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition>()));

        // Act
        await service.CheckAllObserversAsync();

        // Assert
        _deploymentRepository.Received(1).GetAllActive();
        _deploymentRepository.Received(1).Get(deploymentId1);
        _deploymentRepository.Received(1).Get(deploymentId2);
    }

    [Fact]
    public async Task CheckAllObserversAsync_CancellationToken_StopsProcessing()
    {
        // Arrange
        var service = CreateService();
        var cts = new CancellationTokenSource();

        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateRunningDeployment(deploymentId, environmentId, "stack-1");

        _deploymentRepository.GetAllActive().Returns(new List<Deployment> { deployment });

        // Cancel immediately
        cts.Cancel();

        // Act
        await service.CheckAllObserversAsync(cts.Token);

        // Assert - Get should not be called because we cancelled before processing
        _deploymentRepository.DidNotReceive().Get(Arg.Any<DeploymentId>());
    }

    #endregion

    #region Observer Caching

    [Fact]
    public async Task CheckDeploymentObserverAsync_CachesObserverInstance()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateRunningDeployment(deploymentId, environmentId, "test-stack");

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        // Setup stack definition with MaintenanceObserver
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://api.example.com/status",
            PollingInterval = "1s",
            MaintenanceValue = "maintenance",
            NormalValue = "normal"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var observer = Substitute.For<IMaintenanceObserver>();
        observer.Type.Returns(ObserverType.Http);
        observer.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ObserverResult.NormalOperation("normal")));

        _observerFactory.Create(Arg.Any<MaintenanceObserverConfig>()).Returns(observer);

        // Act - call twice with delay to ensure polling interval passes
        await service.CheckDeploymentObserverAsync(deploymentId);
        await Task.Delay(1100); // Wait for polling interval
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert - Factory should only be called once (observer is cached)
        _observerFactory.Received(1).Create(Arg.Any<MaintenanceObserverConfig>());

        // But the observer's CheckAsync should be called twice
        await observer.Received(2).CheckAsync(Arg.Any<CancellationToken>());
    }

    #endregion
}
