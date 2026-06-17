namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Raised when an external identity provider (OIDC) link is removed from a user account.
/// </summary>
public sealed class ExternalIdentityUnlinked : DomainEvent
{
    public UserId UserId { get; }
    public string Provider { get; }
    public string Subject { get; }

    public ExternalIdentityUnlinked(UserId userId, string provider, string subject)
    {
        UserId = userId;
        Provider = provider;
        Subject = subject;
    }
}
