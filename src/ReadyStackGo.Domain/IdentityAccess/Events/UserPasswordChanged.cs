namespace ReadyStackGo.Domain.IdentityAccess.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

public sealed class UserPasswordChanged : DomainEvent
{
    public UserId UserId { get; }

    public UserPasswordChanged(UserId userId)
    {
        UserId = userId;
    }
}
