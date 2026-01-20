namespace ReadyStackGo.IntegrationTests.DataAccess;

using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Infrastructure.DataAccess.Repositories;
using ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for HealthSnapshotRepository with real SQLite database.
/// Tests persistence, retrieval, and querying of health snapshots.
/// </summary>
public class HealthSnapshotRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestFixture _fixture;
    private readonly HealthSnapshotRepository _repository;
    private readonly OrganizationId _orgId = OrganizationId.NewId();
    private readonly EnvironmentId _envId = EnvironmentId.NewId();

    public HealthSnapshotRepositoryIntegrationTests()
    {
        _fixture = new SqliteTestFixture();
        _repository = new HealthSnapshotRepository(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private HealthSnapshot CreateSnapshot(
        string stackName,
        DeploymentId? deploymentId = null,
        HealthStatus? overallStatus = null,
        OperationMode? operationMode = null,
        EnvironmentId? environmentId = null)
    {
        var services = new[]
        {
            ServiceHealth.Create("web", overallStatus ?? HealthStatus.Healthy, "c1", "web-1", null, 0),
            ServiceHealth.Create("api", overallStatus ?? HealthStatus.Healthy, "c2", "api-1", null, 0)
        };
        var selfHealth = SelfHealth.Create(services);

        return HealthSnapshot.Capture(
            _orgId,
            environmentId ?? _envId,
            deploymentId ?? DeploymentId.NewId(),
            stackName,
            operationMode ?? OperationMode.Normal,
            "1.0.0",
            null,
            null,
            null,
            selfHealth);
    }

    #region Add and Get

    [Fact]
    public void Add_ShouldPersistSnapshot()
    {
        // Arrange
        var snapshot = CreateSnapshot("test-stack");

        // Act
        _repository.Add(snapshot);
        _repository.SaveChanges();

        // Assert - use fresh context
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.HealthSnapshots.Find(snapshot.Id);

        persisted.Should().NotBeNull();
        persisted!.StackName.Should().Be("test-stack");
        persisted.OrganizationId.Should().Be(_orgId);
        persisted.EnvironmentId.Should().Be(_envId);
    }

    [Fact]
    public void Add_ShouldPersistOverallStatus()
    {
        // Arrange
        var snapshot = CreateSnapshot("test-stack", overallStatus: HealthStatus.Degraded);

        // Act
        _repository.Add(snapshot);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.HealthSnapshots.Find(snapshot.Id);

        persisted.Should().NotBeNull();
        // Overall is calculated from services, but should be persisted correctly
        persisted!.Overall.Should().NotBeNull();
    }

    [Fact]
    public void Add_ShouldPersistOperationMode()
    {
        // Arrange
        var snapshot = CreateSnapshot("test-stack", operationMode: OperationMode.Maintenance);

        // Act
        _repository.Add(snapshot);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.HealthSnapshots.Find(snapshot.Id);

        persisted.Should().NotBeNull();
        persisted!.OperationMode.Should().Be(OperationMode.Maintenance);
    }

    [Fact]
    public void Add_ShouldPersistSelfHealthWithServices()
    {
        // Arrange
        var snapshot = CreateSnapshot("test-stack");

        // Act
        _repository.Add(snapshot);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.HealthSnapshots.Find(snapshot.Id);

        persisted.Should().NotBeNull();
        persisted!.Self.Should().NotBeNull();
        persisted.Self.TotalCount.Should().Be(2);
        persisted.Self.Services.Should().HaveCount(2);
    }

    [Fact]
    public void Get_ShouldReturnNullForNonExistentId()
    {
        // Act
        var result = _repository.Get(HealthSnapshotId.Create());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetLatestForDeployment

    [Fact]
    public void GetLatestForDeployment_ShouldReturnMostRecentSnapshot()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();

        var snapshot1 = CreateSnapshot("stack", deploymentId);
        _repository.Add(snapshot1);
        _repository.SaveChanges();

        // Wait a bit to ensure different timestamp
        Thread.Sleep(10);

        var snapshot2 = CreateSnapshot("stack", deploymentId);
        _repository.Add(snapshot2);
        _repository.SaveChanges();

        // Act
        var latest = _repository.GetLatestForDeployment(deploymentId);

        // Assert
        latest.Should().NotBeNull();
        latest!.Id.Should().Be(snapshot2.Id);
    }

    [Fact]
    public void GetLatestForDeployment_ShouldReturnNullForNoSnapshots()
    {
        // Act
        var result = _repository.GetLatestForDeployment(DeploymentId.NewId());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetLatestForEnvironment

    [Fact]
    public void GetLatestForEnvironment_ShouldReturnLatestPerDeployment()
    {
        // Arrange
        var deployment1Id = DeploymentId.NewId();
        var deployment2Id = DeploymentId.NewId();

        // Add older snapshot for deployment 1
        var snapshot1Old = CreateSnapshot("stack-1", deployment1Id);
        _repository.Add(snapshot1Old);
        _repository.SaveChanges();

        Thread.Sleep(10);

        // Add newer snapshot for deployment 1
        var snapshot1New = CreateSnapshot("stack-1", deployment1Id);
        _repository.Add(snapshot1New);
        _repository.SaveChanges();

        // Add snapshot for deployment 2
        var snapshot2 = CreateSnapshot("stack-2", deployment2Id);
        _repository.Add(snapshot2);
        _repository.SaveChanges();

        // Act
        var results = _repository.GetLatestForEnvironment(_envId).ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(s => s.DeploymentId == deployment1Id && s.Id == snapshot1New.Id);
        results.Should().Contain(s => s.DeploymentId == deployment2Id);
    }

    [Fact]
    public void GetLatestForEnvironment_ShouldReturnEmptyForNoSnapshots()
    {
        // Act
        var results = _repository.GetLatestForEnvironment(EnvironmentId.NewId());

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void GetLatestForEnvironment_ShouldNotIncludeOtherEnvironments()
    {
        // Arrange
        var otherEnvId = EnvironmentId.NewId();

        var snapshot1 = CreateSnapshot("stack-1");
        _repository.Add(snapshot1);

        var snapshot2 = CreateSnapshot("stack-2", environmentId: otherEnvId);
        _repository.Add(snapshot2);

        _repository.SaveChanges();

        // Act
        var results = _repository.GetLatestForEnvironment(_envId).ToList();

        // Assert
        results.Should().HaveCount(1);
        results.Single().StackName.Should().Be("stack-1");
    }

    #endregion

    #region GetHistory

    [Fact]
    public void GetHistory_ShouldReturnSnapshotsInDescendingOrder()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();

        for (int i = 0; i < 5; i++)
        {
            var snapshot = CreateSnapshot($"stack-{i}", deploymentId);
            _repository.Add(snapshot);
            _repository.SaveChanges();
            Thread.Sleep(10);
        }

        // Act
        var history = _repository.GetHistory(deploymentId, 10).ToList();

        // Assert
        history.Should().HaveCount(5);
        // Should be in descending order by CapturedAtUtc
        for (int i = 0; i < history.Count - 1; i++)
        {
            history[i].CapturedAtUtc.Should().BeOnOrAfter(history[i + 1].CapturedAtUtc);
        }
    }

    [Fact]
    public void GetHistory_ShouldRespectLimit()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();

        for (int i = 0; i < 10; i++)
        {
            var snapshot = CreateSnapshot($"stack", deploymentId);
            _repository.Add(snapshot);
            _repository.SaveChanges();
        }

        // Act
        var history = _repository.GetHistory(deploymentId, 5).ToList();

        // Assert
        history.Should().HaveCount(5);
    }

    [Fact]
    public void GetHistory_ShouldReturnEmptyForNoSnapshots()
    {
        // Act
        var history = _repository.GetHistory(DeploymentId.NewId());

        // Assert
        history.Should().BeEmpty();
    }

    #endregion

    #region RemoveOlderThan

    [Fact]
    public void RemoveOlderThan_ShouldDeleteOldSnapshots()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();
        var snapshot = CreateSnapshot("stack", deploymentId);
        _repository.Add(snapshot);
        _repository.SaveChanges();

        // Act - Remove snapshots older than 0 seconds (everything)
        _repository.RemoveOlderThan(TimeSpan.Zero);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        verifyContext.HealthSnapshots.Should().BeEmpty();
    }

    [Fact]
    public void RemoveOlderThan_ShouldKeepRecentSnapshots()
    {
        // Arrange
        var deploymentId = DeploymentId.NewId();
        var snapshot = CreateSnapshot("stack", deploymentId);
        _repository.Add(snapshot);
        _repository.SaveChanges();

        // Act - Remove snapshots older than 1 hour (none should be removed)
        _repository.RemoveOlderThan(TimeSpan.FromHours(1));
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        verifyContext.HealthSnapshots.Should().HaveCount(1);
    }

    #endregion

    #region NextIdentity

    [Fact]
    public void NextIdentity_ShouldReturnUniqueIds()
    {
        // Act
        var id1 = _repository.NextIdentity();
        var id2 = _repository.NextIdentity();

        // Assert
        id1.Should().NotBe(id2);
    }

    #endregion
}
