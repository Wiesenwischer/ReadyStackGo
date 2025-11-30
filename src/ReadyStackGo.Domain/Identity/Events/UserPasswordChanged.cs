namespace ReadyStackGo.Domain.Identity.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;

public sealed class UserPasswordChanged : DomainEvent
{
    public UserId UserId { get; }

    public UserPasswordChanged(UserId userId)
    {
        UserId = userId;
    }
}
