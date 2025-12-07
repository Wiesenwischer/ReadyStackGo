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
        var result = _sut.RegisterSystemAdmin("admin", "ValidPass1");

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be("admin");
        result.IsSystemAdmin().Should().BeTrue();
    }

    [Fact]
    public void RegisterSystemAdmin_CreatesUserWithSystemAdminRole()
    {
        // Act
        var result = _sut.RegisterSystemAdmin("admin", "ValidPass1");

        // Assert
        result.RoleAssignments.Should().ContainSingle(r =>
            r.RoleId == RoleId.SystemAdmin &&
            r.ScopeType == ScopeType.Global);
    }

    [Fact]
    public void RegisterSystemAdmin_GeneratesSystemEmail()
    {
        // Act
        var result = _sut.RegisterSystemAdmin("admin", "ValidPass1");

        // Assert
        result.Email.Value.Should().Be("admin@system.local");
    }

    [Fact]
    public void RegisterSystemAdmin_HashesPassword()
    {
        // Act
        _sut.RegisterSystemAdmin("admin", "ValidPass1");

        // Assert
        _passwordHasherMock.Verify(h => h.Hash("ValidPass1"), Times.Once);
    }

    [Fact]
    public void RegisterSystemAdmin_AddsUserToRepository()
    {
        // Act
        var result = _sut.RegisterSystemAdmin("admin", "ValidPass1");

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
        var act = () => _sut.RegisterSystemAdmin("newadmin", "ValidPass1");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public void RegisterSystemAdmin_WithWeakPassword_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.RegisterSystemAdmin("admin", "weak");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterSystemAdmin_WithPasswordMissingUppercase_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.RegisterSystemAdmin("admin", "password1");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*uppercase*");
    }

    [Fact]
    public void RegisterSystemAdmin_WithPasswordMissingLowercase_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.RegisterSystemAdmin("admin", "PASSWORD1");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*lowercase*");
    }

    [Fact]
    public void RegisterSystemAdmin_WithPasswordMissingDigit_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.RegisterSystemAdmin("admin", "Password");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*digit*");
    }

    [Fact]
    public void RegisterSystemAdmin_RequestsNextIdentityFromRepository()
    {
        // Act
        _sut.RegisterSystemAdmin("admin", "ValidPass1");

        // Assert
        _userRepositoryMock.Verify(r => r.NextIdentity(), Times.Once);
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
