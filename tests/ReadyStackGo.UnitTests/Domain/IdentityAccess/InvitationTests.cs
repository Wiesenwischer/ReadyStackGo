using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Invitations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

public class InvitationTests
{
    private static readonly DateTime Now = new(2026, 6, 16, 12, 0, 0, DateTimeKind.Utc);

    #region Creation

    [Fact]
    public void Create_ValidOrgInvitation_IsPendingAndRaisesEvent()
    {
        var invitation = CreateOrgInvitation();

        invitation.Status.Should().Be(InvitationStatus.Pending);
        invitation.Email.Value.Should().Be("invitee@example.com");
        var evt = invitation.DomainEvents.OfType<InvitationCreated>().Single();
        evt.PlainToken.Should().Be("plain-token");
        evt.Email.Value.Should().Be("invitee@example.com");
    }

    [Fact]
    public void Create_GlobalScopeWithScopeId_Throws()
    {
        var act = () => Invitation.Create(
            InvitationId.NewId(), Email(), "t", "h", RoleId.SystemAdmin,
            ScopeType.Global, "should-not-be-here", new UserId(), Now, Now.AddDays(7));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NonGlobalScopeWithoutScopeId_Throws()
    {
        var act = () => Invitation.Create(
            InvitationId.NewId(), Email(), "t", "h", RoleId.Operator,
            ScopeType.Organization, null, new UserId(), Now, Now.AddDays(7));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ExpiryNotAfterCreation_Throws()
    {
        var act = () => Invitation.Create(
            InvitationId.NewId(), Email(), "t", "h", RoleId.Operator,
            ScopeType.Organization, "org-1", new UserId(), Now, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyToken_Throws()
    {
        var act = () => Invitation.Create(
            InvitationId.NewId(), Email(), "", "h", RoleId.Operator,
            ScopeType.Organization, "org-1", new UserId(), Now, Now.AddDays(7));

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Accept

    [Fact]
    public void Accept_PendingNotExpired_SetsAccepted()
    {
        var invitation = CreateOrgInvitation();

        invitation.Accept(Now.AddHours(1));

        invitation.Status.Should().Be(InvitationStatus.Accepted);
        invitation.AcceptedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Accept_Expired_MarksExpiredAndThrows()
    {
        var invitation = CreateOrgInvitation();

        var act = () => invitation.Accept(Now.AddDays(8));

        act.Should().Throw<InvalidOperationException>();
        invitation.Status.Should().Be(InvitationStatus.Expired);
    }

    [Fact]
    public void Accept_AlreadyAccepted_Throws()
    {
        var invitation = CreateOrgInvitation();
        invitation.Accept(Now.AddHours(1));

        var act = () => invitation.Accept(Now.AddHours(2));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Accept_Revoked_Throws()
    {
        var invitation = CreateOrgInvitation();
        invitation.Revoke();

        var act = () => invitation.Accept(Now.AddHours(1));

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Revoke

    [Fact]
    public void Revoke_Pending_SetsRevoked()
    {
        var invitation = CreateOrgInvitation();

        invitation.Revoke();

        invitation.Status.Should().Be(InvitationStatus.Revoked);
    }

    [Fact]
    public void Revoke_AlreadyAccepted_Throws()
    {
        var invitation = CreateOrgInvitation();
        invitation.Accept(Now.AddHours(1));

        var act = () => invitation.Revoke();

        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Helpers

    [Fact]
    public void IsExpired_BeforeAndAfterExpiry()
    {
        var invitation = CreateOrgInvitation();

        invitation.IsExpired(Now.AddDays(6)).Should().BeFalse();
        invitation.IsExpired(Now.AddDays(7)).Should().BeTrue();
    }

    [Fact]
    public void ToRoleAssignment_MapsRoleAndScope()
    {
        var invitation = CreateOrgInvitation();

        var assignment = invitation.ToRoleAssignment();

        assignment.RoleId.Should().Be(RoleId.Operator);
        assignment.ScopeType.Should().Be(ScopeType.Organization);
        assignment.ScopeId.Should().Be("org-1");
    }

    private static EmailAddress Email() => new("invitee@example.com");

    private static Invitation CreateOrgInvitation() => Invitation.Create(
        InvitationId.NewId(),
        Email(),
        "plain-token",
        "token-hash",
        RoleId.Operator,
        ScopeType.Organization,
        "org-1",
        new UserId(),
        Now,
        Now.AddDays(7));

    #endregion
}
