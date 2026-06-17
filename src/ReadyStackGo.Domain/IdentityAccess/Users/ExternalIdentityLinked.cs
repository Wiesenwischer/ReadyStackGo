namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Raised when an external identity provider (OIDC) login is linked to a user account.
/// </summary>
public sealed class ExternalIdentityLinked : DomainEvent
{
    public UserId UserId { get; }
    public string Provider { get; }
    public string Subject { get; }

    public ExternalIdentityLinked(UserId userId, string provider, string subject)
    {
        UserId = userId;
        Provider = provider;
        Subject = subject;
    }
}
