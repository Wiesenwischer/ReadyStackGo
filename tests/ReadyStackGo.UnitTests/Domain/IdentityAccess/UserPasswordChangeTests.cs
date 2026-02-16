using FluentAssertions;
using Moq;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Tests for User password change behavior, covering the domain logic
/// used by the ChangePassword endpoint.
/// </summary>
public class UserPasswordChangeTests
{
    private readonly Mock<IPasswordHasher> _hasherMock;

    public UserPasswordChangeTests()
    {
        _hasherMock = new Mock<IPasswordHasher>();
        _hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(p => $"hashed_{p}");
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((plain, hash) => $"hashed_{plain}" == hash);
    }

    private User CreateUser(string password = "OldPass1")
    {
        var hashedPassword = HashedPassword.Create(password, _hasherMock.Object);
        return User.Register(
            UserId.NewId(),
            "testuser",
            new EmailAddress("test@example.com"),
            hashedPassword);
    }

    [Fact]
    public void ChangePassword_WithValidNewPassword_UpdatesPassword()
    {
        var user = CreateUser();
        var newPassword = HashedPassword.Create("NewPass1", _hasherMock.Object);

        user.ChangePassword(newPassword);

        user.Password.Should().Be(newPassword);
        user.Password.Verify("NewPass1", _hasherMock.Object).Should().BeTrue();
    }

    [Fact]
    public void ChangePassword_UpdatesPasswordChangedAt()
    {
        var user = CreateUser();
        var originalChangedAt = user.PasswordChangedAt;

        var newPassword = HashedPassword.Create("NewPass1", _hasherMock.Object);
        user.ChangePassword(newPassword);

        user.PasswordChangedAt.Should().NotBeNull();
        user.PasswordChangedAt.Should().BeOnOrAfter(originalChangedAt!.Value);
    }

    [Fact]
    public void ChangePassword_ClearsMustChangePasswordFlag()
    {
        var user = CreateUser();
        user.RequirePasswordChange();
        user.MustChangePassword.Should().BeTrue();

        var newPassword = HashedPassword.Create("NewPass1", _hasherMock.Object);
        user.ChangePassword(newPassword);

        user.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public void ChangePassword_RaisesUserPasswordChangedEvent()
    {
        var user = CreateUser();
        user.ClearDomainEvents();

        var newPassword = HashedPassword.Create("NewPass1", _hasherMock.Object);
        user.ChangePassword(newPassword);

        user.DomainEvents.Should().ContainSingle(e => e is UserPasswordChanged);
    }

    [Fact]
    public void ChangePassword_WithNullPassword_ThrowsArgumentException()
    {
        var user = CreateUser();

        var act = () => user.ChangePassword(null!);

        act.Should().Throw<ArgumentException>().WithMessage("*password*");
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var user = CreateUser("MyPass99");

        user.Password.Verify("MyPass99", _hasherMock.Object).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var user = CreateUser("MyPass99");

        user.Password.Verify("WrongPass1", _hasherMock.Object).Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_EmptyString_ReturnsFalse()
    {
        var user = CreateUser("MyPass99");

        user.Password.Verify("", _hasherMock.Object).Should().BeFalse();
    }

    [Fact]
    public void CreateHashedPassword_TooShort_ThrowsArgumentException()
    {
        var act = () => HashedPassword.Create("Short1", _hasherMock.Object);

        act.Should().Throw<ArgumentException>().WithMessage("*8 characters*");
    }

    [Fact]
    public void CreateHashedPassword_NoUppercase_ThrowsArgumentException()
    {
        var act = () => HashedPassword.Create("alllower1", _hasherMock.Object);

        act.Should().Throw<ArgumentException>().WithMessage("*uppercase*");
    }

    [Fact]
    public void CreateHashedPassword_NoLowercase_ThrowsArgumentException()
    {
        var act = () => HashedPassword.Create("ALLUPPER1", _hasherMock.Object);

        act.Should().Throw<ArgumentException>().WithMessage("*lowercase*");
    }

    [Fact]
    public void CreateHashedPassword_NoDigit_ThrowsArgumentException()
    {
        var act = () => HashedPassword.Create("NoDigitsHere", _hasherMock.Object);

        act.Should().Throw<ArgumentException>().WithMessage("*digit*");
    }

    [Fact]
    public void CreateHashedPassword_Empty_ThrowsArgumentException()
    {
        var act = () => HashedPassword.Create("", _hasherMock.Object);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateHashedPassword_ValidPassword_Succeeds()
    {
        var password = HashedPassword.Create("ValidPass1", _hasherMock.Object);

        password.Should().NotBeNull();
        password.Hash.Should().Be("hashed_ValidPass1");
    }

    [Fact]
    public void ChangePassword_OldPasswordNoLongerVerifies()
    {
        var user = CreateUser("OldPass1");
        user.Password.Verify("OldPass1", _hasherMock.Object).Should().BeTrue();

        var newPassword = HashedPassword.Create("NewPass1", _hasherMock.Object);
        user.ChangePassword(newPassword);

        user.Password.Verify("OldPass1", _hasherMock.Object).Should().BeFalse();
        user.Password.Verify("NewPass1", _hasherMock.Object).Should().BeTrue();
    }
}
