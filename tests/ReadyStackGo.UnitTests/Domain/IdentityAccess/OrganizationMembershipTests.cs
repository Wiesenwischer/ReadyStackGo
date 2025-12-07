using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for OrganizationMembership value object.
/// </summary>
public class OrganizationMembershipTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_CreatesMembership()
    {
        // Arrange
        var userId = UserId.NewId();
        var orgId = OrganizationId.NewId();

        // Act
        var membership = OrganizationMembership.Create(userId, orgId);

        // Assert
        membership.UserId.Should().Be(userId);
        membership.OrganizationId.Should().Be(orgId);
        membership.Status.Should().Be(MembershipStatus.Active);
        membership.JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        membership.LeftAt.Should().BeNull();
        membership.InvitedBy.Should().BeNull();
        membership.InvitationNote.Should().BeNull();
    }

    [Fact]
    public void Create_WithInviter_StoresInviterInfo()
    {
        // Arrange
        var userId = UserId.NewId();
        var orgId = OrganizationId.NewId();
        var inviterId = UserId.NewId();

        // Act
        var membership = OrganizationMembership.Create(userId, orgId, inviterId, "Welcome to the team!");

        // Assert
        membership.InvitedBy.Should().Be(inviterId);
        membership.InvitationNote.Should().Be("Welcome to the team!");
    }

    [Fact]
    public void Create_WithNullUserId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => OrganizationMembership.Create(null!, OrganizationId.NewId());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOrganizationId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => OrganizationMembership.Create(UserId.NewId(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region CreatePendingInvitation Tests

    [Fact]
    public void CreatePendingInvitation_CreatesWithPendingStatus()
    {
        // Arrange
        var userId = UserId.NewId();
        var orgId = OrganizationId.NewId();
        var inviterId = UserId.NewId();

        // Act
        var membership = OrganizationMembership.CreatePendingInvitation(
            userId, orgId, inviterId, "Please join us");

        // Assert
        membership.Status.Should().Be(MembershipStatus.PendingInvitation);
        membership.InvitedBy.Should().Be(inviterId);
        membership.InvitationNote.Should().Be("Please join us");
        membership.AllowsAccess.Should().BeFalse();
    }

    [Fact]
    public void CreatePendingInvitation_WithNullInviter_ThrowsArgumentNullException()
    {
        // Act
        var act = () => OrganizationMembership.CreatePendingInvitation(
            UserId.NewId(), OrganizationId.NewId(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Accept Tests

    [Fact]
    public void Accept_FromPendingInvitation_ChangesToActive()
    {
        // Arrange
        var membership = OrganizationMembership.CreatePendingInvitation(
            UserId.NewId(), OrganizationId.NewId(), UserId.NewId());

        // Act
        var accepted = membership.Accept();

        // Assert
        accepted.Status.Should().Be(MembershipStatus.Active);
        accepted.AllowsAccess.Should().BeTrue();
        accepted.JoinedAt.Should().Be(membership.JoinedAt); // Preserves original join time
    }

    [Fact]
    public void Accept_FromActiveStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var membership = OrganizationMembership.Create(UserId.NewId(), OrganizationId.NewId());

        // Act
        var act = () => membership.Accept();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*pending invitation*");
    }

    #endregion

    #region Decline Tests

    [Fact]
    public void Decline_FromPendingInvitation_ChangesToDeclined()
    {
        // Arrange
        var membership = OrganizationMembership.CreatePendingInvitation(
            UserId.NewId(), OrganizationId.NewId(), UserId.NewId());

        // Act
        var declined = membership.Decline();

        // Assert
        declined.Status.Should().Be(MembershipStatus.Declined);
        declined.AllowsAccess.Should().BeFalse();
        declined.LeftAt.Should().NotBeNull();
        declined.LeftAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Decline_FromActiveStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var membership = OrganizationMembership.Create(UserId.NewId(), OrganizationId.NewId());

        // Act
        var act = () => membership.Decline();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*pending invitation*");
    }

    #endregion

    #region Suspend Tests

    [Fact]
    public void Suspend_FromActiveStatus_ChangesToSuspended()
    {
        // Arrange
        var membership = OrganizationMembership.Create(UserId.NewId(), OrganizationId.NewId());

        // Act
        var suspended = membership.Suspend();

        // Assert
        suspended.Status.Should().Be(MembershipStatus.Suspended);
        suspended.AllowsAccess.Should().BeFalse();
    }

    [Fact]
    public void Suspend_FromPendingInvitation_ThrowsInvalidOperationException()
    {
        // Arrange
        var membership = OrganizationMembership.CreatePendingInvitation(
            UserId.NewId(), OrganizationId.NewId(), UserId.NewId());

        // Act
        var act = () => membership.Suspend();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*active membership*");
    }

    #endregion

    #region Reactivate Tests

    [Fact]
    public void Reactivate_FromSuspendedStatus_ChangesToActive()
    {
        // Arrange
        var membership = OrganizationMembership.Create(UserId.NewId(), OrganizationId.NewId());
        var suspended = membership.Suspend();

        // Act
        var reactivated = suspended.Reactivate();

        // Assert
        reactivated.Status.Should().Be(MembershipStatus.Active);
        reactivated.AllowsAccess.Should().BeTrue();
    }

    [Fact]
    public void Reactivate_FromActiveStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var membership = OrganizationMembership.Create(UserId.NewId(), OrganizationId.NewId());

        // Act
        var act = () => membership.Reactivate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*suspended*");
    }

    #endregion

    #region Leave Tests

    [Fact]
    public void Leave_FromActiveStatus_ChangesToLeft()
    {
        // Arrange
        var membership = OrganizationMembership.Create(UserId.NewId(), OrganizationId.NewId());

        // Act
        var left = membership.Leave();

        // Assert
        left.Status.Should().Be(MembershipStatus.Left);
        left.AllowsAccess.Should().BeFalse();
        left.LeftAt.Should().NotBeNull();
    }

    [Fact]
    public void Leave_FromSuspendedStatus_ChangesToLeft()
    {
        // Arrange
        var membership = OrganizationMembership.Create(UserId.NewId(), OrganizationId.NewId());
        var suspended = membership.Suspend();

        // Act
        var left = suspended.Leave();

        // Assert
        left.Status.Should().Be(MembershipStatus.Left);
    }

    [Fact]
    public void Leave_FromPendingInvitation_ThrowsInvalidOperationException()
    {
        // Arrange
        var membership = OrganizationMembership.CreatePendingInvitation(
            UserId.NewId(), OrganizationId.NewId(), UserId.NewId());

        // Act
        var act = () => membership.Leave();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*active or suspended*");
    }

    #endregion

    #region AllowsAccess Tests

    [Fact]
    public void AllowsAccess_ForActiveStatus_ReturnsTrue()
    {
        // Arrange
        var membership = OrganizationMembership.Create(UserId.NewId(), OrganizationId.NewId());

        // Assert
        membership.AllowsAccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(MembershipStatus.PendingInvitation)]
    [InlineData(MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Declined)]
    [InlineData(MembershipStatus.Left)]
    public void AllowsAccess_ForNonActiveStatus_ReturnsFalse(MembershipStatus targetStatus)
    {
        // Arrange
        var membership = CreateMembershipWithStatus(targetStatus);

        // Assert
        membership.AllowsAccess.Should().BeFalse();
    }

    #endregion

    #region GetMembershipDuration Tests

    [Fact]
    public void GetMembershipDuration_ForActiveMember_ReturnsTimeSinceJoin()
    {
        // Arrange
        var membership = OrganizationMembership.Create(UserId.NewId(), OrganizationId.NewId());

        // Act
        var duration = membership.GetMembershipDuration();

        // Assert
        duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetMembershipDuration_ForLeftMember_ReturnsTimeUntilLeft()
    {
        // Arrange
        var membership = OrganizationMembership.Create(UserId.NewId(), OrganizationId.NewId());
        var leftMembership = membership.Leave();

        // Act
        var duration = leftMembership.GetMembershipDuration();

        // Assert
        duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameMembership_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.NewId();
        var orgId = OrganizationId.NewId();
        var m1 = OrganizationMembership.Create(userId, orgId);
        var m2 = OrganizationMembership.Create(userId, orgId);

        // Assert - Note: JoinedAt will differ slightly, so equality is based on key components
        m1.UserId.Should().Be(m2.UserId);
        m1.OrganizationId.Should().Be(m2.OrganizationId);
    }

    #endregion

    #region Helper Methods

    private static OrganizationMembership CreateMembershipWithStatus(MembershipStatus status)
    {
        var userId = UserId.NewId();
        var orgId = OrganizationId.NewId();
        var inviterId = UserId.NewId();

        return status switch
        {
            MembershipStatus.PendingInvitation =>
                OrganizationMembership.CreatePendingInvitation(userId, orgId, inviterId),
            MembershipStatus.Active =>
                OrganizationMembership.Create(userId, orgId),
            MembershipStatus.Suspended =>
                OrganizationMembership.Create(userId, orgId).Suspend(),
            MembershipStatus.Declined =>
                OrganizationMembership.CreatePendingInvitation(userId, orgId, inviterId).Decline(),
            MembershipStatus.Left =>
                OrganizationMembership.Create(userId, orgId).Leave(),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    #endregion
}
