using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.SharedKernel;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for organization membership domain events.
/// </summary>
public class MembershipEventTests
{
    #region MemberInvited Tests

    [Fact]
    public void MemberInvited_Constructor_SetsAllProperties()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var userId = UserId.NewId();
        var invitedBy = UserId.NewId();
        var note = "Welcome to our organization!";

        // Act
        var evt = new MemberInvited(orgId, userId, invitedBy, note);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
        evt.UserId.Should().Be(userId);
        evt.InvitedBy.Should().Be(invitedBy);
        evt.Note.Should().Be(note);
    }

    [Fact]
    public void MemberInvited_InheritsFromDomainEvent()
    {
        // Act
        var evt = new MemberInvited(OrganizationId.NewId(), UserId.NewId(), UserId.NewId());

        // Assert
        evt.Should().BeAssignableTo<DomainEvent>();
    }

    [Fact]
    public void MemberInvited_WithNullNote_AllowsNullNote()
    {
        // Act
        var evt = new MemberInvited(OrganizationId.NewId(), UserId.NewId(), UserId.NewId(), null);

        // Assert
        evt.Note.Should().BeNull();
    }

    #endregion

    #region MemberInvitationAccepted Tests

    [Fact]
    public void MemberInvitationAccepted_Constructor_SetsAllProperties()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var userId = UserId.NewId();

        // Act
        var evt = new MemberInvitationAccepted(orgId, userId);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
        evt.UserId.Should().Be(userId);
    }

    [Fact]
    public void MemberInvitationAccepted_InheritsFromDomainEvent()
    {
        // Act
        var evt = new MemberInvitationAccepted(OrganizationId.NewId(), UserId.NewId());

        // Assert
        evt.Should().BeAssignableTo<DomainEvent>();
    }

    #endregion

    #region MemberInvitationDeclined Tests

    [Fact]
    public void MemberInvitationDeclined_Constructor_SetsAllProperties()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var userId = UserId.NewId();

        // Act
        var evt = new MemberInvitationDeclined(orgId, userId);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
        evt.UserId.Should().Be(userId);
    }

    #endregion

    #region MemberJoined Tests

    [Fact]
    public void MemberJoined_Constructor_SetsAllProperties()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var userId = UserId.NewId();

        // Act
        var evt = new MemberJoined(orgId, userId);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
        evt.UserId.Should().Be(userId);
    }

    #endregion

    #region MemberSuspended Tests

    [Fact]
    public void MemberSuspended_Constructor_SetsAllProperties()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var userId = UserId.NewId();
        var reason = "Policy violation";

        // Act
        var evt = new MemberSuspended(orgId, userId, reason);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
        evt.UserId.Should().Be(userId);
        evt.Reason.Should().Be(reason);
    }

    [Fact]
    public void MemberSuspended_WithNullReason_AllowsNullReason()
    {
        // Act
        var evt = new MemberSuspended(OrganizationId.NewId(), UserId.NewId());

        // Assert
        evt.Reason.Should().BeNull();
    }

    #endregion

    #region MemberReactivated Tests

    [Fact]
    public void MemberReactivated_Constructor_SetsAllProperties()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var userId = UserId.NewId();

        // Act
        var evt = new MemberReactivated(orgId, userId);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
        evt.UserId.Should().Be(userId);
    }

    #endregion

    #region MemberLeft Tests

    [Fact]
    public void MemberLeft_Constructor_SetsAllProperties()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var userId = UserId.NewId();

        // Act
        var evt = new MemberLeft(orgId, userId);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
        evt.UserId.Should().Be(userId);
    }

    #endregion

    #region MemberRemoved Tests

    [Fact]
    public void MemberRemoved_Constructor_SetsAllProperties()
    {
        // Arrange
        var orgId = OrganizationId.NewId();
        var userId = UserId.NewId();
        var removedBy = UserId.NewId();
        var reason = "Contract ended";

        // Act
        var evt = new MemberRemoved(orgId, userId, removedBy, reason);

        // Assert
        evt.OrganizationId.Should().Be(orgId);
        evt.UserId.Should().Be(userId);
        evt.RemovedBy.Should().Be(removedBy);
        evt.Reason.Should().Be(reason);
    }

    [Fact]
    public void MemberRemoved_WithNullReason_AllowsNullReason()
    {
        // Act
        var evt = new MemberRemoved(
            OrganizationId.NewId(),
            UserId.NewId(),
            UserId.NewId());

        // Assert
        evt.Reason.Should().BeNull();
    }

    #endregion
}
