using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for Organization aggregate root.
/// </summary>
public class OrganizationTests
{
    #region Provisioning Tests

    [Fact]
    public void Provision_WithValidData_CreatesOrganization()
    {
        // Arrange
        var orgId = OrganizationId.NewId();

        // Act
        var org = Organization.Provision(orgId, "Acme Corp", "A corporation for testing");

        // Assert
        org.Id.Should().Be(orgId);
        org.Name.Should().Be("Acme Corp");
        org.Description.Should().Be("A corporation for testing");
        org.Active.Should().BeFalse(); // Starts inactive
        org.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        org.DomainEvents.Should().ContainSingle(e => e is OrganizationProvisioned);
    }

    [Fact]
    public void Provision_WithEmptyName_ThrowsArgumentException()
    {
        // Act
        var act = () => Organization.Provision(OrganizationId.NewId(), "", "Description");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provision_WithWhitespaceName_ThrowsArgumentException()
    {
        // Act
        var act = () => Organization.Provision(OrganizationId.NewId(), "   ", "Description");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provision_WithEmptyDescription_ThrowsArgumentException()
    {
        // Act
        var act = () => Organization.Provision(OrganizationId.NewId(), "Acme Corp", "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provision_WithNameTooLong_ThrowsArgumentException()
    {
        // Act
        var act = () => Organization.Provision(OrganizationId.NewId(), new string('x', 101), "Description");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provision_WithDescriptionTooLong_ThrowsArgumentException()
    {
        // Act
        var act = () => Organization.Provision(OrganizationId.NewId(), "Acme", new string('x', 501));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Provision_RaisesOrganizationProvisionedEvent()
    {
        // Arrange
        var orgId = OrganizationId.NewId();

        // Act
        var org = Organization.Provision(orgId, "Acme Corp", "Description");

        // Assert
        var domainEvent = org.DomainEvents.OfType<OrganizationProvisioned>().Single();
        domainEvent.OrganizationId.Should().Be(orgId);
        domainEvent.OrganizationName.Should().Be("Acme Corp");
    }

    #endregion

    #region Activation Tests

    [Fact]
    public void Activate_InactiveOrganization_ActivatesIt()
    {
        // Arrange
        var org = CreateTestOrganization();
        org.Active.Should().BeFalse(); // Starts inactive
        org.ClearDomainEvents();

        // Act
        org.Activate();

        // Assert
        org.Active.Should().BeTrue();
        org.DomainEvents.Should().ContainSingle(e => e is OrganizationActivated);
    }

    [Fact]
    public void Activate_AlreadyActive_DoesNothing()
    {
        // Arrange
        var org = CreateTestOrganization();
        org.Activate();
        org.ClearDomainEvents();

        // Act
        org.Activate();

        // Assert
        org.Active.Should().BeTrue();
        org.DomainEvents.Should().BeEmpty(); // No new event
    }

    [Fact]
    public void Activate_RaisesOrganizationActivatedEvent()
    {
        // Arrange
        var org = CreateTestOrganization();
        org.ClearDomainEvents();

        // Act
        org.Activate();

        // Assert
        var domainEvent = org.DomainEvents.OfType<OrganizationActivated>().Single();
        domainEvent.OrganizationId.Should().Be(org.Id);
    }

    [Fact]
    public void Deactivate_ActiveOrganization_DeactivatesIt()
    {
        // Arrange
        var org = CreateTestOrganization();
        org.Activate();
        org.ClearDomainEvents();

        // Act
        org.Deactivate();

        // Assert
        org.Active.Should().BeFalse();
        org.DomainEvents.Should().ContainSingle(e => e is OrganizationDeactivated);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_DoesNothing()
    {
        // Arrange
        var org = CreateTestOrganization();
        org.ClearDomainEvents();

        // Act
        org.Deactivate();

        // Assert
        org.Active.Should().BeFalse();
        org.DomainEvents.Should().BeEmpty(); // No new event
    }

    [Fact]
    public void Deactivate_RaisesOrganizationDeactivatedEvent()
    {
        // Arrange
        var org = CreateTestOrganization();
        org.Activate();
        org.ClearDomainEvents();

        // Act
        org.Deactivate();

        // Assert
        var domainEvent = org.DomainEvents.OfType<OrganizationDeactivated>().Single();
        domainEvent.OrganizationId.Should().Be(org.Id);
    }

    #endregion

    #region Description Update Tests

    [Fact]
    public void UpdateDescription_WithValidDescription_ChangesDescription()
    {
        // Arrange
        var org = CreateTestOrganization();

        // Act
        org.UpdateDescription("New description");

        // Assert
        org.Description.Should().Be("New description");
    }

    [Fact]
    public void UpdateDescription_WithEmptyDescription_ThrowsArgumentException()
    {
        // Arrange
        var org = CreateTestOrganization();

        // Act
        var act = () => org.UpdateDescription("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDescription_WithDescriptionTooLong_ThrowsArgumentException()
    {
        // Arrange
        var org = CreateTestOrganization();

        // Act
        var act = () => org.UpdateDescription(new string('x', 501));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region OrganizationId Tests

    [Fact]
    public void OrganizationId_NewId_CreatesUniqueId()
    {
        // Act
        var id1 = OrganizationId.NewId();
        var id2 = OrganizationId.NewId();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void OrganizationId_FromGuid_CreatesCorrectId()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = OrganizationId.FromGuid(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void OrganizationId_EmptyGuid_ThrowsException()
    {
        // Act
        var act = () => new OrganizationId(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OrganizationId_Equality_WorksCorrectly()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new OrganizationId(guid);
        var id2 = new OrganizationId(guid);

        // Assert
        id1.Should().Be(id2);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var org = CreateTestOrganization();

        // Act
        var result = org.ToString();

        // Assert
        result.Should().Contain("Acme Corp");
        result.Should().Contain("Organization");
    }

    #endregion

    #region ProvisionWithOwner Tests

    [Fact]
    public void ProvisionWithOwner_CreatesOrganizationWithOwnerAsMember()
    {
        // Arrange
        var ownerId = UserId.NewId();

        // Act
        var org = Organization.ProvisionWithOwner(
            OrganizationId.NewId(),
            "Test Org",
            "Test Description",
            ownerId);

        // Assert
        org.OwnerId.Should().Be(ownerId);
        org.Memberships.Should().HaveCount(1);
        org.IsMember(ownerId).Should().BeTrue();
        org.IsOwner(ownerId).Should().BeTrue();
    }

    #endregion

    #region Membership - Invitation Flow

    [Fact]
    public void InviteMember_CreatesPendingInvitation()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        var inviterId = UserId.NewId();

        // Act
        org.InviteMember(userId, inviterId, "Welcome to the team!");

        // Assert
        var membership = org.GetMembership(userId);
        membership.Should().NotBeNull();
        membership!.Status.Should().Be(MembershipStatus.PendingInvitation);
        membership.InvitedBy.Should().Be(inviterId);
        membership.InvitationNote.Should().Be("Welcome to the team!");
    }

    [Fact]
    public void InviteMember_RaisesMemberInvitedEvent()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        var inviterId = UserId.NewId();
        org.ClearDomainEvents();

        // Act
        org.InviteMember(userId, inviterId);

        // Assert
        org.DomainEvents.Should().Contain(e => e is MemberInvited);
    }

    [Fact]
    public void InviteMember_ToInactiveOrganization_Throws()
    {
        // Arrange
        var org = CreateTestOrganization(); // inactive
        var userId = UserId.NewId();
        var inviterId = UserId.NewId();

        // Act & Assert
        var act = () => org.InviteMember(userId, inviterId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public void InviteMember_WhenAlreadyMember_Throws()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        org.AddMember(userId);

        // Act & Assert
        var act = () => org.InviteMember(userId, UserId.NewId());
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public void AcceptInvitation_ChangesStatusToActive()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        org.InviteMember(userId, UserId.NewId());

        // Act
        org.AcceptInvitation(userId);

        // Assert
        org.IsMember(userId).Should().BeTrue();
        org.GetMembership(userId)!.Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public void AcceptInvitation_RaisesEvents()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        org.InviteMember(userId, UserId.NewId());
        org.ClearDomainEvents();

        // Act
        org.AcceptInvitation(userId);

        // Assert
        org.DomainEvents.Should().Contain(e => e is MemberInvitationAccepted);
        org.DomainEvents.Should().Contain(e => e is MemberJoined);
    }

    [Fact]
    public void DeclineInvitation_ChangesStatusToDeclined()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        org.InviteMember(userId, UserId.NewId());

        // Act
        org.DeclineInvitation(userId);

        // Assert
        org.IsMember(userId).Should().BeFalse();
        org.GetMembership(userId)!.Status.Should().Be(MembershipStatus.Declined);
    }

    #endregion

    #region Membership - Direct Add

    [Fact]
    public void AddMember_AddsActiveMember()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();

        // Act
        org.AddMember(userId);

        // Assert
        org.IsMember(userId).Should().BeTrue();
        org.GetMembership(userId)!.Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public void AddMember_RaisesMemberJoinedEvent()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        org.ClearDomainEvents();

        // Act
        org.AddMember(userId);

        // Assert
        org.DomainEvents.Should().Contain(e => e is MemberJoined);
    }

    [Fact]
    public void AddMember_WhenAlreadyActive_Throws()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        org.AddMember(userId);

        // Act & Assert
        var act = () => org.AddMember(userId);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    #endregion

    #region Membership - Suspend/Reactivate

    [Fact]
    public void SuspendMember_ChangesStatusToSuspended()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        org.AddMember(userId);

        // Act
        org.SuspendMember(userId, "Policy violation");

        // Assert
        org.IsMember(userId).Should().BeFalse();
        org.GetMembership(userId)!.Status.Should().Be(MembershipStatus.Suspended);
    }

    [Fact]
    public void SuspendMember_WhenOwner_Throws()
    {
        // Arrange
        var ownerId = UserId.NewId();
        var org = Organization.ProvisionWithOwner(
            OrganizationId.NewId(),
            "Test Org",
            "Description",
            ownerId);
        org.Activate();

        // Act & Assert
        var act = () => org.SuspendMember(ownerId, "Cannot do this");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*owner*");
    }

    [Fact]
    public void ReactivateMember_ChangesStatusToActive()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        org.AddMember(userId);
        org.SuspendMember(userId, "Temporary");

        // Act
        org.ReactivateMember(userId);

        // Assert
        org.IsMember(userId).Should().BeTrue();
        org.GetMembership(userId)!.Status.Should().Be(MembershipStatus.Active);
    }

    #endregion

    #region Membership - Leave/Remove

    [Fact]
    public void MemberLeave_ChangesStatusToLeft()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        org.AddMember(userId);

        // Act
        org.MemberLeave(userId);

        // Assert
        org.IsMember(userId).Should().BeFalse();
        org.GetMembership(userId)!.Status.Should().Be(MembershipStatus.Left);
    }

    [Fact]
    public void MemberLeave_WhenOwner_Throws()
    {
        // Arrange
        var ownerId = UserId.NewId();
        var org = Organization.ProvisionWithOwner(
            OrganizationId.NewId(),
            "Test Org",
            "Description",
            ownerId);
        org.Activate();

        // Act & Assert
        var act = () => org.MemberLeave(ownerId);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*owner cannot leave*");
    }

    [Fact]
    public void RemoveMember_RemovesMemberFromOrganization()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var userId = UserId.NewId();
        var adminId = UserId.NewId();
        org.AddMember(userId);

        // Act
        org.RemoveMember(userId, adminId, "Removed by admin");

        // Assert
        org.HasMembership(userId).Should().BeFalse();
    }

    #endregion

    #region Ownership Transfer

    [Fact]
    public void TransferOwnership_ChangesOwner()
    {
        // Arrange
        var ownerId = UserId.NewId();
        var newOwnerId = UserId.NewId();
        var org = Organization.ProvisionWithOwner(
            OrganizationId.NewId(),
            "Test Org",
            "Description",
            ownerId);
        org.Activate();
        org.AddMember(newOwnerId);

        // Act
        org.TransferOwnership(newOwnerId, ownerId);

        // Assert
        org.OwnerId.Should().Be(newOwnerId);
        org.IsOwner(newOwnerId).Should().BeTrue();
        org.IsOwner(ownerId).Should().BeFalse();
    }

    [Fact]
    public void TransferOwnership_RaisesEvent()
    {
        // Arrange
        var ownerId = UserId.NewId();
        var newOwnerId = UserId.NewId();
        var org = Organization.ProvisionWithOwner(
            OrganizationId.NewId(),
            "Test Org",
            "Description",
            ownerId);
        org.Activate();
        org.AddMember(newOwnerId);
        org.ClearDomainEvents();

        // Act
        org.TransferOwnership(newOwnerId, ownerId);

        // Assert
        var evt = org.DomainEvents.OfType<OrganizationOwnershipTransferred>().Single();
        evt.PreviousOwnerId.Should().Be(ownerId);
        evt.NewOwnerId.Should().Be(newOwnerId);
    }

    #endregion

    #region Membership Queries

    [Fact]
    public void GetActiveMembers_ReturnsOnlyActive()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var user1 = UserId.NewId();
        var user2 = UserId.NewId();
        var user3 = UserId.NewId();

        org.AddMember(user1);
        org.AddMember(user2);
        org.InviteMember(user3, UserId.NewId());
        org.SuspendMember(user2, "Suspended");

        // Act
        var activeMembers = org.GetActiveMembers().ToList();

        // Assert
        activeMembers.Should().HaveCount(1);
        activeMembers.Should().Contain(m => m.UserId == user1);
    }

    [Fact]
    public void GetPendingInvitations_ReturnsOnlyPending()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var user1 = UserId.NewId();
        var user2 = UserId.NewId();

        org.AddMember(user1);
        org.InviteMember(user2, UserId.NewId());

        // Act
        var pending = org.GetPendingInvitations().ToList();

        // Assert
        pending.Should().HaveCount(1);
        pending.Should().Contain(m => m.UserId == user2);
    }

    [Fact]
    public void GetActiveMemberCount_ReturnsCorrectCount()
    {
        // Arrange
        var org = CreateActiveTestOrganization();
        var user1 = UserId.NewId();
        var user2 = UserId.NewId();
        var user3 = UserId.NewId();

        org.AddMember(user1);
        org.AddMember(user2);
        org.AddMember(user3);
        org.SuspendMember(user3, "Suspended");

        // Act & Assert
        org.GetActiveMemberCount().Should().Be(2);
    }

    #endregion

    #region Helper Methods

    private static Organization CreateTestOrganization()
    {
        return Organization.Provision(
            OrganizationId.NewId(),
            "Acme Corp",
            "A test organization");
    }

    private static Organization CreateActiveTestOrganization()
    {
        var org = CreateTestOrganization();
        org.Activate();
        return org;
    }

    #endregion
}
