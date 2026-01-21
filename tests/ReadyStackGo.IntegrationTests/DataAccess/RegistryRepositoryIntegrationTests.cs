namespace ReadyStackGo.IntegrationTests.DataAccess;

using FluentAssertions;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Infrastructure.DataAccess.Repositories;
using ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for RegistryRepository with real SQLite database.
/// Tests verify that the entity configuration works correctly with SQLite,
/// especially the JSON column for ImagePatterns.
/// </summary>
public class RegistryRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestFixture _fixture;
    private readonly RegistryRepository _repository;
    private readonly OrganizationId _testOrgId;

    public RegistryRepositoryIntegrationTests()
    {
        _fixture = new SqliteTestFixture();
        _repository = new RegistryRepository(_fixture.Context);
        _testOrgId = new OrganizationId(Guid.NewGuid());
    }

    public void Dispose() => _fixture.Dispose();

    #region Add Tests

    [Fact]
    public void Add_ShouldPersistRegistry_WithAllProperties()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(
            registryId,
            _testOrgId,
            "Docker Hub",
            "https://index.docker.io/v1",
            "testuser",
            "testpassword");

        // Act
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Assert - use fresh context to verify persistence
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Registries.Find(registryId);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Docker Hub");
        persisted.Url.Should().Be("https://index.docker.io/v1");
        persisted.Username.Should().Be("testuser");
        persisted.Password.Should().Be("testpassword");
        persisted.HasCredentials.Should().BeTrue();
        persisted.IsDefault.Should().BeFalse();
        persisted.OrganizationId.Should().Be(_testOrgId);
    }

    [Fact]
    public void Add_ShouldPersistRegistry_WithoutCredentials()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(
            registryId,
            _testOrgId,
            "Public Registry",
            "https://ghcr.io");

        // Act
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Registries.Find(registryId);

        persisted.Should().NotBeNull();
        persisted!.Username.Should().BeNull();
        persisted.Password.Should().BeNull();
        persisted.HasCredentials.Should().BeFalse();
    }

    [Fact]
    public void Add_ShouldPersistRegistry_WithImagePatterns()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(
            registryId,
            _testOrgId,
            "Pattern Registry",
            "https://registry.example.com");

        registry.SetImagePatterns(new List<string> { "library/*", "myorg/**", "ghcr.io/specific" });

        // Act
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Assert - use fresh context to verify JSON persistence
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Registries.Find(registryId);

        persisted.Should().NotBeNull();
        persisted!.ImagePatterns.Should().HaveCount(3);
        persisted.ImagePatterns.Should().Contain("library/*");
        persisted.ImagePatterns.Should().Contain("myorg/**");
        persisted.ImagePatterns.Should().Contain("ghcr.io/specific");
    }

    [Fact]
    public void Add_ShouldPersistRegistry_WithEmptyImagePatterns()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(
            registryId,
            _testOrgId,
            "No Patterns Registry",
            "https://registry.example.com");
        // Don't set any patterns

        // Act
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Registries.Find(registryId);

        persisted.Should().NotBeNull();
        persisted!.ImagePatterns.Should().BeEmpty();
    }

    [Fact]
    public void Add_ShouldPersistRegistry_WithSpecialCharactersInPatterns()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(
            registryId,
            _testOrgId,
            "Special Patterns Registry",
            "https://registry.example.com");

        // Patterns with special characters that might break JSON
        registry.SetImagePatterns(new List<string>
        {
            "my-org/*",
            "ghcr.io/user_name/**",
            "registry:5000/*"
        });

        // Act
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Registries.Find(registryId);

        persisted.Should().NotBeNull();
        persisted!.ImagePatterns.Should().HaveCount(3);
        persisted.ImagePatterns.Should().Contain("my-org/*");
        persisted.ImagePatterns.Should().Contain("ghcr.io/user_name/**");
        persisted.ImagePatterns.Should().Contain("registry:5000/*");
    }

    #endregion

    #region GetById Tests

    [Fact]
    public void GetById_ShouldReturnRegistry_WhenExists()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(registryId, _testOrgId, "Test Registry", "https://test.io");
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new RegistryRepository(queryContext);
        var result = queryRepository.GetById(registryId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(registryId);
        result.Name.Should().Be("Test Registry");
    }

    [Fact]
    public void GetById_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = _repository.GetById(RegistryId.Create());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetById_ShouldReturnRegistry_WithImagePatterns()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(registryId, _testOrgId, "Test Registry", "https://test.io");
        registry.SetImagePatterns(new List<string> { "test/*", "prod/**" });
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new RegistryRepository(queryContext);
        var result = queryRepository.GetById(registryId);

        // Assert
        result.Should().NotBeNull();
        result!.ImagePatterns.Should().HaveCount(2);
        result.ImagePatterns.Should().Contain("test/*");
    }

    #endregion

    #region GetByOrganization Tests

    [Fact]
    public void GetByOrganization_ShouldReturnAllRegistries_ForOrganization()
    {
        // Arrange
        var registry1 = Registry.Create(RegistryId.Create(), _testOrgId, "Alpha Registry", "https://alpha.io");
        var registry2 = Registry.Create(RegistryId.Create(), _testOrgId, "Beta Registry", "https://beta.io");
        var registry3 = Registry.Create(RegistryId.Create(), _testOrgId, "Gamma Registry", "https://gamma.io");

        _repository.Add(registry1);
        _repository.Add(registry2);
        _repository.Add(registry3);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new RegistryRepository(queryContext);
        var result = queryRepository.GetByOrganization(_testOrgId).ToList();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void GetByOrganization_ShouldReturnOrderedByName()
    {
        // Arrange
        var registry1 = Registry.Create(RegistryId.Create(), _testOrgId, "Zebra Registry", "https://z.io");
        var registry2 = Registry.Create(RegistryId.Create(), _testOrgId, "Alpha Registry", "https://a.io");
        var registry3 = Registry.Create(RegistryId.Create(), _testOrgId, "Middle Registry", "https://m.io");

        _repository.Add(registry1);
        _repository.Add(registry2);
        _repository.Add(registry3);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new RegistryRepository(queryContext);
        var result = queryRepository.GetByOrganization(_testOrgId).ToList();

        // Assert
        result[0].Name.Should().Be("Alpha Registry");
        result[1].Name.Should().Be("Middle Registry");
        result[2].Name.Should().Be("Zebra Registry");
    }

    [Fact]
    public void GetByOrganization_ShouldReturnEmpty_WhenNoRegistries()
    {
        // Act
        var result = _repository.GetByOrganization(_testOrgId).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetByOrganization_ShouldNotReturnRegistries_FromOtherOrganizations()
    {
        // Arrange
        var otherOrgId = new OrganizationId(Guid.NewGuid());
        var ownRegistry = Registry.Create(RegistryId.Create(), _testOrgId, "Own Registry", "https://own.io");
        var otherRegistry = Registry.Create(RegistryId.Create(), otherOrgId, "Other Registry", "https://other.io");

        _repository.Add(ownRegistry);
        _repository.Add(otherRegistry);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new RegistryRepository(queryContext);
        var result = queryRepository.GetByOrganization(_testOrgId).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Own Registry");
    }

    #endregion

    #region GetDefault Tests

    [Fact]
    public void GetDefault_ShouldReturnDefaultRegistry_WhenExists()
    {
        // Arrange
        var defaultRegistry = Registry.Create(RegistryId.Create(), _testOrgId, "Default Registry", "https://default.io");
        var nonDefaultRegistry = Registry.Create(RegistryId.Create(), _testOrgId, "Non-Default Registry", "https://other.io");

        defaultRegistry.SetAsDefault();

        _repository.Add(defaultRegistry);
        _repository.Add(nonDefaultRegistry);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new RegistryRepository(queryContext);
        var result = queryRepository.GetDefault(_testOrgId);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Default Registry");
        result.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void GetDefault_ShouldReturnNull_WhenNoDefaultExists()
    {
        // Arrange
        var registry = Registry.Create(RegistryId.Create(), _testOrgId, "Non-Default", "https://test.io");
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Act
        var result = _repository.GetDefault(_testOrgId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDefault_ShouldReturnNull_WhenNoRegistries()
    {
        // Act
        var result = _repository.GetDefault(_testOrgId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetDefault_ShouldNotReturnDefault_FromOtherOrganization()
    {
        // Arrange
        var otherOrgId = new OrganizationId(Guid.NewGuid());
        var defaultRegistry = Registry.Create(RegistryId.Create(), otherOrgId, "Other Org Default", "https://other.io");
        defaultRegistry.SetAsDefault();

        _repository.Add(defaultRegistry);
        _fixture.Context.SaveChanges();

        // Act
        var result = _repository.GetDefault(_testOrgId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region FindMatchingRegistry Tests

    [Fact]
    public void FindMatchingRegistry_ShouldReturnMatchingRegistry_ByImagePattern()
    {
        // Arrange
        var dockerHubRegistry = Registry.Create(RegistryId.Create(), _testOrgId, "Docker Hub", "https://docker.io");
        dockerHubRegistry.SetImagePatterns(new List<string> { "library/*", "nginx" });

        var ghcrRegistry = Registry.Create(RegistryId.Create(), _testOrgId, "GHCR", "https://ghcr.io");
        ghcrRegistry.SetImagePatterns(new List<string> { "ghcr.io/**" });

        _repository.Add(dockerHubRegistry);
        _repository.Add(ghcrRegistry);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new RegistryRepository(queryContext);
        var result = queryRepository.FindMatchingRegistry(_testOrgId, "ghcr.io/myorg/myimage");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("GHCR");
    }

    [Fact]
    public void FindMatchingRegistry_ShouldReturnDefaultRegistry_WhenNoPatternMatches()
    {
        // Arrange
        var specificRegistry = Registry.Create(RegistryId.Create(), _testOrgId, "Specific", "https://specific.io");
        specificRegistry.SetImagePatterns(new List<string> { "specific/*" });

        var defaultRegistry = Registry.Create(RegistryId.Create(), _testOrgId, "Default", "https://default.io");
        defaultRegistry.SetAsDefault();

        _repository.Add(specificRegistry);
        _repository.Add(defaultRegistry);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new RegistryRepository(queryContext);
        var result = queryRepository.FindMatchingRegistry(_testOrgId, "unmatched/image");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Default");
    }

    [Fact]
    public void FindMatchingRegistry_ShouldReturnNull_WhenNoMatchAndNoDefault()
    {
        // Arrange
        var specificRegistry = Registry.Create(RegistryId.Create(), _testOrgId, "Specific", "https://specific.io");
        specificRegistry.SetImagePatterns(new List<string> { "specific/*" });

        _repository.Add(specificRegistry);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new RegistryRepository(queryContext);
        var result = queryRepository.FindMatchingRegistry(_testOrgId, "unmatched/image");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FindMatchingRegistry_ShouldPreferPatternMatch_OverDefault()
    {
        // Arrange
        var defaultRegistry = Registry.Create(RegistryId.Create(), _testOrgId, "Default", "https://default.io");
        defaultRegistry.SetAsDefault();
        defaultRegistry.SetImagePatterns(new List<string> { "default/*" });

        var matchingRegistry = Registry.Create(RegistryId.Create(), _testOrgId, "Matching", "https://matching.io");
        matchingRegistry.SetImagePatterns(new List<string> { "specific/*" });

        _repository.Add(defaultRegistry);
        _repository.Add(matchingRegistry);
        _fixture.Context.SaveChanges();

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new RegistryRepository(queryContext);
        var result = queryRepository.FindMatchingRegistry(_testOrgId, "specific/image");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Matching");
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_ShouldPersistChanges()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(registryId, _testOrgId, "Original Name", "https://original.io");
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Act
        registry.UpdateName("Updated Name");
        registry.UpdateUrl("https://updated.io");
        _repository.Update(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var updated = verifyContext.Registries.Find(registryId);

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Name");
        updated.Url.Should().Be("https://updated.io");
    }

    [Fact]
    public void Update_ShouldPersistImagePatternChanges()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(registryId, _testOrgId, "Test", "https://test.io");
        registry.SetImagePatterns(new List<string> { "old/*" });
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Act
        registry.SetImagePatterns(new List<string> { "new/*", "another/**" });
        _repository.Update(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var updated = verifyContext.Registries.Find(registryId);

        updated.Should().NotBeNull();
        updated!.ImagePatterns.Should().HaveCount(2);
        updated.ImagePatterns.Should().Contain("new/*");
        updated.ImagePatterns.Should().Contain("another/**");
        updated.ImagePatterns.Should().NotContain("old/*");
    }

    [Fact]
    public void Update_ShouldPersistCredentialChanges()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(registryId, _testOrgId, "Test", "https://test.io");
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Act
        registry.UpdateCredentials("newuser", "newpassword");
        _repository.Update(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var updated = verifyContext.Registries.Find(registryId);

        updated.Should().NotBeNull();
        updated!.Username.Should().Be("newuser");
        updated.Password.Should().Be("newpassword");
        updated.HasCredentials.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldPersistClearingCredentials()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(registryId, _testOrgId, "Test", "https://test.io", "user", "pass");
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Act
        registry.UpdateCredentials(null, null);
        _repository.Update(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var updated = verifyContext.Registries.Find(registryId);

        updated.Should().NotBeNull();
        updated!.Username.Should().BeNull();
        updated.Password.Should().BeNull();
        updated.HasCredentials.Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldPersistDefaultStatus()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(registryId, _testOrgId, "Test", "https://test.io");
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Act
        registry.SetAsDefault();
        _repository.Update(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var updated = verifyContext.Registries.Find(registryId);

        updated.Should().NotBeNull();
        updated!.IsDefault.Should().BeTrue();
    }

    #endregion

    #region Remove Tests

    [Fact]
    public void Remove_ShouldDeleteRegistry()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(registryId, _testOrgId, "To Be Deleted", "https://delete.io");
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Act
        _repository.Remove(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var deleted = verifyContext.Registries.Find(registryId);

        deleted.Should().BeNull();
    }

    [Fact]
    public void Remove_ShouldNotAffectOtherRegistries()
    {
        // Arrange
        var registryToDelete = Registry.Create(RegistryId.Create(), _testOrgId, "Delete Me", "https://delete.io");
        var registryToKeep = Registry.Create(RegistryId.Create(), _testOrgId, "Keep Me", "https://keep.io");

        _repository.Add(registryToDelete);
        _repository.Add(registryToKeep);
        _fixture.Context.SaveChanges();

        // Act
        _repository.Remove(registryToDelete);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var remaining = verifyContext.Registries.ToList();

        remaining.Should().HaveCount(1);
        remaining[0].Name.Should().Be("Keep Me");
    }

    #endregion

    #region Timestamps Tests

    [Fact]
    public void Add_ShouldSetCreatedAt()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var beforeCreate = DateTime.UtcNow;
        var registry = Registry.Create(registryId, _testOrgId, "Test", "https://test.io");

        // Act
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Registries.Find(registryId);

        persisted.Should().NotBeNull();
        persisted!.CreatedAt.Should().BeOnOrAfter(beforeCreate);
        persisted.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldSetUpdatedAt()
    {
        // Arrange
        var registryId = RegistryId.Create();
        var registry = Registry.Create(registryId, _testOrgId, "Test", "https://test.io");
        _repository.Add(registry);
        _fixture.Context.SaveChanges();

        // Act
        var beforeUpdate = DateTime.UtcNow;
        registry.UpdateName("Updated");
        _repository.Update(registry);
        _fixture.Context.SaveChanges();

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var updated = verifyContext.Registries.Find(registryId);

        updated.Should().NotBeNull();
        updated!.UpdatedAt.Should().NotBeNull();
        updated.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
    }

    #endregion
}
