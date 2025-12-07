using FluentAssertions;
using Moq;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for AuthenticationService domain service.
/// </summary>
public class AuthenticationServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly AuthenticationService _sut;

    public AuthenticationServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _sut = new AuthenticationService(_userRepositoryMock.Object, _passwordHasherMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullUserRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AuthenticationService(null!, _passwordHasherMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("userRepository");
    }

    [Fact]
    public void Constructor_WithNullPasswordHasher_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AuthenticationService(_userRepositoryMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("passwordHasher");
    }

    #endregion

    #region Authenticate Tests

    [Fact]
    public void Authenticate_WithValidCredentials_ReturnsUser()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepositoryMock.Setup(r => r.FindByUsername("testuser")).Returns(user);
        _passwordHasherMock.Setup(h => h.Verify("ValidPass1", user.Password.Hash)).Returns(true);

        // Act
        var result = _sut.Authenticate("testuser", "ValidPass1");

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(user);
    }

    [Fact]
    public void Authenticate_WithUnknownUsername_ReturnsNull()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.FindByUsername("unknown")).Returns((User?)null);

        // Act
        var result = _sut.Authenticate("unknown", "password");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Authenticate_WithIncorrectPassword_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepositoryMock.Setup(r => r.FindByUsername("testuser")).Returns(user);
        _passwordHasherMock.Setup(h => h.Verify("wrongpassword", user.Password.Hash)).Returns(false);

        // Act
        var result = _sut.Authenticate("testuser", "wrongpassword");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Authenticate_WithDisabledUser_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();
        user.Disable();
        _userRepositoryMock.Setup(r => r.FindByUsername("testuser")).Returns(user);

        // Act
        var result = _sut.Authenticate("testuser", "ValidPass1");

        // Assert
        result.Should().BeNull();
        // Password hasher should not even be called
        _passwordHasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Authenticate_ChecksEnablementBeforePassword()
    {
        // Arrange
        var user = CreateTestUser();
        user.Disable();
        _userRepositoryMock.Setup(r => r.FindByUsername("testuser")).Returns(user);

        // Act
        _sut.Authenticate("testuser", "anypassword");

        // Assert - Should check enablement first and not call password verification
        _passwordHasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Authenticate_UsesRepositoryToFindUser()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.FindByUsername("testuser")).Returns((User?)null);

        // Act
        _sut.Authenticate("testuser", "password");

        // Assert
        _userRepositoryMock.Verify(r => r.FindByUsername("testuser"), Times.Once);
    }

    #endregion

    #region Helper Methods

    private static User CreateTestUser()
    {
        return User.Register(
            UserId.NewId(),
            "testuser",
            new EmailAddress("test@example.com"),
            HashedPassword.FromHash("hashed_password_value"));
    }

    #endregion
}
