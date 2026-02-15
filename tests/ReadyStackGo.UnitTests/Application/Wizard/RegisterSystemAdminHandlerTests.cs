using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Administration.RegisterSystemAdmin;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Application.Wizard;

public class RegisterSystemAdminHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly RegisterSystemAdminHandler _handler;

    public RegisterSystemAdminHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenServiceMock = new Mock<ITokenService>();

        // Default setup - no existing users
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User>());
        _userRepositoryMock.Setup(r => r.NextIdentity()).Returns(UserId.NewId());
        _passwordHasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_password");
        _tokenServiceMock.Setup(s => s.GenerateToken(It.IsAny<User>())).Returns("jwt-test-token");

        var registrationService = new SystemAdminRegistrationService(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object);

        _handler = new RegisterSystemAdminHandler(registrationService, _tokenServiceMock.Object);
    }

    #region Success Cases

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsSuccess()
    {
        // Act
        var result = await _handler.Handle(
            new RegisterSystemAdminCommand("admin", "ValidPass1"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsToken()
    {
        // Act
        var result = await _handler.Handle(
            new RegisterSystemAdminCommand("admin", "ValidPass1"),
            CancellationToken.None);

        // Assert
        result.Token.Should().Be("jwt-test-token");
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsUsernameAndRole()
    {
        // Act
        var result = await _handler.Handle(
            new RegisterSystemAdminCommand("admin", "ValidPass1"),
            CancellationToken.None);

        // Assert
        result.Username.Should().Be("admin");
        result.Role.Should().Be("admin");
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsUserId()
    {
        // Act
        var result = await _handler.Handle(
            new RegisterSystemAdminCommand("admin", "ValidPass1"),
            CancellationToken.None);

        // Assert
        result.UserId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WithValidCredentials_CallsTokenServiceWithCreatedUser()
    {
        // Act
        await _handler.Handle(
            new RegisterSystemAdminCommand("admin", "ValidPass1"),
            CancellationToken.None);

        // Assert
        _tokenServiceMock.Verify(
            s => s.GenerateToken(It.Is<User>(u => u.Username == "admin")),
            Times.Once);
    }

    #endregion

    #region Error Cases

    [Fact]
    public async Task Handle_WhenAdminAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var existingAdmin = CreateExistingSystemAdmin();
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User> { existingAdmin });

        // Act
        var result = await _handler.Handle(
            new RegisterSystemAdminCommand("admin", "ValidPass1"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public async Task Handle_WhenAdminAlreadyExists_DoesNotReturnToken()
    {
        // Arrange
        var existingAdmin = CreateExistingSystemAdmin();
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User> { existingAdmin });

        // Act
        var result = await _handler.Handle(
            new RegisterSystemAdminCommand("admin", "ValidPass1"),
            CancellationToken.None);

        // Assert
        result.Token.Should().BeNull();
        result.Username.Should().BeNull();
        result.Role.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAdminAlreadyExists_DoesNotGenerateToken()
    {
        // Arrange
        var existingAdmin = CreateExistingSystemAdmin();
        _userRepositoryMock.Setup(r => r.GetAll()).Returns(new List<User> { existingAdmin });

        // Act
        await _handler.Handle(
            new RegisterSystemAdminCommand("admin", "ValidPass1"),
            CancellationToken.None);

        // Assert
        _tokenServiceMock.Verify(s => s.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWeakPassword_ReturnsFailure()
    {
        // Act
        var result = await _handler.Handle(
            new RegisterSystemAdminCommand("admin", "weak"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Token.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithShortUsername_ReturnsFailure()
    {
        // Act
        var result = await _handler.Handle(
            new RegisterSystemAdminCommand("ab", "ValidPass1"),
            CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Token.Should().BeNull();
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
