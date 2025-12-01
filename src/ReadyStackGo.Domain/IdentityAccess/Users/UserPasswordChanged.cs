namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;


public sealed class UserPasswordChanged : DomainEvent
{
    public UserId UserId { get; }

    public UserPasswordChanged(UserId userId)
    {
        UserId = userId;
    }
}
