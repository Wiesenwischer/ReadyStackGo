namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Event raised when a user account is locked.
/// </summary>
public sealed class UserAccountLocked : DomainEvent
{
    public UserId UserId { get; }
    public string Reason { get; }
    public DateTime? LockedUntil { get; }

    public UserAccountLocked(UserId userId, string reason, DateTime? lockedUntil)
    {
        UserId = userId;
        Reason = reason;
        LockedUntil = lockedUntil;
    }
}

/// <summary>
/// Event raised when a user account is unlocked.
/// </summary>
public sealed class UserAccountUnlocked : DomainEvent
{
    public UserId UserId { get; }

    public UserAccountUnlocked(UserId userId)
    {
        UserId = userId;
    }
}
