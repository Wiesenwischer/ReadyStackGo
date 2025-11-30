namespace ReadyStackGo.Domain.Identity.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;

public sealed class UserRegistered : DomainEvent
{
    public UserId UserId { get; }
    public TenantId TenantId { get; }
    public string Username { get; }
    public EmailAddress Email { get; }

    public UserRegistered(UserId userId, TenantId tenantId, string username, EmailAddress email)
    {
        UserId = userId;
        TenantId = tenantId;
        Username = username;
        Email = email;
    }
}
