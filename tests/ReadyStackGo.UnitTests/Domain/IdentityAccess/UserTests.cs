using FluentAssertions;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

namespace ReadyStackGo.UnitTests.Domain.IdentityAccess;

/// <summary>
/// Unit tests for User aggregate root.
/// </summary>
public class UserTests
{
    #region Registration Tests

    [Fact]
    public void Register_WithValidData_CreatesUser()
    {
        // Arrange
        var userId = UserId.NewId();
        var email = new EmailAddress("john@example.com");
        var password = HashedPassword.FromHash("hashed_password_value");

        // Act
        var user = User.Register(userId, "johndoe", email, password);

        // Assert
        user.Id.Should().Be(userId);
        user.Username.Should().Be("johndoe");
        user.Email.Should().Be(email);
        user.Password.Should().Be(password);
        user.Enablement.IsEnabled.Should().BeTrue();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        user.DomainEvents.Should().ContainSingle(e => e is UserRegistered);
    }

    [Fact]
    public void Register_WithNullUserId_ThrowsArgumentException()
    {
        // Arrange
        var email = new EmailAddress("john@example.com");
        var password = HashedPassword.FromHash("hashed_password_value");

        // Act
        var act = () => User.Register(null!, "johndoe", email, password);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_WithEmptyUsername_ThrowsArgumentException()
    {
        // Arrange
        var email = new EmailAddress("john@example.com");
        var password = HashedPassword.FromHash("hashed_password_value");

        // Act
        var act = () => User.Register(UserId.NewId(), "", email, password);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_WithUsernameTooShort_ThrowsArgumentException()
    {
        // Arrange
        var email = new EmailAddress("john@example.com");
        var password = HashedPassword.FromHash("hashed_password_value");

        // Act
        var act = () => User.Register(UserId.NewId(), "ab", email, password);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_WithUsernameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var email = new EmailAddress("john@example.com");
        var password = HashedPassword.FromHash("hashed_password_value");
        var longUsername = new string('x', 51);

        // Act
        var act = () => User.Register(UserId.NewId(), longUsername, email, password);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_WithNullEmail_ThrowsArgumentException()
    {
        // Arrange
        var password = HashedPassword.FromHash("hashed_password_value");

        // Act
        var act = () => User.Register(UserId.NewId(), "johndoe", null!, password);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_WithNullPassword_ThrowsArgumentException()
    {
        // Arrange
        var email = new EmailAddress("john@example.com");

        // Act
        var act = () => User.Register(UserId.NewId(), "johndoe", email, null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_RaisesUserRegisteredEvent()
    {
        // Arrange
        var userId = UserId.NewId();
        var email = new EmailAddress("john@example.com");
        var password = HashedPassword.FromHash("hashed_password_value");

        // Act
        var user = User.Register(userId, "johndoe", email, password);

        // Assert
        var domainEvent = user.DomainEvents.OfType<UserRegistered>().Single();
        domainEvent.UserId.Should().Be(userId);
        domainEvent.Username.Should().Be("johndoe");
        domainEvent.Email.Should().Be(email);
    }

    #endregion

    #region Password Tests

    [Fact]
    public void ChangePassword_WithValidPassword_ChangesPassword()
    {
        // Arrange
        var user = CreateTestUser();
        var newPassword = HashedPassword.FromHash("new_hashed_password");
        user.ClearDomainEvents();

        // Act
        user.ChangePassword(newPassword);

        // Assert
        user.Password.Should().Be(newPassword);
        user.DomainEvents.Should().ContainSingle(e => e is UserPasswordChanged);
    }

    [Fact]
    public void ChangePassword_WithNullPassword_ThrowsArgumentException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var act = () => user.ChangePassword(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangePassword_RaisesUserPasswordChangedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var newPassword = HashedPassword.FromHash("new_hashed_password");
        user.ClearDomainEvents();

        // Act
        user.ChangePassword(newPassword);

        // Assert
        var domainEvent = user.DomainEvents.OfType<UserPasswordChanged>().Single();
        domainEvent.UserId.Should().Be(user.Id);
    }

    #endregion

    #region Enablement Tests

    [Fact]
    public void Disable_EnabledUser_DisablesUser()
    {
        // Arrange
        var user = CreateTestUser();
        user.Enablement.IsEnabled.Should().BeTrue();
        user.ClearDomainEvents();

        // Act
        user.Disable();

        // Assert
        user.Enablement.IsEnabled.Should().BeFalse();
        user.DomainEvents.Should().ContainSingle(e => e is UserEnablementChanged);
    }

    [Fact]
    public void Disable_AlreadyDisabledUser_DoesNothing()
    {
        // Arrange
        var user = CreateTestUser();
        user.Disable();
        user.ClearDomainEvents();

        // Act
        user.Disable();

        // Assert
        user.Enablement.IsEnabled.Should().BeFalse();
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Enable_DisabledUser_EnablesUser()
    {
        // Arrange
        var user = CreateTestUser();
        user.Disable();
        user.ClearDomainEvents();

        // Act
        user.Enable();

        // Assert
        user.Enablement.IsEnabled.Should().BeTrue();
        user.DomainEvents.Should().ContainSingle(e => e is UserEnablementChanged);
    }

    [Fact]
    public void Enable_AlreadyEnabledUser_DoesNothing()
    {
        // Arrange
        var user = CreateTestUser();
        user.ClearDomainEvents();

        // Act
        user.Enable();

        // Assert
        user.Enablement.IsEnabled.Should().BeTrue();
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Disable_RaisesUserEnablementChangedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        user.ClearDomainEvents();

        // Act
        user.Disable();

        // Assert
        var domainEvent = user.DomainEvents.OfType<UserEnablementChanged>().Single();
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Enable_RaisesUserEnablementChangedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        user.Disable();
        user.ClearDomainEvents();

        // Act
        user.Enable();

        // Assert
        var domainEvent = user.DomainEvents.OfType<UserEnablementChanged>().Single();
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.IsEnabled.Should().BeTrue();
    }

    #endregion

    #region Role Assignment Tests

    [Fact]
    public void AssignRole_NewRole_AddsRoleAssignment()
    {
        // Arrange
        var user = CreateTestUser();
        var assignment = RoleAssignment.Global(RoleId.Operator);
        user.ClearDomainEvents();

        // Act
        user.AssignRole(assignment);

        // Assert
        user.RoleAssignments.Should().ContainSingle();
        user.RoleAssignments.First().RoleId.Should().Be(RoleId.Operator);
        user.DomainEvents.Should().ContainSingle(e => e is UserRoleAssigned);
    }

    [Fact]
    public void AssignRole_WithOrganizationScope_AddsRoleAssignment()
    {
        // Arrange
        var user = CreateTestUser();
        var orgId = Guid.NewGuid().ToString();
        var assignment = RoleAssignment.ForOrganization(RoleId.OrganizationOwner, orgId);
        user.ClearDomainEvents();

        // Act
        user.AssignRole(assignment);

        // Assert
        user.RoleAssignments.Should().ContainSingle();
        user.RoleAssignments.First().ScopeType.Should().Be(ScopeType.Organization);
        user.RoleAssignments.First().ScopeId.Should().Be(orgId);
    }

    [Fact]
    public void AssignRole_WithEnvironmentScope_AddsRoleAssignment()
    {
        // Arrange
        var user = CreateTestUser();
        var envId = Guid.NewGuid().ToString();
        var assignment = RoleAssignment.ForEnvironment(RoleId.Operator, envId);
        user.ClearDomainEvents();

        // Act
        user.AssignRole(assignment);

        // Assert
        user.RoleAssignments.Should().ContainSingle();
        user.RoleAssignments.First().ScopeType.Should().Be(ScopeType.Environment);
        user.RoleAssignments.First().ScopeId.Should().Be(envId);
    }

    [Fact]
    public void AssignRole_DuplicateRole_DoesNotAddDuplicate()
    {
        // Arrange
        var user = CreateTestUser();
        var assignment = RoleAssignment.Global(RoleId.Operator);
        user.AssignRole(assignment);
        user.ClearDomainEvents();

        // Act
        user.AssignRole(assignment);

        // Assert
        user.RoleAssignments.Should().HaveCount(1);
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AssignRole_NullAssignment_ThrowsArgumentException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var act = () => user.AssignRole(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AssignRole_RaisesUserRoleAssignedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var assignment = RoleAssignment.Global(RoleId.SystemAdmin);
        user.ClearDomainEvents();

        // Act
        user.AssignRole(assignment);

        // Assert
        var domainEvent = user.DomainEvents.OfType<UserRoleAssigned>().Single();
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.RoleId.Should().Be(RoleId.SystemAdmin);
        domainEvent.ScopeType.Should().Be(ScopeType.Global);
    }

    [Fact]
    public void RevokeRole_ExistingRole_RemovesRoleAssignment()
    {
        // Arrange
        var user = CreateTestUser();
        var assignment = RoleAssignment.Global(RoleId.Operator);
        user.AssignRole(assignment);
        user.ClearDomainEvents();

        // Act
        user.RevokeRole(RoleId.Operator, ScopeType.Global, null);

        // Assert
        user.RoleAssignments.Should().BeEmpty();
        user.DomainEvents.Should().ContainSingle(e => e is UserRoleRevoked);
    }

    [Fact]
    public void RevokeRole_NonExistentRole_DoesNothing()
    {
        // Arrange
        var user = CreateTestUser();
        user.ClearDomainEvents();

        // Act
        user.RevokeRole(RoleId.SystemAdmin, ScopeType.Global, null);

        // Assert
        user.RoleAssignments.Should().BeEmpty();
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RevokeRole_RaisesUserRoleRevokedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var assignment = RoleAssignment.Global(RoleId.Operator);
        user.AssignRole(assignment);
        user.ClearDomainEvents();

        // Act
        user.RevokeRole(RoleId.Operator, ScopeType.Global, null);

        // Assert
        var domainEvent = user.DomainEvents.OfType<UserRoleRevoked>().Single();
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.RoleId.Should().Be(RoleId.Operator);
    }

    [Fact]
    public void HasRole_WithAssignedRole_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.Global(RoleId.Operator));

        // Act
        var result = user.HasRole(RoleId.Operator);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasRole_WithoutRole_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = user.HasRole(RoleId.Operator);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasRoleWithScope_WithMatchingScope_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var envId = Guid.NewGuid().ToString();
        user.AssignRole(RoleAssignment.ForEnvironment(RoleId.Operator, envId));

        // Act
        var result = user.HasRoleWithScope(RoleId.Operator, ScopeType.Environment, envId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasRoleWithScope_WithDifferentScope_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var envId = Guid.NewGuid().ToString();
        user.AssignRole(RoleAssignment.ForEnvironment(RoleId.Operator, envId));

        // Act
        var result = user.HasRoleWithScope(RoleId.Operator, ScopeType.Global, null);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Multiple Role Assignments Tests

    [Fact]
    public void AssignRole_MultipleRoles_TracksAll()
    {
        // Arrange
        var user = CreateTestUser();
        var orgId = Guid.NewGuid().ToString();

        // Act
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.OrganizationOwner, orgId));
        user.AssignRole(RoleAssignment.ForEnvironment(RoleId.Operator, "env-1"));

        // Assert
        user.RoleAssignments.Should().HaveCount(3);
        user.HasRole(RoleId.SystemAdmin).Should().BeTrue();
        user.HasRole(RoleId.OrganizationOwner).Should().BeTrue();
        user.HasRole(RoleId.Operator).Should().BeTrue();
    }

    [Fact]
    public void RevokeRole_PartialRevoke_KeepsOtherRoles()
    {
        // Arrange
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));
        user.AssignRole(RoleAssignment.Global(RoleId.Operator));

        // Act
        user.RevokeRole(RoleId.SystemAdmin, ScopeType.Global, null);

        // Assert
        user.RoleAssignments.Should().HaveCount(1);
        user.HasRole(RoleId.SystemAdmin).Should().BeFalse();
        user.HasRole(RoleId.Operator).Should().BeTrue();
    }

    #endregion

    #region Account Locking Tests

    [Fact]
    public void LockAccount_WithReason_LocksAccountIndefinitely()
    {
        // Arrange
        var user = CreateTestUser();
        user.ClearDomainEvents();

        // Act
        user.LockAccount("Security breach detected");

        // Assert
        user.IsLocked.Should().BeTrue();
        user.LockReason.Should().Be("Security breach detected");
        user.LockedUntil.Should().BeNull();
        user.DomainEvents.Should().ContainSingle(e => e is UserAccountLocked);
    }

    [Fact]
    public void LockAccount_WithDuration_LocksAccountTemporarily()
    {
        // Arrange
        var user = CreateTestUser();
        user.ClearDomainEvents();

        // Act
        user.LockAccount("Too many failed attempts", TimeSpan.FromMinutes(15));

        // Assert
        user.IsLocked.Should().BeTrue();
        user.LockReason.Should().Be("Too many failed attempts");
        user.LockedUntil.Should().NotBeNull();
        user.LockedUntil.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LockAccount_WithEmptyReason_ThrowsArgumentException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var act = () => user.LockAccount("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LockAccount_WithZeroDuration_ThrowsArgumentException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var act = () => user.LockAccount("test", TimeSpan.Zero);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LockAccount_WithNegativeDuration_ThrowsArgumentException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var act = () => user.LockAccount("test", TimeSpan.FromMinutes(-5));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnlockAccount_WhenLocked_UnlocksAccount()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("test reason");
        user.ClearDomainEvents();

        // Act
        user.UnlockAccount();

        // Assert
        user.IsLocked.Should().BeFalse();
        user.LockReason.Should().BeNull();
        user.LockedUntil.Should().BeNull();
        user.FailedLoginAttempts.Should().Be(0);
        user.DomainEvents.Should().ContainSingle(e => e is UserAccountUnlocked);
    }

    [Fact]
    public void UnlockAccount_WhenNotLocked_DoesNothing()
    {
        // Arrange
        var user = CreateTestUser();
        user.ClearDomainEvents();

        // Act
        user.UnlockAccount();

        // Assert
        user.IsLocked.Should().BeFalse();
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearExpiredLock_WhenLockExpired_UnlocksAccount()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("test", TimeSpan.FromMinutes(1));
        user.ClearDomainEvents();

        // Simulate time passing past the lock expiration
        var futureTime = DateTime.UtcNow.AddMinutes(2);

        // Act
        user.ClearExpiredLock(futureTime);

        // Assert
        user.IsLocked.Should().BeFalse();
        user.DomainEvents.Should().ContainSingle(e => e is UserAccountUnlocked);
    }

    [Fact]
    public void ClearExpiredLock_WhenLockNotExpired_DoesNotUnlock()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("test", TimeSpan.FromMinutes(15));
        user.ClearDomainEvents();

        // Act
        user.ClearExpiredLock(DateTime.UtcNow);

        // Assert
        user.IsLocked.Should().BeTrue();
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearExpiredLock_WhenIndefiniteLock_DoesNotUnlock()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("permanent lock");
        user.ClearDomainEvents();

        // Act
        user.ClearExpiredLock(DateTime.UtcNow.AddYears(100));

        // Assert
        user.IsLocked.Should().BeTrue();
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void LockAccount_RaisesUserAccountLockedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        user.ClearDomainEvents();

        // Act
        user.LockAccount("Security reason", TimeSpan.FromMinutes(30));

        // Assert
        var domainEvent = user.DomainEvents.OfType<UserAccountLocked>().Single();
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.Reason.Should().Be("Security reason");
        domainEvent.LockedUntil.Should().NotBeNull();
    }

    [Fact]
    public void UnlockAccount_RaisesUserAccountUnlockedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("test");
        user.ClearDomainEvents();

        // Act
        user.UnlockAccount();

        // Assert
        var domainEvent = user.DomainEvents.OfType<UserAccountUnlocked>().Single();
        domainEvent.UserId.Should().Be(user.Id);
    }

    #endregion

    #region Login Tracking Tests

    [Fact]
    public void RecordSuccessfulLogin_ClearsFailedAttempts()
    {
        // Arrange
        var user = CreateTestUser();
        user.RecordFailedLogin("1.2.3.4");
        user.RecordFailedLogin("1.2.3.4");

        // Act
        user.RecordSuccessfulLogin("1.2.3.4");

        // Assert
        user.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public void RecordSuccessfulLogin_AddsToLoginHistory()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.RecordSuccessfulLogin("192.168.1.1");

        // Assert
        user.LoginHistory.Should().ContainSingle();
        var login = user.LoginHistory.First();
        login.Success.Should().BeTrue();
        login.IpAddress.Should().Be("192.168.1.1");
        login.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordFailedLogin_IncrementsFailedAttempts()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.RecordFailedLogin("1.2.3.4");
        user.RecordFailedLogin("1.2.3.4");

        // Assert
        user.FailedLoginAttempts.Should().Be(2);
    }

    [Fact]
    public void RecordFailedLogin_AddsToLoginHistory()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.RecordFailedLogin("10.0.0.1");

        // Assert
        user.LoginHistory.Should().ContainSingle();
        var login = user.LoginHistory.First();
        login.Success.Should().BeFalse();
        login.IpAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public void RecordFailedLogin_AutoLocksAfterMaxAttempts()
    {
        // Arrange
        var user = CreateTestUser();
        user.ClearDomainEvents();

        // Act - Default max is 5
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin("1.2.3.4");
        }

        // Assert
        user.IsLocked.Should().BeTrue();
        user.LockReason.Should().Contain("Too many failed login attempts");
        user.DomainEvents.Should().ContainSingle(e => e is UserAccountLocked);
    }

    [Fact]
    public void RecordFailedLogin_CustomMaxAttempts_LocksAtCustomThreshold()
    {
        // Arrange
        var user = CreateTestUser();

        // Act - Custom max of 3
        for (int i = 0; i < 3; i++)
        {
            user.RecordFailedLogin(maxAttempts: 3);
        }

        // Assert
        user.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void RecordFailedLogin_CustomLockDuration_SetsCorrectExpiration()
    {
        // Arrange
        var user = CreateTestUser();
        var customDuration = TimeSpan.FromHours(1);

        // Act
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(lockDuration: customDuration);
        }

        // Assert
        user.LockedUntil.Should().BeCloseTo(DateTime.UtcNow.Add(customDuration), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LoginHistory_LimitsTo10Entries()
    {
        // Arrange
        var user = CreateTestUser();

        // Act - Add more than 10 entries
        for (int i = 0; i < 15; i++)
        {
            user.RecordSuccessfulLogin($"ip-{i}");
        }

        // Assert
        user.LoginHistory.Should().HaveCount(10);
    }

    [Fact]
    public void GetLastSuccessfulLogin_ReturnsLatestSuccessfulLogin()
    {
        // Arrange
        var user = CreateTestUser();
        user.RecordSuccessfulLogin("1.1.1.1");
        user.RecordFailedLogin("2.2.2.2");
        user.RecordSuccessfulLogin("3.3.3.3");

        // Act
        var lastLogin = user.GetLastSuccessfulLogin();

        // Assert
        lastLogin.Should().NotBeNull();
        lastLogin.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetLastSuccessfulLogin_WithNoSuccessfulLogins_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();
        user.RecordFailedLogin();

        // Act
        var lastLogin = user.GetLastSuccessfulLogin();

        // Assert
        lastLogin.Should().BeNull();
    }

    #endregion

    #region CanLogin Tests

    [Fact]
    public void CanLogin_EnabledAndNotLocked_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = user.CanLogin(DateTime.UtcNow);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanLogin_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        user.Disable();

        // Act
        var result = user.CanLogin(DateTime.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanLogin_WhenLockedIndefinitely_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("indefinite lock");

        // Act
        var result = user.CanLogin(DateTime.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanLogin_WhenLockNotExpired_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("temp lock", TimeSpan.FromHours(1));

        // Act
        var result = user.CanLogin(DateTime.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanLogin_WhenLockExpired_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("temp lock", TimeSpan.FromMinutes(1));

        // Act - Check after lock has expired
        var result = user.CanLogin(DateTime.UtcNow.AddMinutes(5));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetLoginBlockedReason_WhenDisabled_ReturnsDisabledMessage()
    {
        // Arrange
        var user = CreateTestUser();
        user.Disable();

        // Act
        var reason = user.GetLoginBlockedReason(DateTime.UtcNow);

        // Assert
        reason.Should().Contain("disabled");
    }

    [Fact]
    public void GetLoginBlockedReason_WhenLockedIndefinitely_ReturnsLockReason()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("Security investigation");

        // Act
        var reason = user.GetLoginBlockedReason(DateTime.UtcNow);

        // Assert
        reason.Should().Contain("locked");
        reason.Should().Contain("Security investigation");
    }

    [Fact]
    public void GetLoginBlockedReason_WhenLockedTemporarily_IncludesUnlockTime()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("Too many attempts", TimeSpan.FromHours(1));

        // Act
        var reason = user.GetLoginBlockedReason(DateTime.UtcNow);

        // Assert
        reason.Should().Contain("locked until");
    }

    [Fact]
    public void GetLoginBlockedReason_WhenCanLogin_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var reason = user.GetLoginBlockedReason(DateTime.UtcNow);

        // Assert
        reason.Should().BeNull();
    }

    #endregion

    #region Password Management Tests

    [Fact]
    public void ChangePassword_UpdatesPasswordChangedAt()
    {
        // Arrange
        var user = CreateTestUser();
        var newPassword = HashedPassword.FromHash("new_hash");

        // Act
        user.ChangePassword(newPassword);

        // Assert
        user.PasswordChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ChangePassword_ClearsMustChangePasswordFlag()
    {
        // Arrange
        var user = CreateTestUser();
        user.RequirePasswordChange();
        var newPassword = HashedPassword.FromHash("new_hash");

        // Act
        user.ChangePassword(newPassword);

        // Assert
        user.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public void RequirePasswordChange_SetsMustChangePasswordFlag()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.RequirePasswordChange();

        // Assert
        user.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public void IsPasswordExpired_WhenOlderThanMaxAge_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        // Password was just set at creation time

        // Act - Check with a very short max age
        var result = user.IsPasswordExpired(TimeSpan.FromTicks(1));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPasswordExpired_WhenNewerThanMaxAge_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        user.ChangePassword(HashedPassword.FromHash("fresh_hash"));

        // Act - Check with a long max age
        var result = user.IsPasswordExpired(TimeSpan.FromDays(90));

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Organization Membership Tests

    [Fact]
    public void GetOrganizationMemberships_ReturnsOrganizationsWithRoles()
    {
        // Arrange
        var user = CreateTestUser();
        var org1 = Guid.NewGuid().ToString();
        var org2 = Guid.NewGuid().ToString();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.OrganizationOwner, org1));
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Viewer, org2));
        user.AssignRole(RoleAssignment.Global(RoleId.Operator)); // Not an org role

        // Act
        var memberships = user.GetOrganizationMemberships().ToList();

        // Assert
        memberships.Should().HaveCount(2);
        memberships.Should().Contain(org1);
        memberships.Should().Contain(org2);
    }

    [Fact]
    public void GetOrganizationMemberships_WithNoOrgRoles_ReturnsEmpty()
    {
        // Arrange
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));

        // Act
        var memberships = user.GetOrganizationMemberships().ToList();

        // Assert
        memberships.Should().BeEmpty();
    }

    [Fact]
    public void IsMemberOfOrganization_WhenHasOrgRole_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var orgId = Guid.NewGuid().ToString();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Viewer, orgId));

        // Act
        var result = user.IsMemberOfOrganization(orgId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsMemberOfOrganization_WhenNoOrgRole_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var orgId = Guid.NewGuid().ToString();

        // Act
        var result = user.IsMemberOfOrganization(orgId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RevokeAllRolesForOrganization_RemovesAllOrgRoles()
    {
        // Arrange
        var user = CreateTestUser();
        var orgId = Guid.NewGuid().ToString();
        var otherOrgId = Guid.NewGuid().ToString();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.OrganizationOwner, orgId));
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Viewer, orgId));
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.Viewer, otherOrgId));
        user.AssignRole(RoleAssignment.Global(RoleId.Operator));
        user.ClearDomainEvents();

        // Act
        user.RevokeAllRolesForOrganization(orgId);

        // Assert
        user.RoleAssignments.Should().HaveCount(2);
        user.IsMemberOfOrganization(orgId).Should().BeFalse();
        user.IsMemberOfOrganization(otherOrgId).Should().BeTrue();
        user.HasRole(RoleId.Operator).Should().BeTrue();
        user.DomainEvents.OfType<UserRoleRevoked>().Should().HaveCount(2);
    }

    #endregion

    #region IsSystemAdmin Tests

    [Fact]
    public void IsSystemAdmin_WithSystemAdminRole_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));

        // Act
        var result = user.IsSystemAdmin();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSystemAdmin_WithoutSystemAdminRole_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        user.AssignRole(RoleAssignment.Global(RoleId.Operator));

        // Act
        var result = user.IsSystemAdmin();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSystemAdmin_WithOrgScopedSystemAdmin_ReturnsFalse()
    {
        // Arrange - System admin should be global scope, not org scoped
        var user = CreateTestUser();
        var orgId = Guid.NewGuid().ToString();
        user.AssignRole(RoleAssignment.ForOrganization(RoleId.SystemAdmin, orgId));

        // Act
        var result = user.IsSystemAdmin();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsDescriptiveString()
    {
        // Arrange
        var userId = UserId.NewId();
        var user = User.Register(
            userId,
            "johndoe",
            new EmailAddress("john@example.com"),
            HashedPassword.FromHash("hashed"));

        // Act
        var result = user.ToString();

        // Assert
        result.Should().Contain("johndoe");
        result.Should().Contain(userId.ToString());
    }

    [Fact]
    public void ToString_WhenLocked_IncludesLockedStatus()
    {
        // Arrange
        var user = CreateTestUser();
        user.LockAccount("test");

        // Act
        var result = user.ToString();

        // Assert
        result.Should().Contain("locked=True");
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
