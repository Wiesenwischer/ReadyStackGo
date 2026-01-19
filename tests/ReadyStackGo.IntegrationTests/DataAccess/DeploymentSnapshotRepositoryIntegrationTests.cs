namespace ReadyStackGo.IntegrationTests.DataAccess;

using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Infrastructure.DataAccess.Repositories;
using ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for PendingUpgradeSnapshot persistence with real SQLite database.
/// Tests EF Core owned entity configuration, JSON serialization, and repository methods.
/// </summary>
public class DeploymentSnapshotRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestFixture _fixture;
    private readonly DeploymentRepository _repository;
    private readonly EnvironmentId _envId = EnvironmentId.NewId();
    private readonly UserId _userId = UserId.NewId();

    public DeploymentSnapshotRepositoryIntegrationTests()
    {
        _fixture = new SqliteTestFixture();
        _repository = new DeploymentRepository(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private Deployment CreateRunningDeployment(string stackName = "test-stack")
    {
        var deployment = Deployment.Start(
            _repository.NextIdentity(),
            _envId,
            stackName,
            stackName,
            stackName,
            _userId);

        deployment.SetStackVersion("1.0.0");
        deployment.SetVariables(new Dictionary<string, string>
        {
            ["DB_HOST"] = "localhost",
            ["DB_PORT"] = "5432"
        });
        deployment.MarkAsRunning(new[]
        {
            new DeployedService("db", "c1", "db-1", "postgres:15", "running"),
            new DeployedService("api", "c2", "api-1", "myapp/api:1.0.0", "running")
        });

        return deployment;
    }

    #region Snapshot Persistence Tests

    [Fact]
    public void CreateSnapshot_ShouldPersistPendingUpgradeSnapshot()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        var snapshot = deployment.CreateSnapshot("Before upgrade to v2.0");
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert - use fresh context
        using var verifyContext = _fixture.CreateNewContext();
        var persistedDeployment = verifyContext.Deployments
            .First(d => d.Id == deployment.Id);

        persistedDeployment.PendingUpgradeSnapshot.Should().NotBeNull();
        persistedDeployment.PendingUpgradeSnapshot!.StackVersion.Should().Be("1.0.0");
        persistedDeployment.PendingUpgradeSnapshot.Description.Should().Be("Before upgrade to v2.0");
    }

    [Fact]
    public void CreateSnapshot_ShouldPersistVariablesAsJson()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.SetVariables(new Dictionary<string, string>
        {
            ["CONN_STRING"] = "Server=sql;Database=db;User=sa;Password=P@ss",
            ["API_KEY"] = "secret-key-123"
        });
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        deployment.CreateSnapshot();
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persistedDeployment = verifyContext.Deployments
            .First(d => d.Id == deployment.Id);

        var snapshot = persistedDeployment.PendingUpgradeSnapshot!;
        snapshot.Variables.Should().HaveCount(2);
        snapshot.Variables["CONN_STRING"].Should().Be("Server=sql;Database=db;User=sa;Password=P@ss");
        snapshot.Variables["API_KEY"].Should().Be("secret-key-123");
    }

    [Fact]
    public void CreateSnapshot_ShouldPersistServicesAsJson()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        deployment.CreateSnapshot();
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persistedDeployment = verifyContext.Deployments
            .First(d => d.Id == deployment.Id);

        var snapshot = persistedDeployment.PendingUpgradeSnapshot!;
        snapshot.Services.Should().HaveCount(2);
        snapshot.Services.Should().Contain(s => s.Name == "db" && s.Image == "postgres:15");
        snapshot.Services.Should().Contain(s => s.Name == "api" && s.Image == "myapp/api:1.0.0");
    }

    #endregion

    #region ClearSnapshot Tests

    [Fact]
    public void ClearSnapshot_ShouldRemovePendingUpgradeSnapshot()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        deployment.CreateSnapshot("Before upgrade");
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Verify snapshot exists
        using (var checkContext = _fixture.CreateNewContext())
        {
            var check = checkContext.Deployments.First(d => d.Id == deployment.Id);
            check.PendingUpgradeSnapshot.Should().NotBeNull();
        }

        // Act
        deployment.ClearSnapshot();
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persistedDeployment = verifyContext.Deployments
            .First(d => d.Id == deployment.Id);

        persistedDeployment.PendingUpgradeSnapshot.Should().BeNull();
    }

    #endregion

    #region GetById Tests

    [Fact]
    public void GetById_ShouldLoadPendingUpgradeSnapshot()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        deployment.CreateSnapshot("Test snapshot");
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Clear tracking to force fresh load
        _fixture.Context.ChangeTracker.Clear();

        // Act
        var loaded = _repository.GetById(deployment.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.PendingUpgradeSnapshot.Should().NotBeNull();
        loaded.PendingUpgradeSnapshot!.Description.Should().Be("Test snapshot");
    }

    [Fact]
    public void GetById_ShouldReturnNullForNonExistentDeployment()
    {
        // Act
        var result = _repository.GetById(DeploymentId.Create());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetById_ShouldReturnDeploymentWithNullSnapshot()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Clear tracking
        _fixture.Context.ChangeTracker.Clear();

        // Act
        var loaded = _repository.GetById(deployment.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.PendingUpgradeSnapshot.Should().BeNull();
    }

    #endregion

    #region GetWithSnapshots (Backwards Compatibility) Tests

    [Fact]
    public void GetWithSnapshots_ShouldLoadPendingUpgradeSnapshot()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        deployment.CreateSnapshot("Test snapshot");
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Clear tracking
        _fixture.Context.ChangeTracker.Clear();

        // Act
        var loaded = _repository.GetWithSnapshots(deployment.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.PendingUpgradeSnapshot.Should().NotBeNull();
        loaded.PendingUpgradeSnapshot!.Description.Should().Be("Test snapshot");
    }

    #endregion

    #region Cascade Delete Tests

    [Fact]
    public void RemoveDeployment_ShouldDeletePendingUpgradeSnapshot()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        deployment.CreateSnapshot("Snapshot to be deleted");
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Act
        _repository.Remove(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        verifyContext.Deployments.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CreateSnapshot_ShouldHandleEmptyVariables()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.SetVariables(new Dictionary<string, string>()); // Empty
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        deployment.CreateSnapshot();
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persistedDeployment = verifyContext.Deployments
            .First(d => d.Id == deployment.Id);

        persistedDeployment.PendingUpgradeSnapshot!.Variables.Should().BeEmpty();
    }

    [Fact]
    public void CreateSnapshot_ShouldHandleSpecialCharactersInVariables()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        deployment.SetVariables(new Dictionary<string, string>
        {
            ["CONN"] = "Server=sql;Password=P@ss\"'<>&{}[];",
            ["JSON_VALUE"] = "{\"key\": \"value\", \"nested\": {\"a\": 1}}"
        });
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        deployment.CreateSnapshot();
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var snapshot = verifyContext.Deployments
            .First(d => d.Id == deployment.Id)
            .PendingUpgradeSnapshot!;

        snapshot.Variables["CONN"].Should().Be("Server=sql;Password=P@ss\"'<>&{}[];");
        snapshot.Variables["JSON_VALUE"].Should().Be("{\"key\": \"value\", \"nested\": {\"a\": 1}}");
    }

    [Fact]
    public void CreateSnapshot_ShouldHandleNullDescription()
    {
        // Arrange
        var deployment = CreateRunningDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        deployment.CreateSnapshot(description: null);
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var snapshot = verifyContext.Deployments
            .First(d => d.Id == deployment.Id)
            .PendingUpgradeSnapshot!;

        snapshot.Description.Should().BeNull();
    }

    [Fact]
    public void CreateSnapshot_ShouldHandleServiceWithNullImage()
    {
        // Arrange
        var deployment = Deployment.Start(
            _repository.NextIdentity(),
            _envId,
            "test",
            "test",
            "test",
            _userId);

        deployment.SetStackVersion("1.0.0");
        deployment.MarkAsRunning(new[]
        {
            new DeployedService("service-no-image", "c1", "container-1", null, "running")
        });
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        deployment.CreateSnapshot();
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var snapshot = verifyContext.Deployments
            .First(d => d.Id == deployment.Id)
            .PendingUpgradeSnapshot!;

        snapshot.Services.First().Image.Should().Be("unknown"); // Fallback value
    }

    #endregion

    #region NextSnapshotIdentity Tests

    [Fact]
    public void NextSnapshotIdentity_ShouldReturnUniqueIds()
    {
        // Act
        var id1 = _repository.NextSnapshotIdentity();
        var id2 = _repository.NextSnapshotIdentity();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(id2.Value);
    }

    #endregion
}
