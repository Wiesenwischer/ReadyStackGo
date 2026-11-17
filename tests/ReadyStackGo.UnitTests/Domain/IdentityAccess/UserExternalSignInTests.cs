using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.UnitTests.TestSupport;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Tests for the user behavior added for sign-in through identity provider templates:
/// unverified external registration, initial password and removal of a provider's links.
/// </summary>
public class UserExternalSignInTests
{
    private static readonly IPasswordHasher Hasher = new PrefixPasswordHasher();

    private static User ExternalUser(bool emailVerified = true) =>
        User.RegisterExternal(UserId.NewId(), "oidcuser", new EmailAddress("oidc@example.com"), "wysch", "sub-1", emailVerified);

    #region RegisterExternal

    [Fact]
    public void RegisterExternal_EmailNotVerifiedByProvider_DoesNotVerify()
    {
        var user = ExternalUser(emailVerified: false);

        user.IsEmailVerified.Should().BeFalse();
        user.EmailVerifiedAt.Should().BeNull();
        user.DomainEvents.OfType<EmailVerified>().Should().BeEmpty();
        user.FindExternalIdentity("wysch")!.Subject.Should().Be("sub-1");
        user.HasPassword.Should().BeFalse();
    }

    [Fact]
    public void RegisterExternal_EmailVerifiedByProvider_Verifies()
    {
        var user = ExternalUser(emailVerified: true);

        user.IsEmailVerified.Should().BeTrue();
        user.DomainEvents.OfType<EmailVerified>().Should().ContainSingle();
    }

    #endregion

    #region SetInitialPassword

    [Fact]
    public void SetInitialPassword_UserWithoutPassword_SetsPasswordAndRaisesEvent()
    {
        var user = ExternalUser();
        user.ClearDomainEvents();

        user.SetInitialPassword(HashedPassword.Create("ValidPass1", Hasher));

        user.HasPassword.Should().BeTrue();
        user.Password!.Verify("ValidPass1", Hasher).Should().BeTrue();
        user.PasswordChangedAt.Should().NotBeNull();
        user.MustChangePassword.Should().BeFalse();
        user.DomainEvents.OfType<UserPasswordChanged>().Should().ContainSingle();
    }

    [Fact]
    public void SetInitialPassword_UserWithPassword_ThrowsAndKeepsPassword()
    {
        var user = User.Register(UserId.NewId(), "local", new EmailAddress("local@example.com"), HashedPassword.Create("OldPass123", Hasher));

        var act = () => user.SetInitialPassword(HashedPassword.Create("NewPass123", Hasher));

        act.Should().Throw<InvalidOperationException>();
        user.Password!.Verify("OldPass123", Hasher).Should().BeTrue();
    }

    [Fact]
    public void SetInitialPassword_Twice_SecondCallThrows()
    {
        var user = ExternalUser();
        user.SetInitialPassword(HashedPassword.Create("ValidPass1", Hasher));

        var act = () => user.SetInitialPassword(HashedPassword.Create("OtherPass2", Hasher));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetInitialPassword_Null_Throws()
    {
        var user = ExternalUser();

        var act = () => user.SetInitialPassword(null!);

        act.Should().Throw<ArgumentException>();
        user.HasPassword.Should().BeFalse();
    }

    [Fact]
    public void SetInitialPassword_AllowsUnlinkingTheLastIdentityAfterwards()
    {
        var user = ExternalUser();
        user.SetInitialPassword(HashedPassword.Create("ValidPass1", Hasher));

        user.UnlinkExternalIdentity("wysch");

        user.ExternalIdentities.Should().BeEmpty();
    }

    #endregion

    #region RemoveExternalIdentityOfRemovedProvider

    [Fact]
    public void RemoveExternalIdentityOfRemovedProvider_LastIdentityOfUserWithoutPassword_IsRemoved()
    {
        var user = ExternalUser();
        user.ClearDomainEvents();

        var removed = user.RemoveExternalIdentityOfRemovedProvider("wysch");

        removed.Should().BeTrue();
        user.ExternalIdentities.Should().BeEmpty();
        user.DomainEvents.OfType<ExternalIdentityUnlinked>().Should().ContainSingle();
    }

    [Fact]
    public void RemoveExternalIdentityOfRemovedProvider_IgnoresCaseOfProviderName()
    {
        var user = ExternalUser();

        user.RemoveExternalIdentityOfRemovedProvider("WYSCH").Should().BeTrue();
        user.ExternalIdentities.Should().BeEmpty();
    }

    [Fact]
    public void RemoveExternalIdentityOfRemovedProvider_OtherProvider_KeepsLinks()
    {
        var user = ExternalUser();
        user.LinkExternalIdentity("keycloak", "kc-1");
        user.ClearDomainEvents();

        user.RemoveExternalIdentityOfRemovedProvider("unknown").Should().BeFalse();

        user.ExternalIdentities.Should().HaveCount(2);
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveExternalIdentityOfRemovedProvider_RemovesOnlyThatProvider()
    {
        var user = ExternalUser();
        user.LinkExternalIdentity("keycloak", "kc-1");

        user.RemoveExternalIdentityOfRemovedProvider("wysch").Should().BeTrue();

        user.ExternalIdentities.Should().ContainSingle(e => e.Provider == "keycloak");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RemoveExternalIdentityOfRemovedProvider_EmptyName_ReturnsFalse(string? provider)
    {
        var user = ExternalUser();

        user.RemoveExternalIdentityOfRemovedProvider(provider!).Should().BeFalse();
        user.ExternalIdentities.Should().ContainSingle();
    }

    [Fact]
    public void RemoveExternalIdentityOfRemovedProvider_SameNameLaterLinksAsNew()
    {
        var user = ExternalUser();
        user.RemoveExternalIdentityOfRemovedProvider("wysch");

        // A later provider with the same name must not inherit the old subject.
        user.LinkExternalIdentity("wysch", "sub-new");

        user.FindExternalIdentity("wysch")!.Subject.Should().Be("sub-new");
    }

    #endregion
}
