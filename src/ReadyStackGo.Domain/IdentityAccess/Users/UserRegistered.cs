namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;


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
