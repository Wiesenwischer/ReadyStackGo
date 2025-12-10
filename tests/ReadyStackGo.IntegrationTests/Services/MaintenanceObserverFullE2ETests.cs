using FluentAssertions;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.StackSources;
using ReadyStackGo.Infrastructure.Observers;
using ReadyStackGo.Infrastructure.Services;
using Testcontainers.MsSql;

namespace ReadyStackGo.IntegrationTests.Services;

/// <summary>
/// Complete End-to-End tests for the Maintenance Observer feature.
/// These tests use Testcontainers to spin up a real SQL Server and test
/// the complete flow from Deployment creation with Variables through
/// to the Observer detecting maintenance mode changes.
/// </summary>
public class MaintenanceObserverFullE2ETests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer;
    private string _connectionString = null!;

    // Real services
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly IStackSourceService _stackSourceService;
    private readonly IHealthNotificationService _notificationService;
    private readonly ISender _mediator;
    private readonly ILogger<MaintenanceObserverService> _serviceLogger;

    // Real observer factory that creates actual observers
    private MaintenanceObserverFactory _realObserverFactory = null!;

    public MaintenanceObserverFullE2ETests()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        _deploymentRepository = Substitute.For<IDeploymentRepository>();
        _healthSnapshotRepository = Substitute.For<IHealthSnapshotRepository>();
        _stackSourceService = Substitute.For<IStackSourceService>();
        _notificationService = Substitute.For<IHealthNotificationService>();
        _mediator = Substitute.For<ISender>();
        _serviceLogger = NullLogger<MaintenanceObserverService>.Instance;
    }

    private MaintenanceObserverService CreateService()
    {
        return new MaintenanceObserverService(
            _realObserverFactory,
            _deploymentRepository,
            _healthSnapshotRepository,
            _stackSourceService,
            _notificationService,
            _mediator,
            _serviceLogger);
    }

    private void SetupObserverFactory()
    {
        // Create service collection with required loggers for observers
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        var serviceProvider = services.BuildServiceProvider();
        _realObserverFactory = new MaintenanceObserverFactory(serviceProvider);
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        _connectionString = _sqlContainer.GetConnectionString();

        // Setup observer factory with DI
        SetupObserverFactory();

        // Create the extended property for maintenance mode (initially "0" = normal)
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new SqlCommand(
            "EXEC sp_addextendedproperty @name = N'ams.maintenance', @value = '0'",
            connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    /// <summary>
    /// Complete E2E test: Creates a deployment with AMS_DB variable,
    /// configures a SQL Extended Property observer with ${AMS_DB} placeholder,
    /// and verifies the observer correctly detects normal operation.
    /// </summary>
    [Fact]
    public async Task E2E_Complete_DeploymentWithVariable_ObserverDetectsNormalMode()
    {
        // =====================================================
        // ARRANGE - Set up the complete scenario
        // =====================================================

        // 1. Create a deployment with the AMS_DB variable
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();
        var userId = UserId.Create();

        var deployment = Deployment.Start(deploymentId, environmentId, "Business Services", "ams-business", userId);

        // Set the AMS_DB variable with the actual SQL Server connection string
        deployment.SetVariables(new Dictionary<string, string>
        {
            ["AMS_DB"] = _connectionString
        });

        deployment.MarkAsRunning(new List<DeployedService>());

        // Verify the variable is stored
        deployment.Variables.Should().ContainKey("AMS_DB");
        deployment.Variables["AMS_DB"].Should().Be(_connectionString);

        // 2. Set up repository to return our deployment
        _deploymentRepository.Get(deploymentId).Returns(deployment);
        _deploymentRepository.GetAllActive().Returns(new List<Deployment> { deployment });

        // 3. Set up stack source to return matching stack with MaintenanceObserver
        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "${AMS_DB}",  // This will be resolved from deployment.Variables
            PropertyName = "ams.maintenance",
            PollingInterval = "30s",
            MaintenanceValue = "1",
            NormalValue = "0"
        };

        var stackDefinition = new StackDefinition(
            sourceId: "ams-stacks",
            name: "Business Services",  // Must match deployment.StackName!
            yamlContent: "yaml-content",
            description: "AMS Business Services",
            maintenanceObserver: maintenanceObserver);

        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        // 5. Track notifications
        var notificationsSent = new List<ObserverResultDto>();
        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Any<string>(),
            Arg.Do<ObserverResultDto>(dto => notificationsSent.Add(dto)),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // 6. Create the service with REAL observer factory
        var service = CreateService();

        // =====================================================
        // ACT - Run the observer check
        // =====================================================
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // =====================================================
        // ASSERT - Verify the complete flow worked
        // =====================================================

        // Observer should have run and returned a result
        result.Should().NotBeNull("Observer should be created and check should succeed");
        result!.IsSuccess.Should().BeTrue("SQL connection should work");
        result.ObservedValue.Should().Be("0", "Extended property is set to '0'");
        result.IsMaintenanceRequired.Should().BeFalse("Value '0' means normal operation");

        // Notification should have been sent
        notificationsSent.Should().HaveCount(1);
        notificationsSent[0].IsMaintenanceRequired.Should().BeFalse();
        notificationsSent[0].ObservedValue.Should().Be("0");
    }

    /// <summary>
    /// Complete E2E test: Deployment detects maintenance mode after
    /// the SQL extended property is changed to "1".
    /// </summary>
    [Fact]
    public async Task E2E_Complete_DeploymentWithVariable_ObserverDetectsMaintenanceMode()
    {
        // ARRANGE
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();
        var userId = UserId.Create();

        var deployment = Deployment.Start(deploymentId, environmentId, "Business Services", "ams-business", userId);
        deployment.SetVariables(new Dictionary<string, string>
        {
            ["AMS_DB"] = _connectionString
        });
        deployment.MarkAsRunning(new List<DeployedService>());

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "${AMS_DB}",
            PropertyName = "ams.maintenance",
            PollingInterval = "30s",
            MaintenanceValue = "1",
            NormalValue = "0"
        };

        var stackDefinition = new StackDefinition("source", "Business Services", "yaml", maintenanceObserver: maintenanceObserver);
        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var notificationsSent = new List<ObserverResultDto>();
        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Any<string>(),
            Arg.Do<ObserverResultDto>(dto => notificationsSent.Add(dto)),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = CreateService();

        // Set maintenance mode in SQL Server
        await using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            await using var cmd = new SqlCommand(
                "EXEC sp_updateextendedproperty @name = N'ams.maintenance', @value = '1'",
                connection);
            await cmd.ExecuteNonQueryAsync();
        }

        // ACT
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // ASSERT
        result.Should().NotBeNull();
        result!.IsSuccess.Should().BeTrue();
        result.ObservedValue.Should().Be("1");
        result.IsMaintenanceRequired.Should().BeTrue("Value '1' means maintenance mode");

        notificationsSent.Should().HaveCount(1);
        notificationsSent[0].IsMaintenanceRequired.Should().BeTrue();
    }

    /// <summary>
    /// E2E test: Missing variable in deployment causes observer to not be created.
    /// </summary>
    [Fact]
    public async Task E2E_Complete_DeploymentWithoutVariable_ObserverNotCreated()
    {
        // ARRANGE
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();
        var userId = UserId.Create();

        // Deployment WITHOUT AMS_DB variable
        var deployment = Deployment.Start(deploymentId, environmentId, "Business Services", "ams-business", userId);
        // NOT setting variables - deployment.SetVariables() NOT called
        deployment.MarkAsRunning(new List<DeployedService>());

        deployment.Variables.Should().BeEmpty("No variables were set");

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "${AMS_DB}",  // Cannot be resolved!
            PropertyName = "ams.maintenance",
            PollingInterval = "30s",
            MaintenanceValue = "1"
        };

        var stackDefinition = new StackDefinition("source", "Business Services", "yaml", maintenanceObserver: maintenanceObserver);
        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var service = CreateService();

        // ACT
        var result = await service.CheckDeploymentObserverAsync(deploymentId);

        // ASSERT
        result.Should().BeNull("Observer cannot be created without resolved connection string");

        // No notification should be sent
        await _notificationService.DidNotReceive().NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Any<string>(),
            Arg.Any<ObserverResultDto>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// E2E test: Multiple deployments with different variables, each gets correct observer.
    /// </summary>
    [Fact]
    public async Task E2E_Complete_MultipleDeployments_EachGetsOwnObserver()
    {
        // ARRANGE - Create two deployments with different databases
        var deployment1Id = DeploymentId.Create();
        var deployment2Id = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();
        var userId = UserId.Create();

        // Both deployments use same connection (in real world they'd have different DBs)
        var deployment1 = Deployment.Start(deployment1Id, environmentId, "Business Services", "ams-business-1", userId);
        deployment1.SetVariables(new Dictionary<string, string> { ["AMS_DB"] = _connectionString });
        deployment1.MarkAsRunning(new List<DeployedService>());

        var deployment2 = Deployment.Start(deployment2Id, environmentId, "Business Services", "ams-business-2", userId);
        deployment2.SetVariables(new Dictionary<string, string> { ["AMS_DB"] = _connectionString });
        deployment2.MarkAsRunning(new List<DeployedService>());

        _deploymentRepository.GetAllActive().Returns(new List<Deployment> { deployment1, deployment2 });
        _deploymentRepository.Get(deployment1Id).Returns(deployment1);
        _deploymentRepository.Get(deployment2Id).Returns(deployment2);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "${AMS_DB}",
            PropertyName = "ams.maintenance",
            PollingInterval = "1s",
            MaintenanceValue = "1",
            NormalValue = "0"
        };

        var stackDefinition = new StackDefinition("source", "Business Services", "yaml", maintenanceObserver: maintenanceObserver);
        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var notifiedDeployments = new List<string>();
        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Do<string>(name => notifiedDeployments.Add(name)),
            Arg.Any<ObserverResultDto>(),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = CreateService();

        // ACT
        await service.CheckAllObserversAsync();

        // ASSERT - Both deployments were checked
        notifiedDeployments.Should().HaveCount(2);
        notifiedDeployments.Should().Contain("Business Services");
    }

    /// <summary>
    /// E2E test: Observer detects transition from normal to maintenance and back.
    /// </summary>
    [Fact]
    public async Task E2E_Complete_ObserverDetectsStateTransition()
    {
        // ARRANGE
        var deploymentId = DeploymentId.Create();
        var environmentId = EnvironmentId.Create();
        var userId = UserId.Create();

        var deployment = Deployment.Start(deploymentId, environmentId, "Business Services", "ams-business", userId);
        deployment.SetVariables(new Dictionary<string, string> { ["AMS_DB"] = _connectionString });
        deployment.MarkAsRunning(new List<DeployedService>());

        _deploymentRepository.Get(deploymentId).Returns(deployment);

        var maintenanceObserver = new RsgoMaintenanceObserver
        {
            Type = "sqlExtendedProperty",
            ConnectionString = "${AMS_DB}",
            PropertyName = "ams.maintenance",
            PollingInterval = "1s",  // Short interval for test
            MaintenanceValue = "1",
            NormalValue = "0"
        };

        var stackDefinition = new StackDefinition("source", "Business Services", "yaml", maintenanceObserver: maintenanceObserver);
        _stackSourceService.GetStacksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IEnumerable<StackDefinition>>(new List<StackDefinition> { stackDefinition }));

        var notifications = new List<ObserverResultDto>();
        _notificationService.NotifyObserverResultAsync(
            Arg.Any<DeploymentId>(),
            Arg.Any<string>(),
            Arg.Do<ObserverResultDto>(dto => notifications.Add(dto)),
            Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = CreateService();

        // ACT & ASSERT - Check 1: Normal mode (property is "0")
        var result1 = await service.CheckDeploymentObserverAsync(deploymentId);
        result1!.IsMaintenanceRequired.Should().BeFalse();

        // Change to maintenance mode
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "EXEC sp_updateextendedproperty @name = N'ams.maintenance', @value = '1'", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        // Wait for polling interval
        await Task.Delay(1100);

        // Check 2: Maintenance mode
        var result2 = await service.CheckDeploymentObserverAsync(deploymentId);
        result2!.IsMaintenanceRequired.Should().BeTrue();

        // Change back to normal
        await using (var conn = new SqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(
                "EXEC sp_updateextendedproperty @name = N'ams.maintenance', @value = '0'", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        // Wait for polling interval
        await Task.Delay(1100);

        // Check 3: Normal mode again
        var result3 = await service.CheckDeploymentObserverAsync(deploymentId);
        result3!.IsMaintenanceRequired.Should().BeFalse();

        // Verify all transitions were notified
        notifications.Should().HaveCount(3);
        notifications[0].IsMaintenanceRequired.Should().BeFalse();
        notifications[1].IsMaintenanceRequired.Should().BeTrue();
        notifications[2].IsMaintenanceRequired.Should().BeFalse();
    }
}
