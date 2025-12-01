namespace ReadyStackGo.Domain.IdentityAccess.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

public sealed class UserRoleAssigned : DomainEvent
{
    public UserId UserId { get; }
    public RoleId RoleId { get; }
    public ScopeType ScopeType { get; }
    public string? ScopeId { get; }

    public UserRoleAssigned(UserId userId, RoleId roleId, ScopeType scopeType, string? scopeId)
    {
        UserId = userId;
        RoleId = roleId;
        ScopeType = scopeType;
        ScopeId = scopeId;
    }
}
