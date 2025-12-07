using FluentAssertions;
using Moq;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for OrganizationMembershipService domain service.
/// </summary>
public class OrganizationMembershipServiceTests
{
    private readonly Mock<IOrganizationRepository> _organizationRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly OrganizationMembershipService _sut;

    public OrganizationMembershipServiceTests()
    {
        _organizationRepositoryMock = new Mock<IOrganizationRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _sut = new OrganizationMembershipService(
            _organizationRepositoryMock.Object,
            _userRepositoryMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullOrganizationRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new OrganizationMembershipService(null!, _userRepositoryMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("organizationRepository");
    }

    [Fact]
    public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new OrganizationMembershipService(_organizationRepositoryMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("userRepository");
    }

    #endregion

    #region InviteUser Tests

    [Fact]
    public void InviteUser_WithValidData_CreatesPendingMembership()
    {
        // Arrange
        var org = CreateActiveOrganization();
        var user = CreateTestUser();
        var inviter = CreateOrganizationOwner(org.Id);

        SetupRepositories(org, user, inviter);

        // Act
        var membership = _sut.InviteUser(
            org.Id,
            user.Id,
            inviter.Id,
            RoleId.Operator,
            "Welcome!");

        // Assert
        membership.Status.Should().Be(MembershipStatus.PendingInvitation);
        membership.UserId.Should().Be(user.Id);
        membership.OrganizationId.Should().Be(org.Id);
        membership.InvitedBy.Should().Be(inviter.Id);
        membership.InvitationNote.Should().Be("Welcome!");
    }

    [Fact]
    public void InviteUser_ToInactiveOrganization_ThrowsInvalidOperationException()
    {
        // Arrange
        var org = CreateInactiveOrganization();
        var user = CreateTestUser();
        var inviter = CreateOrganizationOwner(org.Id);

        SetupRepositories(org, user, inviter);

        // Act
        var act = () => _sut.InviteUser(org.Id, user.Id, inviter.Id, RoleId.Operator);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*inactive organization*");
    }

    [Fact]
    public void InviteUser_WhenAlreadyMember_ThrowsInvalidOperationException()
    {
        // Arrange
        var org = CreateActiveOrganization();
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Viewer, org.Id.Value.ToString()));
        var inviter = CreateOrganizationOwner(org.Id);

        SetupRepositories(org, user, inviter);

        // Act
        var act = () => _sut.InviteUser(org.Id, user.Id, inviter.Id, RoleId.Operator);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public void InviteUser_WithUnauthorizedInviter_ThrowsInvalidOperationException()
    {
        // Arrange
        var org = CreateActiveOrganization();
        var user = CreateTestUser();
        var inviter = CreateTestUser(); // Not an owner

        SetupRepositories(org, user, inviter);

        // Act
        var act = () => _sut.InviteUser(org.Id, user.Id, inviter.Id, RoleId.Operator);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*permission to invite*");
    }

    [Fact]
    public void InviteUser_WithSystemAdminInviter_Succeeds()
    {
        // Arrange
        var org = CreateActiveOrganization();
        var user = CreateTestUser();
        var inviter = CreateSystemAdmin();

        SetupRepositories(org, user, inviter);

        // Act
        var membership = _sut.InviteUser(org.Id, user.Id, inviter.Id, RoleId.Operator);

        // Assert
        membership.Should().NotBeNull();
    }

    [Fact]
    public void InviteUser_WithInvalidRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var org = CreateActiveOrganization();
        var user = CreateTestUser();
        var inviter = CreateOrganizationOwner(org.Id);

        SetupRepositories(org, user, inviter);

        // Act - SystemAdmin can only be assigned at Global scope
        var act = () => _sut.InviteUser(org.Id, user.Id, inviter.Id, RoleId.SystemAdmin);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be assigned*organization scope*");
    }

    [Fact]
    public void InviteUser_WithNonExistentOrganization_ThrowsInvalidOperationException()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var userId = UserId.NewId();
        var inviterId = UserId.NewId();

        _organizationRepositoryMock.Setup(r => r.Get(orgId)).Returns((Organization?)null);

        // Act
        var act = () => _sut.InviteUser(orgId, userId, inviterId, RoleId.Operator);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region AddMember Tests

    [Fact]
    public void AddMember_WithValidData_CreatesActiveMembership()
    {
        // Arrange
        var org = CreateActiveOrganization();
        var user = CreateTestUser();

        _organizationRepositoryMock.Setup(r => r.Get(org.Id)).Returns(org);
        _userRepositoryMock.Setup(r => r.Get(user.Id)).Returns(user);

        // Act
        var membership = _sut.AddMember(org.Id, user.Id, RoleId.Operator);

        // Assert
        membership.Status.Should().Be(MembershipStatus.Active);
        membership.UserId.Should().Be(user.Id);
        user.HasRole(RoleId.Operator).Should().BeTrue();
    }

    [Fact]
    public void AddMember_WhenAlreadyMember_ThrowsInvalidOperationException()
    {
        // Arrange
        var org = CreateActiveOrganization();
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Viewer, org.Id.Value.ToString()));

        _organizationRepositoryMock.Setup(r => r.Get(org.Id)).Returns(org);
        _userRepositoryMock.Setup(r => r.Get(user.Id)).Returns(user);

        // Act
        var act = () => _sut.AddMember(org.Id, user.Id, RoleId.Operator);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    #endregion

    #region AcceptInvitation Tests

    [Fact]
    public void AcceptInvitation_WithValidMembership_ActivatesMembership()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var user = CreateTestUser();
        var membership = OrganizationMembership.CreatePendingInvitation(
            user.Id, orgId, UserId.NewId());

        _userRepositoryMock.Setup(r => r.Get(user.Id)).Returns(user);

        // Act
        var accepted = _sut.AcceptInvitation(membership, RoleId.Operator);

        // Assert
        accepted.Status.Should().Be(MembershipStatus.Active);
        user.HasRole(RoleId.Operator).Should().BeTrue();
    }

    #endregion

    #region RemoveMember Tests

    [Fact]
    public void RemoveMember_WithValidData_RevokesMembership()
    {
        // Arrange
        var org = CreateActiveOrganization();
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Operator, org.Id.Value.ToString()));
        var remover = CreateOrganizationOwner(org.Id);
        var membership = OrganizationMembership.Create(user.Id, org.Id);

        _userRepositoryMock.Setup(r => r.Get(user.Id)).Returns(user);
        _userRepositoryMock.Setup(r => r.Get(remover.Id)).Returns(remover);
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new[] { user, remover });

        // Act
        var result = _sut.RemoveMember(membership, remover.Id, "Policy violation");

        // Assert
        result.Status.Should().Be(MembershipStatus.Left);
        user.IsMemberOfOrganization(org.Id.Value.ToString()).Should().BeFalse();
    }

    [Fact]
    public void RemoveMember_WithUnauthorizedRemover_ThrowsInvalidOperationException()
    {
        // Arrange
        var org = CreateActiveOrganization();
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Operator, org.Id.Value.ToString()));
        var remover = CreateTestUser(); // Not an owner
        var membership = OrganizationMembership.Create(user.Id, org.Id);

        _userRepositoryMock.Setup(r => r.Get(user.Id)).Returns(user);
        _userRepositoryMock.Setup(r => r.Get(remover.Id)).Returns(remover);

        // Act
        var act = () => _sut.RemoveMember(membership, remover.Id);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*permission to remove*");
    }

    [Fact]
    public void RemoveMember_LastOwnerRemovingSelf_ThrowsInvalidOperationException()
    {
        // Arrange
        var org = CreateActiveOrganization();
        var owner = CreateOrganizationOwner(org.Id);
        var membership = OrganizationMembership.Create(owner.Id, org.Id);

        _userRepositoryMock.Setup(r => r.Get(owner.Id)).Returns(owner);
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new[] { owner });

        // Act
        var act = () => _sut.RemoveMember(membership, owner.Id);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*last owner*");
    }

    #endregion

    #region GetMemberCount Tests

    [Fact]
    public void GetMemberCount_ReturnsCorrectCount()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var user1 = CreateTestUser();
        user1.AssignRole(RoleAssignment.ForOrganization(RoleId.Operator, orgId.Value.ToString()));
        var user2 = CreateTestUser();
        user2.AssignRole(RoleAssignment.ForOrganization(RoleId.Viewer, orgId.Value.ToString()));
        var user3 = CreateTestUser(); // Not a member

        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new[] { user1, user2, user3 });

        // Act
        var count = _sut.GetMemberCount(orgId);

        // Assert
        count.Should().Be(2);
    }

    #endregion

    #region CanAccessOrganization Tests

    [Fact]
    public void CanAccessOrganization_ForMember_ReturnsTrue()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Operator, orgId.Value.ToString()));

        _userRepositoryMock.Setup(r => r.Get(user.Id)).Returns(user);

        // Act
        var result = _sut.CanAccessOrganization(user.Id, orgId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanAccessOrganization_ForNonMember_ReturnsFalse()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var user = CreateTestUser();

        _userRepositoryMock.Setup(r => r.Get(user.Id)).Returns(user);

        // Act
        var result = _sut.CanAccessOrganization(user.Id, orgId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanAccessOrganization_ForSystemAdmin_ReturnsTrue()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var admin = CreateSystemAdmin();

        _userRepositoryMock.Setup(r => r.Get(admin.Id)).Returns(admin);

        // Act
        var result = _sut.CanAccessOrganization(admin.Id, orgId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanAccessOrganization_ForDisabledUser_ReturnsFalse()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Operator, orgId.Value.ToString()));
        user.Disable();

        _userRepositoryMock.Setup(r => r.Get(user.Id)).Returns(user);

        // Act
        var result = _sut.CanAccessOrganization(user.Id, orgId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private void SetupRepositories(Organization org, User user, User inviter)
    {
        _organizationRepositoryMock.Setup(r => r.Get(org.Id)).Returns(org);
        _userRepositoryMock.Setup(r => r.Get(user.Id)).Returns(user);
        _userRepositoryMock.Setup(r => r.Get(inviter.Id)).Returns(inviter);
    }

    private static Organization CreateActiveOrganization()
    {
        var org = Organization.Provision(OrganizationId.NewId(), "Test Org", "Test organization");
        org.Activate();
        return org;
    }

    private static Organization CreateInactiveOrganization()
    {
        return Organization.Provision(OrganizationId.NewId(), "Test Org", "Test organization");
    }

    private static User CreateTestUser()
    {
        return User.Register(
            UserId.NewId(),
            "testuser",
            new EmailAddress("test@example.com"),
            HashedPassword.FromHash("hashed"));
    }

    private static User CreateOrganizationOwner(OrganizationId orgId)
    {
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.OrganizationOwner, orgId.Value.ToString()));
        return user;
    }

    private static User CreateSystemAdmin()
    {
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));
        return user;
    }

    #endregion
}
