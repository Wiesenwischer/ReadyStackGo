namespace ReadyStackGo.IntegrationTests.DataAccess;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Infrastructure.DataAccess.Repositories;
using ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for ApiKeyRepository with real SQLite database.
/// Tests verify that the entity configuration works correctly with SQLite,
/// especially the JSON column for Permissions.
/// </summary>
public class ApiKeyRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestFixture _fixture;
    private readonly ApiKeyRepository _repository;
    private readonly OrganizationId _testOrgId;

    private const string ValidKeyHash = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";
    private const string ValidKeyPrefix = "rsgo_a1b2c3d";

    public ApiKeyRepositoryIntegrationTests()
    {
        _fixture = new SqliteTestFixture();
        _repository = new ApiKeyRepository(_fixture.Context);
        _testOrgId = new OrganizationId(Guid.NewGuid());
    }

    public void Dispose() => _fixture.Dispose();

    private ApiKey CreateTestApiKey(
        string name = "CI Pipeline",
        string? keyHash = null,
        string keyPrefix = ValidKeyPrefix,
        IEnumerable<string>? permissions = null,
        Guid? environmentId = null,
        DateTime? expiresAt = null)
    {
        return ApiKey.Create(
            ApiKeyId.Create(),
            _testOrgId,
            name,
            keyHash ?? GenerateUniqueHash(),
            keyPrefix,
            permissions ?? new[] { "Hooks.Redeploy" },
            environmentId,
            expiresAt);
    }

    private static string GenerateUniqueHash()
    {
        return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }

    #region Add Tests

    [Fact]
    public void Add_ShouldPersistApiKey_WithAllProperties()
    {
        // Arrange
        var apiKeyId = ApiKeyId.Create();
        var envId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(30);
        var permissions = new[] { "Hooks.Redeploy", "Hooks.Upgrade", "Hooks.SyncSources" };
        var apiKey = ApiKey.Create(
            apiKeyId, _testOrgId, "Full Key", ValidKeyHash, ValidKeyPrefix,
            permissions, envId, expiresAt);

        // Act
        _repository.Add(apiKey);
        _fixture.Context.SaveChanges();

        // Assert - use fresh context to verify persistence
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ApiKeys.Find(apiKeyId);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Full Key");
        persisted.KeyHash.Should().Be(ValidKeyHash);
        persisted.KeyPrefix.Should().Be(ValidKeyPrefix);
        persisted.OrganizationId.Should().Be(_testOrgId);
        persisted.EnvironmentId.Should().Be(envId);
        persisted.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
        persisted.IsRevoked.Should().BeFalse();
        persisted.Permissions.Should().HaveCount(3);
        persisted.Permissions.Should().Contain("Hooks.Redeploy");
        persisted.Permissions.Should().Contain("Hooks.Upgrade");
        persisted.Permissions.Should().Contain("Hooks.SyncSources");
    }

    [Fact]
    public void Add_ShouldPersistApiKey_WithNullableFieldsNull()
    {
        // Arrange
        var apiKey = CreateTestApiKey();

        // Act
        _repository.Add(apiKey);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ApiKeys.Find(apiKey.Id);

        persisted.Should().NotBeNull();
        persisted!.EnvironmentId.Should().BeNull();
        persisted.ExpiresAt.Should().BeNull();
        persisted.LastUsedAt.Should().BeNull();
        persisted.RevokedAt.Should().BeNull();
        persisted.RevokedReason.Should().BeNull();
    }

    [Fact]
    public void Add_ShouldPersistPermissionsAsJson()
    {
        // Arrange
        var permissions = new[] { "Hooks.Redeploy", "Hooks.Upgrade" };
        var apiKey = CreateTestApiKey(permissions: permissions);

        // Act
        _repository.Add(apiKey);
        _fixture.Context.SaveChanges();

        // Assert - verify with fresh context that JSON deserialization works
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ApiKeys.Find(apiKey.Id);

        persisted!.Permissions.Should().HaveCount(2);
        persisted.Permissions.Should().Contain("Hooks.Redeploy");
        persisted.Permissions.Should().Contain("Hooks.Upgrade");
    }

    #endregion

    #region GetById Tests

    [Fact]
    public void GetById_ExistingKey_ReturnsApiKey()
    {
        // Arrange
        var apiKey = CreateTestApiKey();
        _repository.Add(apiKey);
        _fixture.Context.SaveChanges();

        // Act
        var result = _repository.GetById(apiKey.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(apiKey.Id);
        result.Name.Should().Be(apiKey.Name);
    }

    [Fact]
    public void GetById_NonExistingKey_ReturnsNull()
    {
        // Act
        var result = _repository.GetById(ApiKeyId.Create());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByKeyHash Tests

    [Fact]
    public void GetByKeyHash_ExistingHash_ReturnsApiKey()
    {
        // Arrange
        var uniqueHash = GenerateUniqueHash();
        var apiKey = CreateTestApiKey(keyHash: uniqueHash);
        _repository.Add(apiKey);
        _fixture.Context.SaveChanges();

        // Act
        var result = _repository.GetByKeyHash(uniqueHash);

        // Assert
        result.Should().NotBeNull();
        result!.KeyHash.Should().Be(uniqueHash);
        result.Id.Should().Be(apiKey.Id);
    }

    [Fact]
    public void GetByKeyHash_NonExistingHash_ReturnsNull()
    {
        // Act
        var result = _repository.GetByKeyHash("0000000000000000000000000000000000000000000000000000000000000000");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByOrganization Tests

    [Fact]
    public void GetByOrganization_ReturnsKeysForOrganization()
    {
        // Arrange
        var key1 = CreateTestApiKey(name: "Key Alpha");
        var key2 = CreateTestApiKey(name: "Key Beta");
        _repository.Add(key1);
        _repository.Add(key2);
        _fixture.Context.SaveChanges();

        // Act
        var result = _repository.GetByOrganization(_testOrgId).ToList();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetByOrganization_ReturnsOrderedByName()
    {
        // Arrange
        var keyC = CreateTestApiKey(name: "Charlie");
        var keyA = CreateTestApiKey(name: "Alpha");
        var keyB = CreateTestApiKey(name: "Bravo");
        _repository.Add(keyC);
        _repository.Add(keyA);
        _repository.Add(keyB);
        _fixture.Context.SaveChanges();

        // Act
        var result = _repository.GetByOrganization(_testOrgId).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Alpha");
        result[1].Name.Should().Be("Bravo");
        result[2].Name.Should().Be("Charlie");
    }

    [Fact]
    public void GetByOrganization_IsolatesBetweenOrganizations()
    {
        // Arrange
        var otherOrgId = new OrganizationId(Guid.NewGuid());
        var key1 = CreateTestApiKey(name: "Org1 Key");
        var key2 = ApiKey.Create(
            ApiKeyId.Create(), otherOrgId, "Org2 Key", GenerateUniqueHash(),
            ValidKeyPrefix, new[] { "Hooks.Redeploy" });
        _repository.Add(key1);
        _repository.Add(key2);
        _fixture.Context.SaveChanges();

        // Act
        var result = _repository.GetByOrganization(_testOrgId).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Org1 Key");
    }

    [Fact]
    public void GetByOrganization_EmptyOrganization_ReturnsEmptyList()
    {
        // Act
        var result = _repository.GetByOrganization(new OrganizationId(Guid.NewGuid())).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_RecordUsage_PersistsLastUsedAt()
    {
        // Arrange
        var apiKey = CreateTestApiKey();
        _repository.Add(apiKey);
        _fixture.Context.SaveChanges();

        // Act
        apiKey.RecordUsage();
        _repository.Update(apiKey);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ApiKeys.Find(apiKey.Id);

        persisted!.LastUsedAt.Should().NotBeNull();
        persisted.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Update_Revoke_PersistsRevocationFields()
    {
        // Arrange
        var apiKey = CreateTestApiKey();
        _repository.Add(apiKey);
        _fixture.Context.SaveChanges();

        // Act
        apiKey.Revoke("Security audit");
        _repository.Update(apiKey);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ApiKeys.Find(apiKey.Id);

        persisted!.IsRevoked.Should().BeTrue();
        persisted.RevokedAt.Should().NotBeNull();
        persisted.RevokedReason.Should().Be("Security audit");
    }

    #endregion

    #region Remove Tests

    [Fact]
    public void Remove_DeletesApiKey()
    {
        // Arrange
        var apiKey = CreateTestApiKey();
        _repository.Add(apiKey);
        _fixture.Context.SaveChanges();

        // Act
        _repository.Remove(apiKey);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.ApiKeys.Find(apiKey.Id);
        persisted.Should().BeNull();
    }

    [Fact]
    public void Remove_DoesNotAffectOtherKeys()
    {
        // Arrange
        var key1 = CreateTestApiKey(name: "Key to Delete");
        var key2 = CreateTestApiKey(name: "Key to Keep");
        _repository.Add(key1);
        _repository.Add(key2);
        _fixture.Context.SaveChanges();

        // Act
        _repository.Remove(key1);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        verifyContext.ApiKeys.Find(key1.Id).Should().BeNull();
        verifyContext.ApiKeys.Find(key2.Id).Should().NotBeNull();
    }

    #endregion

    #region Unique Constraint Tests

    [Fact]
    public void Add_DuplicateKeyHash_ThrowsException()
    {
        // Arrange
        var sharedHash = GenerateUniqueHash();
        var key1 = CreateTestApiKey(name: "Key 1", keyHash: sharedHash);
        var key2 = CreateTestApiKey(name: "Key 2", keyHash: sharedHash);
        _repository.Add(key1);
        _fixture.Context.SaveChanges();

        // Act
        _repository.Add(key2);
        var act = () => _fixture.Context.SaveChanges();

        // Assert
        act.Should().Throw<DbUpdateException>();
    }

    [Fact]
    public void Add_DuplicateOrgAndName_ThrowsException()
    {
        // Arrange
        var key1 = CreateTestApiKey(name: "Same Name");
        var key2 = CreateTestApiKey(name: "Same Name");
        _repository.Add(key1);
        _fixture.Context.SaveChanges();

        // Act
        _repository.Add(key2);
        var act = () => _fixture.Context.SaveChanges();

        // Assert
        act.Should().Throw<DbUpdateException>();
    }

    [Fact]
    public void Add_SameNameDifferentOrg_Succeeds()
    {
        // Arrange
        var otherOrgId = new OrganizationId(Guid.NewGuid());
        var key1 = CreateTestApiKey(name: "Shared Name");
        var key2 = ApiKey.Create(
            ApiKeyId.Create(), otherOrgId, "Shared Name", GenerateUniqueHash(),
            ValidKeyPrefix, new[] { "Hooks.Redeploy" });
        _repository.Add(key1);
        _repository.Add(key2);

        // Act
        var act = () => _fixture.Context.SaveChanges();

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
