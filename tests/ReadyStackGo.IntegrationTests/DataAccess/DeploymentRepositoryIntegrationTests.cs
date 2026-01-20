namespace ReadyStackGo.IntegrationTests.DataAccess;

using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.RuntimeConfig;
using ReadyStackGo.Infrastructure.DataAccess.Repositories;
using ReadyStackGo.IntegrationTests.Infrastructure;
using DeploymentEnvironment = ReadyStackGo.Domain.Deployment.Environments.Environment;

/// <summary>
/// Integration tests for DeploymentRepository with real SQLite database.
/// These tests verify that all entity configurations work correctly with SQLite,
/// especially the JSON columns like HealthCheckConfigsJson.
/// </summary>
public class DeploymentRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestFixture _fixture;
    private readonly DeploymentRepository _repository;
    private readonly EnvironmentRepository _environmentRepository;

    public DeploymentRepositoryIntegrationTests()
    {
        _fixture = new SqliteTestFixture();
        _repository = new DeploymentRepository(_fixture.Context);
        _environmentRepository = new EnvironmentRepository(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private (DeploymentEnvironment env, EnvironmentId envId) CreateTestEnvironment()
    {
        var envId = EnvironmentId.Create();
        var orgId = new OrganizationId(Guid.NewGuid());
        var env = DeploymentEnvironment.CreateDefault(envId, orgId, "Test Environment", "test-env");
        _environmentRepository.Add(env);
        _fixture.Context.SaveChanges();
        return (env, envId);
    }

    [Fact]
    public void Add_ShouldPersistDeployment_WithAllProperties()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var deploymentId = DeploymentId.Create();
        var userId = new UserId(Guid.NewGuid());

        var deployment = Deployment.StartInstallation(
            deploymentId,
            envId,
            "stack-123",
            "test-stack",
            "rsgo-test-stack",
            userId
        );
        deployment.SetStackVersion("1.0.0");

        // Act
        _repository.Add(deployment);
        _fixture.Context.SaveChanges();

        // Assert - use fresh context to verify persistence
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Deployments.Find(deploymentId);

        persisted.Should().NotBeNull();
        persisted!.StackName.Should().Be("test-stack");
        persisted.StackVersion.Should().Be("1.0.0");
        persisted.ProjectName.Should().Be("rsgo-test-stack");
        persisted.Status.Should().Be(DeploymentStatus.Installing);
    }

    [Fact]
    public void Add_ShouldPersistDeployment_WithVariables()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var deploymentId = DeploymentId.Create();
        var userId = new UserId(Guid.NewGuid());

        var deployment = Deployment.StartInstallation(
            deploymentId,
            envId,
            "stack-123",
            "test-stack",
            "rsgo-test-stack",
            userId
        );

        var variables = new Dictionary<string, string>
        {
            { "DB_HOST", "localhost" },
            { "DB_PORT", "5432" },
            { "API_KEY", "secret-key" }
        };
        deployment.SetVariables(variables);

        // Act
        _repository.Add(deployment);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Deployments.Find(deploymentId);

        persisted.Should().NotBeNull();
        persisted!.Variables.Should().HaveCount(3);
        persisted.Variables["DB_HOST"].Should().Be("localhost");
        persisted.Variables["DB_PORT"].Should().Be("5432");
        persisted.Variables["API_KEY"].Should().Be("secret-key");
    }

    [Fact]
    public void Add_ShouldPersistDeployment_WithHealthCheckConfigs()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var deploymentId = DeploymentId.Create();
        var userId = new UserId(Guid.NewGuid());

        var deployment = Deployment.StartInstallation(
            deploymentId,
            envId,
            "stack-123",
            "test-stack",
            "rsgo-test-stack",
            userId
        );

        var healthConfigs = new List<ServiceHealthCheckConfig>
        {
            new ServiceHealthCheckConfig("api-service", "http") { Path = "/health", Port = 8080, Interval = "30s", Retries = 3 },
            new ServiceHealthCheckConfig("worker-service", "docker")
        };
        deployment.SetHealthCheckConfigs(healthConfigs);

        // Act
        _repository.Add(deployment);
        _fixture.Context.SaveChanges();

        // Assert - use fresh context to verify persistence
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Deployments.Find(deploymentId);

        persisted.Should().NotBeNull();
        persisted!.HealthCheckConfigs.Should().HaveCount(2);

        var apiConfig = persisted.HealthCheckConfigs.First(c => c.ServiceName == "api-service");
        apiConfig.Type.Should().Be("http");
        apiConfig.Path.Should().Be("/health");
        apiConfig.Port.Should().Be(8080);
        apiConfig.Interval.Should().Be("30s");
        apiConfig.Retries.Should().Be(3);

        var workerConfig = persisted.HealthCheckConfigs.First(c => c.ServiceName == "worker-service");
        workerConfig.Type.Should().Be("docker");
    }

    [Fact]
    public void Add_ShouldPersistDeployment_WithEmptyHealthCheckConfigs()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var deploymentId = DeploymentId.Create();
        var userId = new UserId(Guid.NewGuid());

        var deployment = Deployment.StartInstallation(
            deploymentId,
            envId,
            "stack-123",
            "test-stack",
            "rsgo-test-stack",
            userId
        );
        // Don't set any health check configs

        // Act
        _repository.Add(deployment);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Deployments.Find(deploymentId);

        persisted.Should().NotBeNull();
        persisted!.HealthCheckConfigs.Should().BeEmpty();
    }

    [Fact]
    public void GetByEnvironment_ShouldReturnDeployments_WithHealthCheckConfigs()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var deploymentId = DeploymentId.Create();
        var userId = new UserId(Guid.NewGuid());

        var deployment = Deployment.StartInstallation(
            deploymentId,
            envId,
            "stack-123",
            "test-stack",
            "rsgo-test-stack",
            userId
        );

        var healthConfigs = new List<ServiceHealthCheckConfig>
        {
            new ServiceHealthCheckConfig("api-service", "http") { Path = "/health", Port = 8080 }
        };
        deployment.SetHealthCheckConfigs(healthConfigs);

        _repository.Add(deployment);
        _fixture.Context.SaveChanges();

        // Act - use fresh context for query
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new DeploymentRepository(queryContext);
        var deployments = queryRepository.GetByEnvironment(envId).ToList();

        // Assert
        deployments.Should().HaveCount(1);
        deployments[0].HealthCheckConfigs.Should().HaveCount(1);
        deployments[0].HealthCheckConfigs.First().ServiceName.Should().Be("api-service");
    }

    [Fact]
    public void Get_ShouldReturnDeployment_WithAllRelatedData()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var deploymentId = DeploymentId.Create();
        var userId = new UserId(Guid.NewGuid());

        var deployment = Deployment.StartInstallation(
            deploymentId,
            envId,
            "stack-123",
            "test-stack",
            "rsgo-test-stack",
            userId
        );
        deployment.SetStackVersion("2.0.0");

        var variables = new Dictionary<string, string> { { "KEY", "value" } };
        deployment.SetVariables(variables);

        var healthConfigs = new List<ServiceHealthCheckConfig>
        {
            new ServiceHealthCheckConfig("service1", "tcp") { Port = 80 }
        };
        deployment.SetHealthCheckConfigs(healthConfigs);

        _repository.Add(deployment);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new DeploymentRepository(queryContext);
        var result = queryRepository.Get(deploymentId);

        // Assert
        result.Should().NotBeNull();
        result!.StackVersion.Should().Be("2.0.0");
        result.Variables.Should().ContainKey("KEY");
        result.HealthCheckConfigs.Should().HaveCount(1);
        result.HealthCheckConfigs.First().Type.Should().Be("tcp");
    }

    [Fact]
    public void Update_ShouldPersistHealthCheckConfigChanges()
    {
        // Arrange
        var (_, envId) = CreateTestEnvironment();
        var deploymentId = DeploymentId.Create();
        var userId = new UserId(Guid.NewGuid());

        var deployment = Deployment.StartInstallation(
            deploymentId,
            envId,
            "stack-123",
            "test-stack",
            "rsgo-test-stack",
            userId
        );

        var initialConfigs = new List<ServiceHealthCheckConfig>
        {
            new ServiceHealthCheckConfig("old-service", "docker")
        };
        deployment.SetHealthCheckConfigs(initialConfigs);

        _repository.Add(deployment);
        _fixture.Context.SaveChanges();

        // Act - update health configs
        var newConfigs = new List<ServiceHealthCheckConfig>
        {
            new ServiceHealthCheckConfig("new-service", "http") { Path = "/new", Port = 8080 }
        };
        deployment.SetHealthCheckConfigs(newConfigs);
        _repository.Update(deployment);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var updated = verifyContext.Deployments.Find(deploymentId);

        updated.Should().NotBeNull();
        updated!.HealthCheckConfigs.Should().HaveCount(1);
        updated.HealthCheckConfigs.First().ServiceName.Should().Be("new-service");
        updated.HealthCheckConfigs.First().Path.Should().Be("/new");
    }
}
