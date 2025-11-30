namespace ReadyStackGo.Domain.Identity.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;

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
