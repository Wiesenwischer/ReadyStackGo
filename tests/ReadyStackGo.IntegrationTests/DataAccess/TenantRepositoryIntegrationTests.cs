namespace ReadyStackGo.IntegrationTests.DataAccess;

using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Persistence.Repositories;
using ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for OrganizationRepository with real SQLite database.
/// </summary>
public class OrganizationRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestFixture _fixture;
    private readonly OrganizationRepository _repository;

    public OrganizationRepositoryIntegrationTests()
    {
        _fixture = new SqliteTestFixture();
        _repository = new OrganizationRepository(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void Add_ShouldPersistOrganization()
    {
        // Arrange
        var organization = Organization.Provision(
            OrganizationId.Create(),
            "ACME Corp",
            "Test organization"
        );

        // Act
        _repository.Add(organization);

        // Assert - use fresh context to verify persistence
        using var verifyContext = _fixture.CreateNewContext();
        var persisted = verifyContext.Organizations.Find(organization.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("ACME Corp");
        persisted.Description.Should().Be("Test organization");
        persisted.Active.Should().BeFalse();
    }

    [Fact]
    public void Get_ShouldReturnOrganization_WhenExists()
    {
        // Arrange
        var organizationId = OrganizationId.Create();
        var organization = Organization.Provision(organizationId, "Test Org", "Description");
        _repository.Add(organization);

        // Act - use fresh context
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new OrganizationRepository(queryContext);
        var result = queryRepository.Get(organizationId);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Org");
    }

    [Fact]
    public void Get_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var nonExistentId = OrganizationId.Create();

        // Act
        var result = _repository.Get(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetByName_ShouldReturnOrganization_WhenExists()
    {
        // Arrange
        var organization = Organization.Provision(OrganizationId.Create(), "UniqueOrg", "Description");
        _repository.Add(organization);

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new OrganizationRepository(queryContext);
        var result = queryRepository.GetByName("UniqueOrg");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(organization.Id);
    }

    [Fact]
    public void GetByName_ShouldReturnNull_WhenNotExists()
    {
        // Act
        var result = _repository.GetByName("NonExistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Update_ShouldPersistChanges()
    {
        // Arrange
        var organization = Organization.Provision(OrganizationId.Create(), "Original", "Original desc");
        _repository.Add(organization);

        // Act - activate and update
        organization.Activate();
        organization.UpdateDescription("Updated description");
        _fixture.Context.SaveChanges();

        // Assert - verify with fresh context
        using var verifyContext = _fixture.CreateNewContext();
        var updated = verifyContext.Organizations.Find(organization.Id);

        updated.Should().NotBeNull();
        updated!.Active.Should().BeTrue();
        updated.Description.Should().Be("Updated description");
    }

    [Fact]
    public void GetAll_ShouldReturnAllOrganizations()
    {
        // Arrange
        var org1 = Organization.Provision(OrganizationId.Create(), "Org1", "Desc1");
        var org2 = Organization.Provision(OrganizationId.Create(), "Org2", "Desc2");
        _repository.Add(org1);
        _repository.Add(org2);

        // Act
        using var queryContext = _fixture.CreateNewContext();
        var queryRepository = new OrganizationRepository(queryContext);
        var all = queryRepository.GetAll();

        // Assert
        all.Should().HaveCount(2);
        all.Select(t => t.Name).Should().Contain(["Org1", "Org2"]);
    }

    [Fact]
    public void Add_ShouldThrowOnDuplicateName()
    {
        // Arrange
        var org1 = Organization.Provision(OrganizationId.Create(), "DuplicateName", "Desc1");
        _repository.Add(org1);

        var org2 = Organization.Provision(OrganizationId.Create(), "DuplicateName", "Desc2");

        // Act & Assert
        var act = () => _repository.Add(org2);
        act.Should().Throw<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact]
    public void NextIdentity_ShouldReturnUniqueOrganizationId()
    {
        // Act
        var id1 = _repository.NextIdentity();
        var id2 = _repository.NextIdentity();

        // Assert
        id1.Should().NotBeNull();
        id2.Should().NotBeNull();
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void Remove_ShouldDeleteOrganization()
    {
        // Arrange
        var organization = Organization.Provision(OrganizationId.Create(), "ToDelete", "Description");
        _repository.Add(organization);

        // Act
        _repository.Remove(organization);

        // Assert
        using var verifyContext = _fixture.CreateNewContext();
        var deleted = verifyContext.Organizations.Find(organization.Id);
        deleted.Should().BeNull();
    }
}
