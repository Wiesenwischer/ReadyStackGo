namespace ReadyStackGo.IntegrationTests.DataAccess;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Infrastructure.DataAccess.Repositories;
using ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for deployment snapshot persistence with real SQLite database.
/// Tests EF Core configuration, JSON serialization, and repository methods.
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

    private Deployment CreateTestDeployment(string stackName = "test-stack")
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
    public void CreateSnapshot_ShouldPersistSnapshotWithDeployment()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        var snapshot = deployment.CreateSnapshot("Pre-upgrade snapshot");
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert - use fresh context
        using var verifyContext = _fixture.CreateNewContext();
        var persistedDeployment = verifyContext.Deployments
            .Include(d => d.Snapshots)
            .First(d => d.Id == deployment.Id);

        persistedDeployment.Snapshots.Should().HaveCount(1);
        var persistedSnapshot = persistedDeployment.Snapshots.First();
        persistedSnapshot.StackVersion.Should().Be("1.0.0");
        persistedSnapshot.Description.Should().Be("Pre-upgrade snapshot");
    }

    [Fact]
    public void CreateSnapshot_ShouldPersistVariablesAsJson()
    {
        // Arrange
        var deployment = CreateTestDeployment();
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
            .Include(d => d.Snapshots)
            .First(d => d.Id == deployment.Id);

        var snapshot = persistedDeployment.Snapshots.First();
        snapshot.Variables.Should().HaveCount(2);
        snapshot.Variables["CONN_STRING"].Should().Be("Server=sql;Database=db;User=sa;Password=P@ss");
        snapshot.Variables["API_KEY"].Should().Be("secret-key-123");
    }

    [Fact]
    public void CreateSnapshot_ShouldPersistServicesAsJson()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        deployment.CreateSnapshot();
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persistedDeployment = verifyContext.Deployments
            .Include(d => d.Snapshots)
            .First(d => d.Id == deployment.Id);

        var snapshot = persistedDeployment.Snapshots.First();
        snapshot.Services.Should().HaveCount(2);
        snapshot.Services.Should().Contain(s => s.Name == "db" && s.Image == "postgres:15");
        snapshot.Services.Should().Contain(s => s.Name == "api" && s.Image == "myapp/api:1.0.0");
    }

    [Fact]
    public void CreateMultipleSnapshots_ShouldPersistAll()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        deployment.CreateSnapshot("Snapshot 1");
        deployment.SetStackVersion("2.0.0");
        deployment.CreateSnapshot("Snapshot 2");
        deployment.SetStackVersion("3.0.0");
        deployment.CreateSnapshot("Snapshot 3");
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persistedDeployment = verifyContext.Deployments
            .Include(d => d.Snapshots)
            .First(d => d.Id == deployment.Id);

        persistedDeployment.Snapshots.Should().HaveCount(3);
        persistedDeployment.Snapshots.Should().Contain(s => s.Description == "Snapshot 1" && s.StackVersion == "1.0.0");
        persistedDeployment.Snapshots.Should().Contain(s => s.Description == "Snapshot 2" && s.StackVersion == "2.0.0");
        persistedDeployment.Snapshots.Should().Contain(s => s.Description == "Snapshot 3" && s.StackVersion == "3.0.0");
    }

    #endregion

    #region GetWithSnapshots Tests

    [Fact]
    public void GetWithSnapshots_ShouldLoadSnapshotsWithDeployment()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        deployment.CreateSnapshot("Test snapshot");
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Act
        var loaded = _repository.GetWithSnapshots(deployment.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Snapshots.Should().HaveCount(1);
        loaded.Snapshots.First().Description.Should().Be("Test snapshot");
    }

    [Fact]
    public void GetWithSnapshots_ShouldReturnNullForNonExistentDeployment()
    {
        // Act
        var result = _repository.GetWithSnapshots(DeploymentId.Create());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetWithSnapshots_ShouldLoadEmptySnapshotsCollection()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        var loaded = _repository.GetWithSnapshots(deployment.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Snapshots.Should().BeEmpty();
    }

    #endregion

    #region GetWithSnapshotsByStackName Tests

    [Fact]
    public void GetWithSnapshotsByStackName_ShouldLoadSnapshotsForStackName()
    {
        // Arrange
        var deployment = CreateTestDeployment("my-app");
        _repository.Add(deployment);
        _repository.SaveChanges();

        deployment.CreateSnapshot();
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Act
        var loaded = _repository.GetWithSnapshotsByStackName(_envId, "my-app");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.StackName.Should().Be("my-app");
        loaded.Snapshots.Should().HaveCount(1);
    }

    [Fact]
    public void GetWithSnapshotsByStackName_ShouldReturnNullForNonExistentStack()
    {
        // Act
        var result = _repository.GetWithSnapshotsByStackName(_envId, "nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetWithSnapshotsByStackName_ShouldNotReturnRemovedDeployments()
    {
        // Arrange
        var deployment = CreateTestDeployment("my-app");
        _repository.Add(deployment);
        _repository.SaveChanges();

        deployment.CreateSnapshot();
        deployment.MarkAsRemoved();
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Act
        var result = _repository.GetWithSnapshotsByStackName(_envId, "my-app");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetWithSnapshotsByStackName_ShouldReturnMostRecentNonRemovedDeployment()
    {
        // Arrange
        var deployment1 = CreateTestDeployment("my-app");
        deployment1.SetStackVersion("1.0.0");
        _repository.Add(deployment1);
        _repository.SaveChanges();

        Thread.Sleep(10); // Ensure different CreatedAt

        var deployment2 = CreateTestDeployment("my-app");
        deployment2.SetStackVersion("2.0.0");
        _repository.Add(deployment2);
        _repository.SaveChanges();

        // Act
        var loaded = _repository.GetWithSnapshotsByStackName(_envId, "my-app");

        // Assert
        loaded.Should().NotBeNull();
        loaded!.StackVersion.Should().Be("2.0.0");
    }

    #endregion

    #region Snapshot Cascade Delete Tests

    [Fact]
    public void RemoveDeployment_ShouldDeleteAllSnapshots()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        deployment.CreateSnapshot("Snapshot 1");
        deployment.SetStackVersion("2.0.0");
        deployment.CreateSnapshot("Snapshot 2");
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Act
        _repository.Remove(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        verifyContext.Deployments.Should().BeEmpty();
        verifyContext.DeploymentSnapshots.Should().BeEmpty();
    }

    #endregion

    #region Snapshot with Edge Cases

    [Fact]
    public void CreateSnapshot_ShouldHandleEmptyVariables()
    {
        // Arrange
        var deployment = CreateTestDeployment();
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
            .Include(d => d.Snapshots)
            .First(d => d.Id == deployment.Id);

        persistedDeployment.Snapshots.First().Variables.Should().BeEmpty();
    }

    [Fact]
    public void CreateSnapshot_ShouldHandleSpecialCharactersInVariables()
    {
        // Arrange
        var deployment = CreateTestDeployment();
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
            .Include(d => d.Snapshots)
            .First(d => d.Id == deployment.Id)
            .Snapshots.First();

        snapshot.Variables["CONN"].Should().Be("Server=sql;Password=P@ss\"'<>&{}[];");
        snapshot.Variables["JSON_VALUE"].Should().Be("{\"key\": \"value\", \"nested\": {\"a\": 1}}");
    }

    [Fact]
    public void CreateSnapshot_ShouldHandleNullDescription()
    {
        // Arrange
        var deployment = CreateTestDeployment();
        _repository.Add(deployment);
        _repository.SaveChanges();

        // Act
        deployment.CreateSnapshot(description: null);
        _repository.Update(deployment);
        _repository.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var snapshot = verifyContext.Deployments
            .Include(d => d.Snapshots)
            .First(d => d.Id == deployment.Id)
            .Snapshots.First();

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
            .Include(d => d.Snapshots)
            .First(d => d.Id == deployment.Id)
            .Snapshots.First();

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
