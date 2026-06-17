using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for email verification and external identity (OIDC) behavior on the User aggregate.
/// </summary>
public class UserEmailVerificationAndExternalIdentityTests
{
    #region Email Verification

    [Fact]
    public void Register_DoesNotVerifyEmail()
    {
        var user = CreateLocalUser();

        user.IsEmailVerified.Should().BeFalse();
        user.EmailVerifiedAt.Should().BeNull();
    }

    [Fact]
    public void Register_HasLocalPassword()
    {
        var user = CreateLocalUser();

        user.HasPassword.Should().BeTrue();
    }

    [Fact]
    public void VerifyEmail_SetsTimestampAndRaisesEvent()
    {
        var user = CreateLocalUser();
        user.ClearDomainEvents();
        var now = DateTime.UtcNow;

        user.VerifyEmail(now);

        user.IsEmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(now);
        user.DomainEvents.OfType<EmailVerified>().Should().ContainSingle()
            .Which.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void VerifyEmail_WhenAlreadyVerified_IsIdempotent()
    {
        var user = CreateLocalUser();
        var firstVerification = DateTime.UtcNow.AddDays(-1);
        user.VerifyEmail(firstVerification);
        user.ClearDomainEvents();

        user.VerifyEmail(DateTime.UtcNow);

        // Original timestamp preserved, no second event.
        user.EmailVerifiedAt.Should().Be(firstVerification);
        user.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region External Registration (OIDC)

    [Fact]
    public void RegisterExternal_CreatesUserWithoutPassword()
    {
        var user = User.RegisterExternal(
            UserId.NewId(),
            "oidcuser",
            new EmailAddress("oidc@example.com"),
            "identityaccess",
            "subject-123");

        user.HasPassword.Should().BeFalse();
        user.Password.Should().BeNull();
    }

    [Fact]
    public void RegisterExternal_MarksEmailVerified()
    {
        var user = User.RegisterExternal(
            UserId.NewId(),
            "oidcuser",
            new EmailAddress("oidc@example.com"),
            "identityaccess",
            "subject-123");

        user.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public void RegisterExternal_LinksExternalIdentity()
    {
        var user = User.RegisterExternal(
            UserId.NewId(),
            "oidcuser",
            new EmailAddress("oidc@example.com"),
            "IdentityAccess",
            "subject-123");

        var identity = user.FindExternalIdentity("identityaccess");
        identity.Should().NotBeNull();
        identity!.Provider.Should().Be("identityaccess");
        identity.Subject.Should().Be("subject-123");
    }

    [Fact]
    public void RegisterExternal_RaisesRegisteredLinkedAndVerifiedEvents()
    {
        var user = User.RegisterExternal(
            UserId.NewId(),
            "oidcuser",
            new EmailAddress("oidc@example.com"),
            "identityaccess",
            "subject-123");

        user.DomainEvents.OfType<UserRegistered>().Should().ContainSingle();
        user.DomainEvents.OfType<ExternalIdentityLinked>().Should().ContainSingle();
        user.DomainEvents.OfType<EmailVerified>().Should().ContainSingle();
    }

    #endregion

    #region Link External Identity

    [Fact]
    public void LinkExternalIdentity_AddsIdentityAndRaisesEvent()
    {
        var user = CreateLocalUser();
        user.ClearDomainEvents();

        user.LinkExternalIdentity("google", "google-sub-1");

        user.ExternalIdentities.Should().ContainSingle();
        user.FindExternalIdentity("google").Should().NotBeNull();
        var evt = user.DomainEvents.OfType<ExternalIdentityLinked>().Single();
        evt.Provider.Should().Be("google");
        evt.Subject.Should().Be("google-sub-1");
    }

    [Fact]
    public void LinkExternalIdentity_NormalizesProviderToLowercase()
    {
        var user = CreateLocalUser();

        user.LinkExternalIdentity("GitHub", "gh-1");

        user.FindExternalIdentity("github").Should().NotBeNull();
        user.ExternalIdentities.Single().Provider.Should().Be("github");
    }

    [Fact]
    public void LinkExternalIdentity_SameProviderAndSubject_IsIdempotent()
    {
        var user = CreateLocalUser();
        user.LinkExternalIdentity("google", "sub-1");
        user.ClearDomainEvents();

        user.LinkExternalIdentity("google", "sub-1");

        user.ExternalIdentities.Should().HaveCount(1);
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void LinkExternalIdentity_SameProviderDifferentSubject_Throws()
    {
        var user = CreateLocalUser();
        user.LinkExternalIdentity("google", "sub-1");

        var act = () => user.LinkExternalIdentity("google", "sub-2");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LinkExternalIdentity_DifferentProviders_BothLinked()
    {
        var user = CreateLocalUser();

        user.LinkExternalIdentity("google", "g-1");
        user.LinkExternalIdentity("identityaccess", "ia-1");

        user.ExternalIdentities.Should().HaveCount(2);
        user.FindExternalIdentity("google").Should().NotBeNull();
        user.FindExternalIdentity("identityaccess").Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void LinkExternalIdentity_EmptyProvider_Throws(string? provider)
    {
        var user = CreateLocalUser();

        var act = () => user.LinkExternalIdentity(provider!, "sub-1");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void LinkExternalIdentity_EmptySubject_Throws(string? subject)
    {
        var user = CreateLocalUser();

        var act = () => user.LinkExternalIdentity("google", subject!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FindExternalIdentity_WhenNotLinked_ReturnsNull()
    {
        var user = CreateLocalUser();

        user.FindExternalIdentity("google").Should().BeNull();
    }

    #endregion

    #region Unlink External Identity

    [Fact]
    public void UnlinkExternalIdentity_RemovesAndRaisesEvent()
    {
        var user = CreateLocalUser(); // has a password
        user.LinkExternalIdentity("google", "g-1");
        user.ClearDomainEvents();

        user.UnlinkExternalIdentity("google");

        user.FindExternalIdentity("google").Should().BeNull();
        user.ExternalIdentities.Should().BeEmpty();
        user.DomainEvents.OfType<ExternalIdentityUnlinked>().Should().ContainSingle();
    }

    [Fact]
    public void UnlinkExternalIdentity_NotLinked_IsNoOp()
    {
        var user = CreateLocalUser();
        user.ClearDomainEvents();

        user.UnlinkExternalIdentity("google");

        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UnlinkExternalIdentity_LocalUser_CanRemoveAllIdentities()
    {
        // A user with a local password keeps a sign-in method, so the last identity is removable.
        var user = CreateLocalUser();
        user.LinkExternalIdentity("google", "g-1");

        user.UnlinkExternalIdentity("google");

        user.ExternalIdentities.Should().BeEmpty();
    }

    [Fact]
    public void UnlinkExternalIdentity_ExternalOnlyUser_LastIdentity_Throws()
    {
        // No password + only one identity → removing it would lock the user out.
        var user = User.RegisterExternal(UserId.NewId(), "oidcuser", new EmailAddress("oidc@example.com"), "identityaccess", "sub-1");

        var act = () => user.UnlinkExternalIdentity("identityaccess");

        act.Should().Throw<InvalidOperationException>();
        user.FindExternalIdentity("identityaccess").Should().NotBeNull();
    }

    [Fact]
    public void UnlinkExternalIdentity_ExternalOnlyUser_WithTwoIdentities_CanRemoveOne()
    {
        var user = User.RegisterExternal(UserId.NewId(), "oidcuser", new EmailAddress("oidc@example.com"), "identityaccess", "sub-1");
        user.LinkExternalIdentity("google", "g-1");

        user.UnlinkExternalIdentity("google");

        user.ExternalIdentities.Should().ContainSingle();
        user.FindExternalIdentity("identityaccess").Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void FindExternalIdentity_EmptyProvider_ReturnsNull(string? provider)
    {
        var user = CreateLocalUser();

        user.FindExternalIdentity(provider!).Should().BeNull();
    }

    #endregion

    #region Helpers

    private static User CreateLocalUser()
    {
        return User.Register(
            UserId.NewId(),
            "testuser",
            new EmailAddress("test@example.com"),
            HashedPassword.FromHash("hashed_password_value"));
    }

    #endregion
}
