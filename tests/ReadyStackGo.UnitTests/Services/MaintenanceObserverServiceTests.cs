using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.StackSources;
using ReadyStackGo.Infrastructure.Services;

namespace ReadyStackGo.UnitTests.Services;

/// <summary>
/// Unit tests for MaintenanceObserverService, focusing on variable resolution
/// and observer configuration creation.
/// </summary>
public class MaintenanceObserverServiceTests
{
    private readonly Mock<IDeploymentRepository> _deploymentRepository;
    private readonly Mock<IHealthSnapshotRepository> _healthSnapshotRepository;
    private readonly Mock<IStackSourceService> _stackSourceService;
    private readonly Mock<IHealthNotificationService> _notificationService;
    private readonly Mock<IMaintenanceObserverFactory> _observerFactory;
    private readonly Mock<ISender> _mediator;
    private readonly Mock<ILogger<MaintenanceObserverService>> _logger;

    public MaintenanceObserverServiceTests()
    {
        _deploymentRepository = new Mock<IDeploymentRepository>();
        _healthSnapshotRepository = new Mock<IHealthSnapshotRepository>();
        _stackSourceService = new Mock<IStackSourceService>();
        _notificationService = new Mock<IHealthNotificationService>();
        _observerFactory = new Mock<IMaintenanceObserverFactory>();
        _mediator = new Mock<ISender>();
        _logger = new Mock<ILogger<MaintenanceObserverService>>();
    }

    private MaintenanceObserverService CreateService()
    {
        return new MaintenanceObserverService(
            _observerFactory.Object,
            _deploymentRepository.Object,
            _healthSnapshotRepository.Object,
            _stackSourceService.Object,
            _notificationService.Object,
            _mediator.Object,
            _logger.Object);
    }

    private Deployment CreateDeploymentWithVariables(
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

    private static StackDefinition CreateStackDefinition(string stackName, RsgoMaintenanceObserver? maintenanceObserver = null)
    {
        return new StackDefinition(
            sourceId: "test-source",
            name: stackName,
            yamlContent: "version: '3'\nservices:\n  web:\n    image: nginx",
            description: "Test Stack",
            maintenanceObserver: maintenanceObserver);
    }

    #region Variable Resolution Tests

    [Fact]
    public async Task CheckDeploymentObserverAsync_SqlObserverWithVariableConnectionString_ResolvesVariable()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var variables = new Dictionary<string, string>
        {
            ["AMS_DB"] = "Server=sqlserver;Database=AmsDb;User=sa;Password=secret"
        };

        var deployment = CreateDeploymentWithVariables(deploymentId, environmentId, "test-stack", variables);
        _deploymentRepository.Setup(r => r.Get(deploymentId)).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "${AMS_DB}",
            PropertyName = "ams.maintenance",
            PollingInterval = "30s",
            MaintenanceValue = "1"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);
        _stackSourceService.Setup(s => s.GetStacksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StackDefinition> { stackDefinition });

        MaintenanceObserverConfig? capturedConfig = null;
        var observer = new Mock<IMaintenanceObserver>();
        observer.Setup(o => o.Type).Returns(ObserverType.SqlExtendedProperty);
        observer.Setup(o => o.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObserverResult.NormalOperation("0"));

        _observerFactory.Setup(f => f.Create(It.IsAny<MaintenanceObserverConfig>()))
            .Callback<MaintenanceObserverConfig>(c => capturedConfig = c)
            .Returns(observer.Object);

        _notificationService.Setup(n => n.NotifyObserverResultAsync(
            It.IsAny<DeploymentId>(),
            It.IsAny<string>(),
            It.IsAny<ObserverResultDto>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        capturedConfig.Should().NotBeNull();
        var sqlSettings = capturedConfig!.Settings as SqlObserverSettings;
        sqlSettings.Should().NotBeNull();
        sqlSettings!.ConnectionString.Should().Be("Server=sqlserver;Database=AmsDb;User=sa;Password=secret");
    }

    [Fact]
    public async Task CheckDeploymentObserverAsync_MultipleVariables_ResolvesAllVariables()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var variables = new Dictionary<string, string>
        {
            ["DB_SERVER"] = "sqlserver.local",
            ["DB_NAME"] = "MyDatabase",
            ["DB_USER"] = "admin",
            ["DB_PASS"] = "s3cr3t"
        };

        var deployment = CreateDeploymentWithVariables(deploymentId, environmentId, "test-stack", variables);
        _deploymentRepository.Setup(r => r.Get(deploymentId)).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "Server=${DB_SERVER};Database=${DB_NAME};User=${DB_USER};Password=${DB_PASS}",
            PropertyName = "maintenance",
            PollingInterval = "30s",
            MaintenanceValue = "1"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);
        _stackSourceService.Setup(s => s.GetStacksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StackDefinition> { stackDefinition });

        MaintenanceObserverConfig? capturedConfig = null;
        var observer = new Mock<IMaintenanceObserver>();
        observer.Setup(o => o.Type).Returns(ObserverType.SqlExtendedProperty);
        observer.Setup(o => o.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObserverResult.NormalOperation("0"));

        _observerFactory.Setup(f => f.Create(It.IsAny<MaintenanceObserverConfig>()))
            .Callback<MaintenanceObserverConfig>(c => capturedConfig = c)
            .Returns(observer.Object);

        _notificationService.Setup(n => n.NotifyObserverResultAsync(
            It.IsAny<DeploymentId>(),
            It.IsAny<string>(),
            It.IsAny<ObserverResultDto>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        capturedConfig.Should().NotBeNull();
        var sqlSettings = capturedConfig!.Settings as SqlObserverSettings;
        sqlSettings.Should().NotBeNull();
        sqlSettings!.ConnectionString.Should().Be("Server=sqlserver.local;Database=MyDatabase;User=admin;Password=s3cr3t");
    }

    [Fact]
    public async Task CheckDeploymentObserverAsync_ConnectionName_UsesVariableValue()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var variables = new Dictionary<string, string>
        {
            ["DB_CONNECTION"] = "Server=db;Database=Test;Integrated Security=true"
        };

        var deployment = CreateDeploymentWithVariables(deploymentId, environmentId, "test-stack", variables);
        _deploymentRepository.Setup(r => r.Get(deploymentId)).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlQuery",
            ConnectionName = "DB_CONNECTION",
            Query = "SELECT Status FROM Config",
            PollingInterval = "1m",
            MaintenanceValue = "maintenance"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);
        _stackSourceService.Setup(s => s.GetStacksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StackDefinition> { stackDefinition });

        MaintenanceObserverConfig? capturedConfig = null;
        var observer = new Mock<IMaintenanceObserver>();
        observer.Setup(o => o.Type).Returns(ObserverType.SqlQuery);
        observer.Setup(o => o.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObserverResult.NormalOperation("normal"));

        _observerFactory.Setup(f => f.Create(It.IsAny<MaintenanceObserverConfig>()))
            .Callback<MaintenanceObserverConfig>(c => capturedConfig = c)
            .Returns(observer.Object);

        _notificationService.Setup(n => n.NotifyObserverResultAsync(
            It.IsAny<DeploymentId>(),
            It.IsAny<string>(),
            It.IsAny<ObserverResultDto>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        capturedConfig.Should().NotBeNull();
        var sqlSettings = capturedConfig!.Settings as SqlObserverSettings;
        sqlSettings.Should().NotBeNull();
        sqlSettings!.ConnectionString.Should().Be("Server=db;Database=Test;Integrated Security=true");
    }

    [Fact]
    public async Task CheckDeploymentObserverAsync_MissingConnectionString_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var variables = new Dictionary<string, string>(); // No variables
        var deployment = CreateDeploymentWithVariables(deploymentId, environmentId, "test-stack", variables);
        _deploymentRepository.Setup(r => r.Get(deploymentId)).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "${MISSING_VAR}",
            PropertyName = "maintenance",
            PollingInterval = "30s",
            MaintenanceValue = "1"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);
        _stackSourceService.Setup(s => s.GetStacksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StackDefinition> { stackDefinition });

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        result.Should().BeNull();
        _observerFactory.Verify(f => f.Create(It.IsAny<MaintenanceObserverConfig>()), Times.Never);
    }

    [Fact]
    public async Task CheckDeploymentObserverAsync_MissingConnectionName_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var variables = new Dictionary<string, string>
        {
            ["OTHER_VAR"] = "some value"
        };
        var deployment = CreateDeploymentWithVariables(deploymentId, environmentId, "test-stack", variables);
        _deploymentRepository.Setup(r => r.Get(deploymentId)).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlQuery",
            ConnectionName = "MISSING_CONNECTION",
            Query = "SELECT 1",
            PollingInterval = "30s",
            MaintenanceValue = "1"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);
        _stackSourceService.Setup(s => s.GetStacksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StackDefinition> { stackDefinition });

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        result.Should().BeNull();
        _observerFactory.Verify(f => f.Create(It.IsAny<MaintenanceObserverConfig>()), Times.Never);
    }

    #endregion

    #region Observer Type Tests

    [Fact]
    public async Task CheckDeploymentObserverAsync_HttpObserver_CreatesCorrectConfig()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateDeploymentWithVariables(deploymentId, environmentId, "test-stack", new Dictionary<string, string>());
        _deploymentRepository.Setup(r => r.Get(deploymentId)).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "http",
            Url = "https://api.example.com/maintenance",
            Method = "POST",
            Timeout = "15s",
            JsonPath = "$.status",
            PollingInterval = "1m",
            MaintenanceValue = "true",
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token123"
            }
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);
        _stackSourceService.Setup(s => s.GetStacksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StackDefinition> { stackDefinition });

        MaintenanceObserverConfig? capturedConfig = null;
        var observer = new Mock<IMaintenanceObserver>();
        observer.Setup(o => o.Type).Returns(ObserverType.Http);
        observer.Setup(o => o.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObserverResult.NormalOperation("false"));

        _observerFactory.Setup(f => f.Create(It.IsAny<MaintenanceObserverConfig>()))
            .Callback<MaintenanceObserverConfig>(c => capturedConfig = c)
            .Returns(observer.Object);

        _notificationService.Setup(n => n.NotifyObserverResultAsync(
            It.IsAny<DeploymentId>(),
            It.IsAny<string>(),
            It.IsAny<ObserverResultDto>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        capturedConfig.Should().NotBeNull();
        capturedConfig!.Type.Should().Be(ObserverType.Http);
        capturedConfig.PollingInterval.Should().Be(TimeSpan.FromMinutes(1));
        capturedConfig.MaintenanceValue.Should().Be("true");

        var httpSettings = capturedConfig.Settings as HttpObserverSettings;
        httpSettings.Should().NotBeNull();
        httpSettings!.Url.Should().Be("https://api.example.com/maintenance");
        httpSettings.Method.Should().Be("POST");
        httpSettings.Timeout.Should().Be(TimeSpan.FromSeconds(15));
        httpSettings.JsonPath.Should().Be("$.status");
        httpSettings.Headers.Should().ContainKey("Authorization");
    }

    [Fact]
    public async Task CheckDeploymentObserverAsync_FileObserver_CreatesCorrectConfig()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateDeploymentWithVariables(deploymentId, environmentId, "test-stack", new Dictionary<string, string>());
        _deploymentRepository.Setup(r => r.Get(deploymentId)).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "file",
            Path = "/app/maintenance.flag",
            Mode = "exists",
            PollingInterval = "5m",
            MaintenanceValue = "true"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);
        _stackSourceService.Setup(s => s.GetStacksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StackDefinition> { stackDefinition });

        MaintenanceObserverConfig? capturedConfig = null;
        var observer = new Mock<IMaintenanceObserver>();
        observer.Setup(o => o.Type).Returns(ObserverType.File);
        observer.Setup(o => o.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObserverResult.NormalOperation("false"));

        _observerFactory.Setup(f => f.Create(It.IsAny<MaintenanceObserverConfig>()))
            .Callback<MaintenanceObserverConfig>(c => capturedConfig = c)
            .Returns(observer.Object);

        _notificationService.Setup(n => n.NotifyObserverResultAsync(
            It.IsAny<DeploymentId>(),
            It.IsAny<string>(),
            It.IsAny<ObserverResultDto>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        capturedConfig.Should().NotBeNull();
        capturedConfig!.Type.Should().Be(ObserverType.File);
        capturedConfig.PollingInterval.Should().Be(TimeSpan.FromMinutes(5));

        var fileSettings = capturedConfig.Settings as FileObserverSettings;
        fileSettings.Should().NotBeNull();
        fileSettings!.Path.Should().Be("/app/maintenance.flag");
        fileSettings.Mode.Should().Be(FileCheckMode.Exists);
    }

    [Fact]
    public async Task CheckDeploymentObserverAsync_UnknownObserverType_ReturnsNull()
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateDeploymentWithVariables(deploymentId, environmentId, "test-stack", new Dictionary<string, string>());
        _deploymentRepository.Setup(r => r.Get(deploymentId)).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "unknownType",
            PollingInterval = "30s",
            MaintenanceValue = "1"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);
        _stackSourceService.Setup(s => s.GetStacksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StackDefinition> { stackDefinition });

        // Act
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        result.Should().BeNull();
        _observerFactory.Verify(f => f.Create(It.IsAny<MaintenanceObserverConfig>()), Times.Never);
    }

    #endregion

    #region Polling Interval Tests

    [Theory]
    [InlineData("30s", 30)]
    [InlineData("1m", 60)]
    [InlineData("5m", 300)]
    [InlineData("1h", 3600)]
    public async Task CheckDeploymentObserverAsync_ParsesPollingInterval(string interval, int expectedSeconds)
    {
        // Arrange
        var service = CreateService();
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();

        var deployment = CreateDeploymentWithVariables(deploymentId, environmentId, "test-stack", new Dictionary<string, string>());
        _deploymentRepository.Setup(r => r.Get(deploymentId)).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "file",
            Path = "/app/flag",
            Mode = "exists",
            PollingInterval = interval,
            MaintenanceValue = "true"
        };

        var stackDefinition = CreateStackDefinition("test-stack", maintenanceObserver);
        _stackSourceService.Setup(s => s.GetStacksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StackDefinition> { stackDefinition });

        MaintenanceObserverConfig? capturedConfig = null;
        var observer = new Mock<IMaintenanceObserver>();
        observer.Setup(o => o.Type).Returns(ObserverType.File);
        observer.Setup(o => o.CheckAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObserverResult.NormalOperation("false"));

        _observerFactory.Setup(f => f.Create(It.IsAny<MaintenanceObserverConfig>()))
            .Callback<MaintenanceObserverConfig>(c => capturedConfig = c)
            .Returns(observer.Object);

        _notificationService.Setup(n => n.NotifyObserverResultAsync(
            It.IsAny<DeploymentId>(),
            It.IsAny<string>(),
            It.IsAny<ObserverResultDto>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await service.CheckDeploymentObserverAsync(deploymentId);

        // Assert
        capturedConfig.Should().NotBeNull();
        capturedConfig!.PollingInterval.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    #endregion
}
