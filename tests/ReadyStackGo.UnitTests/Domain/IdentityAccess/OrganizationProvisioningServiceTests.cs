using FluentAssertions;
using Moq;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for OrganizationProvisioningService domain service.
/// </summary>
public class OrganizationProvisioningServiceTests
{
    private readonly Mock<IOrganizationRepository> _organizationRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly OrganizationProvisioningService _sut;

    public OrganizationProvisioningServiceTests()
    {
        _organizationRepositoryMock = new Mock<IOrganizationRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _sut = new OrganizationProvisioningService(_organizationRepositoryMock.Object, _userRepositoryMock.Object);

        // Default setup
        _organizationRepositoryMock.Setup(r => r.GetByName(It.IsAny<string>())).Returns((Organization?)null);
        _organizationRepositoryMock.Setup(r => r.NextIdentity()).Returns(OrganizationId.NewId());
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullOrganizationRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new OrganizationProvisioningService(null!, _userRepositoryMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("organizationRepository");
    }

    [Fact]
    public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new OrganizationProvisioningService(_organizationRepositoryMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("userRepository");
    }

    #endregion

    #region ProvisionOrganization Tests

    [Fact]
    public void ProvisionOrganization_WithValidData_CreatesOrganization()
    {
        // Arrange
        var owner = CreateTestUser();

        // Act
        var result = _sut.ProvisionOrganization("Test Org", "A test organization", owner);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Org");
        result.Description.Should().Be("A test organization");
    }

    [Fact]
    public void ProvisionOrganization_ActivatesOrganization()
    {
        // Arrange
        var owner = CreateTestUser();

        // Act
        var result = _sut.ProvisionOrganization("Test Org", "Description", owner);

        // Assert
        result.Active.Should().BeTrue();
    }

    [Fact]
    public void ProvisionOrganization_AssignsOwnerRole()
    {
        // Arrange
        var owner = CreateTestUser();
        var orgId = OrganizationId.NewId();
        _organizationRepositoryMock.Setup(r => r.NextIdentity()).Returns(orgId);

        // Act
        _sut.ProvisionOrganization("Test Org", "Description", owner);

        // Assert
        owner.RoleAssignments.Should().Contain(r =>
            r.RoleId == RoleId.OrganizationOwner &&
            r.ScopeType == ScopeType.Organization &&
            r.ScopeId == orgId.Value.ToString());
    }

    [Fact]
    public void ProvisionOrganization_AddsOrganizationToRepository()
    {
        // Arrange
        var owner = CreateTestUser();

        // Act
        var result = _sut.ProvisionOrganization("Test Org", "Description", owner);

        // Assert
        _organizationRepositoryMock.Verify(r => r.Add(result), Times.Once);
    }

    [Fact]
    public void ProvisionOrganization_UpdatesOwnerInRepository()
    {
        // Arrange
        var owner = CreateTestUser();

        // Act
        _sut.ProvisionOrganization("Test Org", "Description", owner);

        // Assert
        _userRepositoryMock.Verify(r => r.Update(owner), Times.Once);
    }

    [Fact]
    public void ProvisionOrganization_WhenNameExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var existingOrg = Organization.Provision(OrganizationId.NewId(), "Existing Org", "Existing organization");
        _organizationRepositoryMock.Setup(r => r.GetByName("Existing Org")).Returns(existingOrg);
        var owner = CreateTestUser();

        // Act
        var act = () => _sut.ProvisionOrganization("Existing Org", "Description", owner);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public void ProvisionOrganization_RequestsNextIdentityFromRepository()
    {
        // Arrange
        var owner = CreateTestUser();

        // Act
        _sut.ProvisionOrganization("Test Org", "Description", owner);

        // Assert
        _organizationRepositoryMock.Verify(r => r.NextIdentity(), Times.Once);
    }

    [Fact]
    public void ProvisionOrganization_ChecksForDuplicateName()
    {
        // Arrange
        var owner = CreateTestUser();

        // Act
        _sut.ProvisionOrganization("Test Org", "Description", owner);

        // Assert
        _organizationRepositoryMock.Verify(r => r.GetByName("Test Org"), Times.Once);
    }

    [Fact]
    public void ProvisionOrganization_RaisesOrganizationProvisionedEvent()
    {
        // Arrange
        var owner = CreateTestUser();

        // Act
        var result = _sut.ProvisionOrganization("Test Org", "Description", owner);

        // Assert
        result.DomainEvents.Should().Contain(e => e is OrganizationProvisioned);
    }

    [Fact]
    public void ProvisionOrganization_RaisesOrganizationActivatedEvent()
    {
        // Arrange
        var owner = CreateTestUser();

        // Act
        var result = _sut.ProvisionOrganization("Test Org", "Description", owner);

        // Assert
        result.DomainEvents.Should().Contain(e => e is OrganizationActivated);
    }

    [Fact]
    public void ProvisionOrganization_OwnerIsMemberOfOrganization()
    {
        // Arrange
        var owner = CreateTestUser();
        var orgId = OrganizationId.NewId();
        _organizationRepositoryMock.Setup(r => r.NextIdentity()).Returns(orgId);

        // Act
        _sut.ProvisionOrganization("Test Org", "Description", owner);

        // Assert
        owner.IsMemberOfOrganization(orgId.Value.ToString()).Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static User CreateTestUser()
    {
        return User.Register(
            UserId.NewId(),
            "testuser",
            new EmailAddress("test@example.com"),
            HashedPassword.FromHash("hashed_password"));
    }

    #endregion
}
