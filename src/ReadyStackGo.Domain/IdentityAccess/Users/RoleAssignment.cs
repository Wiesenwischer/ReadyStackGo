namespace ReadyStackGo.Domain.IdentityAccess.Users;

using ReadyStackGo.Domain.SharedKernel;
using ReadyStackGo.Domain.IdentityAccess.Roles;

/// <summary>
/// Value object representing a role assignment to a user with scope.
/// </summary>
public sealed class RoleAssignment : ValueObject
{
    public RoleId RoleId { get; private set; }
    public ScopeType ScopeType { get; private set; }
    public string? ScopeId { get; private set; }
    public DateTime AssignedAt { get; private set; }

    // For EF Core
    private RoleAssignment() => RoleId = null!;

    public RoleAssignment(RoleId roleId, ScopeType scopeType, string? scopeId, DateTime assignedAt)
    {
        SelfAssertArgumentNotNull(roleId, "RoleId is required.");

        if (scopeType == ScopeType.Global && scopeId != null)
        {
            throw new ArgumentException("Global scope should not have a ScopeId.");
        }

        if (scopeType != ScopeType.Global && string.IsNullOrEmpty(scopeId))
        {
            throw new ArgumentException("Non-global scope requires a ScopeId.");
        }

        RoleId = roleId;
        ScopeType = scopeType;
        ScopeId = scopeId;
        AssignedAt = assignedAt;
    }

    public static RoleAssignment Global(RoleId roleId) =>
        new(roleId, ScopeType.Global, null, DateTime.UtcNow);

    public static RoleAssignment ForOrganization(RoleId roleId, string organizationId) =>
        new(roleId, ScopeType.Organization, organizationId, DateTime.UtcNow);

    public static RoleAssignment ForEnvironment(RoleId roleId, string environmentId) =>
        new(roleId, ScopeType.Environment, environmentId, DateTime.UtcNow);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RoleId;
        yield return ScopeType;
        yield return ScopeId;
    }

    public override string ToString() =>
        $"RoleAssignment [roleId={RoleId}, scopeType={ScopeType}, scopeId={ScopeId}]";
}
