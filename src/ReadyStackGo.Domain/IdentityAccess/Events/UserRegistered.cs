namespace ReadyStackGo.Domain.IdentityAccess.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

public sealed class UserRegistered : DomainEvent
{
    public UserId UserId { get; }
    public string Username { get; }
    public EmailAddress Email { get; }

    public UserRegistered(UserId userId, string username, EmailAddress email)
    {
        UserId = userId;
        Username = username;
        Email = email;
    }
}
