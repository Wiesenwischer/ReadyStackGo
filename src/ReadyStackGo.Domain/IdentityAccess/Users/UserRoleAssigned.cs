namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.IdentityAccess.Roles;


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
