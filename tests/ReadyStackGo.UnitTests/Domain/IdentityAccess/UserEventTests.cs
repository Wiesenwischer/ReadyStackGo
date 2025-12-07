using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for User domain events.
/// </summary>
public class UserEventTests
{
    #region UserRegistered Tests

    [Fact]
    public void UserRegistered_Constructor_SetsAllProperties()
    {
        // Arrange
        var userId = UserId.NewId();
        var username = "testuser";
        var email = new EmailAddress("test@example.com");

        // Act
        var evt = new UserRegistered(userId, username, email);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.Username.Should().Be(username);
        evt.Email.Should().Be(email);
    }

    [Fact]
    public void UserRegistered_InheritsFromDomainEvent()
    {
        // Arrange & Act
        var evt = new UserRegistered(UserId.NewId(), "user", new EmailAddress("test@example.com"));

        // Assert
        evt.Should().BeAssignableTo<ReadyStackGo.Domain.SharedKernel.DomainEvent>();
    }

    #endregion

    #region UserEnablementChanged Tests

    [Fact]
    public void UserEnablementChanged_Constructor_SetsAllProperties()
    {
        // Arrange
        var userId = UserId.NewId();

        // Act
        var evt = new UserEnablementChanged(userId, true);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UserEnablementChanged_CorrectlyTracksEnablementState(bool isEnabled)
    {
        // Act
        var evt = new UserEnablementChanged(UserId.NewId(), isEnabled);

        // Assert
        evt.IsEnabled.Should().Be(isEnabled);
    }

    #endregion

    #region UserPasswordChanged Tests

    [Fact]
    public void UserPasswordChanged_Constructor_SetsUserId()
    {
        // Arrange
        var userId = UserId.NewId();

        // Act
        var evt = new UserPasswordChanged(userId);

        // Assert
        evt.UserId.Should().Be(userId);
    }

    #endregion

    #region UserRoleAssigned Tests

    [Fact]
    public void UserRoleAssigned_Constructor_SetsAllProperties()
    {
        // Arrange
        var userId = UserId.NewId();
        var roleId = RoleId.Operator;
        var scopeType = ScopeType.Organization;
        var scopeId = "org-123";

        // Act
        var evt = new UserRoleAssigned(userId, roleId, scopeType, scopeId);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.RoleId.Should().Be(roleId);
        evt.ScopeType.Should().Be(scopeType);
        evt.ScopeId.Should().Be(scopeId);
    }

    [Fact]
    public void UserRoleAssigned_GlobalScope_HasNullScopeId()
    {
        // Act
        var evt = new UserRoleAssigned(UserId.NewId(), RoleId.SystemAdmin, ScopeType.Global, null);

        // Assert
        evt.ScopeType.Should().Be(ScopeType.Global);
        evt.ScopeId.Should().BeNull();
    }

    #endregion

    #region UserRoleRevoked Tests

    [Fact]
    public void UserRoleRevoked_Constructor_SetsAllProperties()
    {
        // Arrange
        var userId = UserId.NewId();
        var roleId = RoleId.Operator;
        var scopeType = ScopeType.Organization;
        var scopeId = "org-123";

        // Act
        var evt = new UserRoleRevoked(userId, roleId, scopeType, scopeId);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.RoleId.Should().Be(roleId);
        evt.ScopeType.Should().Be(scopeType);
        evt.ScopeId.Should().Be(scopeId);
    }

    #endregion

    #region UserAccountLocked Tests

    [Fact]
    public void UserAccountLocked_Constructor_SetsAllProperties()
    {
        // Arrange
        var userId = UserId.NewId();
        var reason = "Too many failed login attempts";
        var lockedUntil = DateTime.UtcNow.AddMinutes(15);

        // Act
        var evt = new UserAccountLocked(userId, reason, lockedUntil);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.Reason.Should().Be(reason);
        evt.LockedUntil.Should().Be(lockedUntil);
    }

    [Fact]
    public void UserAccountLocked_IndefiniteLock_HasNullLockedUntil()
    {
        // Act
        var evt = new UserAccountLocked(UserId.NewId(), "Security breach", null);

        // Assert
        evt.LockedUntil.Should().BeNull();
    }

    #endregion

    #region UserAccountUnlocked Tests

    [Fact]
    public void UserAccountUnlocked_Constructor_SetsUserId()
    {
        // Arrange
        var userId = UserId.NewId();

        // Act
        var evt = new UserAccountUnlocked(userId);

        // Assert
        evt.UserId.Should().Be(userId);
    }

    #endregion
}
