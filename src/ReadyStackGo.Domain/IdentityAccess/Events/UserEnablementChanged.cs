namespace ReadyStackGo.Domain.IdentityAccess.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

public sealed class UserEnablementChanged : DomainEvent
{
    public UserId UserId { get; }
    public bool IsEnabled { get; }

    public UserEnablementChanged(UserId userId, bool isEnabled)
    {
        UserId = userId;
        IsEnabled = isEnabled;
    }
}
