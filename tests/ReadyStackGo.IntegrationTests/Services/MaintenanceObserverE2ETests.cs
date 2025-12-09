using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.StackSources;
using ReadyStackGo.Infrastructure.Services;

namespace ReadyStackGo.IntegrationTests.Services;

/// <summary>
/// End-to-end style tests for the Maintenance Observer feature.
/// Tests the complete workflow from observer configuration to notification.
/// </summary>
public class MaintenanceObserverE2ETests
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly IStackSourceService _stackSourceService;
    private readonly IRsgoManifestParser _manifestParser;
    private readonly IHealthNotificationService _notificationService;
    private readonly IMaintenanceObserverFactory _observerFactory;
    private readonly ILogger<MaintenanceObserverService> _logger;

    public MaintenanceObserverE2ETests()
    {
        _deploymentRepository = Substitute.For<IDeploymentRepository>();
        _healthSnapshotRepository = Substitute.For<IHealthSnapshotRepository>();
        _stackSourceService = Substitute.For<IStackSourceService>();
        _manifestParser = Substitute.For<IRsgoManifestParser>();
        _notificationService = Substitute.For<IHealthNotificationService>();
        _observerFactory = Substitute.For<IMaintenanceObserverFactory>();
        _logger = Substitute.For<ILogger<MaintenanceObserverService>>();
    }

    private static Deployment CreateRunningDeployment(DeploymentId deploymentId, EnvironmentId environmentId, string stackName)
    {
        var userId = UserId.Create();
        var deployment = Deployment.Start(deploymentId, environmentId, stackName, stackName, userId);
        deployment.MarkAsRunning(new List<DeployedService>());
        return deployment;
    }

    #region Full Workflow Tests

    [Fact]
    public async Task FullWorkflow_ObserverDetectsMaintenance_NotifiesClients()
    {
        // This E2E test simulates:
        // 1. A deployment with HTTP observer configured
        // 2. Observer checks the endpoint
        // 3. Endpoint returns "maintenance" value
        // 4. SignalR notification is sent to clients

        // Arrange
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateRunningDeployment(deploymentId, environmentId, "prod-api");

        _deploymentRepository.Get(deploymentId).Returns(deployment);
        _deploymentRepository.GetAllActive().Returns(new List<Deployment> { deployment });

        // Setup stack with HTTP observer
        var stackDefinition = new StackDefinition(
            sourceId: "rsgo-stacks",
            name: "prod-api",
            yamlContent: "version: '3'\nservices:\n  api:\n    image: myapp:latest",
            description: "Production API");

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var manifest = new RsgoManifest
        {
            MaintenanceObserver = new RsgoMaintenanceObserver
            {
                Type = "http",
                Url = "https://status.mycompany.com/api/maintenance",
                PollingInterval = "30s",
                MaintenanceValue = "maintenance",
                NormalValue = "normal",
                JsonPath = "status"
            }
        };

        _manifestParser.ParseAsync(Arg.Any<string>()).Returns(Task.FromResult(manifest));

        // Setup observer to return maintenance mode
        var observer = Substitute.For<IMaintenanceObserver>();
        observer.Type.Returns(ObserverType.Http);
        observer.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ObserverResult.MaintenanceRequired("maintenance")));

        _observerFactory.Create(Arg.Any<MaintenanceObserverConfig>()).Returns(observer);

        var notificationsSent = new List<ObserverResultDto>();
        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Any<string>(),
            Arg.Do<ObserverResultDto>(dto => notificationsSent.Add(dto)),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = new MaintenanceObserverService(
            _observerFactory,
            _deploymentRepository,
            _healthSnapshotRepository,
            _stackSourceService,
            _manifestParser,
            _notificationService,
            _logger);

        // Act - run the check cycle (as background service would)
        await service.CheckAllObserversAsync();

        // Assert
        notificationsSent.Should().HaveCount(1);
        notificationsSent[0].IsMaintenanceRequired.Should().BeTrue();
        notificationsSent[0].ObservedValue.Should().Be("maintenance");
    }

    [Fact]
    public async Task FullWorkflow_ObserverClearsMaintenanceMode_NotifiesClients()
    {
        // This E2E test simulates:
        // 1. Observer checks endpoint
        // 2. First check returns "maintenance", second returns "normal"
        // 3. Clients are notified of both states

        // Arrange
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateRunningDeployment(deploymentId, environmentId, "prod-api");

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        var stackDefinition = new StackDefinition(
            sourceId: "rsgo-stacks",
            name: "prod-api",
            yamlContent: "version: '3'\nservices:\n  api:\n    image: myapp:latest");

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var manifest = new RsgoManifest
        {
            MaintenanceObserver = new RsgoMaintenanceObserver
            {
                Type = "http",
                Url = "https://status.mycompany.com/api/maintenance",
                PollingInterval = "1s",
                MaintenanceValue = "maintenance",
                NormalValue = "normal"
            }
        };

        _manifestParser.ParseAsync(Arg.Any<string>()).Returns(Task.FromResult(manifest));

        // Setup observer - first returns maintenance, then normal
        var observer = Substitute.For<IMaintenanceObserver>();
        observer.Type.Returns(ObserverType.Http);

        var callCount = 0;
        observer.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                return Task.FromResult(callCount == 1
                    ? ObserverResult.MaintenanceRequired("maintenance")
                    : ObserverResult.NormalOperation("normal"));
            });

        _observerFactory.Create(Arg.Any<MaintenanceObserverConfig>()).Returns(observer);

        // No previous health snapshot needed
        _healthSnapshotRepository.GetLatestForDeployment(deploymentId).Returns((HealthSnapshot?)null);

        var notificationsSent = new List<ObserverResultDto>();
        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Any<string>(),
            Arg.Do<ObserverResultDto>(dto => notificationsSent.Add(dto)),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = new MaintenanceObserverService(
            _observerFactory,
            _deploymentRepository,
            _healthSnapshotRepository,
            _stackSourceService,
            _manifestParser,
            _notificationService,
            _logger);

        // Act - first check (maintenance)
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Wait for polling interval
        await Task.Delay(1100);

        // Second check (normal)
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        notificationsSent.Should().HaveCount(2);
        notificationsSent[0].IsMaintenanceRequired.Should().BeTrue();
        notificationsSent[1].IsMaintenanceRequired.Should().BeFalse();
        notificationsSent[1].ObservedValue.Should().Be("normal");
    }

    [Fact]
    public async Task FullWorkflow_MultipleDeployments_CheckedInSingleCycle()
    {
        // This E2E test simulates:
        // 1. Multiple deployments with different observer configurations
        // 2. Background service runs a single check cycle
        // 3. All observers are checked

        // Arrange
        var environmentId = EnvironmentId.Create();

        var deployment1Id = DeploymentId.Create();
        var deployment1 = CreateRunningDeployment(deployment1Id, environmentId, "api-service");

        var deployment2Id = DeploymentId.Create();
        var deployment2 = CreateRunningDeployment(deployment2Id, environmentId, "web-frontend");

        var deployment3Id = DeploymentId.Create();
        var deployment3 = CreateRunningDeployment(deployment3Id, environmentId, "background-worker");

        _deploymentRepository.GetAllActive().Returns(new List<Deployment> { deployment1, deployment2, deployment3 });
        _deploymentRepository.Get(deployment1Id).Returns(deployment1);
        _deploymentRepository.Get(deployment2Id).Returns(deployment2);
        _deploymentRepository.Get(deployment3Id).Returns(deployment3);

        // Setup stacks - only first two have observers
        var stack1 = new StackDefinition("source", "api-service", "yaml1");
        var stack2 = new StackDefinition("source", "web-frontend", "yaml2");
        var stack3 = new StackDefinition("source", "background-worker", "yaml3");

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stack1, stack2, stack3 }));

        // Manifest with observer for api-service
        _manifestParser.ParseAsync("yaml1").Returns(Task.FromResult(new RsgoManifest
        {
            MaintenanceObserver = new RsgoMaintenanceObserver
            {
                Type = "http",
                Url = "https://status.com/api",
                PollingInterval = "30s",
                MaintenanceValue = "true",
                NormalValue = "false"
            }
        }));

        // Manifest with observer for web-frontend
        _manifestParser.ParseAsync("yaml2").Returns(Task.FromResult(new RsgoManifest
        {
            MaintenanceObserver = new RsgoMaintenanceObserver
            {
                Type = "http",
                Url = "https://status.com/web",
                PollingInterval = "30s",
                MaintenanceValue = "true",
                NormalValue = "false"
            }
        }));

        // No observer for background-worker
        _manifestParser.ParseAsync("yaml3").Returns(Task.FromResult(new RsgoManifest()));

        // Setup observer - returns normal for all
        var observer = Substitute.For<IMaintenanceObserver>();
        observer.Type.Returns(ObserverType.Http);
        observer.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ObserverResult.NormalOperation("false")));

        _observerFactory.Create(Arg.Any<MaintenanceObserverConfig>()).Returns(observer);

        var deploymentsNotified = new List<string>();
        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Do<string>(name => deploymentsNotified.Add(name)),
            Arg.Any<ObserverResultDto>(),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = new MaintenanceObserverService(
            _observerFactory,
            _deploymentRepository,
            _healthSnapshotRepository,
            _stackSourceService,
            _manifestParser,
            _notificationService,
            _logger);

        // Act
        await service.CheckAllObserversAsync();

        // Assert
        deploymentsNotified.Should().HaveCount(2);
        deploymentsNotified.Should().Contain("api-service");
        deploymentsNotified.Should().Contain("web-frontend");
        deploymentsNotified.Should().NotContain("background-worker"); // No observer configured
    }

    [Fact]
    public async Task FullWorkflow_ObserverFailure_DoesNotAffectOtherDeployments()
    {
        // This E2E test simulates:
        // 1. Multiple deployments with observers
        // 2. One observer fails (e.g., network error)
        // 3. Other deployments are still checked

        // Arrange
        var environmentId = EnvironmentId.Create();

        var deploymentId1 = DeploymentId.Create();
        var deployment1 = CreateRunningDeployment(deploymentId1, environmentId, "service-a");

        var deploymentId2 = DeploymentId.Create();
        var deployment2 = CreateRunningDeployment(deploymentId2, environmentId, "service-b");

        _deploymentRepository.GetAllActive().Returns(new List<Deployment> { deployment1, deployment2 });
        _deploymentRepository.Get(deploymentId1).Returns(deployment1);
        _deploymentRepository.Get(deploymentId2).Returns(deployment2);

        var stack1 = new StackDefinition("source", "service-a", "yaml1");
        var stack2 = new StackDefinition("source", "service-b", "yaml2");

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stack1, stack2 }));

        _manifestParser.ParseAsync("yaml1").Returns(Task.FromResult(new RsgoManifest
        {
            MaintenanceObserver = new RsgoMaintenanceObserver
            {
                Type = "http",
                Url = "https://failing-server.com/status",
                PollingInterval = "30s",
                MaintenanceValue = "maintenance",
                NormalValue = "normal"
            }
        }));

        _manifestParser.ParseAsync("yaml2").Returns(Task.FromResult(new RsgoManifest
        {
            MaintenanceObserver = new RsgoMaintenanceObserver
            {
                Type = "http",
                Url = "https://working-server.com/status",
                PollingInterval = "30s",
                MaintenanceValue = "maintenance",
                NormalValue = "normal"
            }
        }));

        // Track which observers were created for each URL
        var failingObserver = Substitute.For<IMaintenanceObserver>();
        failingObserver.Type.Returns(ObserverType.Http);
        failingObserver.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ObserverResult.Failed("Connection refused")));

        var workingObserver = Substitute.For<IMaintenanceObserver>();
        workingObserver.Type.Returns(ObserverType.Http);
        workingObserver.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ObserverResult.NormalOperation("normal")));

        // Return different observers based on config
        var observerCallCount = 0;
        _observerFactory.Create(Arg.Any<MaintenanceObserverConfig>())
            .Returns(callInfo =>
            {
                observerCallCount++;
                return observerCallCount == 1 ? failingObserver : workingObserver;
            });

        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Any<string>(),
            Arg.Any<ObserverResultDto>(),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = new MaintenanceObserverService(
            _observerFactory,
            _deploymentRepository,
            _healthSnapshotRepository,
            _stackSourceService,
            _manifestParser,
            _notificationService,
            _logger);

        // Act - this should not throw even though one observer fails
        await service.CheckAllObserversAsync();

        // Assert - both observers were checked
        await failingObserver.Received(1).CheckAsync(Arg.Any<CancellationToken>());
        await workingObserver.Received(1).CheckAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Observer Type-Specific Tests

    [Fact]
    public async Task HttpObserver_WithJsonPath_ExtractsNestedValue()
    {
        // Arrange
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateRunningDeployment(deploymentId, environmentId, "json-api");

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        var stackDefinition = new StackDefinition("source", "json-api", "yaml");

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var manifest = new RsgoManifest
        {
            MaintenanceObserver = new RsgoMaintenanceObserver
            {
                Type = "http",
                Url = "https://api.example.com/health",
                PollingInterval = "30s",
                MaintenanceValue = "true",
                NormalValue = "false",
                JsonPath = "data.maintenance.enabled"  // Nested JSONPath
            }
        };

        _manifestParser.ParseAsync(Arg.Any<string>()).Returns(Task.FromResult(manifest));

        // The actual HTTP observer would extract from JSON like:
        // { "data": { "maintenance": { "enabled": true } } }
        var observer = Substitute.For<IMaintenanceObserver>();
        observer.Type.Returns(ObserverType.Http);
        observer.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ObserverResult.MaintenanceRequired("true")));

        _observerFactory.Create(Arg.Any<MaintenanceObserverConfig>()).Returns(observer);

        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Any<string>(),
            Arg.Any<ObserverResultDto>(),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = new MaintenanceObserverService(
            _observerFactory,
            _deploymentRepository,
            _healthSnapshotRepository,
            _stackSourceService,
            _manifestParser,
            _notificationService,
            _logger);

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        result.Should().NotBeNull();
        result!.IsMaintenanceRequired.Should().BeTrue();
        result.ObservedValue.Should().Be("true");
    }

    [Fact]
    public async Task SqlObserver_NotSupportedWithoutConnectionString_ReturnsNull()
    {
        // Arrange
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateRunningDeployment(deploymentId, environmentId, "db-app");

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        var stackDefinition = new StackDefinition("source", "db-app", "yaml");

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        // SQL observer without connection string
        var manifest = new RsgoManifest
        {
            MaintenanceObserver = new RsgoMaintenanceObserver
            {
                Type = "sqlExtendedProperty",
                PropertyName = "MaintenanceMode",
                PollingInterval = "30s",
                MaintenanceValue = "true",
                NormalValue = "false"
                // No ConnectionString or ConnectionName - this should fail
            }
        };

        _manifestParser.ParseAsync(Arg.Any<string>()).Returns(Task.FromResult(manifest));

        var service = new MaintenanceObserverService(
            _observerFactory,
            _deploymentRepository,
            _healthSnapshotRepository,
            _stackSourceService,
            _manifestParser,
            _notificationService,
            _logger);

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert - observer creation should fail due to missing connection string
        result.Should().BeNull();
    }

    #endregion
}
