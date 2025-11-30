namespace ReadyStackGo.Domain.Identity.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;
using ReadyStackGo.Domain.Access.ValueObjects;

public sealed class UserRoleRevoked : DomainEvent
{
    public UserId UserId { get; }
    public RoleId RoleId { get; }
    public ScopeType ScopeType { get; }
    public string? ScopeId { get; }

    public UserRoleRevoked(UserId userId, RoleId roleId, ScopeType scopeType, string? scopeId)
    {
        UserId = userId;
        RoleId = roleId;
        ScopeType = scopeType;
        ScopeId = scopeId;
    }
}
