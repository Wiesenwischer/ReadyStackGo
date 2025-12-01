namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;


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
