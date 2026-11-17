namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Raised when a user's email address has been verified through a real ownership proof
/// (verification link or an external provider that asserts a verified email).
/// </summary>
public sealed class EmailVerified : DomainEvent
{
    public UserId UserId { get; }
    public EmailAddress Email { get; }

    public EmailVerified(UserId userId, EmailAddress email)
    {
        UserId = userId;
        Email = email;
    }
}
