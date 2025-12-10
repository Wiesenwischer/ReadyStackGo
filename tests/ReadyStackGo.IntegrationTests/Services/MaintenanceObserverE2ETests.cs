using FluentAssertions;
using MediatR;
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
    private readonly IHealthNotificationService _notificationService;
    private readonly IMaintenanceObserverFactory _observerFactory;
    private readonly ISender _mediator;
    private readonly ILogger<MaintenanceObserverService> _logger;

    public MaintenanceObserverE2ETests()
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

    private static Deployment CreateRunningDeploymentWithVariables(
        DeploymentId deploymentId,
        EnvironmentId environmentId,
        string stackName,
        Dictionary<string, string> variables)
    {
        var userId = UserId.Create();
        var deployment = Deployment.Start(deploymentId, environmentId, stackName, stackName, userId);
        deployment.SetVariables(variables);
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

        // Setup stack with HTTP observer - MaintenanceObserver is now directly on StackDefinition
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://status.mycompany.com/api/maintenance",
            PollingInterval = "30s",
            MaintenanceValue = "maintenance",
            NormalValue = "normal",
            JsonPath = "status"
        };

        var stackDefinition = new StackDefinition(
            sourceId: "rsgo-stacks",
            name: "prod-api",
            yamlContent: "version: '3'\nservices:\n  api:\n    image: myapp:latest",
            description: "Production API",
            maintenanceObserver: maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

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

        var service = CreateService();

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

        // Setup stack with HTTP observer - MaintenanceObserver is now directly on StackDefinition
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://status.mycompany.com/api/maintenance",
            PollingInterval = "1s",
            MaintenanceValue = "maintenance",
            NormalValue = "normal"
        };

        var stackDefinition = new StackDefinition(
            sourceId: "rsgo-stacks",
            name: "prod-api",
            yamlContent: "version: '3'\nservices:\n  api:\n    image: myapp:latest",
            maintenanceObserver: maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

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

        var service = CreateService();

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

        // Setup stacks - only first two have observers (MaintenanceObserver directly on StackDefinition)
        var observer1 = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://status.com/api",
            PollingInterval = "30s",
            MaintenanceValue = "true",
            NormalValue = "false"
        };

        var observer2 = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://status.com/web",
            PollingInterval = "30s",
            MaintenanceValue = "true",
            NormalValue = "false"
        };

        var stack1 = new StackDefinition("source", "api-service", "yaml1", maintenanceObserver: observer1);
        var stack2 = new StackDefinition("source", "web-frontend", "yaml2", maintenanceObserver: observer2);
        var stack3 = new StackDefinition("source", "background-worker", "yaml3"); // No observer

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stack1, stack2, stack3 }));

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

        var service = CreateService();

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

        // Setup stacks with observers - MaintenanceObserver directly on StackDefinition
        var observerConfig1 = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://failing-server.com/status",
            PollingInterval = "30s",
            MaintenanceValue = "maintenance",
            NormalValue = "normal"
        };

        var observerConfig2 = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://working-server.com/status",
            PollingInterval = "30s",
            MaintenanceValue = "maintenance",
            NormalValue = "normal"
        };

        var stack1 = new StackDefinition("source", "service-a", "yaml1", maintenanceObserver: observerConfig1);
        var stack2 = new StackDefinition("source", "service-b", "yaml2", maintenanceObserver: observerConfig2);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stack1, stack2 }));

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

        var service = CreateService();

        // Act - this should not throw even though one observer fails
        await service.CheckAllObserversAsync();

        // Assert - both observers were checked
        await failingObserver.Received(1).CheckAsync(Arg.Any<CancellationToken>());
        await workingObserver.Received(1).CheckAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Variable Resolution E2E Tests

    [Fact]
    public async Task E2E_SqlObserver_WithVariablePlaceholder_ResolvesAndCreatesObserver()
    {
        // This E2E test simulates the real-world scenario:
        // 1. Manifest has connectionString: ${AMS_DB}
        // 2. Deployment has AMS_DB variable with actual connection string
        // 3. Observer is created with resolved connection string

        // Arrange
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        // Deployment with AMS_DB variable set
        var variables = new Dictionary<string, string>
        {
            ["AMS_DB"] = "Server=sqldev2017;Database=dev-amsproject;User Id=projectuser;Password=projectuser;TrustServerCertificate=true"
        };
        var deployment = CreateRunningDeploymentWithVariables(deploymentId, environmentId, "Business Services", variables);

        _deploymentRepository.Get(deploymentId).Returns(deployment);
        _deploymentRepository.GetAllActive().Returns(new List<Deployment> { deployment });

        // Stack definition with MaintenanceObserver containing ${AMS_DB} placeholder
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "${AMS_DB}",  // Placeholder!
            PropertyName = "ams.maintenance",
            PollingInterval = "30s",
            MaintenanceValue = "1",
            NormalValue = "0"
        };

        var stackDefinition = new StackDefinition(
            sourceId: "rsgo-stacks",
            name: "Business Services",
            yamlContent: "yaml-content",
            description: "AMS Business Services",
            maintenanceObserver: maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        // Capture the config passed to factory
        MaintenanceObserverConfig? capturedConfig = null;
        var observer = Substitute.For<IMaintenanceObserver>();
        observer.Type.Returns(ObserverType.SqlExtendedProperty);
        observer.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ObserverResult.NormalOperation("0")));

        _observerFactory.Create(Arg.Do<MaintenanceObserverConfig>(c => capturedConfig = c))
            .Returns(observer);

        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Any<string>(),
            Arg.Any<ObserverResultDto>(),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        result.Should().NotBeNull();
        capturedConfig.Should().NotBeNull();
        capturedConfig!.Type.Should().Be(ObserverType.SqlExtendedProperty);

        // The key assertion: ${AMS_DB} was resolved to actual connection string
        var sqlSettings = capturedConfig.Settings as SqlObserverSettings;
        sqlSettings.Should().NotBeNull();
        sqlSettings!.ConnectionString.Should().Be("Server=sqldev2017;Database=dev-amsproject;User Id=projectuser;Password=projectuser;TrustServerCertificate=true");
        sqlSettings.PropertyName.Should().Be("ams.maintenance");
    }

    [Fact]
    public async Task E2E_SqlObserver_WithMissingVariable_ReturnsNull()
    {
        // Simulates: Manifest has ${AMS_DB} but deployment has no AMS_DB variable

        // Arrange
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        // Deployment WITHOUT the AMS_DB variable
        var deployment = CreateRunningDeploymentWithVariables(deploymentId, environmentId, "Business Services", new Dictionary<string, string>());

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        // Stack with MaintenanceObserver containing unresolvable placeholder
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "${AMS_DB}",  // Unresolvable!
            PropertyName = "ams.maintenance",
            PollingInterval = "30s",
            MaintenanceValue = "1"
        };

        var stackDefinition = new StackDefinition("source", "Business Services", "yaml", maintenanceObserver: maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var service = CreateService();

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert - should fail gracefully
        result.Should().BeNull();
        _observerFactory.DidNotReceive().Create(Arg.Any<MaintenanceObserverConfig>());
    }

    [Fact]
    public async Task E2E_SqlObserver_WithMultipleVariables_ResolvesAll()
    {
        // Simulates: connectionString with multiple placeholders

        // Arrange
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var variables = new Dictionary<string, string>
        {
            ["DB_SERVER"] = "sqldev2017",
            ["DB_NAME"] = "dev-amsproject",
            ["DB_USER"] = "projectuser",
            ["DB_PASS"] = "projectuser"
        };
        var deployment = CreateRunningDeploymentWithVariables(deploymentId, environmentId, "multi-var-stack", variables);

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        // Stack with MaintenanceObserver containing multiple variable placeholders
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "Server=${DB_SERVER};Database=${DB_NAME};User Id=${DB_USER};Password=${DB_PASS}",
            PropertyName = "maintenance",
            PollingInterval = "30s",
            MaintenanceValue = "1"
        };

        var stackDefinition = new StackDefinition("source", "multi-var-stack", "yaml", maintenanceObserver: maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        MaintenanceObserverConfig? capturedConfig = null;
        var observer = Substitute.For<IMaintenanceObserver>();
        observer.Type.Returns(ObserverType.SqlExtendedProperty);
        observer.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ObserverResult.NormalOperation("0")));

        _observerFactory.Create(Arg.Do<MaintenanceObserverConfig>(c => capturedConfig = c))
            .Returns(observer);

        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Any<string>(),
            Arg.Any<ObserverResultDto>(),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        result.Should().NotBeNull();
        var sqlSettings = capturedConfig!.Settings as SqlObserverSettings;
        sqlSettings!.ConnectionString.Should().Be("Server=sqldev2017;Database=dev-amsproject;User Id=projectuser;Password=projectuser");
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

        // Stack with HTTP observer using nested JSONPath
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://api.example.com/health",
            PollingInterval = "30s",
            MaintenanceValue = "true",
            NormalValue = "false",
            JsonPath = "data.maintenance.enabled"  // Nested JSONPath
        };

        var stackDefinition = new StackDefinition("source", "json-api", "yaml", maintenanceObserver: maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

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

        var service = CreateService();

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

        // SQL observer without connection string - this should fail validation
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            PropertyName = "MaintenanceMode",
            PollingInterval = "30s",
            MaintenanceValue = "true",
            NormalValue = "false"
            // No ConnectionString or ConnectionName - this should fail
        };

        var stackDefinition = new StackDefinition("source", "db-app", "yaml", maintenanceObserver: maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var service = CreateService();

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert - observer creation should fail due to missing connection string
        result.Should().BeNull();
    }

    #endregion
}
