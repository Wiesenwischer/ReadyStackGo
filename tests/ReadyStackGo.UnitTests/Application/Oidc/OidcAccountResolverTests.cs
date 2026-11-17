using FluentAssertions;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Domain.IdentityAccess.Invitations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.UnitTests.TestSupport;

namespace ReadyStackGo.UnitTests.Application.Oidc;

public class OidcAccountResolverTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryInvitationRepository _invitations = new();
    private readonly OidcAccountResolver _sut;

    public OidcAccountResolverTests()
    {
        _sut = new OidcAccountResolver(_users, _invitations, new UsernameGenerator(_users));
    }

    #region Helpers

    private static OidcProviderSettings Provider(bool trustUnverifiedEmail = false) => new()
    {
        Name = "wysch",
        DisplayName = "WYSCH",
        Authority = "https://id.example.com",
        ClientId = "client",
        TrustUnverifiedEmail = trustUnverifiedEmail
    };

    private User AddLocalUser(string username = "alice", string email = "alice@example.com")
    {
        var user = User.Register(_users.NextIdentity(), username, new EmailAddress(email), HashedPassword.FromHash("hash"));
        _users.Add(user);
        return user;
    }

    private Invitation AddInvitation(string email, RoleId? role = null, DateTime? expiresAt = null, ScopeType scope = ScopeType.Global, string? scopeId = null)
    {
        var invitation = Invitation.Create(
            _invitations.NextIdentity(),
            new EmailAddress(email),
            "plain-token",
            "token-hash",
            role ?? RoleId.Operator,
            scope,
            scopeId,
            UserId.Create(),
            Now.AddDays(-1),
            expiresAt ?? Now.AddDays(6));
        _invitations.Add(invitation);
        return invitation;
    }

    #endregion

    #region Existing link

    [Fact]
    public void Resolve_SubjectKnown_ReturnsLinkedUser()
    {
        var user = AddLocalUser();
        user.LinkExternalIdentity("wysch", "sub-1");

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "alice@example.com", true), Now);

        result.Error.Should().BeNull();
        result.User.Should().BeSameAs(user);
    }

    [Fact]
    public void Resolve_SubjectKnownButEmailChangedAtProvider_StillReturnsLinkedUser()
    {
        var user = AddLocalUser();
        user.LinkExternalIdentity("wysch", "sub-1");

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "renamed@example.com", false), Now);

        result.User.Should().BeSameAs(user);
        user.Email.Value.Should().Be("alice@example.com");
    }

    [Fact]
    public void Resolve_SubjectKnownWithoutEmail_StillReturnsLinkedUser()
    {
        var user = AddLocalUser();
        user.LinkExternalIdentity("wysch", "sub-1");

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", null, false), Now);

        result.User.Should().BeSameAs(user);
    }

    [Fact]
    public void Resolve_SubjectKnownAtOtherProvider_IsNotUsedForThisProvider()
    {
        var user = AddLocalUser();
        user.LinkExternalIdentity("other", "sub-1");

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "nobody@example.com", true), Now);

        result.User.Should().BeNull();
        result.Error.Should().Be(OidcSignInErrors.NoAccount);
    }

    [Fact]
    public void Resolve_SubjectKnownButAccountDisabled_IsRejected()
    {
        var user = AddLocalUser();
        user.LinkExternalIdentity("wysch", "sub-1");
        user.Disable();

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "alice@example.com", true), Now);

        result.User.Should().BeNull();
        result.Error.Should().Be(OidcSignInErrors.AccountDisabled);
    }

    #endregion

    #region Email of an existing user

    [Fact]
    public void Resolve_EmailVerified_LinksExistingUserAndVerifiesEmail()
    {
        var user = AddLocalUser();

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "Alice@Example.com", true), Now);

        result.User.Should().BeSameAs(user);
        user.FindExternalIdentity("wysch")!.Subject.Should().Be("sub-1");
        user.IsEmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(Now);
        _users.UpdateCount.Should().Be(1);
    }

    [Fact]
    public void Resolve_EmailVerifiedAndUserAlreadyVerified_KeepsOriginalVerificationTime()
    {
        var user = AddLocalUser();
        var earlier = Now.AddDays(-10);
        user.VerifyEmail(earlier);

        _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "alice@example.com", true), Now);

        user.EmailVerifiedAt.Should().Be(earlier);
    }

    [Fact]
    public void Resolve_EmailUnverifiedAndTrustOff_IsRejectedAndNotLinked()
    {
        var user = AddLocalUser();

        var result = _sut.Resolve(Provider(trustUnverifiedEmail: false), new OidcUserInfo("sub-1", "alice@example.com", false), Now);

        result.User.Should().BeNull();
        result.Error.Should().Be(OidcSignInErrors.EmailUnverified);
        user.ExternalIdentities.Should().BeEmpty();
        _users.UpdateCount.Should().Be(0);
    }

    [Fact]
    public void Resolve_EmailUnverifiedAndTrustOn_LinksButEmailStaysUnverified()
    {
        var user = AddLocalUser();

        var result = _sut.Resolve(Provider(trustUnverifiedEmail: true), new OidcUserInfo("sub-1", "alice@example.com", false), Now);

        result.User.Should().BeSameAs(user);
        user.FindExternalIdentity("wysch").Should().NotBeNull();
        user.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void Resolve_UserLinkedToThisProviderWithOtherSubject_IsRejectedAndLinkUnchanged()
    {
        var user = AddLocalUser();
        user.LinkExternalIdentity("wysch", "sub-original");

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-other", "alice@example.com", true), Now);

        result.User.Should().BeNull();
        result.Error.Should().Be(OidcSignInErrors.SubjectMismatch);
        user.FindExternalIdentity("wysch")!.Subject.Should().Be("sub-original");
        user.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void Resolve_UserLinkedToOtherProvider_GetsSecondLink()
    {
        var user = AddLocalUser();
        user.LinkExternalIdentity("keycloak", "kc-1");

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "alice@example.com", true), Now);

        result.User.Should().BeSameAs(user);
        user.ExternalIdentities.Should().HaveCount(2);
    }

    [Fact]
    public void Resolve_EmailMatchesDisabledUser_IsRejectedAndNotLinked()
    {
        var user = AddLocalUser();
        user.Disable();

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "alice@example.com", true), Now);

        result.Error.Should().Be(OidcSignInErrors.AccountDisabled);
        user.ExternalIdentities.Should().BeEmpty();
        user.IsEmailVerified.Should().BeFalse();
    }

    #endregion

    #region Email missing or invalid

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_EmailMissing_IsRejected(string? email)
    {
        AddInvitation("alice@example.com");

        var result = _sut.Resolve(Provider(trustUnverifiedEmail: true), new OidcUserInfo("sub-1", email, true), Now);

        result.Error.Should().Be(OidcSignInErrors.EmailMissing);
        _users.GetAll().Should().BeEmpty();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("alice+tag@example.com")]
    public void Resolve_EmailInvalid_IsRejectedWithoutException(string email)
    {
        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", email, true), Now);

        result.User.Should().BeNull();
        result.Error.Should().Be(OidcSignInErrors.EmailInvalid);
    }

    #endregion

    #region Invitations

    [Fact]
    public void Resolve_NoUserAndNoInvitation_IsRejectedWithNoAccount()
    {
        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "new@example.com", true), Now);

        result.User.Should().BeNull();
        result.Error.Should().Be(OidcSignInErrors.NoAccount);
        _users.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Resolve_InvitationAndEmailVerified_CreatesUserWithInvitedRole()
    {
        var invitation = AddInvitation("new@example.com", RoleId.Operator);

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "new@example.com", true, Username: "new.user"), Now);

        result.Error.Should().BeNull();
        var user = result.User!;
        user.Username.Should().Be("new_user");
        user.Email.Value.Should().Be("new@example.com");
        user.HasPassword.Should().BeFalse();
        user.IsEmailVerified.Should().BeTrue();
        user.FindExternalIdentity("wysch")!.Subject.Should().Be("sub-1");
        user.RoleAssignments.Should().ContainSingle(r => r.RoleId == RoleId.Operator && r.ScopeType == ScopeType.Global);
        _users.GetAll().Should().ContainSingle().Which.Should().BeSameAs(user);
        invitation.Status.Should().Be(InvitationStatus.Accepted);
        invitation.AcceptedAt.Should().Be(Now);
        _invitations.UpdateCount.Should().Be(1);
    }

    [Fact]
    public void Resolve_InvitationWithOrganizationScope_KeepsScope()
    {
        AddInvitation("new@example.com", RoleId.Operator, scope: ScopeType.Organization, scopeId: "org-1");

        var user = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "new@example.com", true), Now).User!;

        user.RoleAssignments.Should().ContainSingle(r =>
            r.RoleId == RoleId.Operator && r.ScopeType == ScopeType.Organization && r.ScopeId == "org-1");
    }

    [Fact]
    public void Resolve_InvitationAndEmailUnverifiedTrustOff_IsRejectedAndInvitationStaysPending()
    {
        var invitation = AddInvitation("new@example.com");

        var result = _sut.Resolve(Provider(trustUnverifiedEmail: false), new OidcUserInfo("sub-1", "new@example.com", false), Now);

        result.Error.Should().Be(OidcSignInErrors.EmailUnverified);
        invitation.Status.Should().Be(InvitationStatus.Pending);
        _users.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Resolve_InvitationAndEmailUnverifiedTrustOn_CreatesUserWithUnverifiedEmail()
    {
        AddInvitation("new@example.com");

        var result = _sut.Resolve(Provider(trustUnverifiedEmail: true), new OidcUserInfo("sub-1", "new@example.com", false), Now);

        result.User.Should().NotBeNull();
        result.User!.IsEmailVerified.Should().BeFalse();
    }

    [Fact]
    public void Resolve_InvitationExpired_IsRejectedAndNoUserCreated()
    {
        var invitation = AddInvitation("new@example.com", expiresAt: Now.AddMinutes(-1));

        var result = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "new@example.com", true), Now);

        result.User.Should().BeNull();
        result.Error.Should().Be(OidcSignInErrors.NoAccount);
        _users.GetAll().Should().BeEmpty();
        invitation.Status.Should().Be(InvitationStatus.Expired);
    }

    [Fact]
    public void Resolve_InvitationAndUsernameTaken_CreatesUserWithNumberSuffix()
    {
        AddLocalUser("bob", "bob@other.example.com");
        AddInvitation("bob@example.com");

        var user = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "bob@example.com", true, Username: "bob"), Now).User!;

        user.Username.Should().Be("bob1");
    }

    [Fact]
    public void Resolve_InvitationWithoutUsernameClaim_UsesLocalPartOfEmail()
    {
        AddInvitation("carol.smith@example.com");

        var user = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "carol.smith@example.com", true), Now).User!;

        user.Username.Should().Be("carol_smith");
    }

    [Fact]
    public void Resolve_SecondSignInAfterProvisioning_UsesTheLink()
    {
        AddInvitation("new@example.com");
        var first = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "new@example.com", true), Now).User!;

        var second = _sut.Resolve(Provider(), new OidcUserInfo("sub-1", "new@example.com", true), Now.AddHours(1));

        second.User.Should().BeSameAs(first);
        _users.GetAll().Should().ContainSingle();
    }

    #endregion
}
