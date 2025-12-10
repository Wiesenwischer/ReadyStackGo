namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.IdentityAccess.Roles;

/// <summary>
/// Aggregate root representing a user in the system.
/// Users are system-wide entities, not scoped to organizations.
/// Organization membership is handled via RoleAssignments.
///
/// Rich domain model with account locking, login tracking, and security behaviors.
/// </summary>
public class User : AggregateRoot<UserId>
{
    private readonly List<RoleAssignment> _roleAssignments = new();
    private readonly List<LoginAttempt> _loginHistory = new();

    public string Username { get; private set; } = null!;
    public EmailAddress Email { get; private set; } = null!;
    public HashedPassword Password { get; private set; } = null!;
    public Enablement Enablement { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    // Account locking
    public bool IsLocked { get; private set; }
    public string? LockReason { get; private set; }
    public DateTime? LockedUntil { get; private set; }
    public int FailedLoginAttempts { get; private set; }

    // Password management
    public DateTime? PasswordChangedAt { get; private set; }
    public bool MustChangePassword { get; private set; }

    public IReadOnlyCollection<RoleAssignment> RoleAssignments => _roleAssignments.AsReadOnly();
    public IReadOnlyCollection<LoginAttempt> LoginHistory => _loginHistory.AsReadOnly();

    // For EF Core
    protected User() { }

    private User(
        UserId id,
        string username,
        EmailAddress email,
        HashedPassword password)
    {
        SelfAssertArgumentNotNull(id, "UserId is required.");
        SelfAssertArgumentNotEmpty(username, "Username is required.");
        SelfAssertArgumentLength(username, 3, 50, "Username must be 3 to 50 characters.");
        SelfAssertArgumentNotNull(email, "Email is required.");
        SelfAssertArgumentNotNull(password, "Password is required.");

        Id = id;
        Username = username;
        Email = email;
        Password = password;
        Enablement = Enablement.IndefiniteEnablement();
        CreatedAt = SystemClock.UtcNow;
        PasswordChangedAt = SystemClock.UtcNow;

        AddDomainEvent(new UserRegistered(Id, Username, Email));
    }

    #region Factory Methods

    public static User Register(
        UserId id,
        string username,
        EmailAddress email,
        HashedPassword password)
    {
        return new User(id, username, email, password);
    }

    #endregion

    #region Account Status

    /// <summary>
    /// Checks if the user can currently log in.
    /// Considers: enablement, lock status, lock expiration.
    /// </summary>
    public bool CanLogin(DateTime now)
    {
        if (!Enablement.IsEnabled)
            return false;

        if (IsLocked && (!LockedUntil.HasValue || LockedUntil.Value > now))
            return false;

        return true;
    }

    /// <summary>
    /// Gets the reason why the user cannot login, or null if they can.
    /// </summary>
    public string? GetLoginBlockedReason(DateTime now)
    {
        if (!Enablement.IsEnabled)
            return "Account is disabled.";

        if (IsLocked)
        {
            if (!LockedUntil.HasValue)
                return $"Account is locked: {LockReason}";
            if (LockedUntil.Value > now)
                return $"Account is locked until {LockedUntil.Value:u}: {LockReason}";
        }

        return null;
    }

    #endregion

    #region Account Locking

    /// <summary>
    /// Locks the account indefinitely with a reason.
    /// </summary>
    public void LockAccount(string reason)
    {
        SelfAssertArgumentNotEmpty(reason, "Lock reason is required.");

        IsLocked = true;
        LockReason = reason;
        LockedUntil = null;

        AddDomainEvent(new UserAccountLocked(Id, reason, null));
    }

    /// <summary>
    /// Locks the account for a specific duration.
    /// </summary>
    public void LockAccount(string reason, TimeSpan duration)
    {
        SelfAssertArgumentNotEmpty(reason, "Lock reason is required.");
        SelfAssertArgumentTrue(duration > TimeSpan.Zero, "Lock duration must be positive.");

        IsLocked = true;
        LockReason = reason;
        LockedUntil = SystemClock.UtcNow.Add(duration);

        AddDomainEvent(new UserAccountLocked(Id, reason, LockedUntil));
    }

    /// <summary>
    /// Unlocks the account.
    /// </summary>
    public void UnlockAccount()
    {
        if (IsLocked)
        {
            IsLocked = false;
            LockReason = null;
            LockedUntil = null;
            FailedLoginAttempts = 0;

            AddDomainEvent(new UserAccountUnlocked(Id));
        }
    }

    /// <summary>
    /// Automatically clears expired locks.
    /// </summary>
    public void ClearExpiredLock(DateTime now)
    {
        if (IsLocked && LockedUntil.HasValue && LockedUntil.Value <= now)
        {
            UnlockAccount();
        }
    }

    #endregion

    #region Login Tracking

    /// <summary>
    /// Records a successful login attempt.
    /// </summary>
    public void RecordSuccessfulLogin(string? ipAddress = null)
    {
        FailedLoginAttempts = 0;
        _loginHistory.Add(new LoginAttempt(SystemClock.UtcNow, true, ipAddress));

        // Keep only recent history (last 10 attempts)
        while (_loginHistory.Count > 10)
            _loginHistory.RemoveAt(0);
    }

    /// <summary>
    /// Records a failed login attempt and potentially locks the account.
    /// </summary>
    public void RecordFailedLogin(string? ipAddress = null, int maxAttempts = 5, TimeSpan? lockDuration = null)
    {
        FailedLoginAttempts++;
        _loginHistory.Add(new LoginAttempt(SystemClock.UtcNow, false, ipAddress));

        // Keep only recent history
        while (_loginHistory.Count > 10)
            _loginHistory.RemoveAt(0);

        // Auto-lock after max failed attempts
        if (FailedLoginAttempts >= maxAttempts && !IsLocked)
        {
            var duration = lockDuration ?? TimeSpan.FromMinutes(15);
            LockAccount($"Too many failed login attempts ({FailedLoginAttempts})", duration);
        }
    }

    /// <summary>
    /// Gets the last successful login timestamp.
    /// </summary>
    public DateTime? GetLastSuccessfulLogin()
    {
        return _loginHistory
            .Where(l => l.Success)
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefault()?.Timestamp;
    }

    #endregion

    #region Role Management

    public void AssignRole(RoleAssignment assignment)
    {
        SelfAssertArgumentNotNull(assignment, "Role assignment is required.");

        // Check if same role with same scope already exists
        var existing = _roleAssignments.FirstOrDefault(ra =>
            ra.RoleId == assignment.RoleId &&
            ra.ScopeType == assignment.ScopeType &&
            ra.ScopeId == assignment.ScopeId);

        if (existing != null)
        {
            return; // Already has this role assignment
        }

        _roleAssignments.Add(assignment);
        AddDomainEvent(new UserRoleAssigned(Id, assignment.RoleId, assignment.ScopeType, assignment.ScopeId));
    }

    public void RevokeRole(RoleId roleId, ScopeType scopeType, string? scopeId)
    {
        var assignment = _roleAssignments.FirstOrDefault(ra =>
            ra.RoleId == roleId &&
            ra.ScopeType == scopeType &&
            ra.ScopeId == scopeId);

        if (assignment != null)
        {
            _roleAssignments.Remove(assignment);
            AddDomainEvent(new UserRoleRevoked(Id, roleId, scopeType, scopeId));
        }
    }

    /// <summary>
    /// Revokes all roles for a specific organization.
    /// Useful when removing a user from an organization.
    /// </summary>
    public void RevokeAllRolesForOrganization(string organizationId)
    {
        var toRemove = _roleAssignments
            .Where(ra => ra.ScopeType == ScopeType.Organization && ra.ScopeId == organizationId)
            .ToList();

        foreach (var assignment in toRemove)
        {
            _roleAssignments.Remove(assignment);
            AddDomainEvent(new UserRoleRevoked(Id, assignment.RoleId, assignment.ScopeType, assignment.ScopeId));
        }
    }

    public bool HasRole(RoleId roleId) =>
        _roleAssignments.Any(ra => ra.RoleId == roleId);

    public bool HasRoleWithScope(RoleId roleId, ScopeType scopeType, string? scopeId) =>
        _roleAssignments.Any(ra =>
            ra.RoleId == roleId &&
            ra.ScopeType == scopeType &&
            ra.ScopeId == scopeId);

    /// <summary>
    /// Gets all organization IDs where the user has any role.
    /// </summary>
    public IEnumerable<string> GetOrganizationMemberships()
    {
        return _roleAssignments
            .Where(ra => ra.ScopeType == ScopeType.Organization && ra.ScopeId != null)
            .Select(ra => ra.ScopeId!)
            .Distinct();
    }

    /// <summary>
    /// Checks if the user is a member of the specified organization.
    /// </summary>
    public bool IsMemberOfOrganization(string organizationId)
    {
        return _roleAssignments.Any(ra =>
            ra.ScopeType == ScopeType.Organization &&
            ra.ScopeId == organizationId);
    }

    /// <summary>
    /// Checks if the user has the SystemAdmin role (global scope).
    /// </summary>
    public bool IsSystemAdmin()
    {
        return HasRoleWithScope(RoleId.SystemAdmin, ScopeType.Global, null);
    }

    #endregion

    #region Password Management

    public void ChangePassword(HashedPassword newPassword)
    {
        SelfAssertArgumentNotNull(newPassword, "New password is required.");
        Password = newPassword;
        PasswordChangedAt = SystemClock.UtcNow;
        MustChangePassword = false;

        AddDomainEvent(new UserPasswordChanged(Id));
    }

    /// <summary>
    /// Forces the user to change password on next login.
    /// </summary>
    public void RequirePasswordChange()
    {
        MustChangePassword = true;
    }

    /// <summary>
    /// Checks if the password is older than the specified age.
    /// </summary>
    public bool IsPasswordExpired(TimeSpan maxAge)
    {
        if (!PasswordChangedAt.HasValue)
            return true;

        return SystemClock.UtcNow - PasswordChangedAt.Value > maxAge;
    }

    #endregion

    #region Enablement

    public void Enable()
    {
        if (!Enablement.IsEnabled)
        {
            Enablement = Enablement.IndefiniteEnablement();
            AddDomainEvent(new UserEnablementChanged(Id, true));
        }
    }

    public void Disable()
    {
        if (Enablement.IsEnabled)
        {
            Enablement = Enablement.Disabled();
            AddDomainEvent(new UserEnablementChanged(Id, false));
        }
    }

    #endregion

    public override string ToString() =>
        $"User [id={Id}, username={Username}, locked={IsLocked}]";
}

/// <summary>
/// Records a login attempt for security auditing.
/// </summary>
public record LoginAttempt(
    DateTime Timestamp,
    bool Success,
    string? IpAddress);
