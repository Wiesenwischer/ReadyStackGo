using FluentAssertions;
using Moq;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for SystemAdminRegistrationService domain service.
/// </summary>
public class SystemAdminRegistrationServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly SystemAdminRegistrationService _sut;

    public SystemAdminRegistrationServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _sut = new SystemAdminRegistrationService(_userRepositoryMock.Object, _passwordHasherMock.Object);

        // Default setup - no existing users
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User>());
        _userRepositoryMock.Setup(r => r.NextIdentity()).Returns(UserId.NewId());
        _passwordHasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_password");
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SystemAdminRegistrationService(null!, _passwordHasherMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("userRepository");
    }

    [Fact]
    public void Constructor_WithNullPasswordHasher_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SystemAdminRegistrationService(_userRepositoryMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("passwordHasher");
    }

    #endregion

    #region RegisterSystemAdmin Tests

    [Fact]
    public void RegisterSystemAdmin_WithNoExistingAdmin_CreatesSystemAdmin()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User>());

        // Act
        var result = _sut.RegisterSystemAdmin("admin", "admin@example.com", "ValidPass1");

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be("admin");
        result.IsSystemAdmin().Should().BeTrue();
    }

    [Fact]
    public void RegisterSystemAdmin_CreatesUserWithSystemAdminRole()
    {
        // Act
        var result = _sut.RegisterSystemAdmin("admin", "admin@example.com", "ValidPass1");

        // Assert
        result.RoleAssignments.Should().ContainSingle(r =>
            r.RoleId == RoleId.SystemAdmin &&
            r.ScopeType == ScopeType.Global);
    }

    [Fact]
    public void RegisterSystemAdmin_UsesProvidedEmail()
    {
        // Act
        var result = _sut.RegisterSystemAdmin("admin", "Admin@Example.com", "ValidPass1");

        // Assert - email is normalized to lowercase by the value object.
        result.Email.Value.Should().Be("admin@example.com");
    }

    [Fact]
    public void RegisterSystemAdmin_DoesNotVerifyEmail()
    {
        // Act - there is no SMTP server during initial setup, so the bootstrap admin's
        // email must NOT be auto-verified; it stays honest until a real ownership proof.
        var result = _sut.RegisterSystemAdmin("admin", "admin@example.com", "ValidPass1");

        // Assert
        result.IsEmailVerified.Should().BeFalse();
        result.EmailVerifiedAt.Should().BeNull();
    }

    [Fact]
    public void RegisterSystemAdmin_WithInvalidEmail_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.RegisterSystemAdmin("admin", "not-an-email", "ValidPass1");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterSystemAdmin_HashesPassword()
    {
        // Act
        _sut.RegisterSystemAdmin("admin", "admin@example.com", "ValidPass1");

        // Assert
        _passwordHasherMock.Verify(h => h.Hash("ValidPass1"), Times.Once);
    }

    [Fact]
    public void RegisterSystemAdmin_AddsUserToRepository()
    {
        // Act
        var result = _sut.RegisterSystemAdmin("admin", "admin@example.com", "ValidPass1");

        // Assert
        _userRepositoryMock.Verify(r => r.Add(result), Times.Once);
    }

    [Fact]
    public void RegisterSystemAdmin_WhenAdminExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var existingAdmin = CreateExistingSystemAdmin();
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User> { existingAdmin });

        // Act
        var act = () => _sut.RegisterSystemAdmin("newadmin", "newadmin@example.com", "ValidPass1");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public void RegisterSystemAdmin_WithWeakPassword_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.RegisterSystemAdmin("admin", "admin@example.com", "weak");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterSystemAdmin_WithPasswordMissingUppercase_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.RegisterSystemAdmin("admin", "admin@example.com", "password1");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*uppercase*");
    }

    [Fact]
    public void RegisterSystemAdmin_WithPasswordMissingLowercase_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.RegisterSystemAdmin("admin", "admin@example.com", "PASSWORD1");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*lowercase*");
    }

    [Fact]
    public void RegisterSystemAdmin_WithPasswordMissingDigit_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.RegisterSystemAdmin("admin", "admin@example.com", "Password");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*digit*");
    }

    [Fact]
    public void RegisterSystemAdmin_RequestsNextIdentityFromRepository()
    {
        // Act
        _sut.RegisterSystemAdmin("admin", "admin@example.com", "ValidPass1");

        // Assert
        _userRepositoryMock.Verify(r => r.NextIdentity(), Times.Once);
    }

    #endregion

    #region RegisterExternalSystemAdmin Tests

    [Fact]
    public void RegisterExternalSystemAdmin_WithNoExistingAdmin_CreatesAdminWithoutPasswordLinkedAndVerified()
    {
        // Act
        var result = _sut.RegisterExternalSystemAdmin("admin", new EmailAddress("admin@example.com"), "wysch", "sub-1");

        // Assert
        result.Username.Should().Be("admin");
        result.IsSystemAdmin().Should().BeTrue();
        result.HasPassword.Should().BeFalse();
        result.IsEmailVerified.Should().BeTrue();
        result.FindExternalIdentity("wysch")!.Subject.Should().Be("sub-1");
        result.RoleAssignments.Should().ContainSingle(r => r.RoleId == RoleId.SystemAdmin && r.ScopeType == ScopeType.Global);
        _userRepositoryMock.Verify(r => r.Add(result), Times.Once);
        _passwordHasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RegisterExternalSystemAdmin_WhenAdminExists_ThrowsAndAddsNothing()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User> { CreateExistingSystemAdmin() });

        // Act
        var act = () => _sut.RegisterExternalSystemAdmin("admin", new EmailAddress("admin@example.com"), "wysch", "sub-1");

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*already exists*");
        _userRepositoryMock.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void RegisterExternalSystemAdmin_WhenOnlyNonAdminUsersExist_CreatesAdmin()
    {
        // Arrange
        var operatorUser = User.Register(UserId.NewId(), "operator", new EmailAddress("op@example.com"), HashedPassword.FromHash("h"));
        operatorUser.AssignRole(RoleAssignment.Global(RoleId.Operator));
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User> { operatorUser });

        // Act
        var result = _sut.RegisterExternalSystemAdmin("admin", new EmailAddress("admin@example.com"), "wysch", "sub-1");

        // Assert
        result.IsSystemAdmin().Should().BeTrue();
    }

    [Fact]
    public void RegisterSystemAdmin_AfterExternalAdminExists_Throws()
    {
        // Arrange - both setup paths share one rule: exactly one first administrator.
        var external = _sut.RegisterExternalSystemAdmin("admin", new EmailAddress("admin@example.com"), "wysch", "sub-1");
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User> { external });

        // Act
        var act = () => _sut.RegisterSystemAdmin("second", "second@example.com", "ValidPass1");

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SystemAdminExists_ReflectsRepository()
    {
        _sut.SystemAdminExists().Should().BeFalse();

        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User> { CreateExistingSystemAdmin() });

        _sut.SystemAdminExists().Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static User CreateExistingSystemAdmin()
    {
        var user = User.Register(
            UserId.NewId(),
            "existingadmin",
            new EmailAddress("existing@system.local"),
            HashedPassword.FromHash("hashed"));
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));
        return user;
    }

    #endregion
}
